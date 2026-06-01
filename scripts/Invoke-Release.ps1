<#
.SYNOPSIS
    VybeDesk release script - STAGE 1 of the SOFTWARE_RELEASE.md pipeline.

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
    workflow (.github/workflows/auto-release.yml) - see SOFTWARE_RELEASE.md
    Core Rule #1. Never add `gh release create` to this script.

.PARAMETER Major
    Perform a MINOR semver bump (1.0.0 -> 1.1.0) - the "major release" trigger.
    Omit for the default patch bump (1.0.0 -> 1.0.1) - the "release it" trigger.

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

# UTF-8 without BOM - version-bearing files must not gain a BOM (works the same
# under Windows PowerShell 5.1 and PowerShell 7, unlike Set-Content -Encoding utf8).
$Utf8NoBom  = New-Object System.Text.UTF8Encoding($false)

$Csproj     = Join-Path $RepoRoot 'src\VybeDesk.App\VybeDesk.App.csproj'
$Iss        = Join-Path $RepoRoot 'installer.iss'
$Changelog  = Join-Path $RepoRoot 'CHANGELOG.md'
$PublishDir = Join-Path $RepoRoot 'src\VybeDesk.App\bin\Release\net9.0\win-x64\publish'
$ReleasesDir= Join-Path $RepoRoot 'releases\latest'

