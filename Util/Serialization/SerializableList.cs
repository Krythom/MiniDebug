using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;

namespace MiniDebug.Util.Serialization;

[Serializable]
public class SerializableList<T> : List<T>, ISerializationCallbackReceiver
{
    [SerializeField] private string _r;

    public void OnBeforeSerialize()
    {
        _r = ("[" + String.Join(",", this.Select(e => JsonUtility.ToJson(e)).ToArray()) + "]").ToBase64();
    }

    public void OnAfterDeserialize()
    {
        _r = _r.FromBase64();
        if (_r[0] != '[' || _r[_r.Length - 1] != ']')
        {
            throw new SerializationException("Serialized string is not a list");
        }

        int depth = 0;
        int startMark = -1;
        for (int i = 0; i < _r.Length; i++)
        {
            char c = _r[i];
            if (c == '\\')
            {
                c = _r[++i];
            }

            if (c == '{')
            {
                if (depth == 0)
                {
                    startMark = i++;
                }
                depth++;
            } 
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    string s = _r.Substring(startMark, i - startMark + 1);
                    this.Add(JsonUtility.FromJson<T>(s));
                }
            }
        }
    }
}