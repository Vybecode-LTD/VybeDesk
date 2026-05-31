# ADR-0007: Plugin architecture — collectible ALC, JSON manifest, no sandbox

**Status:** Accepted
**Date:** 2026-05-31

## Context

VybeDesk shipped v1.1.0 with 11 hard-wired sidebar modules. To open the app to
community contribution, we needed third parties to add their own modules
**without forking or recompiling the host**. Four host facts shaped the design:

- The sidebar was a **hard-coded constructor list** in `MainWindowViewModel`
  (11 VM parameters), and the DI container was built once in `Program.cs`.
- The module base type `PageViewModel` lived **inside the App `.exe`**, so
  nothing external could subclass it.
- The `ViewLocator` resolved views with `Type.GetType(name)`, which only sees
  the host assembly — it would never find a view shipped in a plugin DLL.
- .NET offers **no real in-process sandbox**. A loaded assembly runs with full
  host privileges, full stop.

We considered: (a) a simple `Assembly.LoadFrom` into the default context;
(b) a collectible `AssemblyLoadContext` per plugin; (c) out-of-process plugins.
(c) is the only true security boundary but can't host a live Avalonia control in
the host visual tree, so it was out for UI plugins. (a) is the least work but
makes per-plugin dependency versions and any future unload impossible.

## Decision

A four-part design, dogfooded by routing the built-ins through the same path:

1. **Extract an SDK assembly** — `VybeDesk.Plugin.Abstractions` (namespace
   `VybeDesk.Plugin`) holding `IVybeModule`, `ModuleManifest`,
   `PluginCapabilities`, `IModuleHost`, and the base VMs moved out of App.
   Plugins reference this + `VybeDesk.Core` (host service interfaces) only.
2. **Catalog-driven sidebar** — `IModuleCatalog` yields the ordered pages
   (built-ins → plugin pages → Settings). `MainWindowViewModel` consumes the
   catalog; built-ins and plugins reach the sidebar by the same mechanism.
3. **Collectible `AssemblyLoadContext` per plugin**, with the critical rule that
   **shared contract assemblies defer to the host**: `PluginLoadContext.Load`
   returns `null` for `VybeDesk.Plugin.Abstractions`, `VybeDesk.Core`,
   `Avalonia*`, `CommunityToolkit*`, and `Microsoft.Extensions.DependencyInjection*`,
   routing them to the default context. This guarantees a plugin's `IVybeModule`
   and its Avalonia controls share **one** type identity with the host — without
   it, `is IVybeModule` fails and controls can't enter the visual tree.
4. **JSON manifest (`plugin.json`)** carrying full pre-load metadata (id,
   version, host-compat range, declared capabilities, entry point) so the
   manager can list, validate, and host-version-gate a plugin **before** running
   any of its code.

The `ViewLocator` was changed to resolve `FooView` from
`vmType.Assembly.GetType(name)` first (the plugin's own assembly), falling back
to `Type.GetType`.

**No sandbox.** Plugins are explicitly full-trust. Mitigations are disclosure +
consent, not enforcement: never auto-loaded, capabilities surfaced at enable
time, one-click disable/remove, and prominent "trusted code" copy in the UI and
[docs/PLUGINS.md](../PLUGINS.md).

## Consequences

- **Layering.** New `VybeDesk.Plugin.Abstractions` sits between Core and App;
  Services references it (the loader lives there). No cycle (`Abstractions → Core`).
- **Discovery runs at composition time.** `PluginLoader.LoadInto(IServiceCollection)`
  is called from `Program.ConfigureServices` (before the provider is built) so
  plugins can register services. Page VMs construct later (post-Avalonia-init),
  when the catalog resolves them.
- **Unload is restart-based for now.** The context is collectible, but live
  unload only completes once every reference (visual tree, DI singletons) drops,
  so enable/disable/install take effect on next launch. A handle to each ALC is
  kept for a future live-unload path.
- **API stability matters now.** The SDK is a public contract community plugins
  compile against. A CI job (`plugin-sdk.yml`) builds `samples/HelloWorldPlugin`
  against the SDK so a breaking change to `IVybeModule`/`PageViewModel`/
  `ModuleManifest` fails the build instead of silently breaking every plugin.
- **Known gaps.** The unified `ModuleHeader` is host-internal (plugin pages
  render their own content); the SDK ships no XML-doc/NuGet package yet
  (planned). Both are tracked, neither blocks authoring.
