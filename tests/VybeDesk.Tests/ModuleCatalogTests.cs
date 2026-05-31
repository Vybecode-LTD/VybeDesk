using VybeDesk.App.Modules;
using VybeDesk.Plugin;
using Xunit;

namespace VybeDesk.Tests;

public class ModuleCatalogTests
{
    private sealed class FakePage : PageViewModel
    {
        private readonly string _title;
        public FakePage(string title) => _title = title;
        public override string Title => _title;
        public override string Glyph => "x";
        public override string Description => "";
    }

    [Fact]
    public void Pages_PreservesGivenOrder()
    {
        var a = new FakePage("A");
        var b = new FakePage("B");
        var c = new FakePage("C");

        var catalog = new ModuleCatalog(new PageViewModel[] { a, b, c });

        Assert.Equal(3, catalog.Pages.Count);
        Assert.Same(a, catalog.Pages[0]);
        Assert.Same(b, catalog.Pages[1]);
        Assert.Same(c, catalog.Pages[2]);
    }

    [Fact]
    public void Pages_IsASnapshot_NotALiveView()
    {
        var source = new List<PageViewModel> { new FakePage("A") };
        var catalog = new ModuleCatalog(source);

        source.Add(new FakePage("B")); // mutating the source after construction

        Assert.Single(catalog.Pages); // catalog took a copy
    }
}
