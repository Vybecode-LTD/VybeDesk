# TESTING PROCEDURES

@DEBUG_PROTOCOL.md

> Reusable testing directive. Include this file from each project's CLAUDE.md
> with an appropriate relative path, for example `@../TESTING_PROCEDURES.md`
> or `@../../TESTING_PROCEDURES.md`.

> **Related directive — `ONLY_IF_DESKTOP_DOWNLOAD_APP → follow release directive`:** @SOFTWARE_RELEASE.md
> A desktop app shipped as a downloadable installer/binary releases via that pipeline (and its pre-release gate runs these testing checks first). Web apps, services, and libraries skip it.

This document is binding when included. Its purpose is to force thorough
initialization testing, lint/static-analysis testing, and regression testing for
these primary stacks:

1. C# / Avalonia desktop applications, primarily targeting Windows.
2. Python / PostgreSQL / FastAPI backends with React / Node.js / Vite frontends.

The agent must prefer evidence over confidence. Do not declare work complete
until the affected project can be initialized, analyzed, tested, and run through
the relevant regression checks, or until every blocker is reported with exact
commands, errors, and remaining risk.

## Core Rules

- Treat tests as part of the implementation, not as optional follow-up work.
- If a project lacks adequate test infrastructure, install and configure it.
- Use the project's existing package manager and conventions before adding new
  tools.
- Do not mix package managers unless the repository already does so.
- Do not install tools globally when a project-local dependency, dev dependency,
  or local .NET tool manifest can be used.
- Never run tests against production databases, production services, or live user
  data.
- Never use destructive cleanup commands in the user's active working tree. If a
  clean-environment test is needed, use a temporary clone, temporary worktree, or
  disposable CI-like directory.
- Every bug fix must have a regression test that fails before the fix and passes
  after the fix. If this is impossible, create an executable reproduction script
  or document the exact reason a test cannot be added.
- If the same issue survives two fix attempts, immediately follow
  `@DEBUG_PROTOCOL.md`. Freeze production-code edits until diagnostic evidence
  identifies a root cause.
- Skipped tests require a reason, an owner, and a condition for removal. Do not
  silently skip failing coverage.
- Final responses must list the exact commands run, their results, and any tests
  or checks that could not be run.

## Required First Pass

Before editing code, the agent must map the project:

1. Identify the stack from files such as `.sln`, `.csproj`, `.axaml`,
   `pyproject.toml`, `requirements.txt`, `alembic.ini`, `package.json`,
   `vite.config.*`, `tsconfig.json`, `docker-compose*.yml`, and CI workflows.
2. Read existing project instructions, including CLAUDE.md, README files, CI
   config, scripts, Makefiles, task runners, and test documentation.
3. Inspect the current git state and preserve user changes.
4. Locate existing tests, fixtures, mocks, factories, database test helpers,
   frontend test setup, coverage config, and CI gates.
5. Determine the package manager:
   - .NET: solution file, project files, `global.json`, `Directory.Build.*`,
     local tool manifest.
   - Python: `uv.lock`, `poetry.lock`, `pdm.lock`, `requirements*.txt`,
     `pyproject.toml`.
   - Node: `pnpm-lock.yaml`, `package-lock.json`, `yarn.lock`, `bun.lockb`.
6. Determine how the app starts in development and production.
7. Determine whether Docker or another container runtime is available for
   PostgreSQL and service integration tests.

Only after this map exists should the agent install missing test tools or change
test configuration.

## Tool Installation Policy

Install the minimum responsible set of test and analysis tools needed for the
project. Prefer adding them as dev/test dependencies and committing the lockfile
changes produced by the project's package manager.

Do not add a new framework if the repository already has an equivalent,
well-configured tool. Improve the existing tool instead.

When adding tools, also add or update the scripts needed to run them. A test
dependency without a documented command is incomplete.

Suggested command names:

- `test`: normal unit/component tests.
- `test:watch`: watch mode when supported.
- `test:coverage`: coverage run.
- `test:integration`: database/service integration tests.
- `test:e2e`: browser or full-app tests.
- `lint`: lint/static analysis.
- `format:check`: formatting verification.
- `typecheck`: type checking.
- `verify`: full local gate combining install validation, lint, typecheck,
  tests, build, and high-value smoke tests.

## C# / Avalonia Minimum Test Stack

For C# / Avalonia projects, ensure the repository has an appropriate test
project. Use the existing test framework if present. If no framework exists,
prefer xUnit unless the repository clearly favors NUnit or MSTest.

Required or strongly preferred packages for test projects:

- `Microsoft.NET.Test.Sdk`
- `xunit` and `xunit.runner.visualstudio`, or the repository's chosen equivalent
- `FluentAssertions`
- `NSubstitute` or `Moq`
- `coverlet.collector`
- `Avalonia.Headless.XUnit` for Avalonia headless UI tests when using xUnit
- `FlaUI.UIA3` for Windows UI automation when headless tests cannot cover the
  workflow
- `Verify.Xunit` or another approval/snapshot tool for stable UI, serialization,
  and generated-output regression tests where useful

Recommended local .NET tools when appropriate:

- `dotnet-reportgenerator-globaltool` through a local tool manifest
- `dotnet-stryker` for mutation testing of high-risk logic

Required C# / Avalonia checks:

- `dotnet --info`
- `dotnet restore`
- `dotnet build` for the full solution
- `dotnet build -c Release` for the full solution
- `dotnet test --no-restore --collect:"XPlat Code Coverage"` or the repository's
  equivalent coverage command
- `dotnet format --verify-no-changes`
- `dotnet list package --vulnerable --include-transitive`
- `dotnet publish -c Release -r win-x64` for Windows desktop deliverables when
  publish artifacts are part of the project

If the project uses `global.json`, SDK roll-forward rules, analyzers, source
generators, trimming, single-file publish, MSIX, ClickOnce, or self-contained
publishing, include those paths in the verification plan.

## C# / Avalonia Initialization Testing

The agent must verify:

- The solution restores from a clean dependency cache or clean temporary clone
  when practical.
- Debug and Release builds succeed.
- Nullable reference type warnings, analyzer warnings, and XAML/Avalonia binding
  diagnostics are reviewed and either fixed or documented.
- The application can initialize without unhandled startup exceptions.
- The main window, app lifetime, dependency injection container, configuration
  loading, resource dictionaries, styles, fonts, icons, and themes initialize.
- View models construct with realistic dependencies and test doubles.
- Commands expose correct `CanExecute` state and raise state-change events.
- Bindings resolve for changed views and reusable controls.
- Navigation, dialogs, file pickers, background tasks, cancellation tokens, and
  error surfaces are covered where present.
- Windows-specific behavior is tested on Windows when relevant: file paths,
  registry access, DPI scaling, window sizing, tray behavior, startup tasks,
  permissions, installer/update logic, and packaged artifact launch.

Avalonia UI smoke tests should run headlessly when possible. At minimum, create
tests that instantiate the app builder, open key windows/views, bind representative
view models, and assert no startup or binding exceptions.

## C# / Avalonia Regression Coverage

For changed Avalonia code, test the affected pattern, not only the specific
instance. Search for sibling views, controls, converters, services, and commands
that share the same pattern and verify representative coverage.

Required regression targets:

- View model state transitions.
- Command enablement and command side effects.
- Input validation and error messaging.
- Async command success, failure, cancellation, and reentrancy behavior.
- Serialization/deserialization and settings migrations.
- File system paths, invalid paths, missing files, locked files, and permission
  failures.
- UI binding names, converter behavior, resource lookup, and theme variants.
- Startup and shutdown behavior.
- Installer, publish, or update behavior when the change touches packaging.
- Any previously fixed bug in the same area.

