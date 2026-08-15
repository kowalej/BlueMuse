using System;

namespace BlueMuse.Helpers
{
    public static class Extensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            if (string.IsNullOrEmpty(source))
                return false;
            if (string.IsNullOrEmpty(toCheck))
                return true;

            return source.IndexOf(toCheck, comp) >= 0;
        }
    }
}
