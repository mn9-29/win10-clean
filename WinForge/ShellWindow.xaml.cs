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
    /// it silently falls back to the classic WPF window so the app still works.
    /// </summary>
    public partial class ShellWindow : Window
    {
        Bridge _bridge;

        public ShellWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await BootAsync();
        }

        async Task BootAsync()
        {
            try
            {
                string uiDir = ExtractUi();
                if (uiDir == null) { FallbackToClassic(); return; }

                // Keep the WebView2 user-data folder in TEMP (the install dir may be read-only).
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
            catch
            {
                FallbackToClassic();
            }
        }

        // Extracts the embedded web UI (app.zip) into a fresh temp folder and
        // returns its path, or null if the resource isn't present.
        static string ExtractUi()
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = null;
            foreach (var n in asm.GetManifestResourceNames())
                if (n.EndsWith("app.zip", StringComparison.OrdinalIgnoreCase)) { resName = n; break; }
            if (resName == null) return null;

            string dir = Path.Combine(Path.GetTempPath(), "WinForge", "ui");
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { /* a previous instance may hold files; reuse what's there */ }
            Directory.CreateDirectory(dir);

            using (var s = asm.GetManifestResourceStream(resName))
            {
                if (s == null) return null;
                using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
                    zip.ExtractToDirectory(dir);
            }
            return File.Exists(Path.Combine(dir, "index.html")) ? dir : null;
        }

        void FallbackToClassic()
        {
            var w = new MainWindow();
            Application.Current.MainWindow = w;
            w.Show();
            Close();
        }
    }
}
