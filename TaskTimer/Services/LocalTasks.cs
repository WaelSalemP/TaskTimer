using TaskTimer.Models;

namespace TaskTimer.Services;

/// <summary>A task kept only on this computer, for work that has no Azure DevOps item.</summary>
public sealed class LocalTask
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string State { get; set; } = LocalTaskStore.States[0];
    public string Customer { get; set; } = "";
    public string Description { get; set; } = "";
    public double? RemainingWork { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string Key => LocalTaskStore.KeyPrefix + Number;
}

public sealed class LocalTaskStore
{
    private const string FileName = "localtasks.json";
    public const string KeyPrefix = "local:";
    public const string ProjectName = "Local";
    public static readonly string[] States = { "To Do", "In Progress", "Done" };

    public List<LocalTask> Tasks { get; set; } = new();
    public int LastNumber { get; set; }

    public static LocalTaskStore Load() => SettingsStore.LoadJson<LocalTaskStore>(FileName);

    public void Save() => SettingsStore.SaveJson(FileName, this);

    public static bool IsLocal(WorkItem item) => item.Key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    public LocalTask? Find(WorkItem item) => Tasks.FirstOrDefault(t => t.Key == item.Key);

    public LocalTask Add(LocalTask task)
    {
        task.Number = ++LastNumber; // numbers are never reused, so old time entries cannot attach to a new task
        Tasks.Add(task);
        Save();
        return task;
    }

    public void Remove(LocalTask task)
    {
        Tasks.Remove(task);
        Save();
    }

    /// <summary>Local tasks shaped like server work items, so the list, filters and timers treat them alike.</summary>
    public IEnumerable<WorkItem> ToWorkItems() => Tasks.Select(t => new WorkItem
    {
        Key = t.Key,
        Id = "L" + t.Number,
        Title = t.Title,
        Type = "Task",
        State = t.State,
        Project = ProjectName,
        // No assignee: they are always the user's own, and a Windows account name would appear as a
        // separate person in the "Assigned to" filter.
        Customer = t.Customer,
        CreatedDate = t.CreatedDate,
        DeliveryDate = t.DeliveryDate,
        RemainingWork = t.RemainingWork
    });
}
