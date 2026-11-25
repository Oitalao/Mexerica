param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\bin\Release\net8.0-windows10.0.19041.0\win-x64\Mexerica.exe'),
    [string]$TestRoot = 'E:\Mexerica\teste\frente',
    [string]$OtherVolume = 'D:\'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$work = Join-Path ([IO.Path]::GetFullPath($TestRoot)) ('office-regressao-' + [Guid]::NewGuid().ToString('N'))
$other = Join-Path ([IO.Path]::GetFullPath($OtherVolume)) ('mexerica-office-regressao-' + [Guid]::NewGuid().ToString('N'))
$alias = Join-Path $work 'junction'
$previousOffice = $env:MEXERICA_SEM_LIBREOFFICE
$config = Join-Path $env:LOCALAPPDATA 'Mexerica\config.json'
if ((Get-Content -LiteralPath $config -Raw | ConvertFrom-Json).OutputDir) { throw 'Zere OutputDir no config.json antes do teste e restaure depois.' }
function Assert($condition, $message) { if (!$condition) { throw $message } }
function RunCli([string[]]$Arguments) {
    $text = & $Exe @Arguments | Out-String
    [pscustomobject]@{ Code = $LASTEXITCODE; Text = $text }
}
function AddXml($zip, $name, $text) {
    $writer = [IO.StreamWriter]::new($zip.CreateEntry($name).Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write($text) } finally { $writer.Dispose() }
}
function ReadXml($path, $entry) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $reader = [IO.StreamReader]::new($zip.GetEntry($entry).Open())
        try { [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
}
function DeleteFixture($path, $parent) {
    $absolute = [IO.Path]::GetFullPath($path)
    $expected = [IO.Path]::GetFullPath($parent).TrimEnd('\') + '\'
    Assert ($absolute.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)) 'Limpeza fora do destino de teste'
    if (![IO.Directory]::Exists($absolute)) { return }
    foreach ($file in [IO.Directory]::GetFiles($absolute, '*', [IO.SearchOption]::AllDirectories)) { [IO.File]::SetAttributes($file, [IO.FileAttributes]::Normal) }
    [IO.Directory]::Delete($absolute, $true)
}
try {
    $env:MEXERICA_SEM_LIBREOFFICE = '1'
    New-Item -ItemType Directory -Path $work,$other | Out-Null
    $source = Join-Path $work 'origem'
    $inside = Join-Path $source 'vazia'
    New-Item -ItemType Directory -Path $inside | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $source '00-readonly.bin'), [byte[]]::new(1048576))
    [IO.File]::SetAttributes((Join-Path $source '00-readonly.bin'), [IO.FileAttributes]::ReadOnly)
    [IO.File]::WriteAllBytes((Join-Path $source 'zz-locked.bin'), [byte[]]::new(2097152))
    foreach ($verb in 'Copiar','Mover') {
        $result = RunCli -Arguments @('acao',$verb,$source,'--p',"destino=$inside")
        Assert ($result.Code -ne 0 -and $result.Text.Contains('destino')) "$verb aceitou destino interno"
        Assert ([IO.Directory]::GetFileSystemEntries($inside).Length -eq 0) 'Criou pasta interna'
    }
    New-Item -ItemType Junction -Path $alias -Target $inside | Out-Null
    $result = RunCli -Arguments @('acao','Copiar',$source,'--p',"destino=$alias")
    Assert ($result.Code -ne 0) 'Aceitou junction para destino interno'
    [IO.Directory]::Delete($alias)
    Assert ([IO.Path]::GetPathRoot($source) -ne [IO.Path]::GetPathRoot($other)) 'OtherVolume deve apontar para outro volume'
    foreach ($verb in 'Copiar','Mover') {
        $locked = [IO.File]::Open((Join-Path $source 'zz-locked.bin'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        try { $result = RunCli -Arguments @('acao',$verb,$source,'--p',"destino=$other") } finally { $locked.Dispose() }
        Assert ($result.Code -ne 0) 'Copia de arquivo bloqueado aceitou'
        Assert ([IO.Directory]::GetFileSystemEntries($other).Length -eq 0) 'Sobrou copia parcial'
        Assert ((Get-Item -LiteralPath (Join-Path $source 'zz-locked.bin')).Length -eq 2097152) 'Origem alterada'
    }
    foreach ($attempt in 1..2) {
        $result = RunCli -Arguments @('acao','Copiar',$source,'--p',"destino=$other")
        Assert ($result.Code -eq 0) 'Copia E->D falhou'
    }
    Assert (Test-Path -LiteralPath (Join-Path $other 'origem (1)\vazia')) 'Colisao ou pasta vazia incorreta'
    [IO.File]::SetAttributes((Join-Path $source '00-readonly.bin'), [IO.FileAttributes]::Normal)
    $result = RunCli -Arguments @('acao','Mover',$source,'--p',"destino=$other")
    Assert ($result.Code -eq 0 -and !(Test-Path -LiteralPath $source)) 'Mover E->D falhou'
    Write-Output 'OK 7: destinos interno/junction recusados; 2 rollback E->D, 0 bytes parciais; colisao e pasta vazia.'

    $xlsx = Join-Path $work 'formulas.xlsx'
    $zip = [IO.Compression.ZipFile]::Open($xlsx, [IO.Compression.ZipArchiveMode]::Create)
    try {
        AddXml $zip '[Content_Types].xml' '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>'
        AddXml $zip '_rels/.rels' '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
        AddXml $zip 'xl/workbook.xml' '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Plan1" sheetId="1" r:id="rId1"/></sheets></workbook>'
        AddXml $zip 'xl/_rels/workbook.xml.rels' '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>'
        AddXml $zip 'xl/worksheets/sheet1.xml' '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1"><v>2</v></c><c r="B1"><v>3</v></c><c r="C1"><f>SUM(A1:B1)</f><v>5</v></c><c r="D1" t="str"><f>IF(A1=2,"x,y;z","")</f><v>x,y;z</v></c><c r="E1" t="e"><f>1/0</f><v>#DIV/0!</v></c><c r="F1" t="b"><f>A1=2</f><v>1</v></c><c r="G1"><f t="shared" si="0" ref="G1:G2">A1+$B$1</f><v>5</v></c><c r="H1"><f>SUM(A:A)</f><v>6</v></c></row><row r="2"><c r="A2"><v>4</v></c><c r="G2"><f t="shared" si="0"/><v>7</v></c></row></sheetData></worksheet>'
    } finally { $zip.Dispose() }
    $report = Join-Path $work 'rel.txt'
    $result = RunCli -Arguments @('--executar','Convert','ODS',$report,$xlsx)
    Assert ($result.Code -eq 0) 'XLSX->ODS falhou'
    Assert ((Get-Content -LiteralPath $report -Raw).Contains('AVISO')) 'Formula nao suportada sem aviso'
    $ods = Join-Path $work 'formulas.ods'
    $xml = ReadXml $ods 'content.xml'
    $formulas = @($xml.SelectNodes('//*[@*[local-name()="formula"]]'))
    Assert ($formulas.Count -eq 6) 'Numero de formulas ODS incorreto'
    Assert ($xml.OuterXml.Contains('of:=SUM([.A1:.B1])')) 'Referencia ODS nao adaptada'
    $result = RunCli -Arguments @('--executar','Convert','XLSX',$report,$ods)
    Assert ($result.Code -eq 0) 'ODS->XLSX falhou'
    $xml = ReadXml (Join-Path $work 'formulas (1).xlsx') 'xl/worksheets/sheet1.xml'
    $expected = @{ C1=@('SUM(A1:B1)','5'); D1=@('IF(A1=2,"x,y;z","")','x,y;z'); E1=@('1/0','#DIV/0!'); F1=@('A1=2','1'); G1=@('A1+$B$1','5'); G2=@('A2+$B$1','7') }
    foreach ($ref in $expected.Keys) {
        $cell = $xml.SelectSingleNode("//*[local-name()='c' and @r='$ref']")
        Assert ($cell.f -eq $expected[$ref][0] -and $cell.v -eq $expected[$ref][1]) "Formula/cache de $ref alterado"
    }
    Assert ($xml.SelectSingleNode("//*[local-name()='c' and @r='E1']").t -eq 'e') 'Cache de erro virou texto'
    Assert ($xml.SelectSingleNode("//*[local-name()='c' and @r='H1']").v -eq '6') 'Cache da formula nao suportada perdido'
    Write-Output 'OK 16: XLSX->ODS->XLSX, 6 formulas/caches iguais, shared/absoluta/erro/bool/string; formula incompatível mantém cache e avisa.'

    $csv = Join-Path $work 'brasileiro.csv'
    [IO.File]::WriteAllText($csv, "Descrição;Valor`r`nAção;25`r`nCafé;7", [Text.Encoding]::GetEncoding(1252))
    $result = RunCli -Arguments @('--executar','Convert','XLSX',$report,$csv)
    Assert ($result.Code -eq 0) 'CSV1252->XLSX falhou'
    $xml = ReadXml (Join-Path $work 'brasileiro.xlsx') 'xl/worksheets/sheet1.xml'
    $text = $xml.DocumentElement.InnerText
    Assert ($text.Contains('Descrição') -and $text.Contains('Ação') -and $text.Contains('Café')) 'Acentos CP1252 alterados'
    foreach ($encoding in @([Text.Encoding]::GetEncoding(1252),[Text.UTF32Encoding]::new($true,$true))) {
        $txt = Join-Path $work ('texto-' + $encoding.CodePage + '.txt')
        [IO.File]::WriteAllText($txt, 'Ação, João e café.', $encoding)
        $result = RunCli -Arguments @('--executar','Convert','DOCX',$report,$txt)
        Assert ($result.Code -eq 0) 'TXT->DOCX falhou'
        $xml = ReadXml ([IO.Path]::ChangeExtension($txt,'docx')) 'word/document.xml'
        Assert ($xml.DocumentElement.InnerText.Contains('Ação, João e café.')) 'Texto nao UTF8 corrompido'
    }
    Write-Output 'OK 18: CSV CP1252 e TXT CP1252/UTF32-BE com BOM mantiveram todos os acentos.'
}
finally {
    $env:MEXERICA_SEM_LIBREOFFICE = $previousOffice
    if ([IO.Directory]::Exists($alias)) { [IO.Directory]::Delete($alias) }
    DeleteFixture $work $TestRoot
    DeleteFixture $other $OtherVolume
}
