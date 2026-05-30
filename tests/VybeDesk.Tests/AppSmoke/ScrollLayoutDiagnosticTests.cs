using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Xunit.Abstractions;

namespace VybeDesk.Tests.AppSmoke;

/// <summary>
/// Diagnostic + regression tests for the recurring "content overflows but the
/// ScrollViewer doesn't scroll" bug (docs/LAYOUT_REGRESSION.md).
///
/// Each test reconstructs the app's real host chain —
///   Window -> ContentControl(VerticalContentAlignment=Stretch) -> [view root]
/// — at a deliberately SMALL fixed size so the content (1000px tall) MUST
/// overflow, then reads the ScrollViewer's Viewport vs Extent after layout.
/// A scrollable ScrollViewer reports Extent.Height > Viewport.Height; a broken
/// (infinite-measure) one reports Extent.Height ~= Viewport.Height.
///
/// This is the deterministic instrument the LAYOUT_REGRESSION saga lacked:
/// it tells us which inner layout SHAPES actually bound a ScrollViewer in
/// Avalonia 11.3, instead of relying on visual smoke tests against a possibly
/// stale running process.
/// </summary>
public sealed class ScrollLayoutDiagnosticTests
{
    private readonly ITestOutputHelper _out;
    public ScrollLayoutDiagnosticTests(ITestOutputHelper output) => _out = output;

    private const double WindowW = 500;
    private const double WindowH = 300;   // < the 1000px content, so it must scroll
    private const double ContentH = 1000;
    private const double HeaderH = 105;   // mirrors the ModuleHeader fixed height

    /// <summary>Tall content that cannot fit a 300px window.</summary>
    private static Control TallContent() =>
        new StackPanel { Children = { new Border { Height = ContentH, Background = Brushes.Gray } } };

    private static Border FakeHeader() => new Border { Height = HeaderH, Background = Brushes.DimGray };

    /// <summary>
    /// Hosts <paramref name="viewRoot"/> exactly like MainWindow does
    /// (ContentControl with Stretch alignment), lays out at a fixed small size,
    /// and returns the first descendant ScrollViewer's geometry.
    /// </summary>
    private (Size viewport, Size extent) Measure(Control viewRoot, bool stretch = true)
    {
        var host = new ContentControl
        {
            Content = viewRoot,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Top
        };
        var window = new Window { Width = WindowW, Height = WindowH, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);

        var sv = viewRoot.GetVisualDescendants().OfType<ScrollViewer>().First();
        _out.WriteLine($"viewport={sv.Viewport}, extent={sv.Extent}, scrollableY={sv.Extent.Height - sv.Viewport.Height:F1}");
        return (sv.Viewport, sv.Extent);
    }

    // ---- Finding: the layout shape is NOT the cause ------------------------

    /// <summary>
    /// FINDING (2026-05-30, via this headless rig): in Avalonia 11.3 layout the
    /// bare DockPanel &gt; ScrollViewer chain establishes a finite viewport and
    /// scrolls EVEN WITHOUT VerticalContentAlignment=Stretch on the host
    /// ContentControl. The layout SHAPE / alignment is therefore not what broke
    /// Settings — every candidate shape (A/B/C below) scrolls. This reframes
    /// docs/LAYOUT_REGRESSION.md: the live "content runs off, no scrollbar"
    /// symptom during the fix attempts was environmental (a stale/dead
    /// VybeDesk.App process — confirmed no process was running — plus a
    /// maximized window where the content simply fit), not a measure-chain
    /// defect. These tests are the permanent guard that the shapes are sound.
    /// </summary>
    [AvaloniaFact]
    public void ContentControl_EitherAlignment_ProvidesScrollRange()
    {
        Control Build()
        {
            var dp = new DockPanel();
            var header = FakeHeader();
            DockPanel.SetDock(header, Dock.Top);
            dp.Children.Add(header);
            dp.Children.Add(new ScrollViewer { Content = TallContent() });
            return dp;
        }

        var stretched = Measure(Build(), stretch: true);
        var topAligned = Measure(Build(), stretch: false);

        Assert.True(stretched.extent.Height > stretched.viewport.Height + 1.0,
            $"Stretch host should scroll: extent={stretched.extent.Height}, viewport={stretched.viewport.Height}");
        Assert.True(topAligned.extent.Height > topAligned.viewport.Height + 1.0,
            $"Top-aligned host also scrolls in 11.3: extent={topAligned.extent.Height}, viewport={topAligned.viewport.Height}");
    }

    // ---- The three shapes from the LAYOUT_REGRESSION saga ------------------

    /// <summary>Shape A — bare ScrollViewer as the DockPanel LastChildFill
    /// (SettingsView v1.0 / the shape that overflowed).</summary>
    [AvaloniaFact]
    public void ShapeA_BareScrollViewer_AsDockPanelFill()
    {
        var dp = new DockPanel();
        var header = FakeHeader();
        DockPanel.SetDock(header, Dock.Top);
        dp.Children.Add(header);
        dp.Children.Add(new ScrollViewer { Content = TallContent() });

        var (vp, ext) = Measure(dp);
        Assert.True(ext.Height > vp.Height + 1.0,
            $"Shape A did NOT scroll: extent={ext.Height}, viewport={vp.Height}");
    }

    /// <summary>Shape B — ScrollViewer in the '*' row of a Grid (Auto,*) that is
    /// the UserControl root (the wizard / first-attempt shape).</summary>
    [AvaloniaFact]
    public void ShapeB_ScrollViewer_InGridStarRow()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var header = FakeHeader();
        Grid.SetRow(header, 0);
        grid.Children.Add(header);
        var sv = new ScrollViewer { Content = TallContent() };
        Grid.SetRow(sv, 1);
        grid.Children.Add(sv);

        var (vp, ext) = Measure(grid);
        Assert.True(ext.Height > vp.Height + 1.0,
            $"Shape B did NOT scroll: extent={ext.Height}, viewport={vp.Height}");
    }

    /// <summary>Shape C — ScrollViewer inside a Border inside a Grid
    /// (the PromptManagerView shape we replicated for Settings).</summary>
    [AvaloniaFact]
    public void ShapeC_ScrollViewer_InBorderInGrid()
    {
        var dp = new DockPanel();
        var header = FakeHeader();
        DockPanel.SetDock(header, Dock.Top);
        dp.Children.Add(header);

        var outerGrid = new Grid();
        var border = new Border();
        var innerGrid = new Grid();
        var sv = new ScrollViewer { Content = TallContent() };
        innerGrid.Children.Add(sv);
        border.Child = innerGrid;
        outerGrid.Children.Add(border);
        dp.Children.Add(outerGrid);

        var (vp, ext) = Measure(dp);
        Assert.True(ext.Height > vp.Height + 1.0,
            $"Shape C did NOT scroll: extent={ext.Height}, viewport={vp.Height}");
    }
}
