using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SolYan.CorelAI.Host;

internal static class WindowsSecretStore
{
    private const uint CryptprotectUiForbidden = 0x1;

    private static string SecretPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SolYan",
        "CorelAI",
        "openai.key");

    public static string? TryLoadApiKey()
    {
        try
        {
            if (!File.Exists(SecretPath)) return null;
            var encrypted = File.ReadAllBytes(SecretPath);
            if (encrypted.Length == 0) return null;
            var clear = Unprotect(encrypted);
            var key = Encoding.UTF8.GetString(clear).Trim();
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveApiKey(string apiKey)
    {
        var key = apiKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length < 20)
            throw new ArgumentException("OpenAI API key is empty or invalid.", nameof(apiKey));

        var directory = Path.GetDirectoryName(SecretPath)
            ?? throw new InvalidOperationException("Could not resolve the local settings directory.");
        Directory.CreateDirectory(directory);

        var encrypted = Protect(Encoding.UTF8.GetBytes(key));
        var tempPath = SecretPath + ".tmp";
        File.WriteAllBytes(tempPath, encrypted);
        File.Move(tempPath, SecretPath, overwrite: true);
    }

    public static void ClearApiKey()
    {
        if (File.Exists(SecretPath)) File.Delete(SecretPath);
    }

    private static byte[] Protect(byte[] clear) => Transform(clear, protect: true);
    private static byte[] Unprotect(byte[] encrypted) => Transform(encrypted, protect: false);

    private static byte[] Transform(byte[] input, bool protect)
    {
        if (input.Length == 0) return Array.Empty<byte>();

        var inputBlob = new DataBlob
        {
            Size = input.Length,
            Data = Marshal.AllocHGlobal(input.Length)
        };
        Marshal.Copy(input, 0, inputBlob.Data, input.Length);

        DataBlob outputBlob = default;
        try
        {
            var ok = protect
                ? CryptProtectData(
                    ref inputBlob,
                    "SolYan Corel AI OpenAI API key",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    out outputBlob)
                : CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    out outputBlob);

            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows DPAPI could not protect the SolYan Corel AI secret.");

            var result = new byte[outputBlob.Size];
            Marshal.Copy(outputBlob.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (inputBlob.Data != IntPtr.Zero) Marshal.FreeHGlobal(inputBlob.Data);
            if (outputBlob.Data != IntPtr.Zero) LocalFree(outputBlob.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
