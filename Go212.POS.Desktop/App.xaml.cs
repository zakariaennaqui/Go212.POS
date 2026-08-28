using AppServices = Go212.POS.Application.ApplicationExtensions;  // alias to avoid clash with System.Windows.Application
using Go212.POS.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Windows;

namespace Go212.POS.Desktop;

public partial class App : global::System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // ── 1. Serilog ──────────────────────────────────────────
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "go212_crash.txt");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "go212-pos-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30)
                .CreateLogger();

            Log.Information("GO212 POS starting up — version {Version}", GetType().Assembly.GetName().Version);

            // ── 2. Configuration ────────────────────────────────────
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();

            // ── 3. DI Container ─────────────────────────────────────
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddLogging(logging => logging.AddSerilog(dispose: true));
            services.AddInfrastructure(config);
            AppServices.AddApplication(services);
            services.AddDesktop();
            Services = services.BuildServiceProvider();

            // ── 4. Show Login window ────────────────────────────────
            var loginWindow = Services.GetRequiredService<Views.Login.LoginWindow>();
            MainWindow = loginWindow;
            loginWindow.Show();
            loginWindow.Activate();
            loginWindow.Topmost = true;
            loginWindow.Topmost = false;
            loginWindow.Focus();
        }
        catch (Exception ex)
        {
            // Write full crash info to Desktop so user can see it
            var crashFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "go212_crash.txt");
            File.WriteAllText(crashFile,
                $"GO212 POS CRASH — {DateTime.Now}\r\n\r\n" +
                $"{ex.GetType().FullName}\r\n{ex.Message}\r\n\r\n{ex.StackTrace}\r\n\r\n" +
                $"Inner: {ex.InnerException?.Message}\r\n{ex.InnerException?.StackTrace}");

            MessageBox.Show(
                $"Erreur au démarrage:\n{ex.Message}\n\nDétails dans: {crashFile}",
                "GO212 POS — Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("GO212 POS shutting down.");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private bool _isHandlingCrash;

    private void Application_DispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (_isHandlingCrash) { e.Handled = true; return; }
        _isHandlingCrash = true;

        Log.Fatal(e.Exception, "Unhandled UI exception");
        MessageBox.Show(
            $"Une erreur est survenue:\n{e.Exception.Message}\n\nConsultez les logs pour plus de détails.",
            "GO212 POS — Erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        _isHandlingCrash = false;
    }
}
