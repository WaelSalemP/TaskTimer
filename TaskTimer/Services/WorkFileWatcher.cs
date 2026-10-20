using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TaskTimer.Services;

/// <summary>
/// Reports when a file under one of the work folders is opened or saved.
/// Windows has no "file opened" notification, so two signals are combined:
/// the shell's Recent list (a shortcut appears there whenever a file is opened from Explorer or a
/// standard Open dialog), and change notifications from the work folders themselves, which catch
/// editors that open files on their own (saving, or the lock file Office creates on open).
/// </summary>
public sealed class WorkFileWatcher : IDisposable
{
    // Folders rewritten by tools rather than by the user's own editing.
    private static readonly string[] IgnoredFolders = { @"\.git\", @"\.vs\", @"\bin\", @"\obj\", @"\node_modules\" };

    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly List<string> _roots = new();

    /// <summary>Raised with the full path of the work file. Arrives on the synchronizing object's thread.</summary>
    public event Action<string>? WorkFileActivity;

    /// <param name="workPaths">Work folders separated by ';'.</param>
    public WorkFileWatcher(string workPaths, ISynchronizeInvoke synchronizingObject)
    {
        foreach (var raw in workPaths.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string root;
            try { root = Path.GetFullPath(raw); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (!Directory.Exists(root)) continue;

            _roots.Add(root.TrimEnd('\\') + "\\");
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                InternalBufferSize = 64 * 1024,
                SynchronizingObject = synchronizingObject
            };
            watcher.Created += (_, e) => Report(e.FullPath);
            watcher.Changed += (_, e) => Report(e.FullPath);
            watcher.Renamed += (_, e) => Report(e.FullPath);
            Start(watcher);
        }
        if (_roots.Count == 0) return;

        var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        if (!Directory.Exists(recent)) return;
        var recentWatcher = new FileSystemWatcher(recent, "*.lnk")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            SynchronizingObject = synchronizingObject
        };
        recentWatcher.Created += (_, e) => ReportShortcut(e.FullPath);
        recentWatcher.Changed += (_, e) => ReportShortcut(e.FullPath);
        Start(recentWatcher);
    }

    private void Start(FileSystemWatcher watcher)
    {
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
    }

    private void ReportShortcut(string shortcutPath)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return;
            dynamic shell = Activator.CreateInstance(type)!;
            string target = shell.CreateShortcut(shortcutPath).TargetPath;
            if (File.Exists(target)) Report(target); // the Recent list also records folders
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            // Shortcut still being written, or not resolvable: the next event for it will do.
        }
    }

    private void Report(string path)
    {
        if (!_roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase))) return;
        if (IgnoredFolders.Any(f => path.Contains(f, StringComparison.OrdinalIgnoreCase))) return;
        if (Directory.Exists(path)) return;
        WorkFileActivity?.Invoke(path);
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
    }
}
