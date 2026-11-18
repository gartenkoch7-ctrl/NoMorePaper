using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ICloudSpike.Session;

/// <summary>
/// Windows DPAPI, bound to the signed-in Windows user. Called directly instead of through the
/// System.Security.Cryptography.ProtectedData package so the spike adds no package to the repo.
/// The product will use ASP.NET Core Data Protection (ADR-0015) instead.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsDataProtector : ISecretProtector
{
    // CRYPTPROTECT_UI_FORBIDDEN: fail instead of showing a dialog.
    private const int NoUserInterface = 0x1;

    public byte[] Protect(byte[] clearData) => Transform(clearData, protect: true);

    public byte[] Unprotect(byte[] protectedData) => Transform(protectedData, protect: false);

    private static unsafe byte[] Transform(byte[] input, bool protect)
    {
        fixed (byte* pointer = input)
        {
            var source = new DataBlob { Size = input.Length, Data = (nint)pointer };
            var succeeded = protect
                ? CryptProtectData(ref source, 0, 0, 0, 0, NoUserInterface, out var result)
                : CryptUnprotectData(ref source, 0, 0, 0, 0, NoUserInterface, out result);
            if (!succeeded)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                var output = new byte[result.Size];
                Marshal.Copy(result.Data, output, 0, result.Size);
                return output;
            }
            finally
            {
                LocalFree(result.Data);
            }
        }
    }

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptProtectData(
        ref DataBlob dataIn, nint description, nint entropy, nint reserved, nint prompt, int flags, out DataBlob dataOut);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CryptUnprotectData(
        ref DataBlob dataIn, nint description, nint entropy, nint reserved, nint prompt, int flags, out DataBlob dataOut);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public nint Data;
    }
}
