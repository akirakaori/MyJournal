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
    }
}
