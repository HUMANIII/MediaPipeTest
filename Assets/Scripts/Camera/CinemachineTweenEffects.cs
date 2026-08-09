using System.Collections;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

public enum CameraMoveStyle
{
    Cut,
    Quick,
    Smooth
}

public sealed class CinemachineTweenEffects : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField]
    private CinemachineBrain brain;

    [Header("Full Body Orbit")]
    [SerializeField]
    private CinemachineCamera fullBodyCamera;

    [SerializeField]
    private CinemachineOrbitalFollow fullBodyOrbitalFollow;

    [SerializeField]
    private int fullBodyOrbitCount = 2;

    [SerializeField]
    [Min(0f)]
    private float fullBodyOrbitDuration = 5f;

    [Header("Camera Focus")]
    [SerializeField]
    private Vector3 defaultCameraOffset = new Vector3(0f, 1f, -2.5f);

    [SerializeField]
    private CinemachineCamera cinemachineCamera;

    [SerializeField]
    private CinemachineFollow cameraFollow;
    
    [SerializeField]
    private CinemachineCamera defaultCamera;

#if UNITY_EDITOR
    private void Awake()
    {
        if (cinemachineCamera == null)
        {
            Debug.LogError("카메라 집중 연출에 사용할 CinemachineCamera가 필요합니다.", this);
        }
        else
        {
            cameraFollow = cinemachineCamera.GetComponent<CinemachineFollow>();
            if (cameraFollow == null)
            {
                Debug.LogError("사용할 CinemachineCamera에 CinemachineFollow가 필요합니다.", this);
            }
        }

        if (brain == null)
        {
            Debug.LogError("사용할 CinemachineCamera를 제어하는 활성 CinemachineBrain이 필요합니다.", this);
        }
    }
