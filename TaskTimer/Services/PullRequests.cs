using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using TaskTimer.Models;

namespace TaskTimer.Services;

public sealed record PullRequest(
    int Id, string Title, string Project, string RepositoryId, string RepositoryName,
    string SourceBranch, string TargetBranch, string CreatedBy, string CreatedById, DateTime? Created,
    bool IsDraft, string MergeStatus, int MyVote, bool IAmReviewer, string Reviewers, string WebUrl);

/// <param name="ChangeType">Azure DevOps change type, e.g. "add", "edit", "delete", "rename" or "edit, rename".</param>
public sealed record PullRequestFileChange(string Path, string OriginalPath, string ChangeType)
{
    public bool IsAdd => ChangeType.Contains("add", StringComparison.OrdinalIgnoreCase);
    public bool IsDelete => ChangeType.Contains("delete", StringComparison.OrdinalIgnoreCase);

    public override string ToString()
    {
        var mark = IsAdd ? "A" : IsDelete ? "D" : ChangeType.Contains("rename", StringComparison.OrdinalIgnoreCase) ? "R" : "M";
        // File name first: paths are long and share most of their beginning.
        var slash = Path.LastIndexOf('/');
        return slash < 0 ? $"{mark}  {Path}" : $"{mark}  {Path[(slash + 1)..]}   ({Path[..slash]})";
    }
}

/// <summary>A comment from a pull request thread. <see cref="FilePath"/> is empty for general comments.</summary>
/// <param name="LeftSide">True when anchored to a line of the old (target) version, e.g. a removed line.</param>
public sealed record PullRequestComment(string FilePath, int Line, bool LeftSide, string Author, string Text, DateTime? Date, string Status);

public sealed record GitRepository(string Id, string Name, string DefaultBranch)
{
    public override string ToString() => Name;
}

/// <summary>How a pull request is completed.</summary>
/// <param name="MergeStrategy">"noFastForward", "squash", "rebase" or "rebaseMerge" (Azure DevOps names).</param>
public sealed record PullRequestCompletion(string MergeStrategy, bool DeleteSourceBranch, bool CompleteWorkItems, string MergeMessage);

/// <summary>Azure Repos pull requests through the Azure DevOps REST API, with the same token as the work items.</summary>
public sealed class PullRequestService : HttpProviderBase
{
    private const string Api = "api-version=7.0";
    public const int VoteApproved = 10;

    private readonly string _org;
    private string? _myId;

