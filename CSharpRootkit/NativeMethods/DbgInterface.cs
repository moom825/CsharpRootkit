using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CSharpRootkit
{
    public class DbgInterface
    {
        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("27fe5639-8407-4f47-8364-ee118fb08ac8")]
        public interface IDebugClient
        {
            int D00(); int D01(); int D02(); int D03(); int D04();
            int D05(); int D06(); int D07(); int D08(); [PreserveSig]
            uint AttachProcess(
                [In] ulong Server,
                [In] uint pid,
                [In] uint flags);
            int D10();

            int D11(); int D12(); int D13(); int D14();
            int D15(); int D16();

            int D17(); int D18(); int D19(); int D20(); int D21();
            int D22(); int D23(); int D24(); int D25(); int D26();
            int D27(); int D28(); int D29(); int D30(); int D31();

        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5182e668-105e-416e-ad92-24ef800424ba")]
        public interface IDebugControl
        {
            int D01(); int D02(); int D03(); int D04(); int D05();
            int D06(); int D07(); int D08(); int D09(); int D10();
            int D11(); int D12(); int D13(); int D14(); int D15();
            int D16(); int D17(); int D18(); int D19(); int D20();
            int D21(); int D22(); int D23();
            [PreserveSig]
            uint Disassemble(
                [In] ulong Offset,
                [In] uint Flags,
                [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
                [In] uint BufferSize,
                [Out] out uint DisassemblySize,
                [Out] out ulong EndOffset);
            int D25();
            int D26(); int D27(); int D28(); int D29(); int D30();
            int D31(); int D32(); int D33(); int D34(); int D35();
            int D36(); int D37(); int D38(); int D39(); int D40();
            int D41(); int D42(); int D43(); int D44(); int D45();
            int D46(); int D47(); [PreserveSig]
            uint SetExecutionStatus(uint Status);  int D49(); int D50();
            int D51(); int D52(); int D53(); int D54(); int D55();
            int D56(); int D57(); int D58(); int D59(); int D60();
            int D61(); int D62(); int D63();
            int D64();
            int D65(); int D66(); int D67(); int D68(); int D69(); int D70();
            int D71(); int D72(); int D73(); int D74(); int D75();
            int D76(); int D77(); int D78(); int D79(); int D80();
            int D81(); int D82(); int D83(); int D84(); int D85();
            int D86(); int D87(); int D88(); int D89(); int D90();
            [PreserveSig]
            uint WaitForEvent(
                [In] int wait,
                [In] int timeout);

        }
    }
}
