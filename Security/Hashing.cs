using System.Text;

namespace Netamite.Security {
    public class Hashing {
        public static string SHA256(string value) {
            if(value == null || value.Length == 0) return value;

            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(value));
            foreach(byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static string SHA1(string value) {
            if(value == null || value.Length == 0) return value;

            var crypt = new System.Security.Cryptography.SHA1Managed();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(value));
            foreach(byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static string MD5(string value) {
            if(value == null || value.Length == 0) return value;

            var crypt = new System.Security.Cryptography.MD5CryptoServiceProvider();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(value));
            foreach(byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }
    }
}
