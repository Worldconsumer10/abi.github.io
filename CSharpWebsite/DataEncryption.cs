using Nancy.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CSharpWebsite
{
    internal class DataEncryption
    {
        private static string SecretKey { get; } = Environment.GetEnvironmentVariable("WebsiteSecretKey", EnvironmentVariableTarget.Machine) ?? "ea4309d";
        internal static List<string> Encrypt(string text, List<string> keys)
        {
            List<string> encrypted = new List<string>();
            var fragments = ShatterString(text, keys.Count);
            for (int i = 0; i < fragments.Count; i++)
            {
                var frag = fragments[Math.Clamp(i, 0, fragments.Count - 1)];
                var key = keys[Math.Clamp(i, 0, keys.Count - 1)];
                using (Aes aes = Aes.Create())
                {
                    encrypted.Add(EncryptFragment(frag, key));
                }
            }
            return encrypted;
        }
        internal static string Decrypt(List<string> data, List<string> keys)
        {
            List<string> encrypted = new List<string>();
            for (int i = 0; i < data.Count; i++)
            {
                var frag = data[Math.Clamp(i, 0, data.Count - 1)];
                var key = keys[Math.Clamp(i, 0, keys.Count - 1)];
                using (Aes aes = Aes.Create())
                {
                    encrypted.Add(DecryptFragment(frag, key));
                }
            }
            return string.Join("", encrypted);
        }
        private static readonly Encoding encoding = Encoding.UTF8;

        public static string EncryptFragment(string plainText, string key)
        {
            try
            {
                RijndaelManaged aes = new RijndaelManaged();
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;

                aes.Key = encoding.GetBytes(MergeKey(key));
                aes.GenerateIV();

                ICryptoTransform AESEncrypt = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] buffer = encoding.GetBytes(plainText);

                string encryptedText = Convert.ToBase64String(AESEncrypt.TransformFinalBlock(buffer, 0, buffer.Length));

                String mac = "";

                mac = BitConverter.ToString(HmacSHA256(Convert.ToBase64String(aes.IV) + encryptedText, key)).Replace("-", "").ToLower();

                var keyValues = new Dictionary<string, object>
                {
                    { "iv", Convert.ToBase64String(aes.IV) },
                    { "value", encryptedText },
                    { "mac", mac },
                };

                JavaScriptSerializer serializer = new JavaScriptSerializer();

                return Convert.ToBase64String(encoding.GetBytes(serializer.Serialize(keyValues)));
            }
            catch (Exception e)
            {
                throw new Exception($"{e.Message}\n{e.InnerException}\n\n{e.StackTrace}");
            }
        }
        static string MergeKey(string key)
        {
            var str = "";
            for (int i = 0; i < Math.Clamp(key.Length, 0, 32 - SecretKey.Length); i++)
            {
                var character = key[i];
                str += character;
            }
            str += SecretKey;
            return str;
        }
        public static string DecryptFragment(string plainText, string key)
        {
            try
            {
                RijndaelManaged aes = new RijndaelManaged();
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;
                aes.Key = encoding.GetBytes(MergeKey(key));

                // Base 64 decode
                byte[] base64Decoded = Convert.FromBase64String(plainText);
                string base64DecodedStr = encoding.GetString(base64Decoded);

                // JSON Decode base64Str

                var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(base64DecodedStr) ?? new Dictionary<string, string>();

                aes.IV = Convert.FromBase64String(payload["iv"]);

                ICryptoTransform AESDecrypt = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] buffer = Convert.FromBase64String(payload["value"]);

                return encoding.GetString(AESDecrypt.TransformFinalBlock(buffer, 0, buffer.Length));
            }
            catch (Exception) { return "broken"; }
        }

        static byte[] HmacSHA256(string data, string key)
        {
            using (HMACSHA256 hmac = new HMACSHA256(encoding.GetBytes(key)))
            {
                return hmac.ComputeHash(encoding.GetBytes(data));
            }
        }
        internal static List<string> GetRandomString(int string_count, int string_length = 5)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            List<string> allkeys = new List<string>();
            var key = "";
            for (int ci = 0; ci < string_count; ci++)
            {
                for (int i = 0; i < string_length; i++)
                {
                    key += chars[new Random().Next(chars.Length)];
                }
                allkeys.Add(key);
                key = "";
            }
            return allkeys;
        }
        internal static List<string> RandomKeyString(int key_count)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            List<string> allkeys = new List<string>();
            var key = "";
            for (int ci = 0; ci < key_count; ci++)
            {
                for (int i = 0; i < 32 - SecretKey.Length; i++)
                {
                    key += chars[new Random().Next(chars.Length)];
                }
                allkeys.Add(key);
                key = "";
            }
            return allkeys;
        }
        private static List<string> ShatterString(string text, int sections)
        {
            var stringcounts = MathF.Floor(text.Length / sections);
            List<string> fragmented = new List<string>();
            var tstring = "";
            for (int i = 0; i < text.Length; i++)
            {
                if (tstring.Length >= stringcounts) { fragmented.Add(tstring); tstring = ""; }
                tstring += text[i];
            }
            fragmented.Add(tstring);
            return fragmented;
        }
    }
}
