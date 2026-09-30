using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;

namespace InterviewPracticeCompanion.Services;

public interface ICredentialService
{
    void SetApiKey(string apiKey);
    bool HasApiKey();
    string? GetApiKey();
    void ClearApiKey();
}

public sealed class CredentialService : ICredentialService
{
    private const string Target = "InterviewPracticeCompanion/ai.meetsin.id";
    private const uint Generic = 1, SessionPersistence = 1;

    public void SetApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required.", nameof(apiKey));
        var bytes = checked((uint)(apiKey.Length * sizeof(char)));
        var blob = Marshal.StringToCoTaskMemUni(apiKey);
        try
        {
            var credential = new NativeCredential { Type = Generic, TargetName = Target, CredentialBlobSize = bytes, CredentialBlob = blob, Persist = SessionPersistence, UserName = Environment.UserName };
            if (!CredWriteW(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            ZeroAndFree(blob, checked((int)bytes));
        }
    }

    public bool HasApiKey() => ReadCredential(pointer => pointer != IntPtr.Zero);

    public string? GetApiKey() => ReadCredential(pointer =>
    {
        if (pointer == IntPtr.Zero) return null;
        var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
        return credential.CredentialBlob == IntPtr.Zero ? null : Marshal.PtrToStringUni(credential.CredentialBlob, checked((int)credential.CredentialBlobSize / sizeof(char)));
    });

    public void ClearApiKey()
    {
        if (!CredDeleteW(Target, Generic, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static T ReadCredential<T>(Func<IntPtr, T> reader)
    {
        if (!CredReadW(Target, Generic, 0, out var pointer))
        {
            if (Marshal.GetLastWin32Error() == 1168) return reader(IntPtr.Zero);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        try { return reader(pointer); }
        finally { CredFree(pointer); }
    }

    private static unsafe void ZeroAndFree(IntPtr pointer, int bytes)
    {
        if (pointer == IntPtr.Zero) return;
        new Span<byte>((void*)pointer, bytes).Clear();
        Marshal.FreeCoTaskMem(pointer);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags, Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredWriteW(ref NativeCredential credential, uint flags);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32")] private static extern void CredFree(IntPtr buffer);
}
