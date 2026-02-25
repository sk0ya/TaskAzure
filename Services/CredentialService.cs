using System.Runtime.InteropServices;
using System.Text;

namespace TaskAzure.Services;

/// <summary>
/// PAT を次の優先順で取得します:
///   1. 指定した環境変数 (デフォルト: ADO_PAT)
///   2. Windows 資格情報マネージャー (汎用資格情報 "TaskAzure_ADO_PAT")
/// </summary>
public class CredentialService
{
    public const string CredentialTarget = "TaskAzure_ADO_PAT";

    public string? GetPat(string envVarName = "ADO_PAT")
    {
        var env = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        return ReadFromCredentialManager(CredentialTarget);
    }

    public bool SaveToCredentialManager(string pat)
    {
        var bytes = Encoding.Unicode.GetBytes(pat);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new NativeMethods.CREDENTIAL
            {
                Flags = 0,
                Type = NativeMethods.CRED_TYPE_GENERIC,
                TargetName = CredentialTarget,
                Comment = IntPtr.Zero,
                LastWritten = default,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = IntPtr.Zero,
                UserName = "ADO_PAT",
            };
            return NativeMethods.CredWrite(ref cred, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
    }

    public bool DeleteFromCredentialManager()
        => NativeMethods.CredDelete(CredentialTarget, NativeMethods.CRED_TYPE_GENERIC, 0);

    private static string? ReadFromCredentialManager(string target)
    {
        if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out var ptr))
            return null;
        try
        {
            var cred = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(ptr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
                return null;

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            NativeMethods.CredFree(ptr);
        }
    }

    private static class NativeMethods
    {
        public const uint CRED_TYPE_GENERIC = 1;
        public const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public IntPtr Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, uint type, int reserved, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredWrite([In] ref CREDENTIAL credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredDelete(string target, uint type, int flags);

        [DllImport("advapi32.dll")]
        public static extern void CredFree(IntPtr credentialPtr);
    }
}
