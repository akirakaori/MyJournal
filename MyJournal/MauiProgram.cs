using JournalMaui.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using MyJournal.Services;
using SQLite;
using System.Reflection;

namespace MyJournal
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Load user secrets in debug mode
#if DEBUG
            builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly());
#endif

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

            // Register services
            builder.Services.AddSingleton<DashboardState>();

            builder.Services.AddSingleton<ThemeState>();
            builder.Services.AddSingleton<MyJournal.Services.AppState>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<OTPService>();
            builder.Services.AddSingleton<EmailService>();
            builder.Services.AddSingleton<PinUnlockService>();
            builder.Services.AddSingleton<StreakService>();
            builder.Services.AddSingleton<PdfExportService>();



#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();

            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "journal.db3");

                var journalDb = new JournalDatabases(dbPath);
                _ = journalDb.InitAsync();

                return journalDb;
            });

            builder.Services.AddSingleton(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "journal.db3");

                var calDb = new CalendarDb(dbPath);
                _ = calDb.InitAsync();

                return calDb;
            });

            builder.Services.AddSingleton(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "journal.db3");
                return new SQLiteAsyncConnection(dbPath);
            });

            builder.Services.AddSingleton<CustomTagService>();

            return builder.Build();
        }
    }
}
