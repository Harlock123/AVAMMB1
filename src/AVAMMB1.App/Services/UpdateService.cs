using AVAMMB1.App.ViewModels;
using AVAMMB1.Core.Info;

namespace AVAMMB1.App.Services;

/// <summary>Asks GitHub for the newest release. Sends nothing but the request (and the game's name and version as the user agent).</summary>
public static class UpdateService
{
    /// <summary>The newest published release's version, or null if it can't be found out.</summary>
    /// <param name="cancel">Cancels the request.</param>
    public static async Task<Version?> FetchLatestAsync(CancellationToken cancel)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"AVAMMB1/{MainViewModel.GameVersion}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var json = await http.GetStringAsync(UpdateCheck.LatestReleaseApi, cancel).ConfigureAwait(false);
            return UpdateCheck.LatestFromJson(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
