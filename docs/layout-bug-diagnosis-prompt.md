# Layout Bug Diagnosis — ScrollViewer won't scroll in two Avalonia views

I have an Avalonia 11.3 (.NET 9) desktop app with 11 sidebar pages. Two of them — **HomeView** and **ProjectsView** — have content that overflows the window vertically, but the ScrollViewer does not scroll. All the other views with scrollable content work fine. I've made **8 attempts** to fix this and none of them worked. I need fresh eyes on the root cause.

## The app structure

The main window has a two-column Grid. The left column (244px) is a sidebar with a TreeView for navigation. The right column (*) holds a Border with a ContentControl that displays the current view via a ViewLocator (IDataTemplate that maps FooViewModel → FooView by naming convention).

**MainWindow.axaml:**
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:VybeDesk.App.ViewModels"
        x:Class="VybeDesk.App.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="VybeDesk — VybeDesk"
        Width="1180" Height="760"
        MinWidth="940" MinHeight="600"
        WindowStartupLocation="CenterScreen"
        WindowState="Maximized">

    <Grid ColumnDefinitions="244,*" RowDefinitions="*">
        <!-- Sidebar (column 0) -->
        <Border Grid.Column="0" Background="#1E1E24">
            <DockPanel>
                <StackPanel DockPanel.Dock="Top" Margin="20,18,20,18" Spacing="2">
                    <TextBlock Text="VybeDesk" FontSize="20" FontWeight="SemiBold"/>
                    <TextBlock Text="VybeDesk" FontSize="11" Opacity="0.45"/>
                </StackPanel>
                <TreeView ItemsSource="{Binding Pages}"
                          SelectedItem="{Binding CurrentPage, Mode=TwoWay}"
                          Background="Transparent" BorderThickness="0" Padding="8,0,8,8">
                    <TreeView.DataTemplates>
                        <TreeDataTemplate DataType="vm:PageViewModel"
                                          ItemsSource="{Binding Children}">
                            <StackPanel Orientation="Horizontal" Spacing="12" Margin="4">
                                <TextBlock Text="{Binding Glyph}" FontSize="16"
                                           Width="22" TextAlignment="Center"/>
                                <TextBlock Text="{Binding Title}" VerticalAlignment="Center"/>
                            </StackPanel>
                        </TreeDataTemplate>
                    </TreeView.DataTemplates>
                </TreeView>
            </DockPanel>
        </Border>

        <!-- Content (column 1) -->
        <Border Grid.Column="1" Background="#26262E">
            <ContentControl Content="{Binding CurrentPage}"/>
        </Border>
    </Grid>
</Window>
```

**ViewLocator.cs:**
```csharp
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "null" };
        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "View not found: " + name };
    }
    public bool Match(object? data) => data is ObservableObject;
}
```

The ViewLocator is registered as a global DataTemplate in App.axaml:
```xml
<Application.DataTemplates>
    <local:ViewLocator/>
</Application.DataTemplates>
```

## Global styles in App.axaml that affect scrolling

```xml
<Application.Styles>
    <FluentTheme/>

    <Style Selector="Button">
        <Setter Property="CornerRadius" Value="6"/>
        <Setter Property="Padding" Value="12,5"/>
        <Setter Property="FontSize" Value="12"/>
    </Style>

    <Style Selector="ScrollViewer">
        <Setter Property="AllowAutoHide" Value="False"/>
    </Style>

    <Style Selector="ScrollBar:vertical">
        <Setter Property="Width" Value="8"/>
        <Setter Property="MinWidth" Value="8"/>
        <Setter Property="MaxWidth" Value="8"/>
    </Style>
    <Style Selector="ScrollBar:horizontal">
        <Setter Property="Height" Value="8"/>
        <Setter Property="MinHeight" Value="8"/>
        <Setter Property="MaxHeight" Value="8"/>
    </Style>

    <Style Selector="ScrollViewer /template/ ScrollContentPresenter#PART_ContentPresenter">
        <Setter Property="Margin" Value="0,0,50,0"/>
    </Style>
</Application.Styles>
```

## The ModuleHeader control (used by every view)

Every view has a `ModuleHeader` UserControl at the top. It has a fixed-height outer Border:

```xml
<!-- ModuleHeader.axaml (abbreviated) -->
<UserControl x:Class="VybeDesk.App.Controls.ModuleHeader"
             x:DataType="vm:PageViewModel" Name="Root">
    <Border Background="#1B1B22" Height="105">
        <Grid RowDefinitions="*,25">
            <!-- Row 0: title, glyph, breadcrumbs, project picker, reset/restart chips -->
            <!-- Row 1: 25px status bar -->
        </Grid>
    </Border>
