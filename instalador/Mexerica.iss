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
// ---------- Programas complementares: o assistente avisa o que falta, pede autorização e instala ----------
const
  IndiceLO = 'https://download.documentfoundation.org/libreoffice/stable/';
  ParamsLO = ' /passive /norestart ALLUSERS=1 REGISTER_NO_MSO_TYPES=1 CREATEDESKTOPLINK=0 QUICKSTART=0 ISCHECKFORPRODUCTUPDATES=0 RebootYesNo=No';

var
  PaginaExtras: TInputOptionWizardPage;
  FaltaLO, FaltaMidia, Reiniciar: Boolean;
  IdxLO, IdxMidia: Integer;

function LibreOfficeInstalado(): Boolean;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\LibreOffice\UNO\InstallPath')
    or FileExists(ExpandConstant('{commonpf64}\LibreOffice\program\soffice.com'))
    or FileExists(ExpandConstant('{commonpf32}\LibreOffice\program\soffice.com'));
end;

// Edições "N" do Windows (vendidas na Europa) vêm sem os recursos de mídia; as prévias de vídeo e áudio dependem deles.
function FaltaPacoteMidia(): Boolean;
var
  Edicao: String;
begin
  Result := False;
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows NT\CurrentVersion', 'EditionID', Edicao) then
    Result := (Length(Edicao) > 1) and (Copy(Edicao, Length(Edicao), 1) = 'N') and not FileExists(ExpandConstant('{sys}\mfplat.dll'));
end;

procedure InitializeWizard();
begin
  FaltaLO := not LibreOfficeInstalado();
  FaltaMidia := FaltaPacoteMidia();
  IdxLO := -1; IdxMidia := -1;
  if not (FaltaLO or FaltaMidia) then Exit;
  PaginaExtras := CreateInputOptionPage(wpSelectTasks,
    'Programas complementares',
    'O Mexerica precisa de mais algumas coisas que este computador ainda não tem',
    'Marque o que deve ser baixado e instalado junto com o Mexerica. Tudo é gratuito e vem do site oficial. ' +
    'O Windows vai pedir sua permissão para instalar. Se você desmarcar, o Mexerica oferece de novo quando precisar.',
    False, False);
  if FaltaLO then
    IdxLO := PaginaExtras.Add('LibreOffice (gratuito, cerca de 360 MB): para converter documentos do Word, Excel e PowerPoint');
  if FaltaMidia then
    IdxMidia := PaginaExtras.Add('Pacote de Mídia do Windows: para ver a prévia de vídeos e músicas (esta edição do Windows vem sem ele)');
  if IdxLO >= 0 then PaginaExtras.Values[IdxLO] := True;
  if IdxMidia >= 0 then PaginaExtras.Values[IdxMidia] := True;
end;

function Marcado(Idx: Integer): Boolean;
begin
  Result := (Idx >= 0) and PaginaExtras.Values[Idx];
end;

