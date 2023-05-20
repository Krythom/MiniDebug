using System;
using System.Text;

namespace MiniDebug.Util;

public static class StringExtensions
{
    public static string ToBase64(this string s)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    public static string FromBase64(this string s)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}