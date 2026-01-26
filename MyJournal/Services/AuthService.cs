using System.Security.Cryptography;
using System.Text;

namespace MyJournal.Services
{
    /// <summary>
    /// Handles authentication operations including PIN validation, hashing, and password reset
    /// </summary>
    public class AuthService
    {
    private const string USERNAME_KEY = "user_username";
    private const string EMAIL_KEY = "user_email";
    private const string PIN_HASH_KEY = "user_pin_hash";
    private const string REGISTRATION_DATE_KEY = "user_registration_date";

        /// <summary>
        /// Checks if a user account exists
        /// </summary>
        public async Task<bool> UserExistsAsync()
        {
            return await Task.Run(() =>
            {
                var username = Preferences.Default.Get(USERNAME_KEY, string.Empty);
                return !string.IsNullOrEmpty(username);
            });
        }

        /// <summary>
        /// Registers a new user with username, email, and PIN
        /// </summary>
        public async Task<bool> RegisterUserAsync(string username, string email, string pin)
        {
            try
            {
                // Prevent multiple user creation - single-user app
                if (await UserExistsAsync())
                    return false;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pin))
                    return false;

                if (pin.Length != 4 || !pin.All(char.IsDigit))
                    return false;

                await Task.Run(() =>
                {
                    // Store username and email in Preferences
                    Preferences.Default.Set(USERNAME_KEY, username);
                    Preferences.Default.Set(EMAIL_KEY, email);

                    // Hash PIN and store in SecureStorage
                    string hashedPin = BCrypt.Net.BCrypt.HashPassword(pin, workFactor: 12);
                    SecureStorage.Default.SetAsync(PIN_HASH_KEY, hashedPin).Wait();
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates user credentials (username and PIN)
        /// </summary>
        public async Task<bool> ValidateCredentialsAsync(string username, string pin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(pin))
                    return false;

                if (pin.Length != 4 || !pin.All(char.IsDigit))
                    return false;

                var storedUsername = Preferences.Default.Get(USERNAME_KEY, string.Empty);
                if (storedUsername != username)
                    return false;

                var storedPinHash = await SecureStorage.Default.GetAsync(PIN_HASH_KEY);
                if (string.IsNullOrEmpty(storedPinHash))
                    return false;

                return BCrypt.Net.BCrypt.Verify(pin, storedPinHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates if the provided email matches the stored email
        /// </summary>
        public async Task<bool> ValidateEmailAsync(string email)
        {
            return await Task.Run(() =>
            {
                var storedEmail = Preferences.Default.Get(EMAIL_KEY, string.Empty);
                return !string.IsNullOrEmpty(storedEmail) && storedEmail.Equals(email, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Gets the stored email address
        /// </summary>
        public async Task<string> GetEmailAsync()
        {
            return await Task.Run(() => Preferences.Default.Get(EMAIL_KEY, string.Empty));
        }

        /// <summary>
        /// Gets the stored email address (alias for compatibility)
        /// </summary>
        public async Task<string> GetStoredEmailAsync()
        {
            return await GetEmailAsync();
        }

        /// <summary>
        /// Gets the stored username
        /// </summary>
        public async Task<string> GetUsernameAsync()
        {
            return await Task.Run(() => Preferences.Default.Get(USERNAME_KEY, string.Empty));
        }

        /// <summary>
        /// Resets the user's PIN after OTP validation
        /// </summary>
        public async Task<bool> ResetPinAsync(string newPin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newPin))
                    return false;

                if (newPin.Length != 4 || !newPin.All(char.IsDigit))
                    return false;

                // Hash new PIN and update SecureStorage
                string hashedPin = BCrypt.Net.BCrypt.HashPassword(newPin, workFactor: 12);
                await SecureStorage.Default.SetAsync(PIN_HASH_KEY, hashedPin);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates the user's PIN (alias for ResetPinAsync for compatibility)
        /// </summary>
        public async Task<bool> UpdatePinAsync(string newPin)
        {
            return await ResetPinAsync(newPin);
        }

        /// <summary>
        /// Gets the registration date
        /// </summary>
        public async Task<DateTime> GetRegistrationDateAsync()
        {
            return await Task.Run(() =>
            {
                var dateStr = Preferences.Default.Get(REGISTRATION_DATE_KEY, string.Empty);
                if (string.IsNullOrEmpty(dateStr))
                {
                    // Fallback for existing users: set to 30 days ago or current date
                    var fallbackDate = DateTime.UtcNow.AddDays(-30);
                    Preferences.Default.Set(REGISTRATION_DATE_KEY, fallbackDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    return fallbackDate;
                }

                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var result))
                {
                    return result;
                }

                return DateTime.UtcNow;
            });
        }

        /// <summary>
        /// Changes the user's PIN/password with validation
        /// </summary>
        public async Task<bool> ChangePinAsync(string currentPin, string newPin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currentPin) || string.IsNullOrWhiteSpace(newPin))
                    return false;

                // Validate PIN format (4 digits)
                if (currentPin.Length != 4 || !currentPin.All(char.IsDigit))
                    return false;

                if (newPin.Length != 4 || !newPin.All(char.IsDigit))
                    return false;

                // Verify current PIN is correct
                var storedPinHash = await SecureStorage.Default.GetAsync(PIN_HASH_KEY);
                if (string.IsNullOrEmpty(storedPinHash))
                    return false;

                if (!BCrypt.Net.BCrypt.Verify(currentPin, storedPinHash))
                    return false;

                // Verify new PIN is different from current
                if (BCrypt.Net.BCrypt.Verify(newPin, storedPinHash))
                    return false; // New PIN is the same as current

                // Hash new PIN and update SecureStorage
                string newHashedPin = BCrypt.Net.BCrypt.HashPassword(newPin, workFactor: 12);
                await SecureStorage.Default.SetAsync(PIN_HASH_KEY, newHashedPin);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Logs out from all devices by clearing all stored authentication data
        /// </summary>
        public async Task LogoutAllDevicesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Clear all authentication data
                    SecureStorage.Default.Remove(PIN_HASH_KEY);
                    Preferences.Default.Remove(USERNAME_KEY);
                    Preferences.Default.Remove(EMAIL_KEY);
                    Preferences.Default.Remove(REGISTRATION_DATE_KEY);
                    Preferences.Default.Remove("IsDarkMode");
                });
            }
            catch
            {
                // Continue even if clear fails
            }
        }

        /// <summary>
        /// Updates user's display name and email
        /// </summary>
        public async Task<bool> UpdateUserInfoAsync(string username, string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
                    return false;

                await Task.Run(() =>
                {
                    Preferences.Default.Set(USERNAME_KEY, username);
                    Preferences.Default.Set(EMAIL_KEY, email);
                });

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