    public PullRequestService(AppSettings settings)
    {
        _org = settings.Url.Trim().TrimEnd('/');
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + SettingsStore.GetPat(settings)));
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string ProjectUrl(string project) => $"{_org}/{Uri.EscapeDataString(project)}";

    private string PullRequestUrl(PullRequest pr) =>
        $"{ProjectUrl(pr.Project)}/_apis/git/repositories/{pr.RepositoryId}/pullrequests/{pr.Id}";

    /// <summary>The signed-in user's Azure DevOps ID, used for votes and "mine".</summary>
    public async Task<string> GetMyIdAsync(CancellationToken ct = default)
    {
        if (_myId != null) return _myId;
        var data = await SendAsync(HttpMethod.Get, $"{_org}/_apis/connectionData", null, "application/json", ct);
        return _myId = data["authenticatedUser"]?["id"]?.ToString() ?? "";
    }

    /// <summary>Active (open) pull requests in the given projects, newest first.</summary>
    public async Task<List<PullRequest>> GetActiveAsync(IEnumerable<string> projects, CancellationToken ct = default)
    {
        var me = await GetMyIdAsync(ct);
        var result = new List<PullRequest>();
        foreach (var project in projects.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var url = $"{ProjectUrl(project)}/_apis/git/pullrequests?searchCriteria.status=active&$top=200&{Api}";
            var json = await SendAsync(HttpMethod.Get, url, null, "application/json", ct);
            foreach (var node in json["value"]?.AsArray() ?? new JsonArray())
                if (node != null) result.Add(Parse(node, project, me));
        }
        return result.OrderByDescending(p => p.Created).ToList();
    }

    private PullRequest Parse(JsonNode node, string project, string me)
    {
        var reviewers = node["reviewers"]?.AsArray().Where(r => r != null).ToList() ?? new List<JsonNode?>();
        var mine = reviewers.FirstOrDefault(r => r!["id"]?.ToString() == me);
        var repository = node["repository"];
        var repoName = repository?["name"]?.ToString() ?? "";
        var id = node["pullRequestId"]!.GetValue<int>();
        return new PullRequest(
            id,
            node["title"]?.ToString() ?? "",
            repository?["project"]?["name"]?.ToString() ?? project,
            repository?["id"]?.ToString() ?? "",
            repoName,
            ShortBranch(node["sourceRefName"]?.ToString()),
            ShortBranch(node["targetRefName"]?.ToString()),
            node["createdBy"]?["displayName"]?.ToString() ?? "",
            node["createdBy"]?["id"]?.ToString() ?? "",
            DateTime.TryParse(node["creationDate"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var created)
                ? created.ToLocalTime() : null,
            node["isDraft"]?.GetValue<bool>() ?? false,
            node["mergeStatus"]?.ToString() ?? "",
            mine?["vote"]?.GetValue<int>() ?? 0,
            mine != null,
            string.Join(", ", reviewers.Select(r => r!["displayName"]?.ToString()).Where(n => !string.IsNullOrEmpty(n))),
            $"{ProjectUrl(repository?["project"]?["name"]?.ToString() ?? project)}/_git/{Uri.EscapeDataString(repoName)}/pullrequest/{id}");
    }

    private static string ShortBranch(string? reference) =>
        reference?.StartsWith("refs/heads/", StringComparison.Ordinal) == true ? reference["refs/heads/".Length..] : reference ?? "";

    /// <summary>Records an "Approved" vote by the signed-in user (adding them as a reviewer if needed).</summary>
    public Task ApproveAsync(PullRequest pr, CancellationToken ct = default) => VoteAsync(pr, VoteApproved, ct);

    /// <param name="vote">10 approved, 5 approved with suggestions, 0 reset, -5 waiting for author, -10 rejected.</param>
    public async Task VoteAsync(PullRequest pr, int vote, CancellationToken ct = default)
    {
        var me = await GetMyIdAsync(ct);
        await SendAsync(HttpMethod.Put, $"{PullRequestUrl(pr)}/reviewers/{me}?{Api}", new { vote }, "application/json", ct);
    }

    // ---------- code review ----------

    /// <summary>
    /// The files changed by the whole pull request (latest iteration compared with its base), plus the
    /// commits to read the new and old versions from.
    /// </summary>
    public async Task<(List<PullRequestFileChange> Files, string NewCommit, string BaseCommit)> GetChangesAsync(
        PullRequest pr, CancellationToken ct = default)
    {
        var iterations = await SendAsync(HttpMethod.Get, $"{PullRequestUrl(pr)}/iterations?{Api}", null, "application/json", ct);
        var last = iterations["value"]?.AsArray().LastOrDefault()
                   ?? throw new InvalidOperationException("This pull request has no changes yet.");
        var iterationId = last["id"]!.GetValue<int>();
        var newCommit = last["sourceRefCommit"]?["commitId"]?.ToString() ?? "";
        // The common commit is what the source branch is compared with, as on the Files page in Azure DevOps.
        var baseCommit = last["commonRefCommit"]?["commitId"]?.ToString() ?? last["targetRefCommit"]?["commitId"]?.ToString() ?? "";

        var files = new List<PullRequestFileChange>();
        var skip = 0;
        while (true)
        {
            var url = $"{PullRequestUrl(pr)}/iterations/{iterationId}/changes?$top=1000&$skip={skip}&$compareTo=0&{Api}";
            var page = await SendAsync(HttpMethod.Get, url, null, "application/json", ct);
            foreach (var entry in page["changeEntries"]?.AsArray() ?? new JsonArray())
            {
                var item = entry?["item"];
                // Folders are listed too; only files have content to review.
                if (item == null || item["isFolder"]?.GetValue<bool>() == true || item["gitObjectType"]?.ToString() == "tree") continue;
                var path = item["path"]?.ToString() ?? "";
                files.Add(new PullRequestFileChange(path, entry!["originalPath"]?.ToString() ?? path, entry["changeType"]?.ToString() ?? "edit"));
            }
            var nextSkip = page["nextSkip"]?.GetValue<int>() ?? 0;
            if (nextSkip <= skip || page["nextTop"]?.GetValue<int>() is null or 0) break;
            skip = nextSkip;
        }
        return (files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList(), newCommit, baseCommit);
    }

    /// <summary>A file's text at a commit; null when it is binary.</summary>
    public async Task<string?> GetFileTextAsync(PullRequest pr, string path, string commit, CancellationToken ct = default)
    {
        var url = $"{ProjectUrl(pr.Project)}/_apis/git/repositories/{pr.RepositoryId}/items?path={Uri.EscapeDataString(path)}" +
                  $"&versionDescriptor.version={commit}&versionDescriptor.versionType=commit&includeContent=true&{Api}";
        var item = await SendAsync(HttpMethod.Get, url, null, "application/json", ct);
        return item["contentMetadata"]?["isBinary"]?.GetValue<bool>() == true ? null : item["content"]?.ToString() ?? "";
    }

    /// <summary>All people's comments on the pull request (system messages and deleted comments left out).</summary>
    public async Task<List<PullRequestComment>> GetCommentsAsync(PullRequest pr, CancellationToken ct = default)
    {
        var json = await SendAsync(HttpMethod.Get, $"{PullRequestUrl(pr)}/threads?{Api}", null, "application/json", ct);
        var result = new List<PullRequestComment>();
        foreach (var thread in json["value"]?.AsArray() ?? new JsonArray())
        {
            if (thread == null || thread["isDeleted"]?.GetValue<bool>() == true) continue;
            var context = thread["threadContext"];
            var path = context?["filePath"]?.ToString() ?? "";
            var right = context?["rightFileStart"]?["line"]?.GetValue<int>();
            var left = context?["leftFileStart"]?["line"]?.GetValue<int>();
            var status = thread["status"]?.ToString() ?? "";
            foreach (var comment in thread["comments"]?.AsArray() ?? new JsonArray())
            {
                if (comment == null || comment["commentType"]?.ToString() == "system" || comment["isDeleted"]?.GetValue<bool>() == true) continue;
                result.Add(new PullRequestComment(path, right ?? left ?? 0, right == null && left != null,
                    comment["author"]?["displayName"]?.ToString() ?? "",
                    comment["content"]?.ToString() ?? "",
                    DateTime.TryParse(comment["publishedDate"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date)
                        ? date.ToLocalTime() : null,
                    status));
            }
        }
        return result;
    }

    /// <summary>Starts a new comment thread: on a file line when <paramref name="filePath"/> is given, otherwise a general one.</summary>
    /// <param name="leftSide">Anchor to the old version's line (for removed lines) instead of the new version's.</param>
    public async Task AddCommentAsync(PullRequest pr, string text, string? filePath, int line, bool leftSide, CancellationToken ct = default)
    {
        var thread = new JsonObject
        {
            ["comments"] = new JsonArray(new JsonObject { ["parentCommentId"] = 0, ["content"] = text, ["commentType"] = 1 }),
            ["status"] = 1 // active
        };
        if (filePath != null)
        {
            JsonObject Position() => new() { ["line"] = line, ["offset"] = 1 };
            thread["threadContext"] = leftSide
                ? new JsonObject { ["filePath"] = filePath, ["leftFileStart"] = Position(), ["leftFileEnd"] = Position() }
                : new JsonObject { ["filePath"] = filePath, ["rightFileStart"] = Position(), ["rightFileEnd"] = Position() };
        }
        await SendAsync(HttpMethod.Post, $"{PullRequestUrl(pr)}/threads?{Api}", thread, "application/json", ct);
    }

    /// <summary>
    /// Completes (merges) the pull request. Branch policies still apply: if a required reviewer, build
    /// or check is missing, Azure DevOps refuses and its reason is passed on.
    /// </summary>
    public async Task CompleteAsync(PullRequest pr, PullRequestCompletion options, CancellationToken ct = default)
    {
        // The merge must name the latest source commit; take it fresh rather than from the list.
        var current = await SendAsync(HttpMethod.Get, $"{PullRequestUrl(pr)}?{Api}", null, "application/json", ct);
        var commit = current["lastMergeSourceCommit"]?["commitId"]?.ToString()
                     ?? throw new InvalidOperationException("Azure DevOps did not report the pull request's latest commit.");

        var body = new
        {
            status = "completed",
            lastMergeSourceCommit = new { commitId = commit },
            completionOptions = new
            {
                mergeStrategy = options.MergeStrategy,
                deleteSourceBranch = options.DeleteSourceBranch,
                transitionWorkItems = options.CompleteWorkItems,
                mergeCommitMessage = options.MergeMessage
            }
        };
        // The merge itself may finish a moment later; the refreshed list then no longer shows the pull request.
        await SendAsync(HttpMethod.Patch, $"{PullRequestUrl(pr)}?{Api}", body, "application/json", ct);
    }

    public async Task<List<GitRepository>> GetRepositoriesAsync(string project, CancellationToken ct = default)
    {
        var json = await SendAsync(HttpMethod.Get, $"{ProjectUrl(project)}/_apis/git/repositories?{Api}", null, "application/json", ct);
        return (json["value"]?.AsArray() ?? new JsonArray())
            .Where(r => r != null && r["isDisabled"]?.GetValue<bool>() != true)
            .Select(r => new GitRepository(r!["id"]!.ToString(), r["name"]?.ToString() ?? "", ShortBranch(r["defaultBranch"]?.ToString())))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<string>> GetBranchesAsync(string project, string repositoryId, CancellationToken ct = default)
    {
        var url = $"{ProjectUrl(project)}/_apis/git/repositories/{repositoryId}/refs?filter=heads/&$top=1000&{Api}";
        var json = await SendAsync(HttpMethod.Get, url, null, "application/json", ct);
        return (json["value"]?.AsArray() ?? new JsonArray())
            .Select(r => ShortBranch(r?["name"]?.ToString()))
            .Where(n => n.Length > 0)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <returns>The new pull request's ID and web address.</returns>
    public async Task<(int Id, string WebUrl)> CreateAsync(string project, GitRepository repository, string sourceBranch,
        string targetBranch, string title, string description, bool isDraft, IReadOnlyCollection<string> workItemIds, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["sourceRefName"] = "refs/heads/" + sourceBranch,
            ["targetRefName"] = "refs/heads/" + targetBranch,
            ["title"] = title,
            ["description"] = description,
            ["isDraft"] = isDraft
        };
        if (workItemIds.Count > 0)
            body["workItemRefs"] = new JsonArray(workItemIds.Select(id => (JsonNode)new JsonObject { ["id"] = id }).ToArray());

        var created = await SendAsync(HttpMethod.Post,
            $"{ProjectUrl(project)}/_apis/git/repositories/{repository.Id}/pullrequests?{Api}", body, "application/json", ct);
        var id = created["pullRequestId"]!.GetValue<int>();
        return (id, $"{ProjectUrl(project)}/_git/{Uri.EscapeDataString(repository.Name)}/pullrequest/{id}");
    }
}
