# ADR-0003: DPAPI for API key storage (Windows-first)

**Status:** Accepted
**Date:** 2026-05-24

## Context

The Anthropic API key is the single most sensitive piece of state VybeDesk
holds. Anyone who reads it can run up the user's wallet under their account.
We need to persist it across runs without writing it to disk in plaintext or
into any file that might be checked into git.

Cross-platform secure stores exist (libsecret on Linux, Keychain on macOS,
DPAPI on Windows) but they're all OS-specific. There's no good cross-platform
abstraction in .NET that wraps all three uniformly.

VybeDesk v1 targets Windows only (the desktop is Windows-first; the App
project is marked `[SupportedOSPlatform("windows")]`). Optimizing for
cross-platform key storage now would be premature.

## Decision

`DpapiKeyStore` (in `VybeDesk.Services.Storage`) writes a DPAPI-encrypted
blob to `%LOCALAPPDATA%\VybeDesk\apikey.bin` via
`System.Security.Cryptography.ProtectedData.Protect` with the current-user
scope. The class is marked `[SupportedOSPlatform("windows")]` — calling it
on another OS is a compile-time error in a Windows-targeting build.

The `ISecureKeyStore` interface in `Core` is platform-neutral; the concrete
`DpapiKeyStore` is the only implementation today. Adding `KeychainKeyStore`
(macOS) or `LibsecretKeyStore` (Linux) is a v1.1+ task that doesn't require
changing any consumer of `ISecureKeyStore`.

## Consequences

- The API key is encrypted at rest, decryptable only by the same Windows
  user account that wrote it.
- The key is read on every API call (cheap — DPAPI is fast), so changing
  it in Settings takes effect without an app restart.
- ASCII validation runs on both save and load
  (`AnthropicChatService.BuildRequest`) — Anthropic rejects non-ASCII
  header characters, and rich-text copy-paste often slips smart quotes
  or em-dashes into the key. The validation catches this with a clear
  error message rather than a generic HTTP 4xx.
- macOS and Linux ports require new `ISecureKeyStore` implementations.
  This is intentional — the v1 scope is Windows.
- **Do not write the key to `CLAUDE.md`, `appsettings.json`, or any
  file that lives next to the binary.** If you need to inspect a key
  for debugging, read it via `DpapiKeyStore.LoadKey()` and log only
  the first/last 4 chars.
