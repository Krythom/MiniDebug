using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniDebug.Util;

// I hate JsonUtility I hate JsonUtility I hat JsonUtility
[Serializable]
public class EnemyPosition
{
    public const char SEPARATOR = '`';
    
    public string Name;
    public Vector3 Pos;

    public static string serializeList(IEnumerable<EnemyPosition> l)
    {
        return String.Join(SEPARATOR.ToString(), l.Select(e => JsonUtility.ToJson(e, false)).ToArray());
    }

    public static List<EnemyPosition> deserializeList(string serialized)
    {
        if (serialized.Length == 0)
            return new();
        return serialized.Split(SEPARATOR).Select(JsonUtility.FromJson<EnemyPosition>).ToList();
    }
}