For high-risk pure logic, add property-based tests or mutation testing. For UI
rendering regressions, use stable snapshot/approval tests only when the snapshot
is deterministic and reviewed.

## Python / FastAPI / PostgreSQL Minimum Test Stack

Use the repository's existing Python tooling if present. If no test stack exists,
install and configure:

- `pytest`
- `pytest-cov`
- `pytest-xdist`
- `pytest-asyncio` or `anyio`, depending on the app's async style
- `httpx` for FastAPI API tests
- `hypothesis` for property-based tests of validators, parsers, transforms, and
  boundary-heavy logic
- `ruff` for linting and formatting
- `mypy` or `pyright` for type checking, based on repository convention
- `bandit` for Python security linting
- `pip-audit` for dependency vulnerability checks
- `testcontainers[postgresql]`, `pytest-postgresql`, or a Docker Compose based
  PostgreSQL test service

Use the existing dependency manager:

- `uv add --dev ...` when `uv.lock` or uv project metadata exists.
- `poetry add --group dev ...` when Poetry is used.
- `pdm add -d ...` when PDM is used.
- Add to development requirements files when the repository uses pip
  requirements files.

Required Python / FastAPI checks:

- Install dependencies from the lockfile or declared requirements.
- `python --version`
- `pytest`
- Coverage command with branch coverage when configured.
- `ruff check .`
- `ruff format --check .`
- `mypy .` or `pyright`
- `bandit -r .` for application code, with generated files excluded.
- `pip-audit` or the repository's dependency audit equivalent.

## PostgreSQL and Migration Testing

The agent must use an isolated test database. Acceptable options:

- Testcontainers PostgreSQL.
- Docker Compose PostgreSQL service dedicated to tests.
- A local PostgreSQL database with a clearly named disposable test database.
- SQLite only if the production code explicitly supports SQLite and the test is
  not pretending to validate PostgreSQL-specific behavior.

Required database checks:

- Database URL points to a test database, never production.
- Migrations apply from an empty database to head.
- Migrations are compatible with the current models.
- Downgrade/rollback is tested when the project supports it and the operation is
  safe.
- Seeds and fixtures are deterministic and isolated per test.
- Tests run in transactions or recreate schemas so test order cannot affect
  results.
- Constraints, indexes, uniqueness, foreign keys, cascades, defaults, generated
  columns, enums, and timestamp behavior are verified when touched.
- Concurrency and transaction behavior are tested for code that does locking,
  queues, background jobs, billing, auth, inventory, or other state transitions.

If Alembic is used, include:

- `alembic upgrade head`
- Model-vs-migration drift check where the project supports it.
- A migration smoke test against a fresh PostgreSQL database.

## FastAPI Initialization and API Testing

The agent must verify:

- The app imports without side effects that require production services.
- Settings load from test configuration and fail fast for missing required
  values.
- Dependency injection overrides work in tests.
- `/openapi.json` renders successfully.
- Health/readiness endpoints work when present.
- The ASGI app can be exercised with `httpx` or the FastAPI test client.
- Startup and shutdown lifespan events run in tests.
- Authentication, authorization, CORS, middleware, exception handlers, and
  request validation are tested when affected.
- Every changed endpoint has tests for success, validation failure, unauthorized
  access, forbidden access where applicable, missing records, conflicting state,
  and server-side error handling.
- API response schemas are asserted, not only status codes.
- Background tasks, queues, scheduled jobs, and external service calls use fakes,
  mocks, or local test services.

For security-sensitive endpoints, include negative tests for privilege bypass,
tenant boundary violations, object ownership, SQL injection vectors, mass
assignment, and unsafe deserialization.

## React / Node.js / Vite Minimum Test Stack

Use the repository's existing frontend tooling if present. If no test stack
exists, install and configure:

- `vitest`
- `@vitest/coverage-v8`
- `jsdom` or `happy-dom`
- `@testing-library/react`
- `@testing-library/jest-dom`
- `@testing-library/user-event`
- `@playwright/test`
- `msw` for API mocking
- `eslint`
- `prettier`
- TypeScript tooling when the project uses TypeScript
- `axe-core` and `@axe-core/playwright` for accessibility checks where useful

