param([string]$ToolsDirectory = (Join-Path $PSScriptRoot 'tools'))
$ErrorActionPreference = 'Stop'
$appRoot = $PSScriptRoot
$ffbin = (Get-ChildItem "$ToolsDirectory\ffmpeg" -Recurse -Filter ffmpeg.exe | Select-Object -First 1).DirectoryName
$binaries = @{
    'yt-dlp.exe' = "$ToolsDirectory\yt-dlp.exe"
    'ffmpeg.exe' = "$ffbin\ffmpeg.exe"
    'ffprobe.exe' = "$ffbin\ffprobe.exe"
    'deno.exe' = "$ToolsDirectory\deno\deno.exe"
}
$resources = @()
# Compress the tools before embedding them. They're most of the EXE's size.
New-Item -ItemType Directory -Force "$appRoot\tools" | Out-Null
foreach ($name in $binaries.Keys) {
    if (!(Test-Path $binaries[$name])) { throw "Missing $name. Run .\setup.ps1 first." }
    $packed = "$appRoot\tools\$name.gz"
    if (!(Test-Path $packed) -or (Get-Item $packed).LastWriteTimeUtc -lt (Get-Item $binaries[$name]).LastWriteTimeUtc) {
        $inputFile = [IO.File]::OpenRead($binaries[$name])
        $outputFile = [IO.File]::Create($packed)
        $gzip = [IO.Compression.GZipStream]::new($outputFile, [IO.Compression.CompressionLevel]::Optimal)
        try { $inputFile.CopyTo($gzip) } finally { $inputFile.Dispose(); $gzip.Dispose(); $outputFile.Dispose() }
    }
    $resources += "/resource:$packed,$name"
}
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:winexe /platform:x64 /optimize+ "/out:$appRoot\YouTube-to-MP3.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll @resources "$appRoot\App.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
$report = Join-Path $appRoot 'self-test.txt'
$testProcess = Start-Process -FilePath "$appRoot\YouTube-to-MP3.exe" -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
if ($testProcess.ExitCode -ne 0) { throw (Get-Content $report -Raw) }
Get-Content $report -TotalCount 4
Get-Item "$appRoot\YouTube-to-MP3.exe" | Select-Object Name,Length
