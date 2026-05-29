using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// Home dashboard (M5 #17). Replaces the v0.18-era read-only project list
/// with per-project health cards — each card shows stale-doc count, recent
/// commit count, pending agent action count, and a last-activity timestamp,
/// computed lazily so the card list renders immediately and the metrics
/// fill in as IProjectHealthService.ComputeAsync resolves per card.
///
/// Clicking a card hands the project off to the Documentation tab via
/// <see cref="IHomeNavigator"/> — Home itself owns no project state beyond
/// the cards collection.
/// </summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly IProjectStore _projects;
    private readonly IProjectHealthService _health;
    private readonly IHomeNavigator _navigator;

    public override string Title => "Home";
    public override string Glyph => "\U0001F3E0";
    public override string Description =>
        "Project health at a glance — click a card to jump into Documentation.";

    public ObservableCollection<ProjectHealthCard> Cards { get; } = new();

    /// <summary>
    /// The slice of <see cref="Cards"/> currently visible on the dashboard.
    /// The ItemsControl binds to this — paginated to <see cref="PageSize"/>
    /// items per page so the card list doesn't spill off the bottom on
    /// projects-heavy installations.
    /// </summary>
    public ObservableCollection<ProjectHealthCard> PagedCards { get; } = new();

    private const int PageSize = 6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages), nameof(HasMultiplePages),
        nameof(PageLabel), nameof(CanGoPrevious), nameof(CanGoNext))]
    private int _currentPage; // 0-based

    public int TotalPages =>
        Cards.Count == 0 ? 1 : (int)Math.Ceiling(Cards.Count / (double)PageSize);

    public string PageLabel => $"Page {CurrentPage + 1} of {TotalPages}";
    public bool CanGoPrevious => CurrentPage > 0;
    public bool CanGoNext => CurrentPage < TotalPages - 1;

    /// <summary>
    /// Derived bool that controls the visibility of the pagination bar in
    /// the view. Using a single VM-side property keeps the XAML free of
    /// custom converters — Avalonia's built-in <c>ObjectConverters</c>
    /// doesn't ship a plain "not-equal-to-int-constant" out of the box.
    /// </summary>
    public bool HasMultiplePages => TotalPages > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;
    public bool IsNotBusy => !IsBusy;

    public HomeViewModel(
        IProjectStore projects,
        IProjectHealthService health,
        IHomeNavigator navigator)
    {
        _projects = projects;
        _health = health;
        _navigator = navigator;
        _projects.Changed += OnProjectsChanged;
        _ = RefreshAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await RefreshAsync());

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var all = await _projects.GetAllAsync();
            Cards.Clear();
            foreach (var p in all)
                Cards.Add(new ProjectHealthCard(p));

            // Clamp CurrentPage to the new range — if the user was on page 3
            // and projects got deleted leaving only 1 page, snap back. This
            // also covers the "Cards.Count == 0" case (CurrentPage = 0).
            var lastPageIndex = Math.Max(0, TotalPages - 1);
            if (CurrentPage > lastPageIndex)
                CurrentPage = lastPageIndex; // setter triggers OnCurrentPageChanged → RebuildPagedCards
            else
                RebuildPagedCards();

            // TotalPages / HasMultiplePages aren't [ObservableProperty]-backed
            // (they're derived from Cards.Count) so notify by hand whenever
            // the underlying collection size shifted.
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasMultiplePages));
            OnPropertyChanged(nameof(PageLabel));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));

            // Compute metrics in parallel — each ProjectHealthCard
            // exposes its own IsLoading flag so cards render immediately
            // and metrics fill in as they resolve.
            foreach (var card in Cards)
                _ = LoadMetricsAsync(card);
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Rebuild <see cref="PagedCards"/> from the current page-index window
    /// over <see cref="Cards"/>. Called whenever Cards is rebuilt or
    /// <see cref="CurrentPage"/> changes.
    /// </summary>
    private void RebuildPagedCards()
    {
        PagedCards.Clear();
        var start = CurrentPage * PageSize;
        foreach (var c in Cards.Skip(start).Take(PageSize))
            PagedCards.Add(c);
    }

    partial void OnCurrentPageChanged(int value) => RebuildPagedCards();

    [RelayCommand]
    private void NextPage()
    {
        if (CanGoNext) CurrentPage++;
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPrevious) CurrentPage--;
    }

    private async Task LoadMetricsAsync(ProjectHealthCard card)
    {
        try
        {
            var metrics = await _health.ComputeAsync(card.Project);
            await Dispatcher.UIThread.InvokeAsync(() => card.ApplyMetrics(metrics));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Home] Failed to load metrics for {card.Project.Name}: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => card.MarkLoadFailed());
        }
    }

    [RelayCommand]
    private void OpenProject(ProjectHealthCard? card)
    {
        if (card is null) return;
        _navigator.NavigateToDocumentation(card.Project);
    }
}

