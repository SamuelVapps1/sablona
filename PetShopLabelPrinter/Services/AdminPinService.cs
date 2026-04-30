using System;
using System.Security.Cryptography;
using System.Text;
using PetShopLabelPrinter.Data;

namespace PetShopLabelPrinter.Services
{
    /// <summary>
    /// Stores admin PIN as PBKDF2 hash + salt in AppSettings. Migrates from legacy plaintext default on first successful login.
    /// </summary>
    public sealed class AdminPinService
    {
        public const int DefaultIterations = 100_000;
        public const int MinPinLength = 4;
        public const int DerivedKeyLengthBytes = 32;

        /// <summary>Default PIN for installs without stored hash; migrated to hash on first successful login.</summary>
        internal const string LegacyDefaultPin = "1234";

        private const string KeySalt = "AdminPinSaltBase64";
        private const string KeyHash = "AdminPinHashBase64";
        private const string KeyIterations = "AdminPinIterations";

        private const int MaxFailuresBeforeLockout = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);

        private readonly Database _db;
        private int _failedAttempts;
        private DateTime _lockoutUntilUtc = DateTime.MinValue;

        public AdminPinService(Database db)
        {
            _db = db;
        }

        public bool IsLockedOut
        {
            get
            {
                if (DateTime.UtcNow < _lockoutUntilUtc) return true;
                if (_lockoutUntilUtc != DateTime.MinValue && DateTime.UtcNow >= _lockoutUntilUtc)
                    _failedAttempts = 0;
                return false;
            }
        }

        public TimeSpan? RemainingLockout
        {
            get
            {
                if (DateTime.UtcNow >= _lockoutUntilUtc) return null;
                return _lockoutUntilUtc - DateTime.UtcNow;
            }
        }

        public bool TryVerify(string pin, out string? errorMessage)
        {
            errorMessage = null;
            if (IsLockedOut)
            {
                errorMessage = $"Príliš veľa neúspešných pokusov. Skúste znova o {Math.Ceiling(RemainingLockout!.Value.TotalSeconds)} s.";
                return false;
            }

            if (string.IsNullOrEmpty(pin))
            {
                RegisterFailure();
                errorMessage = "Zadajte PIN.";
                return false;
            }

            if (HasStoredPin())
            {
                if (!VerifyAgainstStored(pin))
                {
                    RegisterFailure();
                    errorMessage = "Nesprávny PIN.";
                    return false;
                }

                ClearFailures();
                return true;
            }

            // No hash yet: accept only legacy default, then persist hash (transparent migration).
            if (pin != LegacyDefaultPin)
            {
                RegisterFailure();
                errorMessage = "Nesprávny PIN.";
                return false;
            }

            SetPin(pin);
            ClearFailures();
            return true;
        }

        public bool TryChangePin(string oldPin, string newPin, string confirmNewPin, out string? errorMessage)
        {
            errorMessage = null;
            if (newPin != confirmNewPin)
            {
                errorMessage = "Nový PIN a potvrdenie sa nezhodujú.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < MinPinLength)
            {
                errorMessage = $"Nový PIN musí mať aspoň {MinPinLength} znaky.";
                return false;
            }

            if (HasStoredPin())
            {
                if (!VerifyAgainstStored(oldPin))
                {
                    errorMessage = "Súčasný PIN je nesprávny.";
                    return false;
                }
            }
            else
            {
                if (oldPin != LegacyDefaultPin)
                {
                    errorMessage = "Súčasný PIN je nesprávny.";
                    return false;
                }
            }

            SetPin(newPin);
            return true;
        }

        private bool HasStoredPin()
        {
            var salt = _db.GetSetting(KeySalt);
            var hash = _db.GetSetting(KeyHash);
            return !string.IsNullOrWhiteSpace(salt) && !string.IsNullOrWhiteSpace(hash);
        }

        private bool VerifyAgainstStored(string pin)
        {
            var saltB64 = _db.GetSetting(KeySalt);
            var hashB64 = _db.GetSetting(KeyHash);
            var iterStr = _db.GetSetting(KeyIterations);
            if (string.IsNullOrWhiteSpace(saltB64) || string.IsNullOrWhiteSpace(hashB64))
                return false;

            var iterations = int.TryParse(iterStr, out var i) && i > 0 ? i : DefaultIterations;
            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(saltB64);
                expected = Convert.FromBase64String(hashB64);
            }
            catch
            {
                return false;
            }

            if (expected.Length != DerivedKeyLengthBytes)
                return false;

            var actual = DeriveKey(pin, salt, iterations);
            return FixedTimeEquals(actual, expected);
        }

        private void SetPin(string pin)
        {
            var salt = new byte[24];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            var iterations = DefaultIterations;
            var hash = DeriveKey(pin, salt, iterations);

            _db.SetSetting(KeySalt, Convert.ToBase64String(salt));
            _db.SetSetting(KeyHash, Convert.ToBase64String(hash));
            _db.SetSetting(KeyIterations, iterations.ToString());
        }

        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
#pragma warning disable SYSLIB0023 // Rfc2898DeriveBytes is obsolete in newer TFMs; fine for net462
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations);
#pragma warning restore SYSLIB0023
            return pbkdf2.GetBytes(DerivedKeyLengthBytes);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private void RegisterFailure()
        {
            _failedAttempts++;
            if (_failedAttempts >= MaxFailuresBeforeLockout)
            {
                _lockoutUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                _failedAttempts = 0;
            }
        }

        private void ClearFailures()
        {
            _failedAttempts = 0;
            _lockoutUntilUtc = DateTime.MinValue;
        }
    }
}
