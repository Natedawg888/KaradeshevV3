using System;
using UnityEngine;

public class Saveable : MonoBehaviour
{
    [Header("Save Settings")]
    public string uniqueID;

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(uniqueID))
        {
            uniqueID = Guid.NewGuid().ToString();
        }
    }

    public virtual SaveData SaveState()
    {
        SaveData data = new SaveData
        {
            uniqueID = uniqueID,
            transformData = new SerializableTransform(transform),
            jsonData = string.Empty
        };

        return data;
    }

    public virtual void LoadState(SaveData data)
    {
        if (data == null)
            return;

        uniqueID = data.uniqueID;

        if (data.transformData != null)
        {
            transform.position = data.transformData.position;
            transform.rotation = data.transformData.rotation;
            transform.localScale = data.transformData.scale;
        }
    }
}