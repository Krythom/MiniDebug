using System.Collections.Generic;

namespace MiniDebug.Util;

public static class DeconstructExtensions
{
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey first, out TValue second)
    {
        first = pair.Key;
        second = pair.Value;
    }
}