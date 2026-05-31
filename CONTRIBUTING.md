# Contributing to VybeDesk

Thanks for your interest! VybeDesk is an Avalonia / .NET 9 Windows desktop app for
managing Claude-Code-driven work. There are two ways to contribute:

1. **Write a plugin** — the easiest path. Add a whole sidebar module without
   touching the core or recompiling the host. Start at
   [docs/PLUGINS.md](docs/PLUGINS.md); you can stop reading here.
2. **Contribute to the core** — features, fixes, new built-in modules. Read on.

## Dev setup

Requires the **.NET 9 SDK** (10 also works; the app targets `net9.0`). **Windows
only** for now — the secure key store uses DPAPI.

```bash
git clone https://github.com/Vybecode-LTD/VybeDesk.git
cd VybeDesk
dotnet restore
dotnet build
dotnet test            # 323 tests should pass
dotnet run --project src/VybeDesk.App
```

## Project layout

```
src/VybeDesk.Core/                 models + service interfaces (no framework deps)
src/VybeDesk.Services/             SQLite, AI client, DPAPI, plugin loader
src/VybeDesk.Plugin.Abstractions/  the public plugin SDK
src/VybeDesk.App/                  Avalonia UI — Views, ViewModels, DI root
tests/VybeDesk.Tests/              xUnit + NSubstitute + Avalonia.Headless
```

Architecture deep-dive: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and the
[ADRs](docs/adr/README.md).

## Conventions (non-negotiable)

- **Layering** — strict one-direction `Core ← Services ← App`. Core has no
  framework dependencies.
- **MVVM** — every ViewModel is a `partial` class using CommunityToolkit.Mvvm
  source generators; **compiled bindings everywhere** (`x:DataType` on every
  `DataTemplate`).
- **AI calls go through `IAiService`** — never instantiate `HttpClient` against
  the Anthropic endpoint from a ViewModel.
- **AI-initiated filesystem actions go through `AgentActionService`** — the
  preview / execute / undo gate, scoped to registered project roots.
- **The API key** is stored via DPAPI; never written in plaintext, never to git.

## Testing

Three layers, all required before a change is "done":

1. `dotnet build` — green.
2. `dotnet test` — all pass. **A bug fix gets a regression test that fails
   before the fix and passes after.** New source file → new test file.
3. **Smoke test** — for any user-visible change, run the app and verify the
   behavior. Layers 1–2 don't catch layout / binding bugs.

Full contract: [docs/TESTING.md](docs/TESTING.md).

## Commits & pull requests

- **Conventional commits** (`feat:`, `fix:`, `docs:`, `ci:`, `build:`, …).
- **Every commit builds and tests pass** — no "WIP" or "fix previous commit"
  commits; history should be bisectable.
- Keep the change **scoped**; if the blast radius grows beyond the issue, say so
  in the PR rather than expanding silently.
- CI (`plugin-sdk.yml`) must be green. It builds, runs the full test suite, and
  **builds `samples/HelloWorldPlugin` against the SDK** — so a breaking change to
  the public plugin API fails the build by design.
- Never commit a secret. Check the staged diff before every commit.

## Reporting issues

Open a GitHub issue with reproduction steps (what you did / what you expected /
what happened). For a **security-sensitive** issue, please contact the
maintainers directly instead of filing a public issue.

## License

By contributing, you agree your contributions are licensed under the same terms
as the project (see [LICENSE.txt](LICENSE.txt)).
