using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RawData
{
    public string type;
    public long protocolVersion;
    public long frameId;
    public long timestampMs;
    public long mediapipeTimestampMs;
    public bool detected;
    public PoseData[] poses;
    
}

[Serializable]
public class PoseData
{
    public LandmarkData[] poseLandmarks;
    public LandmarkData[] poseWorldLandmarks;
}

[Serializable]
public class LandmarkData
{
    public int id;
    public float x, y, z;
    public float visibility;
    public float presence;
}

public abstract class GeneralMapping : MonoBehaviour
{
    public abstract void UpdateData(string data);
}
