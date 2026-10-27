using System;
using System.Globalization;

namespace AegisWall.Core
{
    public sealed class EnrollResult
    {
        public byte[] VaultKey { get; set; }
        public string RecoveryCode { get; set; }

        public EnrollResult()
        {
            VaultKey = null;
            RecoveryCode = string.Empty;
        }
    }

    public static class KeyVault
    {
        private const int KdfIterations = 200000;

        public static EnrollResult Enroll(IHardwareKey key, AegisConfig config, bool programSlot, bool requireTouch)
        {
            if (key == null)
            {
                throw new HardwareKeyException("No hardware key provider.");
            }

            if (programSlot)
            {
                key.Provision(requireTouch);
            }

            if (!key.IsPresent())
            {
                throw new HardwareKeyException("Hardware key not detected.");
            }

            byte[] challenge = Crypto.RandomBytes(32);
            byte[] response = key.HmacChallenge(challenge);
            byte[] vaultSalt = Crypto.RandomBytes(16);
            byte[] vaultKey = Crypto.RandomBytes(32);
            byte[] kek = Crypto.DeriveKey(Convert.ToBase64String(response), vaultSalt, KdfIterations, 32);

            config.KeyProvider = key.ProviderName;
            config.KeySerial = key.Serial;
            config.KeyChallenge = Convert.ToBase64String(challenge);
            config.KeyEnrolledUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            config.VaultSalt = Convert.ToBase64String(vaultSalt);
            config.VaultBlob = Crypto.Wrap(vaultKey, kek);

            string recoveryCode = Base32.Encode(Crypto.RandomBytes(32));
            byte[] recoverySalt = Crypto.RandomBytes(16);
            byte[] recoveryKek = Crypto.DeriveKey(NormalizeRecoveryCode(recoveryCode), recoverySalt, KdfIterations, 32);
            config.RecoverySalt = Convert.ToBase64String(recoverySalt);
            config.RecoveryBlob = Crypto.Wrap(vaultKey, recoveryKek);

            EnrollResult result = new EnrollResult();
            result.VaultKey = vaultKey;
            result.RecoveryCode = recoveryCode;
            return result;
        }

        public static byte[] Unlock(AegisConfig config, IHardwareKey key)
        {
            if (config == null || key == null || !config.IsKeyEnrolled)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(config.KeySerial) && !string.IsNullOrEmpty(key.Serial)
                && !string.Equals(config.KeySerial, key.Serial, StringComparison.Ordinal))
            {
                return null;
            }

            if (!key.IsPresent())
            {
                return null;
            }

            try
            {
                byte[] challenge = Convert.FromBase64String(config.KeyChallenge);
                byte[] response = key.HmacChallenge(challenge);
                byte[] salt = Convert.FromBase64String(config.VaultSalt);
                byte[] kek = Crypto.DeriveKey(Convert.ToBase64String(response), salt, KdfIterations, 32);
                return Crypto.Unwrap(config.VaultBlob, kek);
            }
            catch
            {
                return null;
            }
        }

        public static byte[] Recover(AegisConfig config, string recoveryCode)
        {
            if (config == null || string.IsNullOrWhiteSpace(recoveryCode) || string.IsNullOrEmpty(config.RecoveryBlob))
            {
                return null;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(config.RecoverySalt);
                byte[] kek = Crypto.DeriveKey(NormalizeRecoveryCode(recoveryCode), salt, KdfIterations, 32);
                return Crypto.Unwrap(config.RecoveryBlob, kek);
            }
            catch
            {
                return null;
            }
        }

        public static string RotateRecovery(AegisConfig config, byte[] vaultKey)
        {
            if (config == null || vaultKey == null)
            {
                return string.Empty;
            }

            string recoveryCode = Base32.Encode(Crypto.RandomBytes(32));
            byte[] recoverySalt = Crypto.RandomBytes(16);
            byte[] recoveryKek = Crypto.DeriveKey(NormalizeRecoveryCode(recoveryCode), recoverySalt, KdfIterations, 32);
            config.RecoverySalt = Convert.ToBase64String(recoverySalt);
            config.RecoveryBlob = Crypto.Wrap(vaultKey, recoveryKek);
            return recoveryCode;
        }

        public static string NormalizeRecoveryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return code.ToUpperInvariant()
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();
        }
    }
}
