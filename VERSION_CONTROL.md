# VERSION CONTROL — Git Discipline Directive

> Reusable building block. `@include` this into a project's CLAUDE.md (local
> `@VERSION_CONTROL.md` when copied into the project). Release tagging/versioning
> for shippable desktop apps is governed by `@SOFTWARE_RELEASE.md`
> (`ONLY_IF_DESKTOP_DOWNLOAD_APP`); this directive governs day-to-day git for
> every project.

This document is binding when included. Goal: a clean, recoverable history with
**no secret ever committed**, no surprise force-pushes, and commits that a future
reader (human or agent) can understand and bisect.

---

## 🔐 Secrets — the cardinal rule

1. **Never `git add -A` / `git add .` without first checking what's staged.** Run
   `git diff --cached --name-only` and scan the diff for secrets
   (`grep -iE "API_KEY|SECRET|TOKEN|PASSWORD|GOCSPX|-----BEGIN"`), every time.
2. **Gitignore secrets before they exist** — `*.env`, `*secret*.json`,
   `client_secret*.json`, `*credentials*`, token/keystore files. Verify with
   `git check-ignore <file>`.
3. **A committed secret is a compromised secret.** If one lands in history:
   (a) **rotate/revoke it at the source first** (the only step that truly matters);
   (b) **purge history** — `git clone --mirror` to a throwaway dir, `git filter-repo
   --invert-paths --path-glob '<secretfile>'` and `--replace-text` to scrub the
   string, force-push, then realign every working copy (`fetch` + `reset --hard` +
   delete stale tags + `gc --prune=now`); (c) verify clean with a fresh clone.
   Deleting the file in a later commit does **not** remove it.

## Branching

- **Never commit directly to the default branch** unless the user explicitly asks.
  If on `main`/`master`, branch first.
- Branch names: `feature/<id>-description`, `fix/<id>-description`,
  `chore/description`. Keep one logical change per branch.

## Commits

- **Conventional, scoped, imperative:** `type(scope): summary` —
  `feat`, `fix`, `docs`, `chore`, `refactor`, `perf`, `test`, `ci`.
- Reference the task/bug id where one exists (`T-12`, `BUG-103`).
- Body explains the **why**, not just the what. Keep each commit buildable.
- **Never skip hooks** (`--no-verify`) or bypass signing unless the user asks. If a
  hook fails, fix the cause.
- Co-author agent commits: end the message with
  `Co-Authored-By: <agent> <noreply@anthropic.com>`.
- **Test-first:** a bug-fix commit should include (or follow) the regression test
  that fails before and passes after — see `@TESTING_PROCEDURES.md`.

## Pushing & history

- On a rejected push (remote ahead): `git pull --rebase` then push — don't merge
  noise. Stash/commit local work first.
- **No force-push to a shared branch without a stated reason** and the user's
  awareness. Prefer non-destructive fixes; a history rewrite is a deliberate,
  announced operation (see Secrets above).
- Tags are immutable markers: `vMAJOR.MINOR.PATCH` (SemVer). Don't move a published
  tag; cut a new version instead.

## .gitignore essentials (every project)

Build output (`bin/`, `obj/`, `dist/`, `build/`, `node_modules/`, `publish/`),
secrets/tokens, local DBs and user data, OS cruft (`.DS_Store`, `Thumbs.db`),
IDE files (`.vs/`, `.idea/`, `*.user`). Keep generated installers/artifacts out
unless a release flow intentionally tracks them.

## Pre-commit gate

Before each commit: lint/format clean, affected tests green, **no secret in the
staged diff**, only intended files staged. Report the exact commands run.

## Gotchas (carry across projects)

- **OneDrive / Dropbox + `.git` is risky** — the sync client can corrupt a repo
  mid-operation. Let it settle before git ops; do history rewrites on a mirror
  clone in a **non-synced** temp dir; ideally keep repos off synced folders.
- **Shells mangle backslashes** — a bash arg like `-o out\dir` becomes `outdir`.
  Use forward slashes or the native shell for Windows paths.
- **Line endings** — `LF will be replaced by CRLF` warnings on Windows are normal;
  set `.gitattributes` (`* text=auto`) if churn becomes noisy.
- **Don't poll a push in a loop** — if a push/CI is rejected, read the reason and
  act; don't retry blindly.
