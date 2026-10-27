using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AegisWall.Core
{
    public static class Crypto
    {
        private const int IvSize = 16;
        private const int MacSize = 32;

        public static byte[] RandomBytes(int length)
        {
            byte[] data = new byte[length];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }
            return data;
        }

        public static string RandomHex(int bytes)
        {
            return ToHex(RandomBytes(bytes));
        }

        public static byte[] Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(data);
            }
        }

        public static string Sha256Hex(string text)
        {
            return ToHex(Sha256(Encoding.UTF8.GetBytes(text ?? string.Empty)));
        }

        public static string Sha256Hex(byte[] data)
        {
            return ToHex(Sha256(data));
        }

        public static byte[] HmacSha256(byte[] key, byte[] data)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        public static byte[] HmacSha1(byte[] key, byte[] data)
        {
            using (HMACSHA1 hmac = new HMACSHA1(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        public static string HmacSha256Base64(byte[] key, string text)
        {
            return Convert.ToBase64String(HmacSha256(key, Encoding.UTF8.GetBytes(text ?? string.Empty)));
        }

        public static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }

        public static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null)
            {
                return false;
            }
            return FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
        }

        public static byte[] DeriveKey(string secret, byte[] salt, int iterations, int length)
        {
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(secret, salt, iterations))
            {
                return derive.GetBytes(length);
            }
        }

        public static string Wrap(byte[] plaintext, byte[] keyMaterial)
        {
            byte[] encKey = HmacSha256(keyMaterial, Encoding.UTF8.GetBytes("aegiswall/enc"));
            byte[] macKey = HmacSha256(keyMaterial, Encoding.UTF8.GetBytes("aegiswall/mac"));
            byte[] iv = RandomBytes(IvSize);

            byte[] cipher;
            using (AesManaged aes = new AesManaged())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    cipher = transform.TransformFinalBlock(plaintext, 0, plaintext.Length);
                }
            }

            byte[] macInput = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, macInput, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, macInput, iv.Length, cipher.Length);
            byte[] mac = HmacSha256(macKey, macInput);

            return "v1|" + Convert.ToBase64String(iv) + "|" + Convert.ToBase64String(cipher) + "|" + Convert.ToBase64String(mac);
        }

        public static byte[] Unwrap(string blob, byte[] keyMaterial)
        {
            if (string.IsNullOrWhiteSpace(blob))
            {
                return null;
            }

            string[] parts = blob.Split('|');
            if (parts.Length != 4 || parts[0] != "v1")
            {
                return null;
            }

            byte[] iv = Convert.FromBase64String(parts[1]);
            byte[] cipher = Convert.FromBase64String(parts[2]);
            byte[] mac = Convert.FromBase64String(parts[3]);
            if (iv.Length != IvSize || mac.Length != MacSize)
            {
                return null;
            }

            byte[] encKey = HmacSha256(keyMaterial, Encoding.UTF8.GetBytes("aegiswall/enc"));
            byte[] macKey = HmacSha256(keyMaterial, Encoding.UTF8.GetBytes("aegiswall/mac"));
            byte[] macInput = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, macInput, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, macInput, iv.Length, cipher.Length);
            byte[] expected = HmacSha256(macKey, macInput);
            if (!FixedTimeEquals(expected, mac))
            {
                return null;
            }

            try
            {
                using (AesManaged aes = new AesManaged())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encKey;
                    aes.IV = iv;
                    using (ICryptoTransform transform = aes.CreateDecryptor())
                    {
                        return transform.TransformFinalBlock(cipher, 0, cipher.Length);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string ToHex(byte[] data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                builder.Append(data[i].ToString("x2"));
            }
            return builder.ToString();
        }

        public static byte[] FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return new byte[0];
            }

            hex = hex.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
            if (hex.Length % 2 != 0)
            {
                throw new FormatException("Hex length must be even.");
            }

            byte[] data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return data;
        }

        public static string Base64(byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        public static byte[] FromBase64(string value)
        {
            return Convert.FromBase64String(value);
        }
    }

    public static class Base32
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Encode(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0;
            int bitsLeft = 0;

            for (int i = 0; i < data.Length; i++)
            {
                buffer = (buffer << 8) | data[i];
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    int index = (buffer >> (bitsLeft - 5)) & 31;
                    result.Append(Alphabet[index]);
                    bitsLeft -= 5;
                }
            }

            if (bitsLeft > 0)
            {
                int index = (buffer << (5 - bitsLeft)) & 31;
                result.Append(Alphabet[index]);
            }

            string raw = result.ToString();
            StringBuilder grouped = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % 5 == 0)
                {
                    grouped.Append('-');
                }
                grouped.Append(raw[i]);
            }
            return grouped.ToString();
        }

        public static byte[] Decode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new byte[0];
            }

            string normalized = value.ToUpperInvariant()
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("O", "0")
                .Replace("I", "1")
                .Replace("L", "1");

            int buffer = 0;
            int bitsLeft = 0;
            using (MemoryStream output = new MemoryStream())
            {
                for (int i = 0; i < normalized.Length; i++)
                {
                    int index = Alphabet.IndexOf(normalized[i]);
                    if (index < 0)
                    {
                        throw new FormatException("Invalid recovery code character.");
                    }

                    buffer = (buffer << 5) | index;
                    bitsLeft += 5;
                    if (bitsLeft >= 8)
                    {
                        output.WriteByte((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                        bitsLeft -= 8;
                    }
                }
                return output.ToArray();
            }
        }
    }
}