/// <summary>
/// One row in the Home dashboard's card list. Wraps a <see cref="Project"/>
/// plus its computed health metrics; loads in two phases so the card chrome
/// renders immediately and the numbers fill in asynchronously (the metric
/// computation can take a few hundred ms per project — git, doc scan, agent
/// log query — and serialising them all before showing anything would
/// produce a long blank-screen pause on the dashboard).
/// </summary>
public sealed partial class ProjectHealthCard : ObservableObject
{
    public Project Project { get; }
    public string Name => Project.Name;
    public string Description => Project.Description;
    public string FolderPath => Project.FolderPath;
    public DateTimeOffset LastActivity => Project.LastActivity;

    /// <summary>
    /// Pass-through to <see cref="Project.LogoPath"/> — null/blank means
    /// "no logo on disk" and the view falls back to the project glyph via
    /// <see cref="HasLogo"/>.
    /// </summary>
    public string? LogoPath => Project.LogoPath;

    /// <summary>
    /// True only when a bitmap successfully loaded from <see cref="LogoPath"/>.
    /// Drives the Image vs. fallback-glyph IsVisible toggle in HomeView.
    /// </summary>
    public bool HasLogo => _logoBitmap is not null;

    private Bitmap? _logoBitmap;
    public Bitmap? LogoBitmap
    {
        get => _logoBitmap;
        private set
        {
            if (SetProperty(ref _logoBitmap, value))
                OnPropertyChanged(nameof(HasLogo));
        }
    }

    public string LastActivityLabel =>
        Project.LastActivity == default
            ? "Never"
            : Project.LastActivity.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StaleDocLabel))]
    private int _staleDocCount;
    public string StaleDocLabel => StaleDocCount.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecentCommitLabel))]
    private int? _recentCommitCount;
    public string RecentCommitLabel =>
        RecentCommitCount.HasValue
            ? RecentCommitCount.Value.ToString()
            : "—"; // em-dash for "no data"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingActionLabel))]
    private int _pendingActionCount;
    public string PendingActionLabel => PendingActionCount.ToString();

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _loadFailed;

    public ProjectHealthCard(Project project)
    {
        Project = project;
        TryLoadLogo();
    }

    public void ApplyMetrics(ProjectHealthMetrics m)
    {
        StaleDocCount       = m.StaleDocCount;
        RecentCommitCount   = m.RecentCommitCount;
        PendingActionCount  = m.PendingActionCount;
        IsLoading = false;
        LoadFailed = false;
    }

    public void MarkLoadFailed() { IsLoading = false; LoadFailed = true; }

    /// <summary>
    /// Eager-load the logo bitmap on the constructor thread (UI thread for
    /// the normal RefreshAsync path). Logos are typically small favicons /
    /// PNGs (32–256 KB); the 36×36 Image viewport with UniformToFill
    /// downsamples on render so a larger source is acceptable for v1. Any
    /// load failure (missing file, unsupported format, permission denied)
    /// silently falls back to the project glyph — a bad logo path must NOT
    /// block card rendering.
    /// </summary>
    private void TryLoadLogo()
    {
        if (string.IsNullOrWhiteSpace(LogoPath) || !File.Exists(LogoPath)) return;
        try
        {
            using var stream = File.OpenRead(LogoPath);
            LogoBitmap = new Bitmap(stream);
        }
        catch
        {
            // Bad image / unsupported format / permission denied — silently
            // fall back to the project glyph.
        }
    }
}
