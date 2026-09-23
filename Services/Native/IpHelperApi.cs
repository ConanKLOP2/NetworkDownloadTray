using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace NetworkDownloadTray.Services.Native;

internal static class IpHelperApi
{
    // MIB_IF_TABLE2 / MIB_IF_ROW2 layout from netioapi.h; all fields are fixed-size, so x86 and x64 match.
    internal const int RowsOffset = 8, RowSize = 1352;
    internal const int GuidOffset = 12, AliasOffset = 28, DescriptionOffset = 542;
    internal const int TypeOffset = 1128, OperStatusOffset = 1156, InOctetsOffset = 1208;

    [DllImport("iphlpapi.dll")]
    internal static extern int GetIfTable2(out IntPtr table);
    [DllImport("iphlpapi.dll")]
    internal static extern void FreeMibTable(IntPtr memory);

    internal static int RowCount(IntPtr table) => Marshal.ReadInt32(table);
    internal static IntPtr Row(IntPtr table, int index) => table + RowsOffset + index * RowSize;

    internal static Guid InterfaceGuid(IntPtr row)
    {
        // Two 64-bit reads instead of PtrToStructure<Guid>, which boxes on every call.
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, Marshal.ReadInt64(row, GuidOffset));
        BinaryPrimitives.WriteInt64LittleEndian(bytes[8..], Marshal.ReadInt64(row, GuidOffset + 8));
        return new Guid(bytes);
    }

    internal static string Alias(IntPtr row) => Marshal.PtrToStringUni(row + AliasOffset) ?? "";
    internal static string Description(IntPtr row) => Marshal.PtrToStringUni(row + DescriptionOffset) ?? "";
    internal static uint Type(IntPtr row) => (uint)Marshal.ReadInt32(row, TypeOffset);
    internal static uint OperStatus(IntPtr row) => (uint)Marshal.ReadInt32(row, OperStatusOffset);
    internal static long InOctets(IntPtr row)
    {
        ulong value = (ulong)Marshal.ReadInt64(row, InOctetsOffset);
        return value > long.MaxValue ? long.MaxValue : (long)value;
    }
}
