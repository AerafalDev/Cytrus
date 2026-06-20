using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cytrus.App.Models;
using Cytrus.App.Services.Download;
using Cytrus.Cdn;
using Cytrus.Download;
using Cytrus.Models;
using ShadUI;

namespace Cytrus.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ICytrusCdnClient _cdn;
    private readonly IGameDownloader _downloader;

    private readonly DispatcherTimer _timer;
    private UiDownloadProgressSink? _sink;
    private CancellationTokenSource? _cts;
    private CytrusIndex? _index;
    private long _lastBytes;
    private DateTime _lastSample;

    [ObservableProperty]
    private string _game = "dofus";

    [ObservableProperty]
    private string _platform = "windows";

    [ObservableProperty]
    private string _release = "dofus3";

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _outputDirectory;

    [ObservableProperty]
    private string _selectPatterns = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedVersion))]
    private VersionRow? _selectedVersion;

    [ObservableProperty]
    private double _bytesProgress;

    [ObservableProperty]
    private double _filesProgress;

    [ObservableProperty]
    private string _statusText = "Idle";

    [ObservableProperty]
    private string _speedText = string.Empty;

    [ObservableProperty]
    private string _log = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private bool _isConfirming;

    public ObservableCollection<string> Games { get; } = [];
    public ObservableCollection<string> Platforms { get; }
    public ObservableCollection<string> Releases { get; } = [];
    public ObservableCollection<VersionRow> Versions { get; } = [];

    public ToastManager ToastManager { get; } = new();
    public DialogManager DialogManager { get; } = new();

    public bool IsIdle =>
        !IsDownloading;

    public bool HasSelectedVersion =>
        SelectedVersion is not null;

    private bool CanDownload =>
        !IsDownloading && !IsConfirming;

    private bool CanCancel =>
        IsDownloading;

    public MainViewModel(ICytrusCdnClient cdn, IGameDownloader downloader)
    {
        _cdn = cdn;
        _downloader = downloader;

        OutputDirectory = Path.Combine(Environment.CurrentDirectory, "output");
        Platforms = ["windows", "darwin", "linux"];

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => SampleProgress();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
    }

    partial void OnGameChanged(string value)
    {
        PopulateReleases();
    }

    partial void OnSelectedVersionChanged(VersionRow? value)
    {
        if (value is null) return;
        Game = value.Game;
        Platform = value.Platform;
        Release = value.Release;
        Version = value.Version;
    }

    [RelayCommand]
    private async Task RefreshIndexAsync()
    {
        try
        {
            StatusText = "Loading cytrus.json…";

            _index = await _cdn.GetIndexAsync();

            Versions.Clear();
            Games.Clear();

            foreach (var (gameName, game) in _index.Games.OrderBy(g => g.Value.Order))
            {
                Games.Add(gameName);

                foreach (var (platform, releases) in game.Platforms.OrderBy(p => p.Key))
                    foreach (var (release, version) in releases.OrderBy(r => r.Key))
                        Versions.Add(new VersionRow(gameName, platform, release, version));
            }

            PopulateReleases();
            StatusText = $"Loaded {Versions.Count} versions.";
            AppendLog($"Index loaded: {Games.Count} games, {Versions.Count} channels.");
        }
        catch (Exception ex)
        {
            StatusText = "Failed to load index.";
            AppendLog($"Error: {ex.Message}");
        }
    }

    private void PopulateReleases()
    {
        Releases.Clear();

        if (_index is null || !_index.Games.TryGetValue(Game, out var game))
            return;

        var releases = game.Platforms.Values
            .SelectMany(static p => p.Keys)
            .Distinct()
            .OrderBy(static r => r);

        foreach (var r in releases)
            Releases.Add(r);
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private void Download()
    {
        GameCoordinates coordinates;

        try
        {
            coordinates = new GameCoordinates(Game, Platform, Release, string.IsNullOrWhiteSpace(Version) ? null : Version.Trim());
        }
        catch (ArgumentException ex)
        {
            ToastManager.CreateToast("Invalid input").WithContent(ex.Message).ShowError();
            return;
        }

        var patterns = ParsePatterns();

        if (patterns is null)
        {
            IsConfirming = true;
            DialogManager.CreateDialog("Download the entire build?", $"No selection pattern is set, so the full {coordinates.Game}/{coordinates.Release} build will be downloaded. This can be several gigabytes.")
                .WithPrimaryButton("Download all", () =>
                {
                    IsConfirming = false;
                    _ = RunDownloadAsync(coordinates, null);
                })
                .WithCancelButton("Cancel", () => IsConfirming = false)
                .WithMaxWidth(520)
                .Show();
            return;
        }

        _ = RunDownloadAsync(coordinates, patterns);
    }

    private async Task RunDownloadAsync(GameCoordinates coordinates, string[]? patterns)
    {
        IsDownloading = true;
        BytesProgress = FilesProgress = 0;

        _sink = new UiDownloadProgressSink();
        _cts = new CancellationTokenSource();
        _lastBytes = 0;
        _lastSample = DateTime.UtcNow;

        _timer.Start();

        AppendLog($"Starting download of {coordinates.Game}/{coordinates.Release} ({coordinates.Platform})…");

        try
        {
            var request = new DownloadRequest
            {
                Coordinates = coordinates,
                OutputDirectory = OutputDirectory,
                Select = patterns,
                Progress = _sink
            };

            var result = await _downloader.DownloadAsync(request, _cts.Token);

            BytesProgress = 100;
            FilesProgress = 100;
            StatusText = "Done.";

            AppendLog($"Done version {result.Version}: {result.FilesWritten} written, " + $"{result.FilesSkipped} skipped, {result.Symlinks} symlinks, " + $"{FormatBytes(result.BytesDownloaded)} downloaded.");

            ToastManager
                .CreateToast($"Downloaded {coordinates.Game} {result.Version}")
                .WithContent($"{result.FilesWritten} files written · {FormatBytes(result.BytesDownloaded)}")
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
            SpeedText = string.Empty;
            BytesProgress = FilesProgress = 0;
            AppendLog("Download cancelled.");
            ToastManager.CreateToast("Download cancelled").DismissOnClick().ShowWarning();
        }
        catch (Exception ex)
        {
            StatusText = "Failed.";
            SpeedText = string.Empty;
            BytesProgress = FilesProgress = 0;
            AppendLog($"Error: {ex.Message}");
            ToastManager.CreateToast("Download failed").WithContent(ex.Message).DismissOnClick().ShowError();
        }
        finally
        {
            _timer.Stop();
            IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "Cancelling…";
    }

    private string[]? ParsePatterns()
    {
        var patterns = SelectPatterns.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return patterns.Length is 0 ? null : patterns;
    }

    private void SampleProgress()
    {
        if (_sink is null)
            return;

        var total = _sink.TotalDownloadBytes;
        var done = _sink.DownloadedBytes;

        BytesProgress = total > 0 ? Math.Min(100, done * 100.0 / total) : 0;
        FilesProgress = _sink.TotalFiles > 0 ? Math.Min(100, _sink.CompletedFiles * 100.0 / _sink.TotalFiles) : 0;

        StatusText = string.IsNullOrEmpty(_sink.CurrentFragment)
            ? $"{_sink.CompletedFiles}/{_sink.TotalFiles} files"
            : $"{_sink.CurrentFragment} — {_sink.CompletedFiles}/{_sink.TotalFiles} files";

        var now = DateTime.UtcNow;
        var dt = (now - _lastSample).TotalSeconds;

        if (dt >= 0.25)
        {
            var speed = (done - _lastBytes) / dt;
            SpeedText = $"{FormatBytes((long)speed)}/s";
            _lastBytes = done;
            _lastSample = now;
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Log = string.IsNullOrEmpty(Log) ? line : $"{Log}\n{line}";
    }

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = value;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
