namespace TaskTimer.Models;

public enum ProviderKind { AzureDevOps, GitHub }

public enum EntryKind { Work, Call, Meeting }

public sealed class WorkItem
{
    /// <summary>Unique across providers, e.g. "ado:1234" or "gh:owner/repo#12".</summary>
    public string Key { get; set; } = "";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Address of the item's page on the server, for opening it in a browser.</summary>
    public string Url { get; set; } = "";
    public string Type { get; set; } = "";
    public string State { get; set; } = "";
    /// <summary>Team project (Azure DevOps) or "owner/repo" (GitHub).</summary>
    public string Project { get; set; } = "";
    /// <summary>Iteration path (Azure DevOps) or milestone title (GitHub). Empty when not planned.</summary>
    public string Sprint { get; set; } = "";
    /// <summary>Display name of the assignee.</summary>
    public string AssignedTo { get; set; } = "";
    /// <summary>Sign-in name of the assignee (Azure DevOps), e.g. "name@contoso.com"; used to assign tasks.</summary>
    public string AssignedToAccount { get; set; } = "";
    /// <summary>Value of the configured customer field (Azure DevOps) or the repository owner (GitHub).</summary>
    public string Customer { get; set; } = "";
    /// <summary>Local time.</summary>
    public DateTime? CreatedDate { get; set; }
    /// <summary>Local time. Value of the configured delivery date field (Azure DevOps) or the milestone due date (GitHub).</summary>
    public DateTime? DeliveryDate { get; set; }
    public double? CompletedWork { get; set; }
    public double? RemainingWork { get; set; }
    /// <summary>Key of the parent work item, or empty for a top-level item.</summary>
    public string ParentKey { get; set; } = "";
    /// <summary>False for ancestors loaded only to show the hierarchy; those cannot be timed.</summary>
    public bool IsListed { get; set; } = true;

    // "Issue" is the only GitHub type, so it has to stay timeable.
    private static readonly HashSet<string> TimeableTypes = new(StringComparer.OrdinalIgnoreCase) { "Task", "Bug", "Issue" };

    /// <summary>Time may be recorded on and sent to Tasks and Bugs only, not on stories, features or epics.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CanLogTime => IsListed && TimeableTypes.Contains(Type);

    public override string ToString() => $"{Id} - {Title}";
}

public enum MachineLocation { OnPremises, Cloud }

/// <summary>How a cloud machine is started and stopped: directly as an Azure VM, or as an LCS environment.</summary>
public enum CloudSource { Azure, Lcs }

/// <summary>A machine the user develops on. Its password lives in Windows Credential Manager, not here.</summary>
public sealed class DevMachine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public MachineLocation Location { get; set; }
    /// <summary>Computer name or IP, optionally with :port, for Remote Desktop; or an https:// link for web-based machines.</summary>
    public string Address { get; set; } = "";
    public string UserName { get; set; } = "";
    /// <summary>Cloud: Azure subscription name or ID; empty = the Azure CLI's default subscription.</summary>
    public string AzureSubscription { get; set; } = "";
    public string AzureResourceGroup { get; set; } = "";
    public string AzureVmName { get; set; } = "";
    public CloudSource CloudSource { get; set; }
    /// <summary>Cloud via LCS: the LCS project ID (a number) and the environment ID.</summary>
    public string LcsProjectId { get; set; } = "";
    public string LcsEnvironmentId { get; set; } = "";
    /// <summary>LCS API address for the region the project is in (EU, UAE, ... have their own).</summary>
    public string LcsApiUrl { get; set; } = "https://lcsapi.lcs.dynamics.com";
    /// <summary>On-premises: the Hyper-V host; empty = this computer.</summary>
    public string HyperVHost { get; set; } = "";
    public string HyperVVmName { get; set; } = "";

    public DevMachine Clone() => (DevMachine)MemberwiseClone();

    public override string ToString() => Name;
}

/// <summary>A task to create on the server. <see cref="ParentId"/> links it under a story or bug.</summary>
/// <param name="AssignedTo">Account to assign to; null = the signed-in user.</param>
public sealed record NewTask(string Project, string Type, string Title, string Description, string Sprint, string? ParentId,
    double? RemainingWork, string? AssignedTo);

