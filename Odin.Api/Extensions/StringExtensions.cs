using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Odin.Api.Extensions
{
    /// <summary>
    /// Extension methods for string sanitization and validation.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Sanitizes a filename by removing dangerous characters and path traversal attempts.
        /// </summary>
        public static string SanitizeFilename(this string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return "file";

            // Remove path separators and dangerous characters
            var sanitized = Regex.Replace(filename, @"[\/\\<>:""|?*\x00-\x1f]", "")
                .TrimStart('.');

            // Limit length
            if (sanitized.Length > 255)
            {
                var ext = Path.GetExtension(sanitized);
                sanitized = sanitized.Substring(0, 255 - ext.Length) + ext;
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
        }

        /// <summary>
        /// HTML-encodes a string to prevent XSS attacks.
        /// </summary>
        public static string HtmlEncode(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return System.Net.WebUtility.HtmlEncode(input);
        }

        /// <summary>
        /// Truncates a string to a maximum length, appending ellipsis if truncated.
        /// </summary>
        public static string Truncate(this string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Validates that a string contains only safe characters (alphanumeric and common safe characters).
        /// </summary>
        public static bool IsSafeString(this string input, bool allowSpaces = true)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var pattern = allowSpaces
                ? @"^[a-zA-Z0-9\s\-_.,!?()]+$"
                : @"^[a-zA-Z0-9\-_.,!?()]+$";

            return Regex.IsMatch(input, pattern);
        }
    }
}
