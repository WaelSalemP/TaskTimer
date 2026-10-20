using System.Runtime.InteropServices;

namespace TaskTimer.Services;

/// <summary>Reports how long it has been since the last keyboard or mouse input in this Windows session.</summary>
public static class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    public static double IdleSeconds
    {
        get
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info)) return 0;
            // Both values are 32-bit tick counts; unchecked subtraction stays correct across wrap-around.
            return unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0;
        }
    }
}
