using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class ModelMapping : GeneralMapping
{
    [SerializeField] private Animator animator;

    // MediaPipe 엉덩이 중앙 원점을 배치할 위치
    [SerializeField] private Transform trackingOrigin;

    [SerializeField] private float positionScale = 1f;
    [SerializeField] private float smoothing = 15f;
    [SerializeField] private float minConfidence = 0.5f;

    [Header("Body Calibration")]
    [Tooltip("버튼을 누른 뒤 캘리브레이션 측정을 시작하기까지 기다릴 시간입니다.")]
    [SerializeField, Min(0f)] private float calibrationDelaySeconds = 3f;

    [Tooltip("체격 스케일 계산에 사용할 유효 프레임 수입니다.")]
    [SerializeField, Min(1)] private int calibrationSampleFrames = 30;

    [Tooltip("어깨와 골반 랜드마크가 이 값 이상일 때만 캘리브레이션에 사용합니다.")]
    [SerializeField, Range(0f, 1f)] private float calibrationConfidence = 0.7f;

    [Tooltip("유효한 자세를 기다리는 최대 시간입니다.")]
    [SerializeField, Min(1f)] private float calibrationTimeoutSeconds = 10f;

    private long lastFrameId = -1;
    private bool initialized;

    private readonly List<float> calibrationScaleSamples = new();
    private Coroutine calibrationCoroutine;
    private bool isCollectingCalibration;

    public bool IsCalibrated { get; private set; }
    public bool IsCalibrationRunning => calibrationCoroutine != null;

    private Vector3 leftHand;
    private Vector3 rightHand;
    private Vector3 leftFoot;
    private Vector3 rightFoot;

    private Vector3 leftElbow;
    private Vector3 rightElbow;
    private Vector3 leftKnee;
    private Vector3 rightKnee;

    //90줄 근처에서 말한 이유때문에 이것도 주석처리함
    // private float leftHandWeight;
    // private float rightHandWeight;
    // private float leftFootWeight;
    // private float rightFootWeight;

    public override void UpdateData(string json)
    {
        RawData frame = JsonConvert.DeserializeObject<RawData>(json);

        if (frame is not { type: "pose" } ||
            !frame.detected ||
            frame.poses == null ||
            frame.poses.Length == 0 ||
            frame.frameId <= lastFrameId)
            return;

        LandmarkData[] points = frame.poses[0].poseWorldLandmarks;

        if (points == null || points.Length < 33)
            return;

        lastFrameId = frame.frameId;

        // 버튼으로 시작된 캘리브레이션 중일 때만 현재 프레임을 측정에 사용합니다.
        if (isCollectingCalibration)
            AddCalibrationSample(points);

        float t = initialized
            ? 1f - Mathf.Exp(-smoothing * Time.deltaTime)
            : 1f;

        CalculateArms(points, t);

        leftFoot  = Vector3.Lerp(leftFoot,  ToUnityPosition(points[27]), t);
        rightFoot = Vector3.Lerp(rightFoot, ToUnityPosition(points[28]), t);

        leftKnee  = Vector3.Lerp(leftKnee,  ToUnityPosition(points[25]), t);
        rightKnee = Vector3.Lerp(rightKnee, ToUnityPosition(points[26]), t);

        // 원래 그 감지 관련해서 그 감지 값을 기반으로 웨이틀르 줬는데 그렇게하면 전신 기준으로
        // 디버그가 어려워져서 그냥 안쓰게 됨 그러므로 주석으로 처리함 호옥시 나중에 필요할지도?
        // leftHandWeight  = ChainWeight(points[11], points[13], points[15]);
        // rightHandWeight = ChainWeight(points[12], points[14], points[16]);
        // leftFootWeight  = ChainWeight(points[23], points[25], points[27]);
        // rightFootWeight = ChainWeight(points[24], points[26], points[28]);

        initialized = true;
    }

    #region 캘리브레이션용
    /// <summary>
    /// Unity UI Button의 OnClick에 연결할 메서드입니다.
    /// 호출하면 카운트다운 후 유효 프레임을 모아 체격 스케일을 계산합니다.
    /// </summary>
    public void StartCalibration()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("비활성화된 ModelMapping에서는 캘리브레이션을 시작할 수 없습니다.");
            return;
        }

        if (animator == null || !animator.isHuman)
        {
            Debug.LogError(
                "체격 캘리브레이션에는 유효한 Humanoid Animator가 필요합니다."
            );
            return;
        }

        if (trackingOrigin == null)
        {
            Debug.LogError("Tracking Origin이 연결되지 않았습니다.");
            return;
        }

        Vector3 originScale = trackingOrigin.lossyScale;
        if (Mathf.Abs(originScale.x - 1f) > 0.01f ||
            Mathf.Abs(originScale.y - 1f) > 0.01f ||
            Mathf.Abs(originScale.z - 1f) > 0.01f)
        {
            Debug.LogWarning(
                $"Tracking Origin의 스케일이 1이 아닙니다: {originScale}. " +
                "IK 위치가 이 스케일만큼 다시 확대될 수 있습니다."
            );
        }

        // 버튼을 연속으로 눌렀다면 이전 요청을 취소하고 카운트다운부터 다시 시작합니다.
        if (calibrationCoroutine != null)
            StopCoroutine(calibrationCoroutine);

        isCollectingCalibration = false;
        calibrationScaleSamples.Clear();
        calibrationCoroutine = StartCoroutine(CalibrationRoutine());
    }

    /// <summary>
    /// 진행 중인 카운트다운 또는 캘리브레이션을 취소합니다.
    /// </summary>
    public void CancelCalibration()
    {
        if (calibrationCoroutine != null)
            StopCoroutine(calibrationCoroutine);

        calibrationCoroutine = null;
        isCollectingCalibration = false;
        calibrationScaleSamples.Clear();
        Debug.Log("체격 캘리브레이션을 취소했습니다.");
    }



    
    

    
    private IEnumerator CalibrationRoutine()
    {
        float measurementStartTime =
            Time.realtimeSinceStartup + calibrationDelaySeconds;
        int previousRemainingSeconds = -1;

        // timeScale이 0이어도 동작하도록 실시간 기준으로 카운트다운합니다.
        while (Time.realtimeSinceStartup < measurementStartTime)
        {
            int remainingSeconds = Mathf.CeilToInt(
                measurementStartTime - Time.realtimeSinceStartup
            );

            if (remainingSeconds != previousRemainingSeconds)
            {
                previousRemainingSeconds = remainingSeconds;
                Debug.Log($"체격 캘리브레이션까지 {remainingSeconds}초");
            }

            yield return null;
        }

        Debug.Log(
            "체격 캘리브레이션을 시작합니다. " +
            "어깨와 골반이 보이도록 정면을 보고 잠시 움직이지 마세요."
        );

        isCollectingCalibration = true;
        float timeoutAt =
            Time.realtimeSinceStartup + calibrationTimeoutSeconds;

        while (calibrationScaleSamples.Count < calibrationSampleFrames &&
               Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        isCollectingCalibration = false;

        if (calibrationScaleSamples.Count >= calibrationSampleFrames)
        {
            CompleteCalibration();
        }
        else
        {
            Debug.LogWarning(
                "체격 캘리브레이션에 실패했습니다. " +
                $"유효 프레임 {calibrationScaleSamples.Count}/{calibrationSampleFrames}. " +
                "어깨와 골반이 모두 카메라에 보이는지 확인하세요."
            );
        }

        calibrationCoroutine = null;
    }

    private void AddCalibrationSample(LandmarkData[] points)
    {
        LandmarkData leftShoulder = points[11];
        LandmarkData rightShoulder = points[12];
        LandmarkData leftHip = points[23];
        LandmarkData rightHip = points[24];

        if (!IsReliableForCalibration(leftShoulder) ||
            !IsReliableForCalibration(rightShoulder) ||
            !IsReliableForCalibration(leftHip) ||
            !IsReliableForCalibration(rightHip))
        {
            return;
        }

        Transform avatarLeftShoulder =
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform avatarRightShoulder =
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform avatarLeftHip =
            animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform avatarRightHip =
            animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

        if (avatarLeftShoulder == null ||
            avatarRightShoulder == null ||
            avatarLeftHip == null ||
            avatarRightHip == null)
        {
            Debug.LogError(
                "Humanoid 어깨 또는 골반 본을 찾지 못해 체격을 계산할 수 없습니다."
            );
            isCollectingCalibration = false;
            return;
        }

        Vector3 trackedLeftShoulder = RawVector(leftShoulder);
        Vector3 trackedRightShoulder = RawVector(rightShoulder);
        Vector3 trackedLeftHip = RawVector(leftHip);
        Vector3 trackedRightHip = RawVector(rightHip);

        Vector3 trackedShoulderCenter =
            (trackedLeftShoulder + trackedRightShoulder) * 0.5f;
        Vector3 trackedHipCenter =
            (trackedLeftHip + trackedRightHip) * 0.5f;

        float trackedShoulderWidth = Vector3.Distance(
            trackedLeftShoulder,
            trackedRightShoulder
        );
        float trackedTorsoLength = Vector3.Distance(
            trackedShoulderCenter,
            trackedHipCenter
        );

        Vector3 avatarShoulderCenter =
            (avatarLeftShoulder.position + avatarRightShoulder.position) * 0.5f;
        Vector3 avatarHipCenter =
            (avatarLeftHip.position + avatarRightHip.position) * 0.5f;

        float avatarShoulderWidth = Vector3.Distance(
            avatarLeftShoulder.position,
            avatarRightShoulder.position
        );
        float avatarTorsoLength = Vector3.Distance(
            avatarShoulderCenter,
            avatarHipCenter
        );

        if (trackedShoulderWidth < 0.01f ||
            trackedTorsoLength < 0.01f ||
            avatarShoulderWidth < 0.01f ||
            avatarTorsoLength < 0.01f)
        {
            return;
        }

        float shoulderScale =
            avatarShoulderWidth / trackedShoulderWidth;
        float torsoScale =
            avatarTorsoLength / trackedTorsoLength;
        float frameScale =
            (shoulderScale + torsoScale) * 0.5f;

        if (float.IsNaN(frameScale) ||
            float.IsInfinity(frameScale) ||
            frameScale <= 0f)
        {
            return;
        }

        calibrationScaleSamples.Add(frameScale);
    }

    private void CompleteCalibration()
    {
        calibrationScaleSamples.Sort();

        int middle = calibrationScaleSamples.Count / 2;
        positionScale = calibrationScaleSamples.Count % 2 == 0
            ? (calibrationScaleSamples[middle - 1] +
               calibrationScaleSamples[middle]) * 0.5f
            : calibrationScaleSamples[middle];

        IsCalibrated = true;

        Debug.Log(
            $"체격 캘리브레이션 완료: " +
            $"scale={positionScale:F3}, " +
            $"samples={calibrationScaleSamples.Count}"
        );
    }

    private bool IsReliableForCalibration(LandmarkData point)
    {
        return point.visibility >= calibrationConfidence &&
               point.presence >= calibrationConfidence;
    }

    #endregion
    
    
    private static Vector3 RawVector(LandmarkData point)
    {
        // 길이만 비교하므로 Unity 좌표계로의 부호 반전은 결과에 영향을 주지 않습니다.
        return new Vector3(point.x, point.y, point.z);
    }

    #region 유니티좌표랑 미디어파이프 좌표랑 매칭하는 용도
    private Vector3 ToUnityPosition(LandmarkData point)
    {
        Vector3 localPosition = new Vector3(
             point.x,
            -point.y,
             point.z
        ) * positionScale;

        return trackingOrigin.position +
               trackingOrigin.rotation * localPosition;
    }
    
    private Vector3 ToUnityDirection(Vector3 direction)
    {
        Vector3 converted = new Vector3(
            direction.x,
            -direction.y,
            direction.z
        );

        return trackingOrigin.rotation * converted.normalized;
    }
    #endregion
    
    //웨이트 값 계산용인데 지금은 1로 그냥 때려서 사용 안함
    private float ChainWeight(LandmarkData root, LandmarkData middle, LandmarkData tip)
    {
        float confidence = Mathf.Min(Mathf.Min(root.visibility, middle.visibility), tip.visibility);

        confidence = Mathf.Min(confidence, Mathf.Min(Mathf.Min(root.presence, middle.presence), tip.presence));

        return Mathf.InverseLerp(minConfidence, 1f, confidence);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!initialized || animator == null)
            return;
        
        //이거 좌우 반전임 - 에잉 귀차나 - 보니까 캐릭터가 나 바라보게 만들어서 그런거넹 ㅎㅎ
        //presence나 visibility로 필터를 한다고 해도 뭔가 뭔가임 물론 그게 정배이긴한데 테스트의 목적에서는 그냥 웨이트 1로 때리고 항시 확인하는게 맞는 듯
        SetGoal(AvatarIKGoal.LeftHand, leftHand, 1f);
        SetGoal(AvatarIKGoal.RightHand, rightHand, 1f);

        SetHint(AvatarIKHint.LeftElbow, leftElbow, 1f);
        SetHint(AvatarIKHint.RightElbow, rightElbow, 1f);

        //지금 캠으로는 다리 잘 안보임
        SetGoal(AvatarIKGoal.LeftFoot, leftFoot, 1f);
        SetGoal(AvatarIKGoal.RightFoot, rightFoot, 1f);

        SetHint(AvatarIKHint.LeftKnee, leftKnee, 1f);
        SetHint(AvatarIKHint.RightKnee, rightKnee, 1f);
    }

    private void SetGoal(AvatarIKGoal goal, Vector3 position, float weight)
    {
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, 0f);
        animator.SetIKPosition(goal, position);
    }

    private void SetHint(AvatarIKHint hint, Vector3 position, float weight)
    {
        animator.SetIKHintPositionWeight(hint, weight);
        animator.SetIKHintPosition(hint, position);
    }
    
    private void CalculateArms(LandmarkData[] points, float t)
    {
        // Left arm bones
        Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        // Right arm bones
        Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);

        // Left arm lengths
        float leftUpperLength = Vector3.Distance(leftUpperArm.position, leftLowerArm.position);
        float leftLowerLength = Vector3.Distance(leftLowerArm.position, leftHandBone.position);

        // Right arm lengths
        float rightUpperLength = Vector3.Distance(rightUpperArm.position, rightLowerArm.position);
        float rightLowerLength = Vector3.Distance(rightLowerArm.position, rightHandBone.position);

        // Left arm points (좌우 바뀌어서)
        Vector3 leftShoulderPoint = RawVector(points[11]);
        Vector3 leftElbowPoint = RawVector(points[13]);
        Vector3 leftWristPoint = RawVector(points[15]);

        // Right arm points (좌우 바뀌어서)
        Vector3 rightShoulderPoint = RawVector(points[12]);
        Vector3 rightElbowPoint = RawVector(points[14]);
        Vector3 rightWristPoint = RawVector(points[16]);

        // Left arm directions
        Vector3 leftUpperDirection = ToUnityDirection(leftElbowPoint - leftShoulderPoint);
        Vector3 leftLowerDirection = ToUnityDirection(leftWristPoint - leftElbowPoint);

        // Right arm directions
        Vector3 rightUpperDirection = ToUnityDirection(rightElbowPoint - rightShoulderPoint);
        Vector3 rightLowerDirection = ToUnityDirection(rightWristPoint - rightElbowPoint);

        // Left arm calculated positions
        Vector3 leftCalculatedElbow = leftUpperArm.position + leftUpperDirection * leftUpperLength;
        Vector3 leftCalculatedHand = leftCalculatedElbow + leftLowerDirection * leftLowerLength;

        // Right arm calculated positions
        Vector3 rightCalculatedElbow = rightUpperArm.position + rightUpperDirection * rightUpperLength;
        Vector3 rightCalculatedHand = rightCalculatedElbow + rightLowerDirection * rightLowerLength;

        // Left arm hint calculation
        Vector3 leftArmVector = leftCalculatedHand - leftUpperArm.position;
        Vector3 leftElbowOnArmLine = leftUpperArm.position + Vector3.Project(leftCalculatedElbow - leftUpperArm.position, leftArmVector);
        Vector3 leftBendDirection = leftCalculatedElbow - leftElbowOnArmLine;

        if (leftBendDirection.sqrMagnitude > 0.0001f)
            leftBendDirection.Normalize();
        else
            leftBendDirection = trackingOrigin.forward;

        Vector3 leftCalculatedHint = leftCalculatedElbow + leftBendDirection * 0.25f;

        // Right arm hint calculation
        Vector3 rightArmVector = rightCalculatedHand - rightUpperArm.position;
        Vector3 rightElbowOnArmLine = rightUpperArm.position + Vector3.Project(rightCalculatedElbow - rightUpperArm.position, rightArmVector);
        Vector3 rightBendDirection = rightCalculatedElbow - rightElbowOnArmLine;

        if (rightBendDirection.sqrMagnitude > 0.0001f)
            rightBendDirection.Normalize();
        else
            rightBendDirection = trackingOrigin.forward;

        Vector3 rightCalculatedHint = rightCalculatedElbow + rightBendDirection * 0.25f;

        // Update both arms
        leftHand = Vector3.Lerp(leftHand, leftCalculatedHand, t);
        leftElbow = Vector3.Lerp(leftElbow, leftCalculatedHint, t);
        rightHand = Vector3.Lerp(rightHand, rightCalculatedHand, t);
        rightElbow = Vector3.Lerp(rightElbow, rightCalculatedHint, t);
    }

    
}
