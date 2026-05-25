# Integration Task — Rebuilt Skill Module (ClaudePM)

This is NOT a build-from-scratch task. The skill area of ClaudePM has been
rebuilt as finished code and delivered as a set of files. Your job is to
integrate those files into the existing solution, remove the files they
replace, and confirm the project still builds and runs. Do not redesign
anything — the code is complete and deliberate.

Read `CLAUDE.md` first for project conventions.

## Why this change was made

The old skill screen put the resource-file list inside a StackPanel. A
StackPanel gives each child the height the child requests, and a ListBox
requests enough height to show every item, so the resource list overflowed the
bottom of the window. The rebuild fixes this at the root by placing the
right-hand column in a Grid whose rows are `Auto,*,Auto,220`: the content
viewer sits in the flexible `*` row and the resource ListBox sits in the fixed
`220` row, so the list is bounded and scrolls internally and can never
overflow. Do not "simplify" that Grid back into a StackPanel — that would
reintroduce the exact bug.

The skill area is also reorganized: a single "Skills" sidebar page now hosts an
in-pane toggle between "Skill Manager" and "Skill Builder", so the left sidebar
needs no submenu support. The Skill Builder module does not exist yet; the
section runs manager-only until it does, with the builder tab hidden.

## Files to ADD (new files in the delivered set)

- `src/ClaudePM.Core/Models/SkillResource.cs` — new model: one supporting file
  belonging to a skill.
- `src/ClaudePM.App/ViewModels/SkillManagerViewModel.cs` — the rebuilt skill
  manager view model.
- `src/ClaudePM.App/ViewModels/SkillSectionViewModel.cs` — the in-pane container
  that hosts the manager (and, later, the builder).
- `src/ClaudePM.App/Views/SkillManagerView.axaml` (+ `.axaml.cs`) — the rebuilt
  manager view with the corrected Grid layout.
- `src/ClaudePM.App/Views/SkillSectionView.axaml` (+ `.axaml.cs`) — the
  container view with the in-pane toggle.

## Files to REPLACE (delivered files overwrite existing ones)

- `src/ClaudePM.Core/Models/SkillFile.cs` — now carries a `Resources` list and
  a `HasResources` flag.
- `src/ClaudePM.Core/Services/ISkillLibraryService.cs` — gains two methods:
  `PopulateResources` and `ReadResourceAsync`.
- `src/ClaudePM.Services/Skills/SkillLibraryService.cs` — implements those two
  new methods.
- `src/ClaudePM.App/ViewModels/MainWindowViewModel.cs` — the shell now takes a
  `SkillSectionViewModel` instead of the old `SkillLibraryViewModel`.
- `src/ClaudePM.App/Program.cs` — DI now registers `SkillManagerViewModel` and
  `SkillSectionViewModel` and no longer registers `SkillLibraryViewModel`.

## Files to DELETE (the rebuild replaces these — they are now dead code)

- `src/ClaudePM.App/ViewModels/SkillLibraryViewModel.cs`
- `src/ClaudePM.App/Views/SkillLibraryView.axaml`
- `src/ClaudePM.App/Views/SkillLibraryView.axaml.cs`

These must be deleted, not merely left alone. If they remain, the compiler will
still build them and they will reference the removed registration, leaving a
confusing half-old module in the project.

## After integration

1. Run `dotnet build`. Resolve any errors — likely candidates are a stale
   `using` of the old type name, or a leftover reference to
   `SkillLibraryViewModel` somewhere not listed above.
2. Run `dotnet test`, confirm green.
3. Run the app. Confirm: the sidebar has a single "Skills" page; opening it
   shows the in-pane toggle with "Skill Manager" active and the "Skill Builder"
   tab hidden; scanning a folder lists skills; selecting a skill shows its text
   in the viewer and its supporting files in the resource list; selecting a
   resource swaps the viewer to that resource's contents; "Show Skill File"
   returns the viewer to the skill; and the resource list stays a fixed size
   with padding below it, never overflowing the window.
4. Update the "Last Completed Task" section of `CLAUDE.md` to record that the
   skill area was rebuilt: the StackPanel layout bug is fixed, `SkillLibrary*`
   was replaced by a `SkillSectionViewModel` hosting `SkillManagerViewModel`,
   and `SkillResource` was added.

## Note for when the Skill Builder module is later built

`SkillSectionViewModel`'s constructor accepts an optional builder page. When the
Skill Builder module is built, register its view model in `Program.cs` and pass
it into `SkillSectionViewModel`; the builder tab then appears automatically with
no change to the sidebar or shell. Nothing about that future step needs doing
now.
