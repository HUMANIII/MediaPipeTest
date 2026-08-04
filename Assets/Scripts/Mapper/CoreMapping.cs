using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Bone
{
    public Vector3 curPos;
    public Transform[] Bones;
    public float minVisibility;
    public float minPresence;

    /*
    public Transform Nose;
    public Transform LeftEye;
    public Transform LeftOuterEye;
    public Transform RightInnerEye;
    public Transform RightEye;
    public Transform RightOuterEye;
    public Transform LeftEar;
    public Transform RightEar;
    public Transform LeftInnerEye;
    public Transform LeftMouth;
    public Transform RightMouth;
    public Transform LeftShoulder;
    public Transform RightShoulder;
    public Transform LeftElow;
    public Transform RightElbow;
    public Transform LeftWrist;
    public Transform RightWrist;
    public Transform LeftPinky;
    public Transform RightPnky;
    public Transform LeftThumbb;
    public Transform RightThumb;
    public Transform LeftHeelOfHand;
    public Transform RightHeelOfHand;
    public Transform LeftHip;
    public Transform RightHip;
    public Transform LeftKnee;
    public Transform RightKnee;
    public Transform LeftAnkle;
    public Transform RightAnkle;
    public Transform LeftHeel;
    public Transform RightHeel;
    public Transform LeftFoot;
    public Transform RightFoot;
    */
    public Bone(Transform[] bones, Vector3 curPos = default, float minVisibility = 0.1f, float minPresence = 0.1f)
    {
        /*
        Nose = bones[0];
        LeftEye = bones[1];
        LeftOuterEye = bones[2];
        RightInnerEye = bones[3];
        RightEye = bones[4];
        RightOuterEye = bones[5];
        LeftEar = bones[6];
        RightEar = bones[7];
        LeftInnerEye = bones[8];
        LeftMouth = bones[9];
        RightMouth = bones[10];
        LeftShoulder = bones[11];
        RightShoulder = bones[12];
        LeftElow = bones[13];
        RightElbow = bones[14];
        LeftWrist = bones[15];
        RightWrist = bones[16];
        LeftPinky = bones[17];
        RightPnky = bones[18];
        LeftThumb = bones[19];
        RightThumb = bones[20];
        LeftHeelOfHand = bones[21];
        RightHeelOfHand = bones[22];
        LeftHip = bones[23];
        RightHip = bones[24];
        LeftKnee = bones[25];
        RightKnee = bones[26];
        LeftAnkle = bones[27];
        RightAnkle = bones[28];
        LeftHeel = bones[29];
        RightHeel = bones[30];
        LeftFoot = bones[31];
        RightFoot = bones[32];
        */
        Bones = bones;
        this.minVisibility = minVisibility;
        this.minPresence = minPresence;
        this.curPos = curPos;
    }


    public void UpdateData(LandmarkData[] data)
    {
        for (int i = 0; i < Bones.Length; i++)
        {
            if (data[i].visibility < 0.1f || Bones[i] == null)
                continue;
            Bones[i].position = new Vector3(-data[i].x, -data[i].y, data[i].z) + curPos;
        }
    }

    public void UpdateData(bool useWorldPos, LandmarkData[] data)
    {
        if (!useWorldPos)
        {
            UpdateData(data);
            return;
        }

        for (int i = 0; i < Bones.Length; i++)
        {
            if (data[i].visibility < 0.1f)
                continue;
            Bones[i].position = new Vector3(-data[i].x, -data[i].y, data[i].z);
        }
    }

    public void UpdatePos(Vector3 pos)
    {
        curPos = pos;
    }
}


public class CoreMapping : GeneralMapping
{
    [SerializeField] private Bone bone;
    private long currentFrameCount = 0;

    public override void UpdateData(string data)
    {
        RawData resultData = JsonUtility.FromJson<RawData>(data);
        if(currentFrameCount < resultData.frameId)
            return;
        currentFrameCount = resultData.frameId;
        bone.UpdateData(resultData.poses[0].poseLandmarks);
    }

    // public void UpdateData(PoseLandmarkerResultData poseLandmarkerResultData)
    // {
    //     bone.UpdateData(poseLandmarkerResultData.pose_world_landmarks[0].ToArray());
    // }
}