Use the detected package manager:

- `pnpm add -D ...` when `pnpm-lock.yaml` exists.
- `npm install -D ...` when `package-lock.json` exists.
- `yarn add -D ...` when `yarn.lock` exists.
- `bun add -d ...` when `bun.lockb` or Bun project metadata exists.

Required React / Vite checks:

- Install dependencies from the lockfile.
- `node --version`
- Package-manager version check.
- `npm run lint` or equivalent.
- `npm run typecheck` or equivalent for TypeScript projects.
- `npm run test` or equivalent.
- Coverage command when configured.
- `npm run build`.
- `npx playwright install` before Playwright tests if browsers are missing.
- `npm run test:e2e` or equivalent for critical workflows.
- Dependency audit using the package manager's audit command, unless the project
  has a different security scanner.

## React / Vite Initialization and UI Testing

The agent must verify:

- The Vite dev server starts successfully.
- Production build succeeds.
- Built assets can be served locally and loaded in a browser.
- Environment variables are documented, validated, and separated between dev,
  test, and production.
- Route-level code splitting, lazy imports, and error boundaries work when
  touched.
- Critical components render with realistic props and providers.
- Forms test typing, validation, submission, loading state, success, failure,
  disabled controls, and reset behavior.
- Client-side routing, protected routes, redirects, and not-found states are
  tested when present.
- API calls use MSW or another deterministic mock in component tests.
- End-to-end tests cover at least the primary user workflow, auth boundary if
  present, and one important failure path.
- Accessibility checks cover keyboard navigation, focus order, labels, dialogs,
  color contrast where practical, and screen-reader names for controls.
- Responsive behavior is checked at mobile and desktop viewport sizes for
  changed screens.

Use Playwright screenshots for visual regression only when the UI is stable
enough for deterministic comparison. Otherwise use DOM and accessibility
assertions.

## Full-Stack Integration Testing

For projects that include FastAPI, PostgreSQL, and React/Vite together, the agent
must test the seams between layers:

- Backend starts against a test PostgreSQL database.
- Frontend points to the test backend or mocked API according to the test type.
- CORS and cookies/session behavior work in the browser.
- Login/logout/session refresh flows are covered when present.
- API schema changes are reflected in frontend types or clients.
- Frontend handles backend validation errors and server failures.
- Database migrations support the API behavior being tested.
- Seeded test data is realistic enough to catch joins, permissions, empty states,
  and pagination/sorting/filtering bugs.

At least one smoke test should exercise the stack through the browser when the
project has a browser UI and backend API.

## Lint, Formatting, and Static Analysis

Linting is not just style. Treat it as a correctness gate.

Required lint/static-analysis expectations:

- Run the repository's existing lint and format checks.
- Add missing lint/format scripts if the project lacks them.
- Keep generated files, vendored files, build output, and migrations excluded
  only when exclusion is justified.
- Fix warnings introduced by the current change.
- Do not hide warnings by widening ignore rules unless there is a documented
  reason.
- Run type checking for typed languages and typed projects.
- Run dependency vulnerability checks.
- Run secret scanning if the repository already has it configured, or recommend
  adding it when secrets or deployment files are touched.

For SQL-heavy projects, consider SQL linting or migration linting. For OpenAPI
or generated clients, validate that generated artifacts are up to date.

## Regression Test Design

Regression tests must prove the bug cannot return through the same path.

For every fix:

1. Reproduce the bug with a failing test, failing script, or exact manual
   reproduction.
2. Make the smallest targeted fix.
3. Run the new regression test and the nearest existing test group.
4. Run the broader suite for the touched stack.
5. Search for the same pattern elsewhere and add coverage for representative
   siblings if the pattern recurs.
6. Verify the failure mode, not only the happy path.

Regression tests should cover:

