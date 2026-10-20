using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace TaskTimer.Services;

/// <summary>Raised when a Microsoft sign-in is needed but the call was not allowed to show one.</summary>
public sealed class SignInRequiredException : InvalidOperationException
{
    public SignInRequiredException() : base("Sign in to Microsoft is needed.") { }
}

/// <summary>
/// Signs the user in to Microsoft Entra ID and hands out tokens for Azure Resource Manager and the
/// LCS API, without the Azure CLI. The sign-in happens once in the browser; tokens are then kept
/// in a cache encrypted for the current Windows user and renewed silently.
/// </summary>
public static class AzureAuth
{
    // The Azure CLI's public client ID: Microsoft pre-authorises it for Azure Resource Manager and LCS,
    // so no app registration is needed in the user's tenant.
    private const string ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
    private const string CacheFile = "msal.cache";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IPublicClientApplication? _app;

    private static async Task<IPublicClientApplication> GetAppAsync()
    {
        if (_app != null) return _app;
        var app = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, "organizations")
            .WithRedirectUri("http://localhost")
            .Build();

        var folder = Path.GetDirectoryName(SettingsStore.PathFor(CacheFile))!;
        Directory.CreateDirectory(folder);
        var storage = new StorageCreationPropertiesBuilder(CacheFile, folder).Build(); // DPAPI-protected on Windows
        (await MsalCacheHelper.CreateAsync(storage)).RegisterCache(app.UserTokenCache);
        return _app = app;
    }

    /// <param name="resource">For example "https://management.azure.com".</param>
    /// <param name="interactive">
    /// Whether a browser sign-in may be shown. Background status checks pass false and get a
    /// <see cref="SignInRequiredException"/> instead, so the browser only opens when the user acted.
    /// </param>
    public static async Task<string> GetTokenAsync(string resource, bool interactive, CancellationToken ct = default)
    {
        var scopes = new[] { resource.TrimEnd('/') + "/.default" };
        // One sign-in at a time: several machines are checked at once, and only the first may prompt.
        await Gate.WaitAsync(ct);
        try
        {
            var app = await GetAppAsync();
            var account = (await app.GetAccountsAsync()).FirstOrDefault();
            if (account != null)
            {
                try
                {
                    return (await app.AcquireTokenSilent(scopes, account).ExecuteAsync(ct)).AccessToken;
                }
                catch (MsalUiRequiredException)
                {
                    // Expired or new consent needed: fall through to the browser sign-in.
                }
            }

            if (!interactive) throw new SignInRequiredException();
            var builder = app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false);
            builder = account == null ? builder.WithPrompt(Prompt.SelectAccount) : builder.WithAccount(account);
            var result = await builder.ExecuteAsync(ct);
            return result.AccessToken;
        }
        catch (MsalException ex) when (ex is not MsalUiRequiredException)
        {
            throw new InvalidOperationException("Microsoft sign-in failed: " + ex.Message, ex);
        }
        finally
        {
            Gate.Release();
        }
    }
}
