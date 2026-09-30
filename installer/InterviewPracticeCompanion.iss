#define AppName "Interview Practice Companion"
#define AppExe "InterviewPracticeCompanion.exe"
[Setup]
AppId={{7E7DB7F5-E781-40FC-8874-17D59DBCB61B}
AppName={#AppName}
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\InterviewPracticeCompanion
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=InterviewPracticeCompanion-Setup-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
[Code]
function IsDotNet8DesktopInstalled: Boolean;
var ResultCode: Integer;
begin
  Result := RegKeyExists(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0');
end;
function InitializeSetup: Boolean;
var ResultCode: Integer;
begin
  Result := True;
  if not IsDotNet8DesktopInstalled then begin
    MsgBox('.NET 8 Desktop Runtime x64 is required. The official Microsoft installer page will open.', mbInformation, MB_OK);
    ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    Result := False;
  end;
end;
