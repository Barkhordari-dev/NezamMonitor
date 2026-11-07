using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.Security.Extensions;
using NezamMonitorPro.Security.Services;
using NezamMonitorPro.Security.Db;
using NezamMonitorPro.App.Views;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NezamMonitorPro.App;
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private DatabaseEncryptionService? _dbEncryptionService;
    private EncryptedSqliteConnection? _encryptedConnection;
    public EncryptedSqliteConnection? DatabaseConnection => _encryptedConnection;

    private static Services.NavigationService? _nav;
    public static Services.NavigationService Navigation => _nav ?? throw new InvalidOperationException("Navigation not initialized.");

    public App()
    {
        // Force a culture that has satellite resources (fa) to avoid missing culture errors
        var faCulture = new CultureInfo("fa-IR");
        CultureInfo.DefaultThreadCurrentCulture = faCulture;
        CultureInfo.DefaultThreadCurrentUICulture = faCulture;

        // Register the bundled Persian font so the UI is identical on any Windows box
        RegisterEmbeddedFonts();

        InitializeComponent();
        var services = new ServiceCollection();
        services.AddNezamSecurity(appDataDirectory: GetAppDirectory());
        _serviceProvider = services.BuildServiceProvider();
        _dbEncryptionService = _serviceProvider.GetRequiredService<DatabaseEncryptionService>();

        // مقدار پیشفرض RTL برای تمام Resource های جهتدهی
        Resources["MainWindowFlowDirection"] = FlowDirection.RightToLeft;
        Resources["TextFlowDirection"] = FlowDirection.RightToLeft;
        Resources["TableFlowDirection"] = FlowDirection.RightToLeft;
        Resources["MenuFlowDirection"] = FlowDirection.RightToLeft;
    }

    /// <summary>
    /// Probes the bundled fonts so a missing font file is noticed early
    /// (Debug output) instead of silently falling back to Tahoma at render
    /// time. The fonts themselves are loaded by ThemeManager.BuildFontFamily
    /// through a pack:// URI; adding a bare-name FontFamily to
    /// SystemFontFamilies (what this used to do) does NOT make the font data
    /// available — it only registers the name, which then resolves against
    /// installed fonts and finds nothing.
    /// </summary>
    private static void RegisterEmbeddedFonts()
    {
        foreach (var file in new[] { "Fonts/Vazirmatn-Regular.ttf", "Fonts/BNazanin.ttf" })
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/{file}", UriKind.Absolute);
                _ = new System.Windows.Media.GlyphTypeface(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bundled font missing ({file}): {ex.Message}");
            }
        }
    }

    protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _nav = new Services.NavigationService();

            // Log file path next to executable
            // مهم: لاگ باید «افزودنی» باشد تا شواهد کرش بعد از اجرای مجدد برنامه پاک نشود.
            string logPath = Path.Combine(GetAppDirectory(), "startup.log");
            try
            {
                var logInfo = new FileInfo(logPath);
                if (logInfo.Exists && logInfo.Length > 2 * 1024 * 1024) File.Delete(logPath);
#if DEBUG
                File.AppendAllText(logPath,
                    $"\n===== SESSION {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====\n[{DateTime.Now}] OnStartup begin\n");
#endif
            }
            catch { }

            // Global exception handlers
            DispatcherUnhandledException += (s, args) =>
            {
#if DEBUG
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] Dispatcher exception: {args.Exception}\n"); } catch { }
#endif
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
#if DEBUG
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] AppDomain exception: {args.ExceptionObject}\n"); } catch { }
#endif
            };
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
#if DEBUG
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] TaskScheduler exception: {args.Exception}\n"); } catch { }
#endif
                args.SetObserved();
            };

            try
            {
                InitializeDatabaseConnection();
            }
            catch (Exception ex)
            {
#if DEBUG
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] InitializeDatabaseConnection exception: {ex}\n"); } catch { }
#endif
                MessageBox.Show($"خطا در پایگاه داده: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

#if DEBUG
            try { File.AppendAllText(logPath, $"[{DateTime.Now}] OnStartup end\n"); } catch { }
#endif

            // ── Headless verification mode: render each view to PNG and exit ──
            // Usage: NezamMonitorPro.exe --render <outDir>
            //        NezamMonitorPro.exe --render <outDir> --full   (whole window incl. sidebar)
            if (e.Args.Contains("--render"))
            {
                int outIdx = Array.IndexOf(e.Args, "--render");
                string outDir = (outIdx >= 0 && outIdx + 1 < e.Args.Length) ? e.Args[outIdx + 1] : GetAppDirectory();
                bool full = e.Args.Contains("--full");
                Dispatcher.BeginInvoke(new Action(() => RunRenderMode(outDir, logPath, full)));
            }
        }

    /// <summary>
    /// Renders every registered view into a PNG file so the UI can be verified
    /// without a human sitting in front of the screen.
    /// </summary>
    
    private void InitializeDatabaseConnection()
    {
        try
        {
            // تا وقتی پنجرهٔ اصلی ساخته نشده، بستن پنجرهٔ ورود نباید برنامه را ببندد
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var credentialStore = _serviceProvider!.GetRequiredService<CredentialStore>();
            var dbEncryptionService = _serviceProvider!.GetRequiredService<DatabaseEncryptionService>();
            var logPath = Path.Combine(GetAppDirectory(), "startup.log");
            var dbPath = Path.Combine(GetAppDirectory(), "data", "nezam_monitor.db");

            if (!credentialStore.HasPassword())
            {
                var credentialsPath = Path.Combine(GetAppDirectory(), "data", "credentials.json");
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(dbPath)) File.Delete(dbPath);
                if (File.Exists(credentialsPath)) File.Delete(credentialsPath);

                while (true)
                {
                    var setPasswordWindow = new SetPasswordWindow();
                    if (setPasswordWindow.ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }

                    try
                    {
                        dbEncryptionService.CreateDatabase(setPasswordWindow.Password);
                        Log(logPath, "initial password created");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // e.g. file locked: keep the dialog open instead of killing the app
                        MessageBox.Show($"ساخت پایگاه داده ممکن نشد: {ex.Message}\nدوباره تلاش کنید.",
                            "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            // ── ورود با امکان تلاش دوباره (به‌جای بستن برنامه در صورت رمز اشتباه) ──
            string? lastError = null;
            while (true)
            {
                var passwordWindow = new PasswordWindow(lastError);
                if (passwordWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                var password = passwordWindow.Password;
                if (string.IsNullOrEmpty(password))
                {
                    lastError = "لطفاً رمز عبور را وارد کنید.";
                    continue;
                }

                try
                {
                    _encryptedConnection = dbEncryptionService.Open(password);
                    Log(logPath, "password verified");
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    lastError = "رمز عبور اشتباه است. دوباره تلاش کنید.";
                    Log(logPath, "password rejected");
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    lastError = "پایگاه داده باز نشد: " + ex.Message;
                    Log(logPath, "db open failed: " + ex.Message);
                }
            }
            Services.DatabaseService.Initialize(_encryptedConnection.Connection);
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطا در پایگاه داده: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void RunRenderMode(string outDir, string logPath, bool full = false)
    {
        try
        {
            Directory.CreateDirectory(outDir);
            var main = MainWindow as MainWindow;
            if (main is null) { Log(logPath, "render: no MainWindow"); return; }

            var views = new (string Key, string File)[]
            {
                ("dashboard", "01-dashboard"),
                ("update", "02-update"),
                ("generator", "03-generator"),
                ("cases", "04-cases"),
                ("reports", "05-reports"),
                ("excel", "06-excel"),
                ("engineers", "07-engineers"),
                ("fees", "08-fees"),
                ("followup", "09-followup"),
                ("changes", "10-changes"),
                ("history", "11-history"),
                ("settings", "12-settings"),
                ("androidexport", "13-android"),
            };

            foreach (var (key, file) in views)
            {
                try
                {
                    main.NavigateTo(key);
                    Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    System.Threading.Thread.Sleep(450);

                    if (full)
                    {
                        // Render the WHOLE window (sidebar + content) so layout
                        // changes (e.g. sidebar side) are visible in the PNG.
                        main.UpdateLayout();
                        int fw = (int)Math.Max(1, main.ActualWidth);
                        int fh = (int)Math.Max(1, main.ActualHeight);
                        var frtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                            fw, fh, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        frtb.Render(main);
                        var fenc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        fenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frtb));
                        using (var fs = File.Create(Path.Combine(outDir, file + ".png")))
                            fenc.Save(fs);
                        Log(logPath, $"render OK: {key} -> {file}.png (full {fw}x{fh})");
                        continue;
                    }

                    // Render the actual view that is inside the Frame
                    var element = main.CurrentViewElement;
                    if (element is null) { Log(logPath, $"render: {key} no content"); continue; }

                    element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height));
                    element.UpdateLayout();

                    int w = (int)Math.Max(1, element.ActualWidth);
                    int h = (int)Math.Max(1, element.ActualHeight);

                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(element);

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using (var fs = File.Create(Path.Combine(outDir, file + ".png")))
                        encoder.Save(fs);

                    Log(logPath, $"render OK: {key} -> {file}.png ({w}x{h})");
                }
                catch (Exception ex)
                {
                    Log(logPath, $"render FAIL {key}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Log(logPath, "render: done");
            Shutdown();
        }
        catch (Exception ex)
        {
            Log(logPath, $"render fatal: {ex}");
            Shutdown();
        }
    }

    private static void Log(string logPath, string message)
    {
#if DEBUG
        try { File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n"); } catch { }
#endif
    }

    // Helper to get real exe directory even in single-file bundle
    private static string GetAppDirectory()
    {
        return Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
    }
}