#endif

    /// <summary>
    /// Brain의 기본 블렌드로 전신 카메라에 진입한 뒤
    /// 설정된 횟수만큼 타겟 주위를 회전하는 구간을 추가합니다.
    /// </summary>
    public void AppendFullBodyOrbit(Sequence sequence, bool orbitClockwise = true)
    {
        float startAngle = 0f;
        float orbitProgress = 0f;
        float riseProgress = 0f;
        float bottomVertical = 0f;
        float topVertical = 0f;

        bool horizontalRecenteringWasEnabled = false;
        bool verticalRecenteringWasEnabled = false;

        float signedOrbitAngle =
            (orbitClockwise ? 1f : -1f) * 360f * fullBodyOrbitCount;

        sequence.AppendCallback(() =>
        {
            horizontalRecenteringWasEnabled =
                fullBodyOrbitalFollow.HorizontalAxis.Recentering.Enabled;
            verticalRecenteringWasEnabled =
                fullBodyOrbitalFollow.VerticalAxis.Recentering.Enabled;

            fullBodyOrbitalFollow.HorizontalAxis.Recentering.Enabled = false;
            fullBodyOrbitalFollow.VerticalAxis.Recentering.Enabled = false;

            fullBodyOrbitalFollow.HorizontalAxis.CancelRecentering();
            fullBodyOrbitalFollow.VerticalAxis.CancelRecentering();

            fullBodyCamera.BlendHint |=
                CinemachineCore.BlendHints.InheritPosition;

            WaitUntilCameraIsLive(
                sequence,
                fullBodyCamera,
                () =>
                {
                    startAngle = fullBodyOrbitalFollow.HorizontalAxis.Value;
                    bottomVertical = fullBodyOrbitalFollow.VerticalAxis.Value;
                    topVertical = fullBodyOrbitalFollow.VerticalAxis.Range.y;
                    orbitProgress = 0f;
                    riseProgress = 0f;
                });
        });

        sequence.Append(
            DOTween.To(
                    () => orbitProgress,
                    value =>
                    {
                        orbitProgress = value;
                        fullBodyOrbitalFollow.HorizontalAxis.Value =
                            startAngle + value;
                    },
                    signedOrbitAngle,
                    fullBodyOrbitDuration)
                .SetEase(Ease.Linear));

        sequence.Join(
            DOTween.To(
                    () => riseProgress,
                    value =>
                    {
                        riseProgress = value;
                        fullBodyOrbitalFollow.VerticalAxis.Value =
                            Mathf.Lerp(bottomVertical, topVertical, value);
                    },
                    1f,
                    fullBodyOrbitDuration)
                .SetEase(Ease.InOutSine));

        sequence.AppendCallback(() =>
        {
            fullBodyOrbitalFollow.HorizontalAxis.Value = startAngle;
            fullBodyOrbitalFollow.VerticalAxis.Value = topVertical;

            fullBodyOrbitalFollow.HorizontalAxis.Recentering.Enabled =
                horizontalRecenteringWasEnabled;
            fullBodyOrbitalFollow.VerticalAxis.Recentering.Enabled =
                verticalRecenteringWasEnabled;

            fullBodyOrbitalFollow.HorizontalAxis.CancelRecentering();
            fullBodyOrbitalFollow.VerticalAxis.CancelRecentering();
        });
    }

    /// <summary>
    /// 지정한 타겟을 추적하면서 카메라 오프셋을 이동하고,
    /// 이동이 끝난 구도를 지정 시간 동안 유지하는 구간을 추가합니다.
    /// </summary>
    public void AppendCameraFocus(Sequence sequence, Transform target, float moveDuration, float focusDuration, CameraMoveStyle moveStyle = CameraMoveStyle.Quick,Vector3? targetCameraOffset = null)
    {
        Vector3 destinationOffset = targetCameraOffset ?? defaultCameraOffset;
        moveDuration = Mathf.Max(0f, moveDuration);
        focusDuration = Mathf.Max(0f, focusDuration);

        sequence.AppendCallback(() =>
            WaitUntilCameraIsLive(
                sequence,
                cinemachineCamera,
                () => SetFocusTarget(target)));

        if (moveStyle == CameraMoveStyle.Cut || moveDuration <= 0f)
        {
            sequence.AppendCallback(
                () => cameraFollow.FollowOffset = destinationOffset);
        }
        else
        {
            sequence.Append(
                DOTween.To(
                        () => cameraFollow.FollowOffset,
                        value => cameraFollow.FollowOffset = value,
                        destinationOffset,
                        moveDuration)
                    .SetEase(GetMoveEase(moveStyle)));
        }

        if (focusDuration > 0f)
        {
            sequence.AppendInterval(focusDuration);
        }
    }

    private void WaitUntilCameraIsLive(
        Sequence sequence,
        CinemachineCamera targetCamera,
        TweenCallback onCameraReady = null)
    {
        if (IsCameraLiveAndBlendComplete(targetCamera))
        {
            onCameraReady?.Invoke();
            return;
        }

        sequence.Pause();
        PrioritizeCamera(targetCamera);
        StartCoroutine(
            ResumeWhenCameraIsLive(sequence, targetCamera, onCameraReady));
    }

    private IEnumerator ResumeWhenCameraIsLive(
        Sequence sequence,
        CinemachineCamera targetCamera,
        TweenCallback onCameraReady)
    {
        // 카메라 우선도 변경을 CinemachineBrain이 반영할 시간을 보장합니다.
        yield return null;

        while (sequence.IsActive() && !IsCameraLiveAndBlendComplete(targetCamera))
        {
            yield return null;
        }

        if (sequence.IsActive())
        {
            onCameraReady?.Invoke();
            sequence.Play();
        }
    }

    private void SetFocusTarget(Transform target)
    {
        cinemachineCamera.Target.TrackingTarget = target;
        cinemachineCamera.Target.LookAtTarget = target;
        cinemachineCamera.Target.CustomLookAtTarget = true;
    }

    private bool IsCameraLiveAndBlendComplete(CinemachineCamera targetCamera)
    {
        return !brain.IsBlending && brain.IsLiveChild(targetCamera);
    }

    private void PrioritizeCamera(CinemachineCamera targetCamera)
    {
        if (brain.ActiveVirtualCamera is CinemachineVirtualCameraBase liveCamera)
        {
            targetCamera.Priority = Mathf.Max(
                targetCamera.Priority.Value,
                liveCamera.Priority.Value);
        }

        targetCamera.Prioritize();
    }

    private static Ease GetMoveEase(CameraMoveStyle moveStyle)
    {
        return moveStyle switch
        {
            CameraMoveStyle.Quick => Ease.OutQuad,
            CameraMoveStyle.Smooth => Ease.InOutSine,
            _ => Ease.Linear
        };
    }
    
    public void SetDefaultCamera(Sequence sequence)
    {
        sequence.AppendCallback(() => PrioritizeCamera(defaultCamera));
    }

    // 필요 시 복구할 후보:
    // - 연출 취소 시 이전 카메라와 TrackingTarget을 원상복구하는 기능
    // - 카메라마다 별도의 블렌드 설정을 적용하는 기능
}
