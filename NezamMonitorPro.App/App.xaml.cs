using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.Security.Extensions;
using NezamMonitorPro.Security.Services;
using NezamMonitorPro.Security.Db;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using NezamMonitorPro.App.Views;
using NezamMonitorPro.Core.Data;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NezamMonitorPro.App;
public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    private DatabaseEncryptionService? _dbEncryptionService;
    private EncryptedSqliteConnection? _encryptedConnection;
    public EncryptedSqliteConnection? DatabaseConnection => _encryptedConnection;

    /// <summary>کانتینر DI برنامه — Viewها ViewModel خود را از اینجا می‌گیرند.</summary>
    public static IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized.");

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
        RegisterViewModels(services);
        _serviceProvider = services.BuildServiceProvider();
        _dbEncryptionService = _serviceProvider.GetRequiredService<DatabaseEncryptionService>();

        // مقدار پیشفرض RTL برای تمام Resource های جهتدهی
        Resources["MainWindowFlowDirection"] = FlowDirection.RightToLeft;
        Resources["TextFlowDirection"] = FlowDirection.RightToLeft;
        Resources["TableFlowDirection"] = FlowDirection.RightToLeft;
        Resources["MenuFlowDirection"] = FlowDirection.RightToLeft;
    }

    /// <summary>
    /// ARCH-001: دیتابیس و همه ViewModelها در کانتینر DI ثبت می‌شوند تا Viewها
    /// به‌جای ساختن دستی، آن‌ها را با App.Services.GetRequiredService&lt;T&gt;() بگیرند.
    /// NezamDatabase همان نمونه DatabaseService.Instance است.
    /// SnapshotViewerViewModel ثبت نمی‌شود چون شناسه اسنپ‌شات را موقع ساختن
    /// می‌خواهد (پارامتر زمان اجرا) و ChangesViewModel خودش آن را می‌سازد.
    /// </summary>
    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddSingleton<NezamDatabase>(_ => DatabaseService.Instance);
        services.AddSingleton<ThemeManager>(sp => new ThemeManager(sp.GetRequiredService<NezamDatabase>()));
        services.AddTransient<MainViewModel>();
        services.AddTransient<AndroidExportViewModel>();
        services.AddTransient<CasesViewModel>();
        services.AddTransient<ChangesViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EngineersViewModel>();
        services.AddTransient<ExcelExportViewModel>();
        services.AddTransient<FeesViewModel>();
        services.AddTransient<FollowUpViewModel>();
        services.AddTransient<GeneratorViewModel>();
        services.AddTransient<HiddenSettingsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<UpdateViewModel>();
        services.AddTransient<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<ThemeManager>(), DatabaseService.DatabasePath));
    }

    /// <summary>
    /// برنامه را از اول اجرا می‌کند و نسخه فعلی را می‌بندد.
    /// (بعد از پاکسازی کامل اطلاعات و تغییر رمز استفاده می‌شود.)
    /// </summary>
    public static void Restart()
    {
        try
        {
            var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
        }
        catch { }
        Application.Current?.Shutdown();
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

            // Global exception handlers
            // (نسخه‌های قدیمی یک فایل startup.log کنار برنامه می‌ساختند؛
            // دیگر ساخته نمی‌شود و اگر از قبل مانده باشد، همین‌جا پاک می‌شود.)
            try { File.Delete(Path.Combine(GetAppDirectory(), "startup.log")); } catch { }
            DispatcherUnhandledException += (s, args) =>
            {
                // ERR-001: خطا دیگر بی‌صدا بلعیده نمی‌شود؛ در لاگ خطا نوشته و
                // یک پیام ساده به کاربر نشان داده می‌شود.
                try { ErrorLog.Write(args.Exception, "DispatcherUnhandledException"); } catch { }
                MessageBox.Show("یه مشکلی پیش اومد. جزییاتش توی لاگ خطا ذخیره شد.",
                    "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                try { ErrorLog.Write(args.Exception, "UnobservedTaskException"); } catch { }
                args.SetObserved();
            };

            try
            {
                InitializeDatabaseConnection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در پایگاه داده: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // ── Headless verification mode: render each view to PNG and exit ──
            // Usage: NezamMonitorPro.exe --render <outDir>
            //        NezamMonitorPro.exe --render <outDir> --full   (whole window incl. sidebar)
            if (e.Args.Contains("--render"))
            {
                int outIdx = Array.IndexOf(e.Args, "--render");
                string outDir = (outIdx >= 0 && outIdx + 1 < e.Args.Length) ? e.Args[outIdx + 1] : GetAppDirectory();
                bool full = e.Args.Contains("--full");
                Dispatcher.BeginInvoke(new Action(() => RunRenderMode(outDir, full)));
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
            // تا وقتی پنجره اصلی ساخته نشده، بستن پنجره ورود نباید برنامه را ببندد
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var credentialStore = _serviceProvider!.GetRequiredService<CredentialStore>();
            var dbEncryptionService = _serviceProvider!.GetRequiredService<DatabaseEncryptionService>();
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
            var failedAttempts = 0;
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
                    lastError = "لطفا رمز عبور را وارد کنید.";
                    continue;
                }

                try
                {
                    _encryptedConnection = dbEncryptionService.Open(password);
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    lastError = "رمز عبور اشتباه است. دوباره تلاش کنید.";
                    failedAttempts++;
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    lastError = "پایگاه داده باز نشد: " + ex.Message;
                    failedAttempts++;
                }

                // SEC-012: بعد از هر تلاش ناموفق، کمی صبر می‌کنیم (۱، ۲، ۴ ثانیه)
                // تا حدس‌زدن رمز با تکرار سریع ممکن نباشد.
                var delaySeconds = failedAttempts <= 1 ? 1 : failedAttempts == 2 ? 2 : 4;
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }
            NezamMonitorPro.App.Services.DatabaseService.Initialize(_encryptedConnection.Connection);
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

    private void RunRenderMode(string outDir, bool full = false)
    {
        try
        {
            Directory.CreateDirectory(outDir);
            var main = MainWindow as MainWindow;
            if (main is null) { Log(outDir, "render: no MainWindow"); return; }

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
                        Log(outDir, $"render OK: {key} -> {file}.png (full {fw}x{fh})");
                        continue;
                    }

                    // Render the actual view that is inside the Frame
                    var element = main.CurrentViewElement;
                    if (element is null) { Log(outDir, $"render: {key} no content"); continue; }

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

                    Log(outDir, $"render OK: {key} -> {file}.png ({w}x{h})");
                }
                catch (Exception ex)
                {
                    Log(outDir, $"render FAIL {key}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Log(outDir, "render: done");
            Shutdown();
        }
        catch (Exception ex)
        {
            Log(outDir, $"render fatal: {ex}");
            Shutdown();
        }
    }

    // Render-mode diagnostics go to render.log next to the PNGs
    // (the app no longer produces startup.log at all).
    private static void Log(string outDir, string message)
    {
#if DEBUG
        try { File.AppendAllText(Path.Combine(outDir, "render.log"), $"[{DateTime.Now}] {message}\n"); } catch { }
#endif
    }

    // Helper to get real exe directory even in single-file bundle
    private static string GetAppDirectory()
    {
        return Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
    }
}
