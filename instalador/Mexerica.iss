; Instalador do Mexerica (Inno Setup 6). Gerado por scripts\publicar.ps1 a partir de dist\Mexerica.
; Instala só para o usuário atual: não pede senha de administrador.

#define Nome "Mexerica"
#ifndef Versao
  #define Versao "1.0.0"
#endif
#define Exe "Mexerica.exe"

[Setup]
AppId={{5E3B7C1A-8F2D-4C6B-A9E1-2D7F4B8C0A15}
AppName={#Nome}
AppVersion={#Versao}
AppVerName={#Nome} {#Versao}
AppPublisher=Ítalo Sêne
AppPublisherURL=https://github.com/Oitalao/Mexerica
AppSupportURL=https://github.com/Oitalao/Mexerica/issues
AppUpdatesURL=https://github.com/Oitalao/Mexerica/releases
VersionInfoVersion={#Versao}
VersionInfoDescription=Instalador do {#Nome}
DefaultDirName={localappdata}\Programs\{#Nome}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\dist
OutputBaseFilename=Mexerica-{#Versao}-instalador
SetupIconFile=..\src\mexerica.ico
UninstallDisplayIcon={app}\{#Exe}
UninstallDisplayName={#Nome}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
pt.WelcomeLabel2=Isto vai instalar o [name/ver] no seu computador.%n%nO Mexerica converte e ajeita fotos, vídeos, músicas, PDFs e documentos direto pelo botão direito ou arrastando o arquivo. Não precisa de senha de administrador.
pt.FinishedLabel=O [name] foi instalado.%n%nEle fica perto do relógio e já aparece no botão direito dos seus arquivos.

[Tasks]
Name: "iniciar"; Description: "Abrir o Mexerica quando o computador ligar (recomendado)"
Name: "atalho"; Description: "Criar um atalho na área de trabalho"; Flags: unchecked

[InstallDelete]
; Restos de versões anteriores (a pasta é sempre a mesma e só do Mexerica).
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "..\dist\Mexerica\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#Nome}"; Filename: "{app}\{#Exe}"
Name: "{autodesktop}\{#Nome}"; Filename: "{app}\{#Exe}"; Tasks: atalho

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Mexerica"; ValueData: """{app}\{#Exe}"" --silencioso"; Tasks: iniciar; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Mexerica"; ValueType: dword; ValueName: "AutoInicioEscolhido"; ValueData: 1; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#Exe}"; Parameters: "--boas-vindas"; Description: "Abrir o Mexerica agora"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "{app}\{#Exe}"; Parameters: "--remover-menu"; Flags: runhidden waituntilterminated; RunOnceId: "RemoverMenu"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Mexerica"
Type: filesandordirs; Name: "{app}"

[Code]
// Fecha o Mexerica que está na bandeja (ele termina o que estiver fazendo e sai).
procedure FecharMexerica();
var
  Code: Integer;
begin
  if FileExists(ExpandConstant('{app}\{#Exe}')) then
    Exec(ExpandConstant('{app}\{#Exe}'), '--sair', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  FecharMexerica();
  // Desinstalou e reinstalou sem sair do Windows: cancela a limpeza agendada (ela apagaria as configurações novas).
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\RunOnce', 'MexericaLimpeza');
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  FecharMexerica();
  Result := True;
end;

// A extensão do menu fica presa pelo Explorador até ele reiniciar: o que não deu para apagar
// agora sai sozinho no próximo login.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Pasta: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Pasta := ExpandConstant('{localappdata}\Mexerica');
    if DirExists(Pasta) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\RunOnce', 'MexericaLimpeza',
        ExpandConstant('{cmd}') + ' /c rmdir /s /q "' + Pasta + '"');
  end;
end;