function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# --- Code signing (optional; configured via env vars, never secrets in-repo) -
# Set ONE of:
#   VYBEDESK_SIGN_PFX  (+ VYBEDESK_SIGN_PASSWORD)  - a .pfx certificate file, OR
#   VYBEDESK_SIGN_THUMBPRINT                        - a cert in the Windows store
#                                                     (EV tokens, Azure-installed certs)
# Optional VYBEDESK_SIGN_TIMESTAMP (RFC3161 URL; defaults to DigiCert).
# If neither is set, signing is SKIPPED with a warning and the release proceeds
# unsigned (today's behaviour) - so dropping in a cert is the only step needed.
function Find-SignTool {
    $cmd = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
    if ($cmd) { return $cmd }
    $kitRoots = @(
        [Environment]::GetEnvironmentVariable('ProgramFiles(x86)'),
        [Environment]::GetEnvironmentVariable('ProgramFiles')
    ) | Where-Object { $_ } | ForEach-Object { Join-Path $_ 'Windows Kits\10\bin' }
    foreach ($root in $kitRoots) {
        if (Test-Path $root) {
            $hit = Get-ChildItem $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
                   Where-Object { $_.FullName -match '\\x64\\' } |
                   Sort-Object FullName -Descending | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    return $null
}

function Invoke-Sign {
    param([Parameter(Mandatory)][string]$Path)
    $pfx   = $env:VYBEDESK_SIGN_PFX
    $thumb = $env:VYBEDESK_SIGN_THUMBPRINT
    $ts    = if ($env:VYBEDESK_SIGN_TIMESTAMP) { $env:VYBEDESK_SIGN_TIMESTAMP } else { 'http://timestamp.digicert.com' }

    if (-not $pfx -and -not $thumb) {
        Write-Warning "Code signing skipped for $([IO.Path]::GetFileName($Path)) - no cert configured (set VYBEDESK_SIGN_PFX + VYBEDESK_SIGN_PASSWORD, or VYBEDESK_SIGN_THUMBPRINT)."
        return
    }
    $signtool = Find-SignTool
    if (-not $signtool) { throw 'Signing configured but signtool.exe not found - install the Windows 10/11 SDK.' }

    $sa = @('sign', '/fd', 'SHA256', '/tr', $ts, '/td', 'SHA256', '/v')
    if ($pfx) {
        if (-not (Test-Path $pfx)) { throw "VYBEDESK_SIGN_PFX not found: $pfx" }
        $sa += @('/f', $pfx)
        if ($env:VYBEDESK_SIGN_PASSWORD) { $sa += @('/p', $env:VYBEDESK_SIGN_PASSWORD) }
    } else {
        $sa += @('/sha1', $thumb)
    }
    $sa += $Path

    Write-Host "  Signing $([IO.Path]::GetFileName($Path)) ..."
    & $signtool @sa
    if ($LASTEXITCODE -ne 0) { throw "signtool failed on $Path (exit $LASTEXITCODE)." }
    & $signtool verify /pa /v $Path | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed on $Path." }
    Write-Host "  Signed + verified: $([IO.Path]::GetFileName($Path))" -ForegroundColor Green
}

# --- Step 1: determine the new version --------------------------------------
Step 'Step 1: Determine version'
$csprojText = [System.IO.File]::ReadAllText($Csproj)
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
[System.IO.File]::WriteAllText($Csproj, $csprojText, $Utf8NoBom)

$issText = [System.IO.File]::ReadAllText($Iss)
$issText = $issText -replace '#define MyAppVersion\s+"\d+\.\d+\.\d+"', "#define MyAppVersion   `"$NewVersion`""
[System.IO.File]::WriteAllText($Iss, $issText, $Utf8NoBom)
Write-Host "  Bumped VybeDesk.App.csproj and installer.iss to $NewVersion"

# --- Step 3: pre-release gate -----------------------------------------------
Step 'Step 3: Pre-release gate (stop app + tests)'
Get-Process VybeDesk.App -ErrorAction SilentlyContinue | Stop-Process -Force
if (-not $SkipTests) {
    dotnet test (Join-Path $RepoRoot 'tests\VybeDesk.Tests\VybeDesk.Tests.csproj') -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed - aborting release. Do not release red.' }
} else {
    Write-Warning 'Tests skipped (-SkipTests).'
}

# --- Step 4: publish self-contained build -----------------------------------
Step 'Step 4: Publish (win-x64, self-contained, single-file)'
dotnet publish (Join-Path $RepoRoot 'src\VybeDesk.App') -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# --- Step 4b: sign the published app exe (before Inno Setup packages it) -----
Step 'Step 4b: Sign published app'
Invoke-Sign (Join-Path $PublishDir 'VybeDesk.App.exe')

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
Invoke-Sign $built
Copy-Item $built $ReleasesDir -Force
Write-Host "  Staged: releases/latest/VybeDesk-Setup-$NewVersion.exe"

# --- Step 7: promote CHANGELOG [Unreleased] -> [vX.Y.Z] - date ---------------
Step 'Step 7: Promote CHANGELOG entry'
$date = (Get-Date).ToString('yyyy-MM-dd')
$emdash = [char]0x2014   # ASCII-safe source; the file's own em-dashes are read via UTF-8
$clog = [System.IO.File]::ReadAllText($Changelog)
if ($clog -match '##\s*\[Unreleased\]') {
    # Replace only the "## [Unreleased]" token; any " <dash> Title" suffix on the
    # same line is preserved from the original (read correctly as UTF-8).
    $clog = $clog -replace '##\s*\[Unreleased\]', "## [$Tag] $emdash $date"
} else {
    Write-Warning 'No [Unreleased] section found in CHANGELOG.md - add the entry manually before release.'
}
[System.IO.File]::WriteAllText($Changelog, $clog, $Utf8NoBom)

# --- Step 8: commit, tag, push (NO GitHub Release) --------------------------
Step 'Step 8: Commit + tag + push'
# git writes benign warnings (e.g. "LF will be replaced by CRLF") to stderr.
# Under $ErrorActionPreference='Stop', Windows PowerShell 5.1 promotes native
# stderr to a TERMINATING error - which would abort the release after the build
# but before the commit. Drop to Continue here and gate on $LASTEXITCODE.
$ErrorActionPreference = 'Continue'
git add $Csproj $Iss $Changelog $ReleasesDir
if ($LASTEXITCODE) { throw "git add failed ($LASTEXITCODE)" }
git commit -m "Release $Tag"
if ($LASTEXITCODE) { throw "git commit failed ($LASTEXITCODE)" }
git tag $Tag
if ($LASTEXITCODE) { throw "git tag failed ($LASTEXITCODE)" }
git push
if ($LASTEXITCODE) { throw "git push failed ($LASTEXITCODE)" }
git push --tags
if ($LASTEXITCODE) { throw "git push --tags failed ($LASTEXITCODE)" }

Write-Host "`nStage 1 complete for $Tag." -ForegroundColor Green
Write-Host "The pushed installer triggers .github/workflows/auto-release.yml, which is the"
Write-Host "ONLY release creator. Verify with:  gh run list --limit 1   then   gh release view $Tag"
