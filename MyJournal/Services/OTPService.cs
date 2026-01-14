using System.Security.Cryptography;

namespace MyJournal.Services
{
    /// <summary>
    /// Manages OTP generation, storage, validation, and expiry
    /// </summary>
    public class OTPService
    {
        private const string OTP_HASH_KEY = "user_otp_hash";
        private const string OTP_CREATED_AT_KEY = "user_otp_createdat";
        private const int OTP_EXPIRY_MINUTES = 10;

        /// <summary>
        /// Generates a 6-digit OTP and stores it securely with timestamp
        /// </summary>
        public async Task<string> GenerateOTPAsync()
        {
            try
            {
                // Generate 6-digit OTP
                var otp = GenerateRandomOTP();

                // Hash the OTP before storing
                var otpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 12);

                // Store hashed OTP and creation timestamp
                await SecureStorage.Default.SetAsync(OTP_HASH_KEY, otpHash);
                await SecureStorage.Default.SetAsync(OTP_CREATED_AT_KEY, DateTime.UtcNow.ToString("o")); // ISO 8601 format

                return otp; // Return plaintext OTP for email sending (only time it's in plaintext)
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates the provided OTP and checks expiry
        /// </summary>
        public async Task<(bool isValid, string errorMessage)> ValidateOTPAsync(string otp)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6 || !otp.All(char.IsDigit))
                    return (false, "Invalid OTP format");

                var storedOtpHash = await SecureStorage.Default.GetAsync(OTP_HASH_KEY);
                var createdAtStr = await SecureStorage.Default.GetAsync(OTP_CREATED_AT_KEY);

                if (string.IsNullOrEmpty(storedOtpHash) || string.IsNullOrEmpty(createdAtStr))
                    return (false, "No OTP found. Please request a new one.");

                // Check expiry
                if (!DateTime.TryParse(createdAtStr, out DateTime createdAt))
                    return (false, "Invalid OTP timestamp");

                var expiryTime = createdAt.AddMinutes(OTP_EXPIRY_MINUTES);
                if (DateTime.UtcNow > expiryTime)
                    return (false, "OTP has expired. Please request a new one.");

                // Verify OTP
                bool isValid = BCrypt.Net.BCrypt.Verify(otp, storedOtpHash);
                return isValid 
                    ? (true, string.Empty) 
                    : (false, "Invalid OTP. Please try again.");
            }
            catch (Exception ex)
            {
                return (false, $"Error validating OTP: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the stored OTP and timestamp
        /// </summary>
        public async Task ClearOTPAsync()
        {
            try
            {
                SecureStorage.Default.Remove(OTP_HASH_KEY);
                SecureStorage.Default.Remove(OTP_CREATED_AT_KEY);
                await Task.CompletedTask;
            }
            catch
            {
                // Ignore errors when clearing
            }
        }

        /// <summary>
        /// Generates a random 6-digit OTP
        /// </summary>
        private string GenerateRandomOTP()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[4];
                rng.GetBytes(randomBytes);
                int randomNumber = Math.Abs(BitConverter.ToInt32(randomBytes, 0));
                return (randomNumber % 1000000).ToString("D6"); // Ensures 6 digits with leading zeros
            }
        }
    }
}
