using System.Globalization;
using System.Text;

namespace Avatar5.Gameplay.Session
{
    /// <summary>Normalisation of invite codes typed or pasted by players.</summary>
    public static class JoinCodes
    {
        public const int MinLength = 4;
        public const int MaxLength = 16;

        /// <summary>Trims, removes spaces/dashes and upper-cases. Returns false for anything that is not a plausible code.</summary>
        public static bool TryNormalizeServiceCode(string raw, out string code)
        {
            code = null;
            if (string.IsNullOrEmpty(raw)) return false;
            var builder = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c == ' ' || c == '-' || c == '\t') continue;
                if (!IsAsciiLetterOrDigit(c)) return false;
                builder.Append(char.ToUpperInvariant(c));
            }
            if (builder.Length < MinLength || builder.Length > MaxLength) return false;
            code = builder.ToString();
            return true;
        }

        /// <summary>
        /// Direct-connection code for LAN/local tests: "host:port" or just "host" (default port).
        /// Host is an IPv4 address or "localhost".
        /// </summary>
        public static bool TryParseDirect(string raw, ushort defaultPort, out string address, out ushort port)
        {
            address = null;
            port = defaultPort;
            if (string.IsNullOrEmpty(raw)) return false;
            var text = raw.Trim();
            var colon = text.LastIndexOf(':');
            if (colon >= 0)
            {
                int parsedPort;
                if (!int.TryParse(text.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out parsedPort)
                    || parsedPort < 1 || parsedPort > ushort.MaxValue)
                {
                    return false;
                }
                port = (ushort)parsedPort;
                text = text.Substring(0, colon);
            }

            if (string.Equals(text, "localhost", System.StringComparison.OrdinalIgnoreCase))
            {
                address = "127.0.0.1";
                return true;
            }
            if (!IsIPv4(text)) return false;
            address = text;
            return true;
        }

        private static bool IsIPv4(string text)
        {
            var parts = text.Split('.');
            if (parts.Length != 4) return false;
            foreach (var part in parts)
            {
                int value;
                if (part.Length == 0 || part.Length > 3
                    || !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value > 255)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsAsciiLetterOrDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
    }
}
