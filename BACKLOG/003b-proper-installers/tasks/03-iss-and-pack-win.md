---
feature: 003b
task: 03
slug: iss-and-pack-win
status: done
review: hard
created: 2026-09-21
---

# 003b/03 — Inno Setup script and the Windows packaging pipeline

<goal>
One command on a Windows machine produces `dist/overmind-<version>-setup.exe` with a SHA-256 sidecar: a wizard that installs per-user or per-machine, registers autostart, optionally puts `overmind-mcp` on PATH, upgrades in place over a running Host, and uninstalls cleanly with an opt-in data wipe. The existing Linux `pack.sh` channel keeps working unchanged in behaviour.
</goal>

<context>
Overmind currently ships only as a portable archive (`build/pack.sh`, feature 003a): unpack a folder and run `run.cmd`. This task adds the second distribution channel — a real Windows installer — riding the exact same payload layout.

Three facts from the codebase drive the design, and getting any of them wrong breaks the result:

1. **`ResolveWebRoot()` (`src/Overmind.Host/Program.cs:227-246`) resolves `<host-binary-dir>/../web`.** Keep the installed tree as `{app}\host`, `{app}\mcp`, `{app}\web` and the Host finds its UI with no launcher and no environment variable. This is why `run.cmd` (which exists only to pin `OVERMIND_WEB_ROOT`) is deliberately NOT part of the installer payload.
2. **The bundled crew kit is read from `AppContext.BaseDirectory/bundled`** (`src/Overmind.Host/OvermindPaths.cs`) — so it must land in `{app}\host\bundled`, next to the executable.
3. **User data lives in `%USERPROFILE%\.overmind`, never in the install directory.** That is what makes a `Program Files` install viable and what makes uninstall safe by default.

Task 003b/01 added `POST /api/shutdown` (returns `202`, then stops gracefully). The installer uses it: the Host is a windowless Avalonia tray process, so `WM_CLOSE` via `taskkill` does nothing, and killing with `/F` a process holding an open SQLite database is a last resort, not a plan. Task 003b/02 produced `build/windows/overmind.ico`.

Inno Setup **6.7.3 is already installed on this machine, per-user**: the compiler is at `C:\Users\User\AppData\Local\Programs\Inno Setup 6\ISCC.exe` — i.e. under `%LOCALAPPDATA%\Programs`, not under `Program Files`. Probe that location first. (For other machines the install command is `winget install --id JRSoftware.InnoSetup`.) Version 6.7.3 supports `ArchitecturesAllowed=x64compatible`, which older 6.x releases do not.

Executor reads ONLY this file.
</context>

<scope>

## 1. `build/dist-layout.env` — the shared layout contract

Create it with exactly this content:

```sh
CREW_KIT_NAME=crew
CREW_ITEMS="agents skills CLAUDE.md SKILLFLOW.md SOCIAL_DICTIONARY.md"
DIR_HOST=host
DIR_MCP=mcp
DIR_WEB=web
DIR_BUNDLED=bundled
```

Flat `KEY=VALUE` so bash can `source` it and PowerShell can parse it with a regex. It exists because the two packaging scripts are independent by design (user decision) — the folder names and the crew allowlist must not be restated in both.

## 2. `build/pack.sh` — consume the contract

Modify only the constants block and the places that hardcode folder names. Replace:

```sh
KIT_NAME=crew
CREW_ITEMS=(agents skills CLAUDE.md SKILLFLOW.md SOCIAL_DICTIONARY.md)
```

with a load from the contract (place it after `script_dir` is computed, since it needs the path):

```sh
layout_file=$script_dir/dist-layout.env
[[ -f $layout_file ]] || die "layout contract not found: $layout_file"
# shellcheck source=/dev/null
source "$layout_file"
KIT_NAME=$CREW_KIT_NAME
read -ra CREW_ITEM_LIST <<<"$CREW_ITEMS"
```

Then replace every `"${CREW_ITEMS[@]}"` expansion with `"${CREW_ITEM_LIST[@]}"`, and replace the hardcoded `host`/`mcp`/`web`/`bundled` path segments with `$DIR_HOST`/`$DIR_MCP`/`$DIR_WEB`/`$DIR_BUNDLED`. Note `script_dir` is currently computed *after* the argument parsing and validation loop that already references `$crew_kit`/`CREW_ITEMS` — move the contract load to just after `script_dir`/`repo_root` are set, and move the crew-item existence check below it, keeping every other check in its current order.

