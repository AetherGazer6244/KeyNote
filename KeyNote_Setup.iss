; 版本以 csproj 为唯一真源,build.ps1 经 /DAppVer 注入;直接编译本脚本时回退 1.0.0
#ifndef AppVer
  #define AppVer "1.0.0"
#endif
; 打包源目录:build.ps1 传入干净 staging(build\setup_stage),直接编译时回退 dist
#ifndef SrcDir
  #define SrcDir "dist"
#endif

[Setup]
AppName=KeyNote
AppVersion={#AppVer}
AppPublisher=KeyNote Project
; 显式 AppId:升级链依赖它稳定(隐式取 AppName,改名即断链)
AppId=KeyNote
DefaultDirName={localappdata}\KeyNote
; auto:首装显示目录页,升级(同 AppId)隐藏并沿用旧目录
DisableDirPage=auto
UsePreviousAppDir=yes
MinVersion=10.0.19041
DefaultGroupName=KeyNote
OutputDir=installer
OutputBaseFilename=KeyNote_Setup
Compression=lzma2/max
SolidCompression=yes
UninstallDisplayIcon={app}\KeyNote.App.exe
SetupIconFile=icon.ico
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; 静默安装时自动关掉正在运行的 KeyNote(用 Restart Manager)并替换文件,装完自动重启
; 应用内「下载并安装」就走这条路:拉起本安装器 + /VERYSILENT
CloseApplications=yes
RestartApplications=yes
; 不设 AppMutex:它会让 Setup 弹窗等用户手动关闭,静默升级会卡住

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce
Name: "autostart"; Description: "开机自动启动 KeyNote"; GroupDescription: "启动选项："; Flags: checkedonce

[Files]
; 发布产物(含 WindowsAppSDK 运行时);双保险:排除一切用户数据
Source: "{#SrcDir}\*"; DestDir: "{app}"; Excludes: "KeyNote_Setup.exe,keynote.db,keynote.db-wal,keynote.db-shm,config.json,portable.flag,backups\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\KeyNote"; Filename: "{app}\KeyNote.App.exe"
; 用 {userdesktop} 而非 {commondesktop}:后者是 C:\Users\Public\Desktop,
; 非管理员安装(PermissionsRequired=lowest)写不进去,快捷方式会静默失败
Name: "{userdesktop}\KeyNote"; Filename: "{app}\KeyNote.App.exe"; Tasks: desktopicon
Name: "{group}\说明 (README)"; Filename: "{app}\README.md"
Name: "{group}\卸载 KeyNote"; Filename: "{uninstallexe}"

[Registry]
; 开机自启（也可在应用内设置面板开关）
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KeyNote"; ValueData: """{app}\KeyNote.App.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\KeyNote.App.exe"; Description: "启动 KeyNote"; Flags: postinstall nowait skipifsilent shellexec

[Code]
// 目录可写性校验:PrivilegesRequired=lowest 下写不进系统目录(如 Program Files),
// 提前在目录页拦截给中文提示,而不是等复制文件阶段报 Access Denied
function IsDirWritable(const ADir: String): Boolean;
var
  Parent: String;
  TestFile: String;
begin
  Result := False;
  if ADir = '' then Exit;
  if not DirExists(ADir) then
  begin
    // 目录还不存在:向上找最近一级已存在祖先来试写
    Parent := ExtractFileDir(ADir);
    if (Parent = '') or (Parent = ADir) then
    begin
      Result := True;   // 已到盘符根,交由安装阶段处理
      Exit;
    end;
    Result := IsDirWritable(Parent);
    Exit;
  end;
  // 目录已存在:直接试写一个临时文件
  TestFile := AddBackslash(ADir) + 'keynote_wtest.tmp';
  if SaveStringToFile('t', TestFile, False) then
  begin
    Result := True;
    DeleteFile(TestFile);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not IsDirWritable(WizardDirValue) then
    begin
      MsgBox('所选目录不可写(通常需要管理员权限)。' + #13#10#13#10 +
        '请改选一个当前用户可写的目录,例如 D:\Apps\KeyNote。' + #13#10 +
        '(按键记录等数据不受影响,始终保存在 %LOCALAPPDATA%\KeyNote)',
        mbError, MB_OK);
      Result := False;
    end;
  end;
end;
