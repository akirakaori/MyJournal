using System;
using Microsoft.Maui.Storage; // Preferences for MAUI

namespace MyJournal.Services
{
    public class AppState
    {
        private const string DarkModeKey = "IsDarkMode";

        public bool IsLoggedIn { get; private set; } = false;
        
        public string Username { get; private set; } = string.Empty;

        // Global dark mode flag
        public bool IsDarkMode { get; private set; }

        public event Action? OnChange;

        public AppState()
        {
            // Load saved preference (default false)
            IsDarkMode = Preferences.Default.Get(DarkModeKey, false);
        }

        public void Login(string username = "")
        {
            IsLoggedIn = true;
            Username = username;
            NotifyStateChanged();
        }

        public void Logout()
        {
            IsLoggedIn = false;
            Username = string.Empty;
            NotifyStateChanged();
        }

        public void ToggleDarkMode()
        {
            IsDarkMode = !IsDarkMode;
            Preferences.Default.Set(DarkModeKey, IsDarkMode);
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
