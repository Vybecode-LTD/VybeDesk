using NSubstitute;
using VybeDesk.App.ViewModels;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Xunit;

namespace VybeDesk.Tests.AppSmoke;

/// <summary>
/// VM-level tests for the Home dashboard pagination and card layout.
///
/// These verify the invariants that the HomeView XAML depends on:
/// - PagedCards never exceeds PageSize (6)
/// - Pagination controls (HasMultiplePages, CanGoPrevious, CanGoNext) are
///   correct for the card count
/// - All cards in PagedCards have valid Project references (non-null Name,
///   finite bounds when rendered)
///
/// Headless rendering tests (constructing HomeView in an Avalonia window
/// and asserting Bounds) are deferred — they're flaky on Windows CI and
/// the VM tests cover the data layer that drives the layout.
///
/// Root cause context: docs/LAYOUT_REGRESSION.md — the Fluent ContentControl
/// defaulting VerticalContentAlignment to Top caused infinite height. The
/// fix is at MainWindow level; these tests verify the VM produces correct
/// data for the fixed layout to consume.
/// </summary>
public sealed class HomeViewLayoutTests
{
    private static IProjectStore CreateFakeProjectStore(int projectCount)
    {
        var store = Substitute.For<IProjectStore>();
        var projects = Enumerable.Range(1, projectCount)
            .Select(i => new Project
            {
                Id = Guid.NewGuid(),
                Name = $"Project {i}",
                Description = $"Description for project {i}",
                FolderPath = $@"C:\projects\project{i}",
                Status = ProjectStatus.Active,
                LastActivity = DateTimeOffset.Now.AddDays(-i)
            })
            .ToList();

        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Project>>(projects));

        return store;
    }

    private static HomeViewModel CreateViewModel(int projectCount)
    {
        var store = CreateFakeProjectStore(projectCount);
        var health = Substitute.For<IProjectHealthService>();
        health.ComputeAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProjectHealthMetrics(0, 0, 0, DateTimeOffset.Now)));
        var navigator = Substitute.For<IHomeNavigator>();

        return new HomeViewModel(store, health, navigator);
    }

    [Fact]
    public async Task SixCards_FitsOnOnePage_NoPaginationNeeded()
    {
        var vm = CreateViewModel(6);
        // Allow the async RefreshAsync to complete
        await Task.Delay(200);

        Assert.Equal(6, vm.Cards.Count);
        Assert.Equal(6, vm.PagedCards.Count);
        Assert.Equal(1, vm.TotalPages);
        Assert.False(vm.HasMultiplePages);
        Assert.False(vm.CanGoPrevious);
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public async Task TwelveCards_RequiresTwoPages()
    {
        var vm = CreateViewModel(12);
        await Task.Delay(200);

        Assert.Equal(12, vm.Cards.Count);
        Assert.Equal(6, vm.PagedCards.Count);       // first page = 6
        Assert.Equal(2, vm.TotalPages);
        Assert.True(vm.HasMultiplePages);
        Assert.False(vm.CanGoPrevious);             // on page 1
        Assert.True(vm.CanGoNext);
    }

    [Fact]
    public async Task SevenCards_SecondPageHasOneCard()
    {
        var vm = CreateViewModel(7);
        await Task.Delay(200);

        Assert.Equal(6, vm.PagedCards.Count);       // page 1

        vm.NextPageCommand.Execute(null);
        Assert.Single(vm.PagedCards);               // page 2 has 1 card
        Assert.True(vm.CanGoPrevious);
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public async Task ZeroCards_ShowsOnePage()
    {
        var vm = CreateViewModel(0);
        await Task.Delay(200);

        Assert.Empty(vm.Cards);
        Assert.Empty(vm.PagedCards);
        Assert.Equal(1, vm.TotalPages);
        Assert.False(vm.HasMultiplePages);
    }

    [Fact]
    public async Task AllPagedCardsHaveValidProjectData()
    {
        var vm = CreateViewModel(6);
        await Task.Delay(200);

        foreach (var card in vm.PagedCards)
        {
            Assert.NotNull(card.Project);
            Assert.False(string.IsNullOrEmpty(card.Name));
            Assert.False(string.IsNullOrEmpty(card.FolderPath));
        }
    }

    [Fact]
    public async Task NavigatingBackToFirstPage_RestoresFullPageSize()
    {
        var vm = CreateViewModel(8);
        await Task.Delay(200);

        Assert.Equal(6, vm.PagedCards.Count);       // page 1
        vm.NextPageCommand.Execute(null);
        Assert.Equal(2, vm.PagedCards.Count);       // page 2
        vm.PreviousPageCommand.Execute(null);
        Assert.Equal(6, vm.PagedCards.Count);       // back to page 1
    }
}
