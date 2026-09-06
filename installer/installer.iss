#define MyAppName "HasamiBoard"
#define MyAppPublisher "HasamiBoard"
#define MyAppExeName "HasamiBoard.exe"
#define MyAppVersion Trim(FileRead(FileOpen(SourcePath + "..\VERSION")))

[Setup]
AppId={{B3B9B2F0-3E7E-4C2B-9B0E-6C7B7A6E9C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userappdata}\Programs\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
UninstallDisplayName={#MyAppName} ({username}@{code:GetUserDomain})
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=HasamiBoardSetup
OutputDir=..\dist
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern zircon
WizardImageFile=images\wizard_large.bmp
WizardSmallImageFile=images\wizard_small.bmp
SetupIconFile=images\app.ico
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
ArchitecturesAllowed=x64compatible
;ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; 発行済みビルド成果物一式を配置する
; 事前に `dotnet publish -c Release -r win-x64 --self-contained false -o publish` 等でビルドしておくこと
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Registry]
; アンインストール時にスタートアップ自動起動レジストリが残っていれば削除する
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "HasamiBoard"; Flags: uninsdeletevalue dontcreatekey

[Code]
function GetUserDomain(Param: string): string;
begin
  Result := GetEnv('USERDOMAIN');
  if Result = '' then
    Result := 'unknown';
end;
