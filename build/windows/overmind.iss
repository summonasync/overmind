#define AppName "Overmind"
#define AppPublisher "Overmind"
#define AppExe "Overmind.Host.exe"
#define McpExe "overmind-mcp.exe"

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\..\dist\stage-win"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\dist"
#endif
#ifndef MaxPayloadPath
  #define MaxPayloadPath 0
#endif

[Setup]
AppId={{22F04E3B-CF50-4887-B2A5-3F0816BA1BE9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\Overmind
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=overmind-{#AppVersion}-setup
SetupIconFile=..\..\src\Overmind.Host\Assets\overmind.ico
UninstallDisplayIcon={app}\host\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
CloseApplications=no
ChangesEnvironment=yes

[Tasks]
Name: "autostart"; Description: "Start Overmind when I sign in"; GroupDescription: "Startup:"
Name: "mcppath"; Description: "Add the MCP adapter to PATH (lets agents launch 'overmind-mcp')"; GroupDescription: "Integration:"
Name: "mcpregister"; Description: "Register Overmind with AI agents found on this machine (writes to their config files)"; GroupDescription: "Integration:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\host\*"; DestDir: "{app}\host"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\mcp\*"; DestDir: "{app}\mcp"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\web\*"; DestDir: "{app}\web"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\host\{#AppExe}"; WorkingDir: "{app}\host"; Flags: runminimized
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\host\{#AppExe}"; WorkingDir: "{app}\host"; Tasks: desktopicon; Flags: runminimized

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Overmind"; ValueData: """{app}\host\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[InstallDelete]
Type: filesandordirs; Name: "{app}\host\bundled\crew"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\host"
Type: filesandordirs; Name: "{app}\mcp"
Type: filesandordirs; Name: "{app}\web"
Type: dirifempty; Name: "{app}"

[Run]
Filename: "{app}\mcp\overmind-mcp.exe"; Parameters: "install"; Tasks: mcpregister; Flags: runhidden waituntilterminated; StatusMsg: "Registering with AI agents..."
Filename: "{app}\host\{#AppExe}"; Description: "Start Overmind now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\mcp\overmind-mcp.exe"; Parameters: "install --remove"; RunOnceId: "UnregisterAgents"; Flags: runhidden waituntilterminated

[Code]
const
  EnvKeyUser = 'Environment';
  EnvKeyMachine = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  StopTimeoutMs = 10000;
  StopPollMs = 500;
  HealthReceiveTimeoutMs = 15000;
  MaxPathBudget = 259;
  MaxPayloadPathChars = {#MaxPayloadPath};
  UninstallKeyName = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{22F04E3B-CF50-4887-B2A5-3F0816BA1BE9}_is1';

function EnvRootKey(): Integer;
begin
  if IsAdminInstallMode then
    Result := HKEY_LOCAL_MACHINE
  else
    Result := HKEY_CURRENT_USER;
end;

function EnvSubKey(): String;
begin
  if IsAdminInstallMode then
    Result := EnvKeyMachine
  else
    Result := EnvKeyUser;
end;

function GetPreviousAppDir(): String;
begin
  Result := '';
  RegQueryStringValue(EnvRootKey(), UninstallKeyName, 'Inno Setup: App Path', Result);
end;

function IsUpgradeInstall(): Boolean;
begin
  Result := GetPreviousAppDir() <> '';
end;

function PathContains(const RootKey: Integer; const SubKey, Segment: String): Boolean;
var
  ExistingPath: String;
begin
  if not RegQueryStringValue(RootKey, SubKey, 'Path', ExistingPath) then
  begin
    Result := False;
    exit;
  end;

  Result := Pos(';' + Uppercase(Segment) + ';', ';' + Uppercase(ExistingPath) + ';') > 0;
end;

procedure AddPathSegment(const RootKey: Integer; const SubKey, Segment: String);
var
  ExistingPath, NewPath: String;
begin
  if not RegQueryStringValue(RootKey, SubKey, 'Path', ExistingPath) then
    ExistingPath := '';

  if ExistingPath = '' then
    NewPath := Segment
  else
    NewPath := ExistingPath + ';' + Segment;

  RegWriteExpandStringValue(RootKey, SubKey, 'Path', NewPath);
end;

procedure RemovePathSegment(const Segment: String);
var
  ExistingPath, Padded: String;
  Position: Integer;
begin
  if not RegQueryStringValue(EnvRootKey(), EnvSubKey(), 'Path', ExistingPath) then
    exit;

  Padded := ';' + ExistingPath + ';';
  Position := Pos(';' + Uppercase(Segment) + ';', Uppercase(Padded));
  if Position = 0 then
    exit;

  Delete(Padded, Position, Length(Segment) + 1);
  RegWriteExpandStringValue(EnvRootKey(), EnvSubKey(), 'Path', Copy(Padded, 2, Length(Padded) - 2));
end;

function DataDirectory(): String;
begin
  Result := ExpandConstant('{%USERPROFILE}') + '\.overmind';
end;

function ReadHostPort(var Port: String): Boolean;
var
  PortFile: String;
  Lines: TArrayOfString;
begin
  Result := False;
  PortFile := DataDirectory() + '\host.port';
  if not FileExists(PortFile) then
    exit;
  if not LoadStringsFromFile(PortFile, Lines) then
    exit;
  if GetArrayLength(Lines) = 0 then
    exit;

  Port := Trim(Lines[0]);
  Result := Port <> '';
end;

function HttpStatus(const Method, Url: String): Integer;
var
  Request: Variant;
begin
  Result := -1;
  try
    Request := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Request.SetTimeouts(2000, 2000, 2000, 5000);
    Request.Open(Method, Url, False);
    Request.Send('');
    Result := Request.Status;
  except
    Result := -1;
  end;
end;

function ProbeHealth(const Url: String; var StatusCode: Integer; var Body: String): Boolean;
var
  Request: Variant;
begin
  Result := False;
  StatusCode := -1;
  Body := '';
  try
    Request := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Request.SetTimeouts(2000, 2000, 2000, HealthReceiveTimeoutMs);
    Request.Open('GET', Url, False);
    Request.Send('');
    StatusCode := Request.Status;
    Body := Request.ResponseText;
    Result := True;
  except
    Result := False;
  end;
end;

function IsOurHost(const BaseUrl: String): Boolean;
var
  StatusCode: Integer;
  Body: String;
begin
  Result := False;
  if not ProbeHealth(BaseUrl + '/api/health', StatusCode, Body) then
    exit;
  if StatusCode <> 200 then
    exit;
  Result := (Pos('"status"', Body) > 0) and (Pos('"vectorSearch"', Body) > 0);
end;

procedure StopRunningMcpAdapter();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM {#McpExe} /FI "USERNAME eq ' + ExpandConstant('{username}') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StopRunningHost();
var
  Port, BaseUrl: String;
  Waited, ResultCode: Integer;
begin
  if not ReadHostPort(Port) then
    exit;

  BaseUrl := 'http://127.0.0.1:' + Port;
  if not IsOurHost(BaseUrl) then
    exit;

  HttpStatus('POST', BaseUrl + '/api/shutdown');

  Waited := 0;
  while Waited < StopTimeoutMs do
  begin
    Sleep(StopPollMs);
    Waited := Waited + StopPollMs;
    if not IsOurHost(BaseUrl) then
      exit;
  end;

  Exec(ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM {#AppExe} /FI "USERNAME eq ' + ExpandConstant('{username}') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function TargetDirTooLong(const TargetDir: String): Boolean;
begin
  Result := (Length(TargetDir) + 1 + MaxPayloadPathChars) > MaxPathBudget;
end;

function PathLengthOverflow(const TargetDir: String): Integer;
begin
  Result := (Length(TargetDir) + 1 + MaxPayloadPathChars) - MaxPathBudget;
end;

function PathLengthMessage(const TargetDir: String): String;
begin
  Result := 'This installation directory is too long by ' + IntToStr(PathLengthOverflow(TargetDir)) + ' character(s).' + #13#10#13#10
    + 'Windows limits full file paths to ' + IntToStr(MaxPathBudget) + ' characters, and Overmind''s bundled '
    + 'crew-kit content includes deeply nested files that push the total past that limit from here.' + #13#10#13#10
    + 'Please choose a shorter installation directory (for example, one closer to the drive root).';
end;

function UpgradePathLengthMessage(const TargetDir: String): String;
begin
  Result := 'This installation cannot be upgraded in place: its remembered install location is too long by '
    + IntToStr(PathLengthOverflow(TargetDir)) + ' character(s).' + #13#10#13#10
    + 'Windows limits full file paths to ' + IntToStr(MaxPathBudget) + ' characters, and Overmind''s bundled '
    + 'crew-kit content includes deeply nested files that push the total past that limit from here.' + #13#10#13#10
    + 'Please uninstall the existing Overmind installation, then reinstall into a shorter directory.';
end;

function EffectiveSilentTargetDir(): String;
var
  DirParam, PrevDir: String;
begin
  DirParam := ExpandConstant('{param:DIR|}');
  if DirParam <> '' then
  begin
    Result := DirParam;
    exit;
  end;

  PrevDir := GetPreviousAppDir();
  if PrevDir <> '' then
  begin
    Result := PrevDir;
    exit;
  end;

  Result := ExpandConstant('{autopf}\Overmind');
end;

function InitializeSetup(): Boolean;
var
  TargetDir: String;
begin
  Result := True;
  if not WizardSilent() then
    exit;

  TargetDir := EffectiveSilentTargetDir();

  if TargetDirTooLong(TargetDir) then
  begin
    if IsUpgradeInstall() then
      Log('Overmind: refusing silent upgrade - remembered install directory ''' + TargetDir + ''' is '
        + IntToStr(PathLengthOverflow(TargetDir)) + ' character(s) over the '
        + IntToStr(MaxPathBudget) + '-character Windows path budget; uninstall and reinstall into a shorter directory.')
    else
      Log('Overmind: refusing silent install into ''' + TargetDir + ''' - '
        + IntToStr(PathLengthOverflow(TargetDir)) + ' character(s) over the '
        + IntToStr(MaxPathBudget) + '-character Windows path budget; choose a shorter directory.');
    Result := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if TargetDirTooLong(WizardDirValue()) then
    begin
      MsgBox(PathLengthMessage(WizardDirValue()), mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = wpReady then
  begin
    if TargetDirTooLong(ExpandConstant('{app}')) then
    begin
      if IsUpgradeInstall() then
        MsgBox(UpgradePathLengthMessage(ExpandConstant('{app}')), mbError, MB_OK)
      else
        MsgBox(PathLengthMessage(ExpandConstant('{app}')), mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningHost();
  StopRunningMcpAdapter();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  McpPath: String;
begin
  if CurStep <> ssPostInstall then
    exit;
  if not WizardIsTaskSelected('mcppath') then
    exit;

  McpPath := ExpandConstant('{app}\mcp');
  if IsAdminInstallMode then
  begin
    if not PathContains(HKEY_LOCAL_MACHINE, EnvKeyMachine, McpPath) then
      AddPathSegment(HKEY_LOCAL_MACHINE, EnvKeyMachine, McpPath);
  end
  else
  begin
    if not PathContains(HKEY_CURRENT_USER, EnvKeyUser, McpPath) then
      AddPathSegment(HKEY_CURRENT_USER, EnvKeyUser, McpPath);
  end;
end;

function DeleteDataRequested(): Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    if CompareText(ParamStr(Index), '/DELETEDATA') = 0 then
    begin
      Result := True;
      exit;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Target: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    StopRunningHost();
    StopRunningMcpAdapter();
    RemovePathSegment(ExpandConstant('{app}\mcp'));
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    Target := DataDirectory();
    if not DirExists(Target) then
      exit;

    if UninstallSilent then
    begin
      if not DeleteDataRequested() then
        exit;
    end
    else
    begin
      if MsgBox('Remove Overmind data (database, settings, downloaded models) from'
        + #13#10 + Target + '?' + #13#10 + #13#10
        + 'Choose No to keep it for a future install.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES then
        exit;
    end;

    DelTree(Target, True, True, True);
  end;
end;
