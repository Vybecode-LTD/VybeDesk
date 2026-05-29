using NSubstitute;
using VybeDesk.App.ViewModels;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Xunit;

namespace VybeDesk.Tests.AppSmoke;

/// <summary>
/// VM-level tests for the Projects editor form layout.
///
/// These verify the invariants that the ProjectsView XAML depends on:
/// - Selecting a project populates ALL edit fields (including the M4 #16
///   additions: Model, DefaultOutputPath, LogoPath)
/// - HasSelection toggles correctly to control form visibility
/// - The Save command correctly writes all fields back to the store
///
/// The layout regression context: ProjectsView's form grew past the
/// viewport in M4 #16 (model/output/logo fields added). The root cause
/// (Fluent ContentControl VerticalContentAlignment defaulting to Top)
/// was fixed at the MainWindow level. These tests verify the VM layer
/// provides correct data so the ScrollViewer has something meaningful
/// to scroll.
/// </summary>
public sealed class ProjectsViewLayoutTests
{
    private static (ProjectsViewModel vm, IProjectStore store) CreateViewModel(
        params Project[] projects)
    {
        var store = Substitute.For<IProjectStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Project>>(projects.ToList()));

        var picker = Substitute.For<IFilePickerService>();
        var launcher = Substitute.For<IClaudeCodeLauncher>();
        var importer = Substitute.For<IProjectImportService>();

        var vm = new ProjectsViewModel(store, picker, launcher, importer);
        return (vm, store);
    }

    [Fact]
    public async Task SelectingProject_PopulatesAllFormFields()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "A test description",
            FolderPath = @"C:\dev\test",
            Status = ProjectStatus.Active,
            Model = "claude-sonnet-4-6",
            DefaultOutputPath = @"C:\output",
            LogoPath = @"C:\logo.png"
        };

        var (vm, _) = CreateViewModel(project);
        await Task.Delay(200); // wait for LoadAsync

        vm.SelectedProject = vm.Projects.FirstOrDefault();
        Assert.NotNull(vm.SelectedProject);

        // All form fields that the ScrollViewer must make reachable
        Assert.Equal("Test Project", vm.EditName);
        Assert.Equal("A test description", vm.EditDescription);
        Assert.Equal(@"C:\dev\test", vm.EditFolderPath);
        Assert.Equal(ProjectStatus.Active, vm.EditStatus);
        Assert.Equal("claude-sonnet-4-6", vm.EditModel);
        Assert.Equal(@"C:\output", vm.EditDefaultOutputPath);
        Assert.Equal(@"C:\logo.png", vm.EditLogoPath);
    }

    [Fact]
    public async Task HasSelection_TrueWhenProjectSelected()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Proj",
            FolderPath = @"C:\dev"
        };

        var (vm, _) = CreateViewModel(project);
        await Task.Delay(200);

        Assert.False(vm.HasSelection); // nothing selected initially

        vm.SelectedProject = vm.Projects.First();
        Assert.True(vm.HasSelection);
    }

    [Fact]
    public async Task NullModelOnProject_MapsToEmptyEditModel()
    {
        // Null Model/DefaultOutputPath means "use global default" — the
        // edit field shows empty so the watermark hint is visible.
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "NullModel",
            FolderPath = @"C:\dev",
            Model = null,
            DefaultOutputPath = null,
            LogoPath = null
        };

        var (vm, _) = CreateViewModel(project);
        await Task.Delay(200);

        vm.SelectedProject = vm.Projects.First();
        Assert.Equal("", vm.EditModel);
        Assert.Equal("", vm.EditDefaultOutputPath);
        Assert.Equal("", vm.EditLogoPath);
    }

    [Fact]
    public async Task DeselectingProject_ClearsAllFields()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "ToDeselect",
            Description = "Will deselect",
            FolderPath = @"C:\dev",
            Model = "claude-opus-4",
            DefaultOutputPath = @"C:\out",
            LogoPath = @"C:\icon.png"
        };

        var (vm, _) = CreateViewModel(project);
        await Task.Delay(200);

        vm.SelectedProject = vm.Projects.First();
        Assert.True(vm.HasSelection);

        vm.SelectedProject = null;
        Assert.False(vm.HasSelection);
        Assert.Equal("", vm.EditName);
        Assert.Equal("", vm.EditDescription);
        Assert.Equal("", vm.EditModel);
        Assert.Equal("", vm.EditDefaultOutputPath);
        Assert.Equal("", vm.EditLogoPath);
    }

    [Fact]
    public async Task SaveCommand_WritesAllFieldsBackToStore()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            FolderPath = @"C:\dev"
        };

        var (vm, store) = CreateViewModel(project);
        await Task.Delay(200);

        vm.SelectedProject = vm.Projects.First();

        // Simulate the user editing all fields in the form
        vm.EditName = "Renamed";
        vm.EditDescription = "New description";
        vm.EditFolderPath = @"C:\new\path";
        vm.EditStatus = ProjectStatus.OnHold;
        vm.EditModel = "claude-haiku-4-5";
        vm.EditDefaultOutputPath = @"C:\new\output";
        vm.EditLogoPath = @"C:\new\logo.png";

        await vm.SaveCommand.ExecuteAsync(null);

        // Verify the store received the update
        await store.Received(1).UpdateAsync(Arg.Is<Project>(p =>
            p.Name == "Renamed" &&
            p.Description == "New description" &&
            p.FolderPath == @"C:\new\path" &&
            p.Status == ProjectStatus.OnHold &&
            p.Model == "claude-haiku-4-5" &&
            p.DefaultOutputPath == @"C:\new\output" &&
            p.LogoPath == @"C:\new\logo.png"
        ), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyModelString_SavesAsNullOnProject()
    {
        // Empty edit field → null on the Project → "use global default".
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "GlobalDefault",
            FolderPath = @"C:\dev",
            Model = "claude-opus-4"
        };

        var (vm, store) = CreateViewModel(project);
        await Task.Delay(200);

        vm.SelectedProject = vm.Projects.First();
        vm.EditModel = "";               // clear = use global default
        vm.EditDefaultOutputPath = "";   // clear = use global default

        await vm.SaveCommand.ExecuteAsync(null);

        await store.Received(1).UpdateAsync(Arg.Is<Project>(p =>
            p.Model == null &&
            p.DefaultOutputPath == null
        ), Arg.Any<CancellationToken>());
    }
}
