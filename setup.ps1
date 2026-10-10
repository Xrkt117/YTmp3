$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot 'tools'
New-Item -ItemType Directory -Force $tools | Out-Null

# Keep downloads local to the project. Nothing gets added to PATH.
$downloads = @{
    'yt-dlp.exe' = 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe'
    'ffmpeg.zip' = 'https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip'
    'deno.zip' = 'https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip'
}
foreach ($name in $downloads.Keys) {
    $destination = Join-Path $tools $name
    if (!(Test-Path $destination)) {
        Write-Host "Downloading $name..."
        $temporary = "$destination.download"
        Invoke-WebRequest $downloads[$name] -OutFile $temporary
        Move-Item -LiteralPath $temporary -Destination $destination -Force
    }
}
Expand-Archive (Join-Path $tools 'ffmpeg.zip') (Join-Path $tools 'ffmpeg') -Force
Expand-Archive (Join-Path $tools 'deno.zip') (Join-Path $tools 'deno') -Force
Write-Host 'Ready. Run .\build.ps1 next.'
