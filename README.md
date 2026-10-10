# YTmp3

A small Windows app for turning a YouTube link into an MP3. Paste the link, click **Download MP3**, and wait for it to finish.

Files go in **Music / YouTube MP3**. The **Open MP3 folder** button takes you there.

## Build

Windows 10 or 11, 64-bit. Uses Windows Forms and the .NET Framework compiler included with Windows, so there's no Visual Studio project or NuGet setup.

In PowerShell, from this folder:

```powershell
.\setup.ps1
.\build.ps1
```

Setup downloads the tools from their official GitHub releases. The first build takes a bit because it compresses them into one EXE. After that, run `YouTube-to-MP3.exe`. You can move that file wherever you want; the other files aren't needed to run it.

The EXE is roughly 190 MB because it includes the downloader, audio converter, and JavaScript runtime. It's unsigned. Build outputs and downloaded tools aren't checked into this repo.

## How it works

- `App.cs` is the window, download handling, and a small offline self-test.
- `setup.ps1` gets yt-dlp, FFmpeg, and Deno.
- `build.ps1` bundles the tools, compiles the app, and runs the self-test.

On the first download, tools are unpacked to `%LOCALAPPDATA%\SimpleYouTubeMP3\tools-v1`. Downloads run in the background so the window stays responsive. Closing waits until the download finishes. Only the selected video is downloaded, even if the link includes a playlist. Existing files aren't overwritten.

If something fails, check `last-download.log` in the MP3 folder. Private videos, sign-in requirements, and YouTube blocking requests can prevent a download. There's no cookie import or sign-in feature. Only download content you own or have permission to download.

## Checks

The build checks URL validation, unpacking the bundled tools, starting yt-dlp and Deno, and creating/probing a one-second MP3 test tone. It doesn't contact YouTube. Live downloads still need a manual check.

## Tools

This app is a wrapper around these projects:

- [yt-dlp](https://github.com/yt-dlp/yt-dlp)
- [FFmpeg builds](https://github.com/yt-dlp/FFmpeg-Builds)
- [Deno](https://github.com/denoland/deno)

Their licenses and third-party notices apply to the bundled binaries. The FFmpeg download is a GPL build; consult upstream license and source requirements before redistributing a packaged EXE.

Setup uses the latest releases available when you run it, so builds aren't pinned to specific tool versions. To refresh the tools, remove the local `tools` folder and run setup and build again. For an existing app installation, also remove its extracted `tools-v1` folder while the app is closed so the rebuilt EXE can unpack the new versions.
