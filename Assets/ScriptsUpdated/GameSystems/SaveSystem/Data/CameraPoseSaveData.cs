using System;
using UnityEngine;

[Serializable]
public class CameraPoseSaveData
{
    public Vector3 rigPosition;
    public Quaternion rigRotation;

    public bool hasSeparateMainCameraTransform;
    public Vector3 mainCameraLocalPosition;
    public Quaternion mainCameraLocalRotation;

    public bool cloudsVisible = true;
}