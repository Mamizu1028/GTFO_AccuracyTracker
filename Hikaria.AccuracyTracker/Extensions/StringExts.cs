using System.Text.RegularExpressions;

namespace Hikaria.AccuracyTracker.Extensions
{
    internal static class StringExts
    {
        public static string RemoveTMPTags(this string richTextString)
        {
            string plainText = Regex.Replace(richTextString, "<.*?>", string.Empty);

            return plainText;
        }
    }
}
