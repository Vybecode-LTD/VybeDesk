# Building VybeDesk plugins

VybeDesk can be extended with **plugins** — self-contained assemblies that add
their own sidebar pages, view models, and services to the app without modifying
(or recompiling) the host. This guide is everything you need to build one.

> **New here?** The fastest way to learn is to read and copy
> [`samples/HelloWorldPlugin`](../samples/HelloWorldPlugin) — a complete,
> working plugin that adds a page to the sidebar.

---

## ⚠ Trust model — read this first

A VybeDesk plugin is **trusted, in-process .NET code**. When loaded it runs with
**the same privileges you have**: it can read and write your files, read your
saved Anthropic API key, and make network calls. **VybeDesk does not — and
technically cannot — sandbox a plugin.** This is the same model VS Code and
Obsidian use.

What the host *does* do:

- Plugins are **never auto-loaded** silently — discovery lists them, but a
  plugin only loads when present and enabled.
- A plugin **declares its capabilities** in its manifest, and the host shows
  them to you in **Settings → Plugins** before you rely on it.
- You can **disable or remove** any plugin from that screen.

Install plugins only from sources you trust. If you publish a plugin, declare
your capabilities honestly.

---

## How it fits together

A plugin plugs into five host seams:

| Seam | What the plugin provides |
|---|---|
| **Module contract** | a class implementing `IVybeModule` |
| **Navigation** | one or more `PageViewModel`s returned from `GetPages` |
| **Views** | a `FooView` for each `FooViewModel` (host naming convention) |
| **DI** | service/VM registrations via `ConfigureServices` |
| **Resources** | the host's design tokens + styles, available to your views |

At startup the host scans `%LOCALAPPDATA%\VybeDesk\plugins\*`, reads each
`plugin.json`, loads every enabled + compatible plugin into its **own
collectible `AssemblyLoadContext`**, calls `ConfigureServices`, and inserts the
pages from `GetPages` into the sidebar (after the built-ins, before Settings).

---

## The SDK

Plugins compile against two host assemblies:

- **`VybeDesk.Plugin.Abstractions`** (namespace `VybeDesk.Plugin`) — the SDK:
  `IVybeModule`, `ModuleManifest`, `PluginCapabilities`, `IModuleHost`, and the
  base view models `PageViewModel` / `ProjectScopedViewModel` / `ViewModelBase`.
- **`VybeDesk.Core`** — the host **service interfaces** you may inject
  (`IAiService`, `IProjectStore`, `IActiveProjectContext`, `IAgentActionService`,
  `IClipboardService`, …) plus the shared models.

