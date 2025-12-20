public class ThemeState
{
    public bool IsDarkMode { get; private set; }

    public event Action? OnChange;

    public void Toggle()
    {
        IsDarkMode = !IsDarkMode;
        OnChange?.Invoke();
    }

    public void Set(bool value)
    {
        IsDarkMode = value;
        OnChange?.Invoke();
    }
}
