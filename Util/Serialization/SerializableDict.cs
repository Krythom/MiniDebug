using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniDebug.Util.Serialization;

// DO NOT use with types that aren't directly serializable by JsonUtility.ToJson()
// string-type keys are the only exception
[Serializable]
public class SerializableDict<K, V> : Dictionary<K, V>, ISerializationCallbackReceiver where K : class
{
    [SerializeField] private string _r;
    
    public void OnBeforeSerialize()
    {
        SerializableList<Entry> e = new();
        e.AddRange(this.Select(entry => new Entry { k = entry.Key, v = entry.Value }));

        _r = JsonUtility.ToJson(e).ToBase64();
    }

    public void OnAfterDeserialize()
    {
        var e = JsonUtility.FromJson<SerializableList<Entry>>(_r.FromBase64());
        this.Clear();
        foreach (var entry in e)
        {
            this.Add(entry.k, entry.v);
        }
    }

    [Serializable]
    private class Entry : ISerializationCallbackReceiver
    {
        [NonSerialized] public K k;
        [NonSerialized] public V v;
        [SerializeField] private string _k;
        [SerializeField] private string _v;

        public void OnBeforeSerialize()
        {
            _k = typeof(K) == typeof(string) ? k as string : JsonUtility.ToJson(k);
            _v = JsonUtility.ToJson(v);
        }

        public void OnAfterDeserialize()
        {
            k = typeof(K) == typeof(string) ? _k as K : JsonUtility.FromJson<K>(_k);
            v = JsonUtility.FromJson<V>(_v);
        }
    }
}