/// <summary>Changes to an existing task; null means "leave as it is".</summary>
/// <param name="AssignedTo">New assignee account; "" = unassigned.</param>
public sealed record TaskChanges(string? Title, string? State, double? RemainingWork, string? AssignedTo);

/// <summary>Someone a task can be assigned to.</summary>
public sealed record Person(string Name, string Account)
{
    /// <summary>What Azure DevOps should receive: the sign-in name when known, which is unambiguous.</summary>
    public string Value => Account.Length > 0 ? Account : Name;

    public override string ToString() =>
        Account.Length > 0 && !string.Equals(Account, Name, StringComparison.OrdinalIgnoreCase) ? $"{Name} <{Account}>" : Name;
}

/// <summary>A sprint known to the server, whether past, current or future.</summary>
public sealed record SprintInfo(string Path, bool IsCurrent);

public sealed class TimeEntry
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public EntryKind Kind { get; set; }
    public DateTime StartUtc { get; set; }
    public double Seconds { get; set; }
    public bool Synced { get; set; }
}

public sealed class AppSettings
{
    public ProviderKind Provider { get; set; } = ProviderKind.AzureDevOps;
    /// <summary>Azure DevOps: https://dev.azure.com/{org} (or a server collection URL). GitHub: API base URL.</summary>
    public string Url { get; set; } = "";
    /// <summary>Azure DevOps only. Empty = all projects in the organization.</summary>
    public string Project { get; set; } = "";
    /// <summary>Personal access token, encrypted with DPAPI for the current Windows user.</summary>
    public string PatProtected { get; set; } = "";
    public bool AutoDetectTeams { get; set; } = true;
    public bool ReduceRemainingWork { get; set; } = true;
    public bool AddComment { get; set; } = true;
    /// <summary>Pause the task timer after this many seconds without keyboard/mouse input. 0 = never.</summary>
    public int IdlePauseSeconds { get; set; } = 300;
    /// <summary>Continue timing the task that was running when the app was last closed.</summary>
    public bool AutoResumeLastTask { get; set; } = true;
    /// <summary>When false, starting a task stops the one that was running.</summary>
    public bool AllowMultipleTasks { get; set; }
    public string LastTaskKey { get; set; } = "";
    public bool LastTaskRunning { get; set; }
    /// <summary>Work folders separated by ';'. Opening or saving a file in them starts the task timer.</summary>
    public string WorkPaths { get; set; } = "";
    /// <summary>Main window transparency in percent: 0 = solid, up to 70 = mostly see-through.</summary>
    public int WindowTransparency { get; set; }
    public bool DarkTheme { get; set; }
    /// <summary>Grid column widths the user set by hand, by column name. Other columns fit their content.</summary>
    public Dictionary<string, int> ColumnWidths { get; set; } = new();
    /// <summary>Names of grid columns the user has hidden.</summary>
    public List<string> HiddenColumns { get; set; } = new();
    public List<DevMachine> DevMachines { get; set; } = new();
    public string SelectedMachineId { get; set; } = "";
    public string Language { get; set; } = "en";
    /// <summary>Sprint filter on the main form. Empty = all sprints.</summary>
    public string SelectedSprint { get; set; } = "";
    public string SelectedState { get; set; } = "";
    public string SelectedCustomer { get; set; } = "";
    public string SelectedAssignee { get; set; } = "";
    /// <summary>
    /// Azure DevOps only. When set, items assigned to any member of this team are loaded as well
    /// as my own. A plain team name is resolved inside <see cref="Project"/>.
    /// </summary>
    public string Team { get; set; } = "";
    /// <summary>Azure DevOps only. E-mail accounts of team members whose items are loaded as well as my own.</summary>
    public List<string> TeamEmails { get; set; } = new();
    /// <summary>
    /// Azure DevOps field that identifies the customer, by reference name (e.g. "Custom.Customer").
    /// There is no built-in customer field, so this defaults to the area path.
    /// </summary>
    public string CustomerField { get; set; } = "System.AreaPath";
    /// <summary>Azure DevOps field that holds the delivery date, by display or reference name.</summary>
    public string DeliveryDateField { get; set; } = "Delivery Date";
}
