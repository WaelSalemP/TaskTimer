using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TaskTimer.Models;

namespace TaskTimer.Services;

/// <summary>
/// Opens dev machines and keeps their passwords in Windows Credential Manager under
/// "TERMSRV/&lt;address&gt;", the entry Remote Desktop Connection (mstsc) signs in with by itself.
/// Passwords are never written to the app's own files.
/// </summary>
public static partial class RemoteDesktop
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    // Computer name, IPv4, or [IPv6], each optionally followed by :port. Nothing that could smuggle
    // extra arguments onto the mstsc command line.
    [GeneratedRegex(@"^([A-Za-z0-9][A-Za-z0-9.\-_]*|\[[0-9A-Fa-f:.]+\])(:\d{1,5})?$")]
    private static partial Regex RdpAddress();

    public static bool IsValidAddress(string address) =>
        IsWebAddress(address) ? Uri.TryCreate(address, UriKind.Absolute, out _) : RdpAddress().IsMatch(address);

    /// <summary>Browser-based machines (Microsoft Dev Box, Azure Bastion, ...) are given as an https:// link.</summary>
    public static bool IsWebAddress(string address) =>
        address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        address.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    /// <summary>mstsc looks the password up by the address as typed; with a port it may also use the bare host.</summary>
    private static IEnumerable<string> Targets(DevMachine machine)
    {
        var address = machine.Address.Trim();
        yield return "TERMSRV/" + address;
        var colon = address.LastIndexOf(':');
        if (colon > 0 && !address.EndsWith(']') && address.IndexOf(':') == colon)
            yield return "TERMSRV/" + address[..colon];
    }

    public static bool HasPassword(DevMachine machine) => ReadPassword(machine) != null;

    public static string? ReadPassword(DevMachine machine)
    {
        if (IsWebAddress(machine.Address)) return null;
        if (!CredRead(Targets(machine).First(), CredTypeGeneric, 0, out var pointer)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            return credential.CredentialBlob == IntPtr.Zero
                ? ""
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public static void SavePassword(DevMachine machine, string password)
    {
        foreach (var target in Targets(machine))
        {
            var blob = Marshal.StringToCoTaskMemUni(password);
            try
            {
                var credential = new Credential
                {
                    Type = CredTypeGeneric,
                    TargetName = target,
                    UserName = machine.UserName.Trim(),
                    CredentialBlob = blob,
                    CredentialBlobSize = (uint)(password.Length * 2),
                    Persist = CredPersistLocalMachine
                };
                if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
            }
        }
    }

    public static void DeletePassword(DevMachine machine)
    {
        foreach (var target in Targets(machine)) CredDelete(target, CredTypeGeneric, 0);
    }

    /// <summary>Opens Remote Desktop to the machine, or its link in the browser for web-based machines.</summary>
    public static void Connect(DevMachine machine)
    {
        var address = machine.Address.Trim();
        if (!IsValidAddress(address)) throw new InvalidOperationException($"\"{address}\" is not a valid address.");

        var start = IsWebAddress(address)
            ? new ProcessStartInfo(address) { UseShellExecute = true }
            : new ProcessStartInfo("mstsc.exe", "/v:" + address) { UseShellExecute = true };
        Process.Start(start);
    }
}
