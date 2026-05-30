; ---------------------------------------------------------------------------
; VybeDesk Installer - Inno Setup Script
; ---------------------------------------------------------------------------
; Build the publish output first:
;   dotnet publish src\VybeDesk.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
;
; Then compile this script with Inno Setup 6.x:
;   iscc installer.iss
; ---------------------------------------------------------------------------

#define MyAppName      "VybeDesk"
#define MyAppVersion   "1.1.0"
#define MyAppPublisher "Vybecode LTD"
#define MyAppURL       "https://vybecode.com"
#define MyAppExeName   "VybeDesk.App.exe"
#define MyAppCopyright "Copyright (c) 2026 Vybecode LTD"

; Path to the dotnet publish output (relative to this .iss file)
#define PublishDir     "src\VybeDesk.App\bin\Release\net9.0\win-x64\publish"

[Setup]
AppId={{B7E3F4A1-9C2D-4E5F-A6B8-1D3E5F7A9B0C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright={#MyAppCopyright}

; Install location
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=no

; License - placeholder MIT license embedded below via LicenseFile
LicenseFile=LICENSE.txt

; Output installer location and naming
OutputDir=installer-output
OutputBaseFilename=VybeDesk-Setup-{#MyAppVersion}

; Icon for the installer executable itself
SetupIconFile=src\VybeDesk.App\Assets\app.ico

; Uninstaller icon in Add/Remove Programs
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Require admin for Program Files install
PrivilegesRequired=admin

; Wizard style
WizardStyle=modern
WizardSizePercent=110,110

; Minimum Windows version (Windows 10 1607 / Server 2016)
MinVersion=10.0.14393

; Misc
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ShowLanguageDialog=auto
DisableWelcomePage=no
DisableDirPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "{cm:CreateDesktopIcon}";   GroupDescription: "{cm:AdditionalIcons}"
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Include everything from the publish output folder
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcut (when task is selected)
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\Uninstall {#MyAppName}";  Filename: "{uninstallexe}";        Tasks: startmenuicon

; Desktop shortcut (when task is selected)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Option to launch the app after installation
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up the install directory (Inno handles this automatically, but this
; catches any files the app may have written into its own folder at runtime)
Type: filesandordirs; Name: "{app}"

[Code]
// ---------------------------------------------------------------------------
// Uninstall: prompt the user about removing local data
// ---------------------------------------------------------------------------

var
  RemoveDataCheckbox: TNewCheckBox;

procedure InitializeUninstallProgressForm();
begin
  // Add a checkbox to the uninstall progress form asking whether to
  // remove all user data (database, API key, settings).
  RemoveDataCheckbox := TNewCheckBox.Create(UninstallProgressForm);
  RemoveDataCheckbox.Parent := UninstallProgressForm;
  RemoveDataCheckbox.Caption := 'Remove all user data (settings, database, API keys)';
  RemoveDataCheckbox.Checked := False;
  RemoveDataCheckbox.Left := ScaleX(10);
  RemoveDataCheckbox.Top := UninstallProgressForm.ClientHeight - ScaleY(50);
  RemoveDataCheckbox.Width := UninstallProgressForm.ClientWidth - ScaleX(20);
  RemoveDataCheckbox.Height := ScaleY(20);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RemoveDataCheckbox.Checked then
    begin
      // %LOCALAPPDATA%\VybeDesk contains:
      //   vybedesk.db    - SQLite database (projects, prompts, notes, bugs, etc.)
      //   apikey.bin     - DPAPI-encrypted Anthropic API key
      //   settings.json  - app settings (model, theme, paths)
      DataDir := ExpandConstant('{localappdata}\VybeDesk');
      if DirExists(DataDir) then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
