# VybeDesk Plugin SDK

`VybeDesk.Plugin.Abstractions` is the SDK for building [VybeDesk](https://github.com/Vybecode-LTD/VybeDesk)
plugins — assemblies that add their own pages and services to the app without
recompiling the host.

```csharp
public sealed class HelloModule : IVybeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "com.acme.hello", Name = "Hello", Version = "1.0.0",
    };

    public void ConfigureServices(IServiceCollection s) => s.AddSingleton<HelloViewModel>();

    public IEnumerable<PageViewModel> GetPages(IServiceProvider s)
        => new PageViewModel[] { s.GetRequiredService<HelloViewModel>() };
}
```

- **Scaffold a plugin:** `dotnet new install VybeDesk.Templates` then
  `dotnet new vybeplugin -n MyPlugin`.
- **Full guide:** [docs/PLUGINS.md](https://github.com/Vybecode-LTD/VybeDesk/blob/main/docs/PLUGINS.md)
- **Trust model:** plugins are in-process, full-trust code. VybeDesk does not
  sandbox them.

> Pre-1.0 — the contract may still change between minor versions.
