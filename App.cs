using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

class App : Form {
    readonly TextBox link = new TextBox();
    readonly Button download = new Button(), folder = new Button();
    readonly Label status = new Label();
    readonly ProgressBar progress = new ProgressBar();
    volatile bool busy;
    readonly string output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "YouTube MP3");
    static string tools = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleYouTubeMP3", "tools-v1");

    [STAThread] static void Main(string[] args) {
        if (args.Length == 2 && args[0] == "--self-test") {
            try { SelfTest(args[1]); Environment.Exit(0); }
            catch(Exception e) { File.WriteAllText(args[1], e.ToString()); Environment.Exit(1); }
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new App());
    }
    public App() {
        Text = "YouTube to MP3"; ClientSize = new Size(520, 285);
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(247,248,250);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Label title = new Label { Text = "YouTube to MP3", Font = new Font("Segoe UI", 21, FontStyle.Bold), Location = new Point(24,20), AutoSize = true };
        Label hint = new Label { Text = "Paste a video link. Get an MP3.", Location = new Point(26,65), AutoSize = true };
        link.SetBounds(26,98,468,30);
        download.Text = "Download MP3"; download.SetBounds(26,145,300,43);
        download.BackColor = Color.FromArgb(36,94,224); download.ForeColor = Color.White;
        download.FlatStyle = FlatStyle.Flat;
        folder.Text = "Open MP3 folder"; folder.SetBounds(338,145,156,43);
        folder.Click += delegate { Directory.CreateDirectory(output); Process.Start("explorer.exe", Quote(output)); };
        download.Click += async delegate { await Download(); };
        progress.SetBounds(26,205,468,5); progress.Visible = false;
        status.SetBounds(26,222,468,48); status.Text = "Saves to Music \\ YouTube MP3"; status.ForeColor = Color.FromArgb(80,85,96);
        Controls.AddRange(new Control[] {title,hint,link,download,folder,progress,status});
        AcceptButton = download;
        // Let ffmpeg finish writing the file before closing.
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (busy) { e.Cancel = true; status.Text = "Please wait for the current download to finish."; }
        };
    }
    public static bool ValidUrl(string value) {
        Uri u;
        if (!Uri.TryCreate(value, UriKind.Absolute, out u) || (u.Scheme != "https" && u.Scheme != "http")) return false;
        string host = u.Host.ToLowerInvariant();
        // Check the host, not whether the link just contains youtube.com.
        return host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com");
    }
    static string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }
    // Unpack once so the next download starts faster.
    static void Prepare() {
        Directory.CreateDirectory(tools);
        foreach (string name in new [] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" }) {
            string target = Path.Combine(tools, name);
            if (File.Exists(target)) continue;
            // A second copy of the app might be unpacking these too.
            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using(Stream packed = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using(Stream input = new GZipStream(packed, CompressionMode.Decompress))
            using(FileStream dest = File.Create(temp)) input.CopyTo(dest);
            try { File.Move(temp, target); } catch(IOException) { if (!File.Exists(target)) throw; File.Delete(temp); }
        }
    }
    static int Run(string exe, string args, Action<string> line) {
        using(Process p = new Process()) {
            p.StartInfo = new ProcessStartInfo(Path.Combine(tools,exe),args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8, StandardErrorEncoding=Encoding.UTF8 };
            p.OutputDataReceived += (s,e) => { if(e.Data != null) line(e.Data); };
            p.ErrorDataReceived += (s,e) => { if(e.Data != null) line(e.Data); };
            p.Start();
            // Read both streams so a full error buffer can't stall the download.
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            return p.ExitCode;
        }
    }
    async Task Download() {
        string url = link.Text.Trim();
        if (!ValidUrl(url)) { status.Text = "Paste a valid YouTube video link first."; link.Focus(); return; }
        busy=true; download.Enabled=false; link.Enabled=false;
        progress.Visible=true; progress.Style=ProgressBarStyle.Marquee;
        status.Text="Getting ready...";
        StringBuilder log = new StringBuilder(); object gate = new object();
        try {
            int code = await Task.Run(() => {
                Prepare(); Directory.CreateDirectory(output);
                // One video at a time; keep the video ID to avoid title collisions.
                string args = "--ignore-config --no-playlist --no-overwrites --newline --no-color --socket-timeout 30 --retries 3 --extract-audio --audio-format mp3 --audio-quality 0 --windows-filenames --trim-filenames 180 --ffmpeg-location " + Quote(tools) + " --js-runtimes " + Quote("deno:" + Path.Combine(tools,"deno.exe")) + " -o " + Quote(Path.Combine(output,"%(title)s [%(id)s].%(ext)s")) + " -- " + Quote(url);
                return Run("yt-dlp.exe", args, line => {
                    lock(gate) log.AppendLine(line);
                    BeginInvoke((Action)(() => {
                        if(line.StartsWith("[download]")) status.Text=line.Replace("[download]", "Downloading:").Trim();
                        else if(line.StartsWith("[ExtractAudio]")) status.Text="Converting to MP3...";
                        else if(line.StartsWith("[youtube]")) status.Text="Finding audio...";
                    }));
                });
            });
            File.WriteAllText(Path.Combine(output,"last-download.log"),log.ToString());
            if(code != 0) {
                status.Text="Could not download this video. Try another link.";
                MessageBox.Show("The video may be unavailable, restricted, or YouTube may be blocking downloads.\n\nDetails are in last-download.log in your MP3 folder.", "Download failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else status.Text="Done! Your MP3 is in the MP3 folder.";
        } catch(Exception e) { status.Text="Something went wrong. Please try again."; MessageBox.Show(e.Message,"Download failed"); }
        finally { busy=false; download.Enabled=true; link.Enabled=true; progress.Visible=false; }
    }
    // A quick offline check. This doesn't prove YouTube is accepting requests.
    static void SelfTest(string report) {
        tools=Path.Combine(Path.GetDirectoryName(report),"test-tools");
        if(!ValidUrl("https://youtu.be/test") || !ValidUrl("https://www.youtube.com/watch?v=test") || ValidUrl("https://youtube.com.evil.example/test") || ValidUrl("file:///test") || ValidUrl("--exec bad")) throw new Exception("URL validation failed");
        Prepare(); StringBuilder log=new StringBuilder(); object gate=new object(); Action<string> capture = s => { lock(gate) log.AppendLine(s); };
        if(Run("yt-dlp.exe","--version",capture)!=0 || Run("deno.exe","--version",capture)!=0) throw new Exception("Dependency check failed");
        string sample=Path.Combine(Path.GetDirectoryName(report),"test-tone.mp3");
        if(Run("ffmpeg.exe","-y -f lavfi -i sine=frequency=440:duration=1 -codec:a libmp3lame " + Quote(sample),capture)!=0) throw new Exception("MP3 encoding failed");
        if(Run("ffprobe.exe","-v error -show_entries format=duration -of default=noprint_wrappers=1 " + Quote(sample),capture)!=0) throw new Exception("MP3 verification failed");
        File.WriteAllText(report,"PASS: URL validation, embedded tool extraction, downloader/runtime startup, MP3 encoding and probe.\n"+log);
    }
}
