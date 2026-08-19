using System;
using UnityEngine;
using Newtonsoft.Json;

public class MotionTracer : GeneralMapping
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int boneCount;
    private GameObject[] bones;

    private void Awake()
    {
        bones = new GameObject[boneCount];
        for (int i = 0; i < boneCount; i++)
        {
            bones[i] = Instantiate(prefab);
            bones[i].SetActive(false);
        }
    }

    public override void UpdateData(string data)
    {
        RawData resultData = JsonConvert.DeserializeObject<RawData>(data);
        if(resultData is not { detected: true } || resultData?.poses?[0].poseLandmarks == null || resultData.poses[0].poseLandmarks.Length == 0)
            return;

        var posData = resultData.poses[0].poseLandmarks;
        for (int i = 0; i < boneCount; i++)
        {
            if (posData[i].visibility < 0.1f)
            {
                bones[i].SetActive(false);
                continue;
            }
            bones[i].SetActive(true);
            bones[i].transform.SetPositionAndRotation(new Vector3(-posData[i].x, -posData[i].y, posData[i].z), Quaternion.identity);
        }
    }
}