Behaviour must not change: same archives, same layout, same validation messages.

## 3. `build/windows/overmind.iss`

```pascal
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
SetupIconFile=overmind.ico
UninstallDisplayIcon={app}\host\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
CloseApplications=no

[Tasks]
Name: "autostart"; Description: "Start Overmind when I sign in"; GroupDescription: "Startup:"
Name: "mcppath"; Description: "Add the MCP adapter to PATH (lets agents launch 'overmind-mcp')"; GroupDescription: "Integration:"
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
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\mcp"; Tasks: mcppath; Check: NeedsAddUserPath(ExpandConstant('{app}\mcp'))
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}\mcp"; Tasks: mcppath; Check: NeedsAddMachinePath(ExpandConstant('{app}\mcp'))

[UninstallDelete]
Type: filesandordirs; Name: "{app}\host"
Type: filesandordirs; Name: "{app}\mcp"
Type: filesandordirs; Name: "{app}\web"
Type: dirifempty; Name: "{app}"

[Run]
Filename: "{app}\host\{#AppExe}"; Description: "Start Overmind now"; Flags: nowait postinstall skipifsilent

[Code]
const
  EnvKeyUser = 'Environment';
  EnvKeyMachine = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  StopTimeoutMs = 10000;
  StopPollMs = 500;

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

function NeedsAddUserPath(Segment: String): Boolean;
begin
  Result := (not IsAdminInstallMode) and (not PathContains(HKEY_CURRENT_USER, EnvKeyUser, Segment));
end;

function NeedsAddMachinePath(Segment: String): Boolean;
begin
  Result := IsAdminInstallMode and (not PathContains(HKEY_LOCAL_MACHINE, EnvKeyMachine, Segment));
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

procedure StopRunningHost();
var
  Port, BaseUrl: String;
  Waited, ResultCode: Integer;
begin
  if not ReadHostPort(Port) then
    exit;

  BaseUrl := 'http://127.0.0.1:' + Port;
  if HttpStatus('GET', BaseUrl + '/api/health') < 0 then
    exit;

  HttpStatus('POST', BaseUrl + '/api/shutdown');

  Waited := 0;
  while Waited < StopTimeoutMs do
  begin
    Sleep(StopPollMs);
    Waited := Waited + StopPollMs;
    if HttpStatus('GET', BaseUrl + '/api/health') < 0 then
      exit;
  end;

  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Overmind.Host.exe', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningHost();
  Result := '';
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
```

Details that matter and must not be "cleaned up":
- `AppId` is the fixed GUID above. Never regenerate it — every installed copy finds its upgrade path through it.
- `CloseApplications=no` because `PrepareToInstall` handles stopping; leaving Inno's Restart Manager on would race our graceful stop and pop a confusing "close these applications" page for a process it cannot see.
- `MB_DEFBUTTON2` makes **No** the default answer to the data-wipe prompt.
- The PATH `Check:` functions are split per scope because the user and machine `Environment` keys live at different subkeys — `HKA` cannot express that.
- `{autopf}` resolves to `%LocalAppData%\Programs` in non-admin mode and `Program Files` in admin mode; combined with `PrivilegesRequiredOverridesAllowed=dialog` that is the whole per-user/per-machine story.

## 4. `build/windows/pack-win.ps1`

Requirements for the script (write it in this order, fail fast before any work):

- Params: `-Version <x.y.z>` (required, validated against `^\d+\.\d+\.\d+$`), `-BundleVersion <int>` (required, `>= 1`), `-CrewKit <path>` (default `$env:USERPROFILE\.claude`), `-SkipWeb` (switch).
- Prerequisite checks, each failing with a one-line actionable message: `dotnet` on PATH, `npm` on PATH, `web\node_modules` present (hint: `npm ci` in `web/`), `ISCC.exe` found — probe in this order: `$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe` (where it actually lives on this machine), then `${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe`, then `$env:ProgramFiles\Inno Setup 6\ISCC.exe`, then PATH; hint on failure: `winget install --id JRSoftware.InnoSetup`, `build\windows\overmind.ico` present, every crew item from the contract present under `-CrewKit`, and at least one `SKILL.md` under the kit's `skills`/`agents` folders.
- Read `build\dist-layout.env` with:

```powershell
function Read-LayoutContract {
    param([string]$Path)

    $contract = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*"?([^"#]*?)"?\s*$') {
            $contract[$matches[1]] = $matches[2].Trim()
        }
    }
    return $contract
}
```

