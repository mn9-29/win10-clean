using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace WinForge
{
    /// <summary>
    /// Hosts the React UI inside a WebView2 and bridges it to the C# engine.
    /// If WebView2 can't initialise (runtime missing, embedded UI absent, etc.)
    /// it writes the error to a log, tells the user, then falls back to the
    /// classic WPF window so the app still works.
    /// </summary>
    public partial class ShellWindow : Window
    {
        Bridge _bridge;
        static readonly string LogPath =
            Path.Combine(Path.GetTempPath(), "WinForge", "shell-error.log");

        public ShellWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await BootAsync();
        }

        async Task BootAsync()
        {
            try
            {
                lblBoot.Text = "Loading WinForge UI…";

                string uiDir = ExtractUi();
                if (uiDir == null)
                    throw new InvalidOperationException(
                        "Embedded UI (app.zip) was not found in the executable.");

                // WebView2 user-data folder must be writable (the install dir may not be).
                string udf = Path.Combine(Path.GetTempPath(), "WinForge", "WebView2");
                Directory.CreateDirectory(udf);

                var env = await CoreWebView2Environment.CreateAsync(null, udf);
                await web.EnsureCoreWebView2Async(env);

                var core = web.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreDevToolsEnabled = false;

                _bridge = new Bridge(json => Dispatcher.Invoke(() =>
                {
                    try { web.CoreWebView2.PostWebMessageAsString(json); } catch { }
                }));
                core.WebMessageReceived += (s, e) =>
                {
                    string msg;
                    try { msg = e.TryGetWebMessageAsString(); }
                    catch { return; }
                    _bridge.HandleMessage(msg);
                };

                core.SetVirtualHostNameToFolderMapping(
                    "winforge.app", uiDir, CoreWebView2HostResourceAccessKind.Allow);

                lblBoot.Visibility = Visibility.Collapsed;
                core.Navigate("https://winforge.app/index.html");
            }
            catch (Exception ex)
            {
                FallbackToClassic(ex);
            }
        }

        // Extracts the embedded web UI (app.zip) into a fresh per-launch temp
        // folder (so locks/leftovers from a previous run can't break it) and
        // returns its path, or null if the resource isn't present.
        static string ExtractUi()
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames())
                if (n.EndsWith("app.zip", StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
            if (resName == null) return null;

            string dir = Path.Combine(Path.GetTempPath(), "WinForge",
                "ui_" + DateTime.Now.ToString("yyyyMMdd_HHmm_") + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(dir);

            using (var s = asm.GetManifestResourceStream(resName))
            {
                if (s == null) return null;
                using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                        string dest = Path.Combine(dir, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        entry.ExtractToFile(dest, true);
                    }
                }
            }
            return File.Exists(Path.Combine(dir, "index.html")) ? dir : null;
        }

        void FallbackToClassic(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.WriteAllText(LogPath,
                    "WinForge could not start the new (WebView2) UI and fell back to the classic window.\n\n" +
                    DateTime.Now + "\n\n" + ex);
            }
            catch { }

            MessageBox.Show(
                "The new WinForge UI (WebView2) couldn't start, so the classic window was opened instead.\n\n" +
                "Reason: " + ex.Message + "\n\n" +
                "Details were written to:\n" + LogPath + "\n\n" +
                "If WebView2 Runtime is missing, install it from:\n" +
                "https://developer.microsoft.com/microsoft-edge/webview2/",
                "WinForge", MessageBoxButton.OK, MessageBoxImage.Warning);

            var w = new MainWindow();
            Application.Current.MainWindow = w;
            w.Show();
            Close();
        }
    }
}