</UserControl>
```

The outer Border has `Height="105"` so it should report a fixed desired height. But the UserControl itself has no explicit Height constraint.

## The two BROKEN views

### ProjectsView (broken — no scrollbar, content overflows past window)

Current state after my most recent fix attempt (Grid root, no DockPanel):

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:vm="using:VybeDesk.App.ViewModels"
             xmlns:m="using:VybeDesk.Core.Models"
             xmlns:ctl="using:VybeDesk.App.Controls"
             x:Class="VybeDesk.App.Views.ProjectsView"
             x:DataType="vm:ProjectsViewModel">

    <Grid RowDefinitions="Auto,*" ColumnDefinitions="340,*">

        <ctl:ModuleHeader Grid.Row="0" Grid.ColumnSpan="2"
                          StatusMessage="{Binding StatusMessage}"/>

        <!-- LEFT: project list -->
        <Border Grid.Row="1" Grid.Column="0" Background="#22222A" Padding="14">
            <DockPanel>
                <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="6">
                    <Button Content="New project" Command="{Binding NewProjectCommand}"
                            IsEnabled="{Binding IsNotBusy}"/>
                    <Button Content="Import existing…" Command="{Binding ImportExistingCommand}"
                            IsEnabled="{Binding IsNotBusy}"/>
                </StackPanel>
                <ListBox Margin="0,12,0,0"
                         ItemsSource="{Binding Projects}"
                         SelectedItem="{Binding SelectedProject}"
                         Background="Transparent" BorderThickness="0">
                    <ListBox.ItemTemplate>
                        <DataTemplate x:DataType="m:Project">
                            <StackPanel Spacing="2" Margin="2,4">
                                <TextBlock Text="{Binding Name}" FontWeight="SemiBold"
                                           TextTrimming="CharacterEllipsis"/>
                                <TextBlock Text="{Binding FolderPath}" FontSize="10"
                                           Opacity="0.45" TextTrimming="CharacterEllipsis"/>
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </DockPanel>
        </Border>

        <!-- RIGHT: editor form -->
        <ScrollViewer Grid.Row="1" Grid.Column="1" Padding="28,28,50,28">
            <StackPanel Spacing="16" MaxWidth="640" HorizontalAlignment="Left">
                <TextBlock Text="Select a project on the left or create a new one."
                           Opacity="0.5" IsVisible="{Binding !HasSelection}"/>
                <StackPanel Spacing="12" IsVisible="{Binding HasSelection}">
                    <TextBlock Text="Edit project" FontSize="18" FontWeight="SemiBold"/>
                    <!-- ~8 form field groups: Name, Description, Folder, Status,
                         Logo, Model dropdown + custom ID, Default output path -->
                    <!-- ... form fields ... -->
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <Button Content="Save" Command="{Binding SaveCommand}"/>
                        <Button Content="Delete" Command="{Binding DeleteCommand}"/>
                        <Button Content="Open in Claude Code" Command="{Binding OpenInClaudeCodeCommand}"/>
                    </StackPanel>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </Grid>
</UserControl>
```

**Symptom:** The form has ~8 field groups plus action buttons. The content extends past the bottom of the window. No scrollbar appears at all. The Save/Delete/Open buttons are unreachable.

### HomeView (broken — scrollbar appears but barely moves)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:vm="using:VybeDesk.App.ViewModels"
             xmlns:m="using:VybeDesk.Core.Models"
             xmlns:ctl="using:VybeDesk.App.Controls"
             x:Class="VybeDesk.App.Views.HomeView"
             x:DataType="vm:HomeViewModel">

    <Grid RowDefinitions="Auto,*">
        <ctl:ModuleHeader Grid.Row="0"/>

        <ScrollViewer Grid.Row="1" Padding="32,32,50,32">
            <StackPanel Spacing="14" MaxWidth="960" HorizontalAlignment="Left">
                <TextBlock Text="No projects yet — open Projects..."
                           Opacity="0.5" TextWrapping="Wrap"
                           IsVisible="{Binding !Cards.Count}"/>
                <ItemsControl ItemsSource="{Binding PagedCards}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate x:DataType="vm:ProjectHealthCard">
                            <Button Classes="home-card" Padding="0"
                                    Command="{Binding $parent[ItemsControl].((vm:HomeViewModel)DataContext).OpenProjectCommand}"
                                    CommandParameter="{Binding}" Margin="0,0,0,12"
                                    HorizontalAlignment="Stretch" HorizontalContentAlignment="Stretch">
                                <Border Background="#2F2F38" CornerRadius="8" Padding="18">
                                    <StackPanel Spacing="8">
                                        <!-- card content: logo, name, description, path, metrics -->
                                    </StackPanel>
                                </Border>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
                <!-- Pagination controls at the bottom -->
                <StackPanel Orientation="Horizontal" Spacing="8"
                            HorizontalAlignment="Center" IsVisible="{Binding HasMultiplePages}">
                    <Button Content="‹ Previous" Command="{Binding PreviousPageCommand}"/>
                    <TextBlock Text="{Binding PageLabel}" VerticalAlignment="Center"/>
                    <Button Content="Next ›" Command="{Binding NextPageCommand}"/>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </Grid>