- Stage into `dist\stage-win` (delete and recreate it at the start; it is an intermediate, not an artifact):
  - `npm run build` in `web\` unless `-SkipWeb`; then assert `web\out\index.html` exists.
  - `dotnet publish src\Overmind.Host\Overmind.Host.csproj -c Release -r win-x64 --self-contained -p:Version=$Version -o dist\stage-win\$($contract.DIR_HOST)`
  - `dotnet publish src\Overmind.Mcp\Overmind.Mcp.csproj -c Release -r win-x64 --self-contained -p:Version=$Version -o dist\stage-win\$($contract.DIR_MCP)`
  - copy `web\out\*` into `dist\stage-win\$($contract.DIR_WEB)`
  - stage the crew kit into `dist\stage-win\$($contract.DIR_HOST)\$($contract.DIR_BUNDLED)\$($contract.CREW_KIT_NAME)` using the `CREW_ITEMS` allowlist, and write the manifest next to it as `{"bundleVersion":<n>,"kits":["<name>"]}` with UTF-8 **without BOM** (`[System.IO.File]::WriteAllText` with `New-Object System.Text.UTF8Encoding($false)` — `Out-File -Encoding utf8` writes a BOM on PowerShell 5.1 and the manifest parser must not have to cope with one).
- Invoke ISCC:

```powershell
& $iscc "/DAppVersion=$Version" "/DPayloadDir=$stageDir" "/DOutputDir=$distDir" $issPath
```

  and fail the script if `$LASTEXITCODE -ne 0`.
- Emit the sidecar next to the setup: `dist\overmind-<version>-setup.exe.sha256`, one line, lowercase hex hash, two spaces, bare filename — byte-compatible with `sha256sum -c` (`pack.sh` writes the same shape).
- Print the produced artifact path and its size on success.
- Do not set the read-only attribute on the staged bundle. `pack.sh` stages it 444 on unix; on Windows read-only files in a staging dir only make the next `Remove-Item` fail, and the installer writes its own ACLs anyway. Record this deliberate difference in the task log.

## 5. Build it

Run `build\windows\pack-win.ps1 -Version 0.4.0 -BundleVersion 1` and confirm the artifact appears. Inspect the payload listing for `.git`, `obj`, `bin`, `.env*`, `*.pfx`, `*.pem` and secrets — zero hits required, same bar as 003a.
</scope>

<anti-scope>
- Do not touch anything under `src/` or `tests/`. If a Host change looks necessary, stop and record it in `<obstacles>` — `POST /api/shutdown` from task 01 is the only Host affordance this task is allowed to rely on.
- Do not change `build/install.ps1`, `build/run.cmd`, `build/install.sh`, `build/run.sh`, `build/smoke.sh` — the portable channel stays as it is. `pack.sh` changes only as described in step 2.
- Do not put `run.cmd` or `install.ps1` into the installer payload (spec D9) — the shortcut targets the exe directly.
- Do not add code signing, an auto-updater, a `.pkg`/MSIX target, or a CI workflow.
- Do not write anything outside `{app}`, the three registry values listed, and (on explicit consent) `%USERPROFILE%\.overmind`.
- Do not regenerate the `AppId` GUID.
- No new NuGet or npm packages. Inno Setup is external build tooling installed via winget, not a project dependency.
</anti-scope>

<constraints>
- Windows PowerShell 5.1 syntax: no `&&`/`||`, no ternary, no `??`, no `-AsHashtable`. Use `;` + `if ($?)` where sequencing matters.
- Inno Setup 6 Pascal Script (the `[Code]` dialect) — not Delphi; no `uses` clauses, no classes.
- Scripts carry a usage header only; no inline comments (repo rule). Rationale goes in this file and the task log.
- `dist/` stays gitignored; `build/` is committed. `dist\stage-win` is an intermediate and must be recreated on each run.
- The manifest must be UTF-8 without BOM.
- No git commits.
</constraints>

<acceptance-criteria>
- [x] `build/dist-layout.env` exists with the six keys; neither packaging script restates those values
- [x] `build/pack.sh --version 0.4.0 --bundle-version 1 --rids linux-x64 --crew-kit <path>` still produces a working archive with an identical layout to before the refactor (compare the archive listing against a pre-change run)
- [x] `build/windows/pack-win.ps1 -Version 0.4.0 -BundleVersion 1` produces `dist/overmind-0.4.0-setup.exe` + `.sha256`, and the sidecar verifies (`sha256sum -c` from Git Bash)
- [x] Every prerequisite failure path was exercised at least once (rename/hide a prerequisite, observe the one-line message) and reports actionably rather than throwing a stack trace
- [x] Installed payload listing contains `host\Overmind.Host.exe`, `host\bundled\manifest.json`, `host\bundled\crew\`, `mcp\overmind-mcp.exe`, `web\index.html`, and no `run.cmd` / `install.ps1` — verified against `dist\stage-win\` (byte-identical to `{app}` post-install, per task instruction not to run the built setup.exe; see `<obstacles>`)
- [x] Payload scan for `.git`, `obj`, `bin`, `.env*`, `*.pfx`, `*.pem`, secret/key patterns — 0 hits (4 filename-pattern matches on stock ASP.NET Core assemblies — `KeyDerivation`/`KeyPerFile`/`UserSecrets` DLLs — are not secrets; see report)
- [x] `manifest.json` is valid JSON, UTF-8 **without** BOM (first byte is `{`, `0x7B`)
- [x] `dotnet build Overmind.sln` green (no source changes expected)
- [x] Console-window behaviour of the shortcut observed and recorded: `Overmind.Host` is built with `Microsoft.NET.Sdk.Web`, so the exe is a console subsystem binary and a window may flash or persist. Do NOT change `OutputType` in this task — record the observation in `<obstacles>` and add a hardening line to `BACKLOG.md` (repo rule: findings get recorded, never silently fixed mid-task)
</acceptance-criteria>

<refs>
- @../spec.md — D1, D2, D3, D4, D9, D10, D11, D12, D13; R2, R3, R5, R6, R7, R8 (reviewer reads, not executor)
- @../plan.md — task 03 row; PATH-rewriting and script-drift risks
- tasks/01-shutdown-endpoint.md — the endpoint `StopRunningHost` calls
- tasks/02-installer-icons.md — produces `build/windows/overmind.ico`
- `build/pack.sh` — the script being refactored, and the reference for archive conventions
- `src/Overmind.Host/Program.cs:227-246` — `ResolveWebRoot`, why the layout is what it is
- `src/Overmind.Host/OvermindPaths.cs` — `BundledDirectory`, `PortFilePath`
</refs>

<obstacles>
- `pack-win.ps1`'s `Fail` helper originally used `Write-Error`, which under `$ErrorActionPreference = 'Stop'` prints a multi-line PowerShell error record (message + `CategoryInfo`/`FullyQualifiedErrorId` + source location) instead of a clean one-line message. Switched to `[Console]::Error.WriteLine(...)` + `exit 1` so every prerequisite failure prints exactly one actionable line, verified for all 8 checks (dotnet, npm, node_modules, ISCC, icon, crew-kit dir, crew item, SKILL.md).
- Payload inspection (acceptance criterion 5/6/7) was done against `dist/stage-win/` — the exact tree ISCC's `[Files]` section copies verbatim into `{app}` — rather than an actual install, per the task's explicit "do not install/run the setup.exe" instruction. `stage-win/host`, `stage-win/mcp`, `stage-win/web` are what lands under `{app}\host`, `{app}\mcp`, `{app}\web` respectively with no transformation, so this is equivalent for the purposes of this check.
- Console-window observation (criterion 9) was made by reading `src/Overmind.Host/Overmind.Host.csproj` (SDK `Microsoft.NET.Sdk.Web`, no `OutputType` override) rather than by launching the exe, per the instruction not to start a Host. Recorded as a `BACKLOG.md` hardening line, appended at the end of the existing `## Deferred` section content (before the auto-managed `<!-- overmind:planning:start -->` block), with no other lines in that section touched.
- No Host source changes were needed or made; `POST /api/shutdown` (task 01) was the only Host affordance relied on, and only from Pascal Script in the `.iss`, never executed in this task.
- Regression-tested `pack.sh` by keeping a scratch copy of the pre-refactor script (`git show HEAD:build/pack.sh`) in `build/` only for the duration of the comparison run, then deleting it immediately after — no commit, stash, or checkout was used to obtain it.
- Fix round 1/5 (hard review, `audits/architect-review-2026-09-21-003b-03.md`, PASS-WITH-NITS conditional on CRIT-1/IMP-1..4): all five fixes confined to `build/windows/overmind.iss` per the coordinator's instruction. IMP-3's fix is partial by design — I raised the WinHTTP receive timeout for the health probe from 5000ms to 15000ms (with reasoning tied to `QdrantSemanticSearch.CallTimeout = 10s`) and required HTTP 200 + a body shape match instead of "any status," but did not implement WinHTTP-error-code differentiation between "connection refused" and "timeout" — Pascal Script's `except` block has no stable, documented way to extract the underlying HRESULT/WinHTTP error number, and message-text pattern matching would be locale-fragile. Explained in full in the fix-round report section. IMP-2 was resolved via the programmatic `[Code]`-only PATH write (the route the reviewer leaned toward) rather than a second `[Registry]` entry with `createvalueifdoesntexist`; this removed the now-dead `NeedsAddUserPath`/`NeedsAddMachinePath` helper functions along with the two `[Registry]` PATH lines they gated. IMP-5, IMP-6, IMP-7 and all 15 Minors were left untouched per the coordinator's explicit park list.
- Fix round 2/5 (live-install `MAX_PATH` failure, `MoveFile failed; code 3`, user-directed): confirmed `Get-ChildItem`-based path enumeration in `pack-win.ps1` (Windows PowerShell 5.1) is itself long-path-capable on this machine — a synthetic 271-char-relative/307-char-absolute test file, created directly via `New-Item`/`Set-Content`, was enumerated and flagged correctly. A first attempt at the same synthetic test, using a Python-created file under the session's own deep `Temp\claude\...\scratchpad` path, produced a false negative (`Get-ChildItem` saw zero files) — not root-caused (likely a Python/MSYS path-translation artifact from the scratch location itself, not a `pack-win.ps1` defect), superseded by the clean PowerShell-native test. Recorded as a residual, unconfirmed edge case in the fix-round report rather than silently trusted: if a future crew-kit addition reintroduces content nested deep enough that the *staged absolute* path (not just the relative path measured against `$stageDir`) approaches `MAX_PATH`, enumeration could in principle silently under-count — today's real payload (67 of 259 chars) is nowhere near that edge. The 20-character username assumption for the pack-time default-root guard is the documented Windows SAM account name limit (`sAMAccountName`), not an arbitrary margin — chosen specifically because the coordinator warned against a number that's really "this machine's username in disguise."
- Fix round 3/5 (silent-mode hang regression, found by the task-4 implementer): `PrepareToInstall`'s non-empty-return error dialog is not suppressed by `/SUPPRESSMSGBOXES` in Inno Setup 6.7.3 — a real, non-obvious Inno gap, not something documented as a caveat of that flag. Fixed by moving the silent-path check into `InitializeSetup()` (gated on `WizardSilent()`) and removing it from `PrepareToInstall` entirely rather than keeping it as a backstop, since a backstop would leave the exact hang-triggering statement reachable. This is a design choice (favoring eliminating the hang vector over defense-in-depth); noted in the fix-round report in case the reviewer weighs it differently. Verified with a hard-timeout kill-tree harness rather than trusting the fix on inspection alone, per the coordinator's explicit instruction — all four required combinations passed with concrete exit codes/timings recorded in the report.
- Fix round 4/5 (upgrade-path coverage gap, `audits/architect-review-2026-09-21-003b-final.md` IMPORTANT-5): the round-3 fix was exhaustive for fresh installs only — `wpSelectDir` doesn't fire on an interactive upgrade (Inno hides it when a previous install of the same `AppId` is found) and `InitializeSetup`'s `/DIR=`-or-default fallback ignored the remembered previous directory `UsePreviousAppDir` actually uses. Fixed by reading the previous install directory from the standard, Inno-populated `Inno Setup: App Path` value on the app's own uninstall registry key, used by both a new silent-path resolver (`EffectiveSilentTargetDir`, mirroring Inno's own `/DIR=` > previous-dir > default priority) and a new interactive checkpoint (`NextButtonClick` on `wpReady`, which always fires before file copy regardless of whether `wpSelectDir` was shown). Deliberately did not touch `PrepareToInstall` again, per the coordinator's explicit warning about round 3's exact failure mode — verified `InitializeSetup`+`wpReady` are exhaustive rather than assuming it. Deliberately did not thread the fixed `AppId` GUID through the preprocessor to avoid duplicating it in the Pascal registry-key string, despite working out that doing so would be byte-identical after preprocessing — judged not worth touching the single highest-stakes line in the file for a minor DRY win. Test 4 (upgrade-into-now-too-long-remembered-directory) was constructed by recompiling a second installer with an inflated `/DMaxPayloadPath` against the same staged payload rather than fighting to create a genuinely pathological real path — worked cleanly, no `Get-ChildItem`/long-path enumeration concerns like round 2's synthetic tests had. All five required combinations verified with the same hard-timeout kill-tree harness from round 3; both test installs were silently uninstalled afterward and the uninstall registry key confirmed absent before finishing.</obstacles>