- Boundary values.
- Empty, null, missing, malformed, and oversized input.
- Permission failures.
- Network and database errors.
- Retry behavior and idempotency.
- Time zones, dates, clocks, and scheduled behavior.
- Unicode and path handling where user input or file systems are involved.
- Concurrency, cancellation, and reentrancy for async code.
- Serialization compatibility and migration behavior.
- Previously fixed issues in the same module.

## Coverage Expectations

Coverage is a signal, not a substitute for good assertions. Still, the agent
must collect coverage when practical.

Minimum expectations:

- Changed business logic should have direct unit coverage.
- Changed API endpoints should have request/response coverage.
- Changed database behavior should have integration coverage against PostgreSQL.
- Changed React components should have component or E2E coverage.
- Changed Avalonia view models and commands should have unit coverage.
- Changed Avalonia views should have binding/headless UI coverage where
  practical.
- Critical workflows should be covered by integration or E2E tests.

If the project already has thresholds, do not lower them. If there are no
thresholds, recommend reasonable thresholds after the first coverage run rather
than inventing arbitrary gates blindly.

For high-risk logic, add mutation testing:

- .NET: `dotnet stryker`
- Python: `mutmut` or `cosmic-ray`
- JavaScript/TypeScript: StrykerJS

Mutation testing is especially useful for validators, permissions, state
machines, calculations, parsers, and serialization code.

## Initialization Test Matrix

When practical, verify these initialization paths:

- Fresh install from lockfiles.
- Clean build.
- Test database creation.
- Migration from empty database to latest schema.
- Application startup.
- Health/readiness route or equivalent startup smoke check.
- Production build or publish.
- Local packaged artifact launch for desktop apps.
- Browser load for web apps.
- CI workflow parity with local commands.

If any path is too expensive for the current task, state why and run the closest
lower-cost substitute.

## CI Requirements

If CI exists, keep local verification aligned with CI. If CI does not exist and
the task touches test infrastructure, recommend or add a CI workflow when within
scope.

Preferred CI gates:

- Restore/install from lockfiles.
- Lint and format check.
- Type check.
- Unit tests.
- Integration tests with PostgreSQL service where needed.
- Build or publish.
- Coverage artifact.
- Dependency audit.
- Windows runner for Avalonia desktop projects.
- Browser install/cache and Playwright report for web E2E tests.

Do not create CI that only runs a partial suite while naming it "full verify."
Names must match what the workflow actually checks.

## Evidence Ledger

At completion, the agent must report:

- Test infrastructure added or changed.
- Exact commands run.
- Pass/fail result for each command.
- Coverage result, if collected.
- Any skipped tests and exact reason.
- Any tests that are still missing and the residual risk.
- Any environmental blockers, such as missing Docker, missing SDK, locked file,
  unavailable browser binary, or unavailable database.
- The final status of the bug or feature.

Use this final response shape:

```text
Implemented:
- ...

Verified:
- PASS: <command>
- PASS: <command>
- FAIL/BLOCKED: <command> - <reason>

Residual risk:
- ...
```

Do not claim "all tests pass" unless the full relevant suite actually ran and
passed.

## Quick Command Templates

These templates are starting points. Adapt them to the repository scripts and
package manager.

C# / Avalonia:

```powershell
dotnet --info
dotnet restore
dotnet build
dotnet build -c Release
dotnet test --collect:"XPlat Code Coverage"
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
dotnet publish -c Release -r win-x64
```

Python / FastAPI:

```powershell
python --version
pytest
pytest --cov --cov-branch
ruff check .
ruff format --check .
mypy .
bandit -r .
pip-audit
alembic upgrade head
```

React / Vite:

```powershell
node --version
npm run lint
npm run typecheck
npm run test
npm run test:coverage
npm run build
npx playwright install
npm run test:e2e
npm audit
```

Replace `npm` with `pnpm`, `yarn`, or `bun` when the lockfile indicates a
different package manager.

