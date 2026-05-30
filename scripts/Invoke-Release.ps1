<#
.SYNOPSIS
    VybeDesk release script — STAGE 1 of the SOFTWARE_RELEASE.md pipeline.

.DESCRIPTION
    Runs locally (needs the .NET 9 SDK + Inno Setup 6). In one pass it:
      1. Computes the next version (patch by default; minor with -Major).
      2. Bumps the version in EVERY version-bearing file atomically
         (VybeDesk.App.csproj <Version>/<AssemblyVersion>/<FileVersion>
          + installer.iss #define MyAppVersion).
      3. Pre-release gate: kills any running VybeDesk.App, runs the test suite.
      4. Publishes a self-contained win-x64 single-file build.
      5. Compiles the Inno Setup installer.
      6. Copies the installer to releases/latest/ (the location CI watches).
      7. Promotes CHANGELOG.md [Unreleased] -> [vX.Y.Z] - <date>.
      8. Commits, tags vX.Y.Z, and pushes (commit + tags).

    It does NOT create the GitHub Release. That is the EXCLUSIVE job of the CI
    workflow (.github/workflows/auto-release.yml) — see SOFTWARE_RELEASE.md
    Core Rule #1. Never add `gh release create` to this script.

.PARAMETER Major
    Perform a MINOR semver bump (1.0.0 -> 1.1.0) — the "major release" trigger.
    Omit for the default patch bump (1.0.0 -> 1.0.1) — the "release it" trigger.

.PARAMETER SkipTests
    Skip the test gate (NOT recommended; for emergency re-runs only).

.EXAMPLE
    pwsh scripts/Invoke-Release.ps1            # patch release (release it)
    pwsh scripts/Invoke-Release.ps1 -Major     # minor release (major release)
#>
[CmdletBinding()]
param(
    [switch]$Major,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

# --- Resolve paths (repo root = parent of this script's folder) -------------
$RepoRoot   = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$Csproj     = Join-Path $RepoRoot 'src\VybeDesk.App\VybeDesk.App.csproj'
$Iss        = Join-Path $RepoRoot 'installer.iss'
$Changelog  = Join-Path $RepoRoot 'CHANGELOG.md'
$PublishDir = Join-Path $RepoRoot 'src\VybeDesk.App\bin\Release\net9.0\win-x64\publish'
$ReleasesDir= Join-Path $RepoRoot 'releases\latest'

function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# --- Step 1: determine the new version --------------------------------------
Step 'Step 1: Determine version'
$csprojText = Get-Content $Csproj -Raw
if ($csprojText -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    throw "Could not read <Version>X.Y.Z</Version> from $Csproj"
}
$maj = [int]$Matches[1]; $min = [int]$Matches[2]; $pat = [int]$Matches[3]
$current = "$maj.$min.$pat"
if ($Major) { $min++; $pat = 0 } else { $pat++ }   # -Major = minor bump; default = patch
$NewVersion = "$maj.$min.$pat"
$Tag = "v$NewVersion"
Write-Host "  $current -> $NewVersion  (tag $Tag)"

# Guard: tag must not already exist.
if ((git tag -l $Tag)) { throw "Tag $Tag already exists. Aborting." }

# --- Step 2: bump version in ALL version-bearing files ----------------------
Step 'Step 2: Bump version (csproj + installer.iss)'
$csprojText = $csprojText `
    -replace '<Version>\d+\.\d+\.\d+</Version>', "<Version>$NewVersion</Version>" `
    -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$NewVersion.0</AssemblyVersion>" `
    -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>', "<FileVersion>$NewVersion.0</FileVersion>"
Set-Content -Path $Csproj -Value $csprojText -NoNewline -Encoding utf8

$issText = Get-Content $Iss -Raw
$issText = $issText -replace '#define MyAppVersion\s+"\d+\.\d+\.\d+"', "#define MyAppVersion   `"$NewVersion`""
Set-Content -Path $Iss -Value $issText -NoNewline -Encoding utf8
Write-Host "  Bumped VybeDesk.App.csproj and installer.iss to $NewVersion"

# --- Step 3: pre-release gate -----------------------------------------------
Step 'Step 3: Pre-release gate (stop app + tests)'
Get-Process VybeDesk.App -ErrorAction SilentlyContinue | Stop-Process -Force
if (-not $SkipTests) {
    dotnet test (Join-Path $RepoRoot 'tests\VybeDesk.Tests\VybeDesk.Tests.csproj') -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed — aborting release. Do not release red.' }
} else {
    Write-Warning 'Tests skipped (-SkipTests).'
}

# --- Step 4: publish self-contained build -----------------------------------
Step 'Step 4: Publish (win-x64, self-contained, single-file)'
dotnet publish (Join-Path $RepoRoot 'src\VybeDesk.App') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# --- Step 5: compile the installer ------------------------------------------
Step 'Step 5: Compile Inno Setup installer'
$iscc = $null
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    (Join-Path $env:LOCALAPPDATA 'InnoSetup6\iscc.exe'),
    'C:\Program Files (x86)\Inno Setup 6\iscc.exe'
)
foreach ($c in $isccCandidates) { if ($c -and (Test-Path $c)) { $iscc = $c; break } }
if (-not $iscc) { throw 'Inno Setup compiler (iscc.exe) not found. Install Inno Setup 6 or add it to PATH.' }
& $iscc $Iss
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

# --- Step 6: copy installer to the CI-watched location ----------------------
Step 'Step 6: Stage installer in releases/latest/'
New-Item -ItemType Directory -Force -Path $ReleasesDir | Out-Null
Get-ChildItem (Join-Path $ReleasesDir '*.exe') -ErrorAction SilentlyContinue | Remove-Item -Force
$built = Join-Path $RepoRoot "installer-output\VybeDesk-Setup-$NewVersion.exe"
if (-not (Test-Path $built)) { throw "Expected installer not found: $built" }
Copy-Item $built $ReleasesDir -Force
Write-Host "  Staged: releases/latest/VybeDesk-Setup-$NewVersion.exe"

# --- Step 7: promote CHANGELOG [Unreleased] -> [vX.Y.Z] - date ---------------
Step 'Step 7: Promote CHANGELOG entry'
$date = (Get-Date).ToString('yyyy-MM-dd')
$clog = Get-Content $Changelog -Raw
if ($clog -match '##\s*\[Unreleased\]\s*(—|-)\s*([^\r\n]*)') {
    $title = $Matches[2].Trim()
    $clog  = $clog -replace '##\s*\[Unreleased\]\s*(—|-)\s*[^\r\n]*', "## [$Tag] — $date — $title"
} elseif ($clog -match '##\s*\[Unreleased\]') {
    $clog  = $clog -replace '##\s*\[Unreleased\]', "## [$Tag] — $date"
} else {
    Write-Warning 'No [Unreleased] section found in CHANGELOG.md — add the entry manually before release.'
}
Set-Content -Path $Changelog -Value $clog -NoNewline -Encoding utf8

# --- Step 8: commit, tag, push (NO GitHub Release) --------------------------
Step 'Step 8: Commit + tag + push'
git add $Csproj $Iss $Changelog (Join-Path $ReleasesDir '*.exe')
git commit -m "Release $Tag"
git tag $Tag
git push
git push --tags

Write-Host "`nStage 1 complete for $Tag." -ForegroundColor Green
Write-Host "The pushed installer triggers .github/workflows/auto-release.yml, which is the"
Write-Host "ONLY release creator. Verify with:  gh run list --limit 1   then   gh release view $Tag"
