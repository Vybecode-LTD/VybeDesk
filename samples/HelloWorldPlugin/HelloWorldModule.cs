using Microsoft.Extensions.DependencyInjection;
using VybeDesk.Plugin;

namespace HelloWorldPlugin;

/// <summary>
/// The plugin entry point. The host discovers this type, calls
/// <see cref="ConfigureServices"/> to register the page view model, then
/// <see cref="GetPages"/> to add the page to the sidebar.
/// </summary>
public sealed class HelloWorldModule : IVybeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "com.vybedesk.helloworld",
        Name = "Hello World",
        Version = "1.0.0",
        Author = "VybeDesk",
        Description = "A minimal sample plugin that adds a Hello page to the sidebar.",
    };

    public void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<HelloWorldViewModel>();

    public IEnumerable<PageViewModel> GetPages(IServiceProvider services)
        => new PageViewModel[] { services.GetRequiredService<HelloWorldViewModel>() };
}
