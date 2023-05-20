using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using Modding;

namespace MiniDebug.Util;

public static class FsmVariableHelper
{
    public static TDict ToDict<TDict, TVal>(IEnumerable<NamedVariable> vars) where TDict : SerializableDictionary<string, TVal>, new()
    {
        TDict res = new TDict();
        foreach (var v in vars)
        {
            try
            {
                res.Add(v.Name, (TVal)v.RawValue);
            }
            catch (ArgumentException) {}
        }

        return res;
    }

    public static void FromDict<T>(SerializableDictionary<string, T> values, FsmVariables vars)
    {
        foreach (var entry in values)
        {
            var v = vars.GetVariable(entry.Key);
            if (v == null) continue;
            
            v.RawValue = entry.Value;
        }
    }
}