function ProgressoDownload(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if ProgressMax > 0 then
  begin
    WizardForm.ProgressGauge.Max := 1000;
    WizardForm.ProgressGauge.Position := Integer(Progress * 1000 div ProgressMax);
    WizardForm.FilenameLabel.Caption := Format('%d de %d MB', [Integer(Progress div 1048576), Integer(ProgressMax div 1048576)]);
  end;
  Result := True;
end;

// Compara versões "26.8.1" pedaço por pedaço.
function VersaoMaior(A, B: String): Boolean;
var
  PA, PB, NA, NB: Integer;
begin
  Result := False;
  while (A <> '') or (B <> '') do
  begin
    PA := Pos('.', A); PB := Pos('.', B);
    if PA = 0 then begin NA := StrToIntDef(A, 0); A := ''; end else begin NA := StrToIntDef(Copy(A, 1, PA - 1), 0); A := Copy(A, PA + 1, 99); end;
    if PB = 0 then begin NB := StrToIntDef(B, 0); B := ''; end else begin NB := StrToIntDef(Copy(B, 1, PB - 1), 0); B := Copy(B, PB + 1, 99); end;
    if NA <> NB then begin Result := NA > NB; Exit; end;
  end;
end;

// Versão estável mais nova listada no site oficial.
function UltimaVersaoLO(): String;
var
  Html: AnsiString;
  Resto, V: String;
  P, Q: Integer;
begin
  Result := '';
  DownloadTemporaryFile(IndiceLO, 'lo-indice.html', '', nil);
  LoadStringFromFile(ExpandConstant('{tmp}\lo-indice.html'), Html);
  Resto := String(Html);
  P := Pos('href="', Resto);
  while P > 0 do
  begin
    Resto := Copy(Resto, P + 6, Length(Resto));
    Q := Pos('/"', Resto);
    if (Q > 0) and (Q < 12) then
    begin
      V := Copy(Resto, 1, Q - 1);
      if (Length(V) >= 5) and (V[1] >= '0') and (V[1] <= '9') and (Pos('.', V) > 0) then
        if (Result = '') or VersaoMaior(V, Result) then Result := V;
    end;
    P := Pos('href="', Resto);
  end;
end;

procedure InstalarLibreOffice();
var
  V, Msi: String;
  Code: Integer;
begin
  try
    WizardForm.StatusLabel.Caption := 'Procurando a versão mais nova do LibreOffice...';
    V := UltimaVersaoLO();
    if V = '' then RaiseException('versão não encontrada');
    WizardForm.StatusLabel.Caption := 'Baixando o LibreOffice ' + V + '...';
    DownloadTemporaryFile(IndiceLO + V + '/win/x86_64/LibreOffice_' + V + '_Win_x86-64.msi', 'libreoffice.msi', '', @ProgressoDownload);
    Msi := ExpandConstant('{tmp}\libreoffice.msi');
    WizardForm.FilenameLabel.Caption := '';
    WizardForm.StatusLabel.Caption := 'Instalando o LibreOffice. Aceite o pedido de permissão do Windows; leva alguns minutos...';
    if not ShellExec('runas', 'msiexec.exe', '/i "' + Msi + '"' + ParamsLO, '', SW_SHOW, ewWaitUntilTerminated, Code) then Code := 1223;
    if (Code = 1223) or (Code = 1602) then
      MsgBox('O LibreOffice não foi instalado porque a permissão foi recusada. Sem problema: o Mexerica oferece de novo quando você for converter um documento.', mbInformation, MB_OK)
    else if (Code <> 0) and (Code <> 3010) and (Code <> 1641) then
      MsgBox('O instalador do LibreOffice terminou com erro (código ' + IntToStr(Code) + '). O Mexerica oferece de novo quando você for converter um documento.', mbError, MB_OK);
    DeleteFile(Msi);
  except
    MsgBox('Não deu para baixar o LibreOffice agora (o computador está sem internet?). O Mexerica foi instalado e oferece o LibreOffice de novo quando você for converter um documento.', mbInformation, MB_OK);
  end;
end;

procedure InstalarPacoteMidia();
var
  Code: Integer;
begin
  WizardForm.StatusLabel.Caption := 'Instalando o Pacote de Mídia do Windows. Aceite o pedido de permissão; pode levar alguns minutos...';
  WizardForm.FilenameLabel.Caption := '';
  if ShellExec('runas', ExpandConstant('{sys}\dism.exe'), '/online /add-capability /capabilityname:Media.MediaFeaturePack~~~~0.0.1.0 /norestart /quiet', '', SW_HIDE, ewWaitUntilTerminated, Code)
     and ((Code = 0) or (Code = 3010)) then
    Reiniciar := True
  else
    MsgBox('O Pacote de Mídia não foi instalado. Ele também pode ser instalado em Configurações > Aplicativos > Recursos opcionais > Adicionar um recurso > "Pacote de Mídia".', mbInformation, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if Marcado(IdxLO) then InstalarLibreOffice();
    if Marcado(IdxMidia) then InstalarPacoteMidia();
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := Reiniciar;
end;

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
