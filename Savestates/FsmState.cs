using System;
using Modding;

namespace MiniDebug.Savestates;

[Serializable]
public class FsmState
{
    public string parentName;
    public string fsmName;
    public string stateName;

    public bool waitRealTime;
    public float waitTimer;
    public float waitTime;
    
    public SerializableFloatDictionary fsmFloats = new();
    public SerializableIntDictionary fsmInts = new();
    public SerializableBoolDictionary fsmBools = new();
    public SerializableStringDictionary fsmStrings = new();
    // public SerializableVector2Dictionary fsmVector2s = new();
    // public SerializableVector3Dictionary fsmVector3s = new();
    // public SerializableColorDictionary fsmColors = new();
    // public SerializableRectDictionary fsmRects = new();
    // public SerializableQuaternionDictionary fsmQuaternions = new();
}