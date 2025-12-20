using MyJournal.Services;
using JournalMaui.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using MyJournal;

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
            //builder.Services.AddSingleton<AppState>();
            builder.Services.AddMudServices();
            builder.Services.AddSingleton<ThemeState>();

            builder.Services.AddSingleton<MyJournal.Services.AppState>();
            builder.Services.AddSingleton<JournalDatabases>();






            builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "journal.db3");
                var db = new JournalDatabases(dbPath);
                _ = db.InitAsync(); // fire once (safe), no UI thread blocking
                return db;
            });

            return builder.Build();
        }
    }
}
