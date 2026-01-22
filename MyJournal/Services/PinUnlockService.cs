namespace JournalMaui.Services;

public class PinUnlockService
{
    private readonly Dictionary<string, DateTime> _unlockedUntil = new();

    public void Unlock(string dateKey, TimeSpan ttl)
    {
        _unlockedUntil[dateKey] = DateTime.UtcNow.Add(ttl);
    }

    public bool IsUnlocked(string dateKey)
    {
        if (string.IsNullOrWhiteSpace(dateKey)) return false;

        if (_unlockedUntil.TryGetValue(dateKey, out var untilUtc))
        {
            if (DateTime.UtcNow <= untilUtc) return true;

            // expired -> cleanup
            _unlockedUntil.Remove(dateKey);
        }

        return false;
    }

    public void Lock(string dateKey)
    {
        if (!string.IsNullOrWhiteSpace(dateKey))
            _unlockedUntil.Remove(dateKey);
    }

    public void ClearAll() => _unlockedUntil.Clear();
}
