---
document: BUGS
version: 0.3.0
last-updated: 2026-05-31
last-audit: 2026-05-31
managed-by: session-orchestrator/bug-fix-tracker
---

# BUGS — VybeDesk

> Defect log. Shares one version with the other managed docs (see
> `@DOCUMENTATION_MANAGER.md`). Lives at the repo root (not `docs/`) per this
> project's doc-layout convention.

## Open

**None.** As of 2026-05-31 there are **no open bugs** (323/323 tests pass).

## Resolved (recent)

Full hypothesis history lives in the stacked STOP sections of `HANDOFF.md` and
in the dedicated postmortems under `docs/`:

- **Cross-module project persistence** — RESOLVED 2026-05-28. Passive null writes
  through TwoWay ComboBox bindings cleared `ActiveProjectContext`. Fix: idempotent
  null-safe `SetCurrent` + explicit `ClearCurrent()` + per-module isolation.
  → [docs/PROJECT_PERSISTENCE_BUG.md](docs/PROJECT_PERSISTENCE_BUG.md)
- **HomeView / ProjectsView layout overflow** — RESOLVED 2026-05-28. Fluent
  ContentControl defaulted `VerticalContentAlignment` to `Top` (infinite-height
  measure). Fix at the MainWindow level (`VerticalContentAlignment="Stretch"`).
  → [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md)
- **Prompt editor not showing + all-black Stratum theme** — RESOLVED 2026-05-30.
  The recurring "scroll bug" was ultimately ENVIRONMENTAL (a stale/dead
  `VybeDesk.App` process), not a layout-shape defect — see LAYOUT_REGRESSION §10.

## Conventions

- A bug fix gets a regression test that **fails before, passes after**
  (`@DEBUG_PROTOCOL.md` / `@TESTING_PROCEDURES.md`).
- Severity: CRITICAL / HIGH / MEDIUM / LOW. No deploy with an open CRITICAL/HIGH bug.