You **reference** these but you **do not ship** them — the host provides them at
runtime (see [Packaging](#packaging--installing)).

---

## 1. The module entry point — `IVybeModule`

```csharp
using Microsoft.Extensions.DependencyInjection;
using VybeDesk.Plugin;

public sealed class HelloWorldModule : IVybeModule
{
    public ModuleManifest Manifest { get; } = new()
    {
        Id = "com.acme.hello",        // stable, reverse-DNS, == plugin.json "id"
        Name = "Hello World",
        Version = "1.0.0",
        Author = "Acme",
        Description = "Adds a Hello page.",
        Capabilities = new[] { PluginCapabilities.Network },  // declare honestly
    };

    // Register your services + view models. Host services are already registered
    // and can be constructor-injected into your VMs.
    public void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<HelloViewModel>();

    // Hand back the page(s) for the sidebar, resolved from the built container.
    public IEnumerable<PageViewModel> GetPages(IServiceProvider services)
        => new PageViewModel[] { services.GetRequiredService<HelloViewModel>() };
}
```

**Rules**

- One `IVybeModule` per plugin assembly, with a **public parameterless
  constructor**. Do real work in `ConfigureServices`/`GetPages`, not the ctor.
- `Manifest.Id` **must** equal the `id` in your `plugin.json` (the host warns on
  a mismatch).

---

## 2. The page — `PageViewModel`

A sidebar page is a `PageViewModel`. The three abstract members drive the
sidebar entry and the unified header:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VybeDesk.Plugin;

public sealed partial class HelloViewModel : PageViewModel
{
    public override string Title       => "Hello";
    public override string Glyph       => "\U0001F44B"; // 👋 sidebar icon
    public override string Description  => "A sample plugin page.";

    [ObservableProperty] private string _greeting = "Hello!";

    [RelayCommand]
    private void Wave() => Greeting = "👋";
}
```

Useful inherited members you can override:

- `Children` — return sub-pages to make this a sidebar **group** (like the
  built-in Skills → Manager/Builder).
- `OnActivated()` — called every time the page becomes active; re-sync state here.
- `Breadcrumbs`, `GoModuleHomeCommand`, `ResetCommand`, `RestartCommand` — wire
  up the header chrome.
- For a project-scoped page, derive from **`ProjectScopedViewModel`** instead and
  you inherit the whole project-picker lifecycle.

> Every VM **must be `partial`** for the CommunityToolkit `[ObservableProperty]` /
> `[RelayCommand]` source generators to work.

---

## 3. The view — naming convention

The host's `ViewLocator` maps `FooViewModel` → `FooView` **in the same
assembly**. Name your view to match:

```xml
<!-- HelloView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Acme.Hello"
             x:Class="Acme.Hello.HelloView"
             x:DataType="vm:HelloViewModel">
  <StackPanel Margin="32" Spacing="12">
    <TextBlock Text="{Binding Greeting}" Foreground="{DynamicResource VdTextPrimary}"/>
    <Button Classes="accent" Content="Wave 👋" Command="{Binding WaveCommand}"/>
  </StackPanel>
</UserControl>
```

```csharp
// HelloView.axaml.cs
using Avalonia.Controls;
namespace Acme.Hello;
public partial class HelloView : UserControl { public HelloView() => InitializeComponent(); }
```

Your view lives in the host visual tree, so it can use the host's **design
tokens** (`{DynamicResource VdAccent}`, `VdTextPrimary`, `VdSurface1`, …) and
style classes (`Classes="accent"`) for a native look.

---

## 4. `plugin.json`

Sits at the root of your plugin folder. The host reads it **before loading any
code** — that's how the manager lists and validates a plugin without running it.

```json
{
  "schemaVersion": 1,
  "id": "com.acme.hello",
  "name": "Hello World",
  "version": "1.0.0",
  "author": "Acme",
  "description": "Adds a Hello page to the sidebar.",
  "entryAssembly": "Acme.Hello.dll",
  "entryType": "Acme.Hello.HelloWorldModule",
  "minHostVersion": "1.0.0",
  "maxHostVersion": null,
  "capabilities": ["network"]
}
```

| Field | Required | Notes |
|---|---|---|
| `id` | ✅ | Stable, unique, reverse-DNS. Identity for enable state + data dir. |
| `name`, `version`, `author`, `description` | – | Shown in the manager. |
| `entryAssembly` | ✅ | Your DLL's file name, relative to the folder. |
| `entryType` | – | FQN of your `IVybeModule`. Omit to let the host scan for it. |
| `minHostVersion` / `maxHostVersion` | – | Inclusive compat range; incompatible plugins are listed but not loaded. |
| `capabilities` | – | Disclosure labels (see `PluginCapabilities`). |

---

## Host services you can inject

Constructor-inject any of these into your view models or services:

| Interface | Use |
|---|---|
| `IAiService` | Call Claude through the host (key + retries + caching handled). |
| `IProjectStore` | Read/observe the user's projects. |
| `IActiveProjectContext` | The currently-focused project, shared across modules. |
| `IAgentActionService` | The preview/execute/undo gate for filesystem writes. |
| `IClipboardService` | Copy text (set `Clipboard` to enable the base `CopyCommand`). |
| `IModuleHost` | `HostVersion` + `GetPluginDataDirectory(id)` for per-plugin state. |

---

## Packaging & installing

A plugin folder ships **only its own assembly** + `plugin.json`. The host
supplies Avalonia, the SDK, Core, CommunityToolkit, and MS.DI at runtime — the
load context resolves those shared contracts to the host's copies, so **do not
bundle them** (use `Private="false"` on the SDK/Core project references, as the
sample does).

```
com.acme.hello/
├── plugin.json
└── Acme.Hello.dll        ← + any PRIVATE dependencies of your own
```

**Install**

- **Manually:** drop the folder into `%LOCALAPPDATA%\VybeDesk\plugins\`, restart.
- **From the app:** Settings → Plugins → **Install from file…** and pick a
  `.vybeplugin` (a normal `.zip` of the folder, renamed). Restart to load.

Enable/disable and uninstall are in **Settings → Plugins**. Because the host
loads plugins at startup, enable/disable/install changes take effect on the next
launch.

---

## Build the sample

```bash
dotnet build samples/HelloWorldPlugin/HelloWorldPlugin.csproj
# then copy HelloWorldPlugin.dll + plugin.json into
#   %LOCALAPPDATA%\VybeDesk\plugins\com.vybedesk.helloworld\
# and restart VybeDesk.
```

---

## Current limitations (v1)

- **Restart to apply.** Enable/disable/install/uninstall take effect on next
  launch — live unload of a collectible context only completes once every
  reference is released.
- **No shared header control yet.** The built-in `ModuleHeader` is host-internal;
  plugin pages render their own content area. (Planned for the SDK.)
- **No sandbox.** Covered above — plugins are full-trust.
- **Single API version.** The SDK is pre-1.0; pin `minHostVersion` and expect the
  contract to firm up.

See [ADR-0007](adr/0007-plugin-architecture-collectible-alc.md) for the design
rationale behind the load model, the manifest, and the trust boundary.