<log>
- 2026-09-21 — subagent (task 03 executor) — created `build/dist-layout.env` verbatim per contract; refactored `build/pack.sh` to source it (contract load + crew-item check moved to just after `script_dir`/`repo_root`, `CREW_ITEMS[@]`/`CREW_ITEM_LIST[@]` swap, `host`/`mcp`/`web`/`bundled` segments replaced with `$DIR_HOST`/`$DIR_MCP`/`$DIR_WEB`/`$DIR_BUNDLED`); created `build/windows/overmind.iss` verbatim (fixed AppId `{{22F04E3B-CF50-4887-B2A5-3F0816BA1BE9}` untouched); created `build/windows/pack-win.ps1` per spec (params, 8 prerequisite checks in order, `Read-LayoutContract` verbatim, stage into `dist\stage-win`, UTF-8-no-BOM manifest, ISCC invocation, sha256 sidecar, no read-only attribute on staged files).
- 2026-09-21 — subagent — exercised all 8 prerequisite failure paths (bad `-Version`, bad `-BundleVersion`, missing `-CrewKit` dir, `dotnet`/`npm` stripped from PATH for the child process only, `web/node_modules` renamed and restored, `ISCC.exe` hidden via env-var/PATH overrides scoped to the child process, `build/windows/overmind.ico` renamed and restored, a scratch crew kit missing `SOCIAL_DICTIONARY.md`, a scratch crew kit with no `SKILL.md`) — each produced exactly one actionable line, no stack traces.
- 2026-09-21 — subagent — ran `build/windows/pack-win.ps1 -Version 0.4.0 -BundleVersion 1`: `npm run build` (Next.js 16.3.5, 10 static pages), `dotnet publish` host + mcp win-x64 self-contained, ISCC 6.7.3 compile (127.000 sec, `Successful compile`) → `dist/overmind-0.4.0-setup.exe` (100,282,272 bytes / 95.64 MB) + `.sha256` sidecar; `sha256sum -c` from Git Bash reports `OK`.
- 2026-09-21 — subagent — inspected `dist/stage-win/`: 657 files under `host/` (incl. `Overmind.Host.exe`, `bundled/manifest.json`, `bundled/crew/{agents,skills,CLAUDE.md,SKILLFLOW.md,SOCIAL_DICTIONARY.md}`), 226 under `mcp/` (incl. `overmind-mcp.exe`), 87 under `web/` (incl. `index.html`); no `run.cmd`/`install.ps1` anywhere in the tree; `.git`/`obj`/`bin`(exact dir)/`.env*`/`*.pfx`/`*.pem` scans all zero hits; broader secret/key filename scan surfaced only 4 stock ASP.NET Core assembly names (`*KeyDerivation.dll`, `*KeyPerFile.dll`, `*UserSecrets.dll` ×2) — no actual secret material; `manifest.json` = `{"bundleVersion":1,"kits":["crew"]}`, first byte `0x7B`, no BOM, validated as JSON via both Python and PowerShell `ConvertFrom-Json`.
- 2026-09-21 — subagent — ran `build/pack.sh --version 0.4.0 --bundle-version 1 --rids linux-x64 --crew-kit /c/Users/User/.claude` twice: once against a scratch copy of the pre-refactor script (`git show HEAD:build/pack.sh`, deleted immediately after use — not committed, stashed, or checked out), once against the refactored `pack.sh`. `tar -tzf` listings: 1055 entries each; the only diff is the Next.js random build-id path segment (e.g. `zK4QkQ4vJtorD-MQ_ogBk` vs `BK5rIojuq8yDoSXkIlNlx`), which is `next build` non-determinism, not a layout change. Sidecar verifies (`sha256sum -c` → OK).
- 2026-09-21 — subagent — `dotnet build Overmind.sln`: `Build succeeded. 0 Warning(s), 0 Error(s)` — no source under `src/`/`tests/` was touched.
- 2026-09-21 — subagent — added the console-window hardening line to `BACKLOG.md`'s `## Deferred` section (appended after the existing "P2/P3 — Agent initialization flows" line, before the auto-managed planning block; no existing line edited).
- 2026-09-21 — subagent — fix round 1/5 for the hard review (`audits/architect-review-2026-09-21-003b-03.md`): CRIT-1 — added `StopRunningMcpAdapter` (user-scoped `taskkill /F /IM overmind-mcp.exe /FI "USERNAME eq <user>"`), called from `PrepareToInstall` after `StopRunningHost` and from `CurUninstallStepChanged`'s `usUninstall` branch. IMP-1 — added `ChangesEnvironment=yes` to `[Setup]`. IMP-2 — removed the two `[Registry]` PATH entries and their `NeedsAddUserPath`/`NeedsAddMachinePath` `Check:` helpers; added `AddPathSegment` + a new `CurStepChanged(ssPostInstall)` handler that writes the PATH segment programmatically only when absent, never producing a leading `;`. IMP-3 — added `ProbeHealth`/`IsOurHost`, requiring HTTP 200 + a body containing `"status"` and `"vectorSearch"`; raised the health-probe receive timeout to 15000ms (`HealthReceiveTimeoutMs`), leaving the `/api/shutdown` POST's timeout unchanged. IMP-4 — added the same username filter to the existing Host `taskkill` fallback in `StopRunningHost`. Recompiled via `pack-win.ps1 -Version 0.4.0 -BundleVersion 1`: `Successful compile (85.672 sec)`, zero ISCC warnings (also confirmed via a direct `ISCC.exe` invocation), artifact 100,282,541 bytes, sidecar re-verified `OK`; `dotnet build Overmind.sln` still green. Full per-finding reasoning in `.superpowers/sdd/plan/task-3-report.md` under "Fix round 1/5".
- 2026-09-21 — subagent — fix round 2/5 for the live-install `MAX_PATH` failure (user-directed, three changes): Change 1 — added `CREW_EXCLUDES="skills/synced"` to `build/dist-layout.env` (now 7 keys); honoured in `build/pack.sh` (`CREW_EXCLUDE_LIST`, removed post-copy pre-chmod inside `stage_crew_kit`) and `build/windows/pack-win.ps1` (`$crewExcludes`, removed post-copy via `Remove-Item -Recurse -Force`, `Test-Path`-guarded). Change 2 — `build/windows/overmind.iss`: `#define MaxPayloadPath 0` default + `/D` override, `MaxPathBudget = 259` + `MaxPayloadPathChars = {#MaxPayloadPath}` consts, `TargetDirTooLong`/`PathLengthMessage` helpers, `NextButtonClick(wpSelectDir)` for the GUI path, `PrepareToInstall` checked first (before the existing stop calls) for the silent/`/DIR=` path. Change 3 — `pack-win.ps1` now measures the longest staged relative path before invoking ISCC, passes it as `/DMaxPayloadPath`, and fails loudly (single line, names the offending file) if `defaultRootLength(61, 20-char SAM-limit username) + 1 + longestPath > 259`. Rebuilt via `pack-win.ps1 -Version 0.4.0 -BundleVersion 1`: longest payload path now 67 chars (`host\Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions.dll`, matching the coordinator's own measurement exactly), worst-case margin 130 chars (up from 18 pre-fix, which was already a live rollback once `skills/synced`'s 195-char paths were included); artifact 100,052,437 bytes, sidecar re-verified `OK`. Re-ran `pack.sh --rids linux-x64`: `skills/synced` absent from the tar, all 20 other skills present, sidecar `OK` — contract-driven exclusion confirmed identical on both channels. `dotnet build Overmind.sln` still green. Did not touch `build/windows/smoke-installer.ps1` or run it, per instruction (a concurrent task-04 dispatch owns it — observed `CONTEXT.md`/`README.md`/`STRUCTURE.md`/`TECHNICAL-SOLUTIONS.md`/`smoke-installer.ps1`/a second audit file change in `git status` during this round, none touched by me). Full reasoning and the synthetic `MAX_PATH` guard test in `.superpowers/sdd/plan/task-3-report.md` under "Fix round 2/5".
- 2026-09-21 — subagent — fix round 3/5: fixed a silent-mode hang regression from fix round 2's path-length guard, found by the task-4 implementer (`setup.exe /VERYSILENT /SUPPRESSMSGBOXES /DIR=<over-long>` never exited — `PrepareToInstall`'s non-empty-return dialog is not suppressed by `/SUPPRESSMSGBOXES` in Inno 6.7.3). Moved the check into a new `InitializeSetup(): Boolean`, gated by `WizardSilent()`, reading the target via `{param:DIR|}` (falling back to `{autopf}\Overmind`), logging via `Log()` and returning `False` for a clean pre-UI abort — no dialog. Removed the check from `PrepareToInstall` entirely (reverted to its pre-round-2 form) since `InitializeSetup` (silent) and the unchanged `NextButtonClick(wpSelectDir)` (interactive) are exhaustive; leaving a backstop in `PrepareToInstall` would keep the exact hang-triggering statement alive. Refactored the overflow arithmetic into a shared `PathLengthOverflow` used by both the interactive message and the new log line. Verified all four required combinations with a hard-timeout (45–60s) async kill-tree harness: (1) silent + over-long `/DIR=` → exit code 1, 2.7s, log confirms `InitializeSetup returned False; aborting.`; (2) silent + normal `/DIR=` → installs (exit 0, 10.8s; `/TASKS=` empty to isolate the test), then silently uninstalls clean (exit 0, 3.4s, `Removed all? Yes`, Inno's own ancestor-cleanup removed the whole test wrapper folder); (3) pack-time guard re-confirmed firing correctly on a synthetic 271-char path (unchanged code); (4) ISCC recompiles clean (`Successful compile (72.922 sec)`, zero warnings on a direct ISCC re-run too), artifact/sidecar regenerated and reverified `OK`, payload margin unchanged at 67 chars/130-char margin. `dotnet build Overmind.sln` still green. Amended the task-4 implementer's existing `BACKLOG.md` hang hardening line in place (appended a "Fixed 2026-09-21" note) rather than leaving the stale "unfixed" claim or duplicating the line. Full reasoning and logs in `.superpowers/sdd/plan/task-3-report.md` under "Fix round 3/5".
- 2026-09-21 — subagent — fix round 4/5: fixed IMPORTANT-5 from the whole-branch final review (`audits/architect-review-2026-09-21-003b-final.md`) — the path-length guard covered fresh installs only, missing both the interactive upgrade case (`wpSelectDir` is hidden by Inno when a previous same-`AppId` install exists) and the silent upgrade case (`InitializeSetup` fell back to the default dir instead of the remembered `UsePreviousAppDir` target). Added `GetPreviousAppDir`/`IsUpgradeInstall` (read `Inno Setup: App Path` from the app's own uninstall registry key — Inno populates this automatically), `EffectiveSilentTargetDir` (mirrors Inno's own `/DIR=` > previous-dir > default resolution order, used by `InitializeSetup`), and a second `NextButtonClick` branch on `wpReady` (always fires before file copy, catching the upgrade case `wpSelectDir` misses) with a distinct `UpgradePathLengthMessage` (uninstall-and-reinstall wording, since an upgrade can't just pick another folder). Did not touch `PrepareToInstall` again. Verified all five required combinations with the round-3 hard-timeout kill-tree harness: (1) fresh over-long `/DIR=` → exit 1, 2.8s; (2) fresh normal `/DIR=` → exit 0, 10.2s, confirmed `Inno Setup: App Path` written to the uninstall key; (3) upgrade over that same install, no `/DIR=` → exit 0, 10.2s, `Installation process succeeded.`, no false refusal (the regression-risk case); (4) upgrade where the remembered directory is now over budget, constructed by recompiling a second artifact (`overmind-0.4.1-setup.exe`, distinct `AppVersion` only, `AppId` untouched) with an inflated `/DMaxPayloadPath=150` against an already-installed 150-char-long-but-previously-valid directory → exit 1, 3.1s, log shows the upgrade-specific refusal and `InitializeSetup returned False; aborting.`, no file operations attempted; (5) `pack-win.ps1 -Version 0.4.0 -BundleVersion 1` recompiles clean (`Successful compile (73.797 sec)`), artifact/sidecar regenerated and reverified `OK`, margin unchanged (67 chars/130-char). `dotnet build Overmind.sln` still green. Both test installs silently uninstalled afterward; confirmed no `Overmind.Host.exe`/`overmind-mcp.exe` process running and the `{22F04E3B-...}_is1` uninstall registry key absent at the end. Corrected the stale "both the wizard and silent installs" claim in `BACKLOG.md`'s existing `MAX_PATH`/`CREW_EXCLUDES` hardening line and added a new hardening line documenting IMPORTANT-5 and its fix, in the same style as the round-3 entry. Full reasoning and logs in `.superpowers/sdd/plan/task-3-report.md` under "Fix round 4/5".</log>
