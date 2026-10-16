using System.Runtime.InteropServices;

namespace SolYan.CorelAI.Corel;

internal static class ComRunningObject
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    public static object GetOrCreate(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: true)
            ?? throw new InvalidOperationException($"COM ProgID not found: {progId}");

        var clsid = type.GUID;
        try
        {
            GetActiveObject(ref clsid, IntPtr.Zero, out var instance);
            return instance;
        }
        catch (COMException)
        {
            return Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not create COM application: {progId}");
        }
    }
}
