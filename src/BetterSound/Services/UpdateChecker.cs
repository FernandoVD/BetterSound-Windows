using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace BetterSound.Services;

/// <summary>
/// Windows port of UpdateChecker.swift: polls GitHub Releases, never
/// downloads or installs anything silently, just surfaces a link. Update
/// the Repo constant once this project has its own GitHub repository —
/// it's a placeholder for now.
/// </summary>
public sealed class UpdateChecker : INotifyPropertyChanged
{
    public sealed record Release(string Version, string HtmlUrl);

    private const string Repo = "FernandoVD/BetterSound-Windows";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private readonly HttpClient _http = new();
    private readonly DispatcherTimer _timer = new();

    public Release? AvailableUpdate { get; private set; }
    public bool IsChecking { get; private set; }
    public bool CheckFailed { get; private set; }
    public DateTime? LastCheckedAt { get; private set; }

    public string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpdateChecker()
    {
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BetterSound-Windows");
        _timer.Interval = CheckInterval;
        _timer.Tick += async (_, _) => await Check();
    }

    public void SyncPeriodicChecks(bool enabled)
    {
        _timer.Stop();
        if (!enabled) return;

        _ = Check();
        _timer.Start();
    }

    public async Task Check()
    {
        IsChecking = true;
        CheckFailed = false;
        RaiseAll(nameof(IsChecking), nameof(CheckFailed));
        LastCheckedAt = DateTime.Now;

        try
        {
            var json = await _http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? "";

            var latestVersion = tagName.StartsWith('v') ? tagName[1..] : tagName;
            AvailableUpdate = IsNewer(latestVersion, CurrentVersion)
                ? new Release(latestVersion, htmlUrl)
                : null;
        }
        catch
        {
            CheckFailed = true;
        }
        finally
        {
            IsChecking = false;
            RaiseAll(nameof(IsChecking), nameof(CheckFailed), nameof(AvailableUpdate), nameof(LastCheckedAt));
        }
    }

    private static bool IsNewer(string a, string b)
    {
        var aParts = a.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var bParts = b.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        for (var i = 0; i < Math.Max(aParts.Length, bParts.Length); i++)
        {
            var x = i < aParts.Length ? aParts[i] : 0;
            var y = i < bParts.Length ? bParts[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    private void RaiseAll(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
