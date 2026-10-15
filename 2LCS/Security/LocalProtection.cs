using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LCS.Security
{
    // DPAPI encryption (current Windows user) of data stored on disk.
    internal static class LocalProtection
    {
        private const string Prefix = "dpapi1:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("2LCS local data protection");

        public static string DataFolder { get; internal set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "2LCS");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            return Prefix + Convert.ToBase64String(ProtectBytes(Encoding.UTF8.GetBytes(plainText)));
        }

        public static string Unprotect(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue)) return string.Empty;
            if (!IsProtected(storedValue)) return storedValue;
            try
            {
                return Encoding.UTF8.GetString(UnprotectBytes(Convert.FromBase64String(storedValue.Substring(Prefix.Length))));
            }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
            {
                return string.Empty;
            }
        }

        public static bool IsProtected(string storedValue) => storedValue != null && storedValue.StartsWith(Prefix, StringComparison.Ordinal);

        public static byte[] ProtectBytes(byte[] data) => ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);

        public static byte[] UnprotectBytes(byte[] data) => ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);

        public static void WriteProtectedFile(string path, string plainText)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, ProtectBytes(Encoding.UTF8.GetBytes(plainText)));
            File.Move(temp, path, true);
        }

        public static string ReadProtectedFile(string path)
        {
            return Encoding.UTF8.GetString(UnprotectBytes(File.ReadAllBytes(path)));
        }
    }
}
