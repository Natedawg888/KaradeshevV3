using System;
using UnityEngine;

[Serializable]
public class SaveData
{
    public string uniqueID;
    public SerializableTransform transformData;
    public string jsonData;
}

[Serializable]
public class SerializableTransform
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;

    public SerializableTransform() { }

    public SerializableTransform(Transform t)
    {
        position = t.position;
        rotation = t.rotation;
        scale = t.localScale;
    }
}