</UserControl>
```

**Symptom:** With 5+ project cards, the list overflows past the bottom of the window. A scrollbar DOES appear, but dragging it only moves the content a few pixels — like the ScrollViewer thinks the extent is roughly equal to the viewport.

## Three WORKING views for comparison

### DocumentationView (works — scrolls correctly)

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top" ShowPicker="True" .../>

    <Border DockPanel.Dock="Left" Width="320" Background="#22222A" Padding="14">
        <DockPanel>
            <StackPanel DockPanel.Dock="Top" Spacing="10">
                <!-- folder path + scan controls -->
            </StackPanel>
            <ListBox ... />  <!-- fill child -->
        </DockPanel>
    </Border>

    <!-- Fill child of the OUTER DockPanel -->
    <Grid Margin="20" RowDefinitions="Auto,*" RowSpacing="12">
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            <!-- action toolbar: severity chips + buttons -->
        </StackPanel>
        <Grid Grid.Row="1">
            <!-- Three overlaid states, each with their own ScrollViewer -->
            <Grid IsVisible="..." RowDefinitions="*,Auto,Auto" RowSpacing="12">
                <Border Grid.Row="0"><!-- findings list (bounded * row) --></Border>
                <Border Grid.Row="1" MaxHeight="220"><!-- AI result --></Border>
                <Border Grid.Row="2"><!-- fix prompt --></Border>
            </Grid>
            <DockPanel IsVisible="...">
                <!-- audit overlay with its own ScrollViewer -->
                <ScrollViewer>
                    <StackPanel MaxWidth="900"><!-- audit body --></StackPanel>
                </ScrollViewer>
            </DockPanel>
        </Grid>
    </Grid>
</DockPanel>
```

### BugTrackerView (works — scrolls correctly)

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top" ShowPicker="True" .../>
    <Border DockPanel.Dock="Left" Width="340" Background="#22222A" Padding="14">
        <!-- left rail content -->
    </Border>
    <!-- Fill child: the right pane with ScrollViewer -->
    <ScrollViewer Padding="28,28,50,28">
        <StackPanel Spacing="16" MaxWidth="700" HorizontalAlignment="Left">
            <!-- editor form content -->
        </StackPanel>
    </ScrollViewer>
</DockPanel>
```

### NotebookView (works — scrolls correctly)

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top" ShowPicker="True" .../>
    <Grid ColumnDefinitions="*,366">
        <DockPanel Grid.Column="0" Margin="24,20,12,20">
            <Grid DockPanel.Dock="Bottom"><!-- input bar --></Grid>
            <ScrollViewer><!-- chat messages --></ScrollViewer>
        </DockPanel>
        <!-- Column 1: notes sidebar -->
    </Grid>
</DockPanel>
```

## Every fix attempt and the result

### Previous session (4 attempts, all failed)

**Plan A — bounded Grid pattern (RowDefinitions="Auto,*,Auto"):**
Wrapped the ScrollViewer form content in `DockPanel > Grid RowDefinitions="*,Auto"` with buttons in the Auto footer. Same shape that works in 5 wizard views. **Result: no scrollbar at all. Failed.**

**Plan B — explicit RowDefinitions="*" on MainWindow + nested explicit Grids:**
Added `RowDefinitions="*"` to MainWindow's outer Grid, rewrote both views with explicit `Grid RowDefinitions="Auto,*,Auto"`. **Result: no change. Failed.** (The MainWindow RowDefinitions="*" was kept.)

**Plan C — remove global ScrollContentPresenter Margin style:**
Removed the `ScrollViewer /template/ ScrollContentPresenter#PART_ContentPresenter { Margin: 0,0,50,0 }` style from App.axaml. **Result: no change. Failed.** Style was restored.

**Plan D — revert to simple pre-v0.31 pattern (Grid root, no DockPanel, no ModuleHeader):**
Replicated the exact shape from an earlier commit where ProjectsView worked. **Result: no change. Failed.** (But see note: that earlier version never had enough content to actually test scrolling.)

