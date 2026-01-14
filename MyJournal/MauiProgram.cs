using MyJournal.Services;
using JournalMaui.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

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

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Services.AddMudServices();

            builder.Services.AddSingleton<ThemeState>();
            builder.Services.AddSingleton<MyJournal.Services.AppState>();

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

            return builder.Build();
        }
    }
}
