using System.Security.Cryptography;

namespace OneSourceTaskScheduler.Security
{
    public static class SecurityUtils
    {
        public static string HashString(params string[] hashParts)
        {
            if (!hashParts.Any())
            {
                return String.Empty;
            }

            using (var sha = SHA256.Create())
            {
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(string.Join('-', hashParts));
                byte[] hashBytes = sha.ComputeHash(textBytes);

                string hash = BitConverter
                    .ToString(hashBytes)
                    .Replace("-", String.Empty);

                return hash;
            }
        }
    }
}
