using TaskTimer.Forms;

namespace TaskTimer;

internal static class Program
{
    /// <summary>Window icon shared by all forms (the same image as the executable's icon).</summary>
    public static readonly Icon AppIcon =
        new(typeof(Program).Assembly.GetManifestResourceStream("TaskTimer.app.ico")!);

    [STAThread]
    private static void Main()
    {
        // The app keeps running in the notification area after its window is closed, so starting it
        // again must bring that instance back instead of running a second set of timers.
        using var mutex = new Mutex(true, @"Local\TaskTimer.SingleInstance", out var isFirstInstance);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TaskTimer.Show");
        if (!isFirstInstance)
        {
            showSignal.Set();
            return;
        }

        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        var registration = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) =>
        {
            if (form.IsHandleCreated && !form.IsDisposed) form.BeginInvoke(form.ShowFromTray);
        }, null, Timeout.Infinite, executeOnlyOnce: false);

        Application.Run(form);
        registration.Unregister(null);
    }
}
