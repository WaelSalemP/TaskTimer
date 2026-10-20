using Microsoft.Win32;

namespace TaskTimer.Services;

/// <summary>
/// Detects an active Teams call/meeting by checking whether Teams currently holds the microphone.
/// Windows records this per app under the CapabilityAccessManager consent store:
/// LastUsedTimeStop == 0 while the app is still using the device.
/// </summary>
public static class TeamsCallDetector
{
    private const string MicKey =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    public static bool IsInCall()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(MicKey);
            if (root == null) return false;
            if (AnyTeamsAppInUse(root)) return true;
            using var nonPackaged = root.OpenSubKey("NonPackaged");
            return nonPackaged != null && AnyTeamsAppInUse(nonPackaged);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool AnyTeamsAppInUse(RegistryKey parent)
    {
        foreach (var name in parent.GetSubKeyNames())
        {
            // Packaged: "MSTeams_8wekyb3d8bbwe"; classic: "...#Teams.exe" / "...#ms-teams.exe".
            if (!name.Contains("teams", StringComparison.OrdinalIgnoreCase)) continue;
            using var app = parent.OpenSubKey(name);
            if (app?.GetValue("LastUsedTimeStart") is long start && start != 0 &&
                app.GetValue("LastUsedTimeStop") is long stop && stop == 0)
                return true;
        }
        return false;
    }
}
