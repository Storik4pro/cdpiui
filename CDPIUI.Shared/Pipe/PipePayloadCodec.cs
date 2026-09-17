using System;
using System.Text;

namespace CDPIUI.Shared.Pipe
{
    /// <summary>
    /// Encodes arbitrary pipe payloads as URL-safe Base64. Pipe messages are URI-shaped,
    /// so regular Base64 characters such as '+' cannot be transported without escaping.
    /// </summary>
    public static class PipePayloadCodec
    {
        public static string Encode(string value) =>
            Encode(Encoding.UTF8.GetBytes(value ?? string.Empty));

        public static string Encode(byte[] value) => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        public static string DecodeString(string value) =>
            Encoding.UTF8.GetString(Decode(value));

        public static byte[] Decode(string value)
        {
            string base64 = (value ?? string.Empty)
                .Replace('-', '+')
                .Replace('_', '/');

            int padding = base64.Length % 4;
            if (padding != 0)
            {
                base64 = base64.PadRight(base64.Length + (4 - padding), '=');
            }

            return Convert.FromBase64String(base64);
        }
    }
}
