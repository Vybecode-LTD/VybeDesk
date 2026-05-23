# ClaudePM — Claude Project Manager

An AI-driven desktop project manager for Claude-based work: keeps project
documentation reconciled, manages a reusable prompt library, builds Claude Code
handoff packages from claude.ai web sessions, provides an AI notebook that can
take scoped filesystem actions, and manages a `.skill` file library.

## Stack

Avalonia 11 · .NET 9 · CommunityToolkit.Mvvm · Microsoft.Extensions.DependencyInjection

## Solution layout

```
ClaudePM.sln
src/
  ClaudePM.Core/      Domain models + service interfaces. No framework deps.
  ClaudePM.Services/  Implementations: storage, secure key store, AI service.
  ClaudePM.App/       Avalonia UI — Views, ViewModels, DI composition root.
tests/
  ClaudePM.Tests/     xUnit + NSubstitute.
```

## Build & run

```
dotnet restore
dotnet build
dotnet run --project src/ClaudePM.App
dotnet test
```

Requires the .NET 9 SDK. Windows-only for v1 (the API key store uses DPAPI).

## Status

Feature-complete v1. The shell, navigation, Home, Settings, and all five
modules (Documentation, Prompts, Session Builder, Notebook, Skill Library)
are implemented end to end. See `SPEC.md` for the full plan and `CLAUDE.md`
for the current build state.