### This session (4 more attempts, all failed)

**Fix 1 — ContentControl alignment stretch:**
Added `HorizontalContentAlignment="Stretch" VerticalContentAlignment="Stretch"` to MainWindow's ContentControl. **Result: no change. Failed.** Reverted.

**Fix 2 — intermediate Grid RowDefinitions="*" wrapper:**
Wrapped the ScrollViewer in both views inside an additional `Grid RowDefinitions="*"` between the DockPanel fill area and the ScrollViewer. **Result: no change. Failed.** Reverted.

**Fix 3 — DockPanel.Dock="Left" for rail (matching DocumentationView):**
Changed ProjectsView's left rail from `Grid.Column="0"` in a `Grid ColumnDefinitions="340,*"` to `DockPanel.Dock="Left" Width="340"`, matching the pattern DocumentationView uses. **Result: no change. Failed.** Reverted.

**Fix 4 — Grid root replacing DockPanel entirely:**
Replaced the DockPanel root in both views with a single `Grid RowDefinitions="Auto,*"` (and `ColumnDefinitions="340,*"` for ProjectsView). ModuleHeader in the Auto row, ScrollViewer in the * row. No DockPanel anywhere. **Result: no change. Failed.** This is the current state of the code.

**Fix 5 — disable ALL global scroll styles (diagnostic):**
Commented out all three scroll-related global styles from App.axaml (AllowAutoHide, ScrollBar width/height, ScrollContentPresenter Margin). **Result: no change. Failed.** Styles restored.

## What I've ruled out

- **View-internal layout structure**: 6 different view-level XAML arrangements tried. None helped.
- **DockPanel vs Grid as root**: Both tried. No difference.
- **DockPanel.Dock="Left" vs Grid.Column for the rail**: Both tried. No difference.
- **Extra intermediate Grid wrappers**: Tried. No difference.
- **ContentControl alignment**: Stretch added. No difference.
- **Global scroll styles**: All three disabled simultaneously. No difference.
- **Stale build artifacts**: Clean bin/obj wipe + full rebuild done. No difference.

## What the working views have that the broken ones might not

The three working views with left rails (DocumentationView, BugTrackerView, SkillManagerView) all use `DockPanel.Dock="Left"` for the rail. But when I tried that in Fix 3, it didn't help. They also all nest the ScrollViewer differently (some inside Grid RowDefinitions="Auto,*", some directly as DockPanel fill child). BugTrackerView uses a ScrollViewer as a direct DockPanel fill child with no intermediate Grid — the simplest case — and it works.

HomeView has no left rail at all, just a header and a ScrollViewer with cards. Its pattern (`DockPanel > header Top > ScrollViewer fill` or `Grid Auto/* > header > ScrollViewer`) is the simplest possible scrollable layout, yet it doesn't scroll.

## What I need help with

1. **What could cause a ScrollViewer to not scroll even when placed in a bounded * row of a Grid that itself has bounded height from the Window?** All 8 attempts focused on giving the ScrollViewer a bounded parent, but none worked.

2. **Why do BugTrackerView and DocumentationView scroll correctly with essentially the same pattern that fails in ProjectsView and HomeView?** I cannot identify a structural difference that explains the divergence.

3. **Could the issue be in how ContentControl/ViewLocator/ContentPresenter propagates size constraints to the loaded UserControl?** If the ContentPresenter inside the Fluent theme's ContentControl template doesn't pass a bounded height to the child UserControl, nothing inside the UserControl would matter.

4. **Should I instrument with Avalonia DevTools (F12) to inspect Bounds.Height, Viewport, and Extent at runtime?** If so, what specific properties should I look at and on which elements?

## Files to include if attaching

If you want me to attach full files instead of the snippets above:
- `src/VybeDesk.App/Views/MainWindow.axaml` (66 lines)
- `src/VybeDesk.App/Views/ProjectsView.axaml` (150 lines — current broken state)
- `src/VybeDesk.App/Views/HomeView.axaml` (162 lines — current broken state)
- `src/VybeDesk.App/Views/DocumentationView.axaml` (working reference)
- `src/VybeDesk.App/Views/BugTrackerView.axaml` (working reference)
- `src/VybeDesk.App/App.axaml` (87 lines — global styles)
- `src/VybeDesk.App/Controls/ModuleHeader.axaml` (270 lines)
- `src/VybeDesk.App/ViewLocator.cs` (23 lines)

## Environment

- Avalonia 11.3.0, .NET 9, Windows 11
- FluentTheme (dark)
- Window opens maximized (WindowState="Maximized")
- CommunityToolkit.Mvvm 8.4 for ViewModels
