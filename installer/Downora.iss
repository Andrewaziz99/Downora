; Downora installer. Build it with installer\build-installer.ps1, which publishes the app, puts the tools next to it and runs this
; script. The defines below can be overridden on the ISCC command line (/DAppVersion=1.2.3 ...).

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef OutputBaseName
  #define OutputBaseName "Downora-Setup-" + AppVersion
#endif

#define AppName "Downora"
#define AppExe "Downora.exe"
#define RepoUrl "https://github.com/Andrewaziz99/Downora"

[Setup]
; Never change AppId: it is how Windows (and this installer) recognise an installed Downora when upgrading or uninstalling.
AppId={{EB998A37-C681-45E6-A010-30D5EF5D9E12}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Andrew Aziz
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}

; Installs for the current user (no administrator prompt) into %LocalAppData%\Programs; the dialog lets an administrator choose
; "for all users" (Program Files) instead.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041

#ifndef TestInstall
; A running Downora is closed first (it holds a named mutex while it runs).
AppMutex=Downora.SingleInstance.v1
CloseApplications=yes
RestartApplications=no
#endif

OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseName}
SetupIconFile=..\src\Downora.App\Assets\downora.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
#ifdef TestInstall
; Test builds (/DTestInstall) skip the running-app check and compress fast; never ship one.
Compression=lzma2/fast
#else
Compression=lzma2/ultra64
#endif
SolidCompression=yes
WizardStyle=modern
#ifexist "..\LICENSE"
LicenseFile=..\LICENSE
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[CustomMessages]
english.DesktopIcon=Create a desktop shortcut
english.LaunchApp=Start Downora
english.DeleteData=Also delete your Downora settings and download history?%n%nYour downloaded files are never deleted. Choose No to keep your settings and history for a later reinstall.
arabic.DesktopIcon=إنشاء اختصار على سطح المكتب
arabic.LaunchApp=تشغيل Downora
arabic.DeleteData=هل تريد أيضًا حذف إعدادات Downora وسجل التنزيلات؟%n%nلا تُحذف ملفاتك التي تم تنزيلها أبدًا. اختر «لا» للاحتفاظ بالإعدادات والسجل عند إعادة التثبيت لاحقًا.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The published app, with yt-dlp, FFmpeg, FFprobe and Deno in its tools folder. Downora copies those into its data folder the
; first time it starts, so it works offline and never needs to download anything just to run.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Re-creatable data goes with the program. Settings and history (downora.db) are only removed if the user says so (see [Code]).
Type: filesandordirs; Name: "{localappdata}\Downora\tools"
Type: filesandordirs; Name: "{localappdata}\Downora\logs"
Type: filesandordirs; Name: "{localappdata}\Downora\thumbnails"

[Code]
procedure CurrentUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { "Start Downora when I sign in" lives in the current user's Run key; leave nothing behind pointing at a deleted program. }
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Downora');

    { Settings and history are only deleted on request. A silent uninstall never asks and keeps them. }
    if (not UninstallSilent) and
       (MsgBox(CustomMessage('DeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(ExpandConstant('{localappdata}\Downora'), True, True, True);
  end;
end;
