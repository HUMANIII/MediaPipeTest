using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class CameraFocusEffect : SequenceEffect
    {
        [SerializeField]
        private CinemachineCamera camera;

        [SerializeField]
        private EffectTargetSource targetSource = EffectTargetSource.Explicit;

        [SerializeField]
        private Transform explicitTarget;

        [SerializeField]
        private Vector3 targetCameraOffset = new Vector3(0f, 1f, -2.5f);

        [SerializeField]
        [Range(1f, 179f)]
        private float targetFieldOfView = 30f;

        [SerializeField]
        [FormerlySerializedAs("moveDuration")]
        [Min(0f)]
        private float zoomDuration = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float focusDuration = 0.3f;

        [SerializeField]
        [FormerlySerializedAs("moveStyle")]
        private CameraMoveStyle zoomStyle = CameraMoveStyle.Quick;

        [SerializeField]
        private Ease customEase = Ease.OutQuad;

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            Transform target = ResolveTarget(targetSource, explicitTarget, context);
            var follow = camera.GetComponent<CinemachineFollow>();

            sequence.AppendCallback(() =>
            {
                SetFocusTarget(camera, target);
                follow.FollowOffset = targetCameraOffset;
                context.WaitUntilCameraIsLive(sequence, camera);
            });

            if (zoomStyle == CameraMoveStyle.Cut || zoomDuration <= 0f)
            {
                sequence.AppendCallback(
                    () => SetFieldOfView(camera, targetFieldOfView));
            }
            else
            {
                sequence.Append(
                    DOTween.To(
                            () => camera.Lens.FieldOfView,
                            value => SetFieldOfView(camera, value),
                            targetFieldOfView,
                            Mathf.Max(0f, zoomDuration))
                        .SetEase(GetZoomEase()));
            }

            if (focusDuration > 0f)
            {
                sequence.AppendInterval(focusDuration);
            }
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            ValidateCamera(camera, context, path, errors);
            ValidateTarget(targetSource, explicitTarget, context, path, errors);

            if (camera != null && camera.GetComponent<CinemachineFollow>() == null)
            {
                errors.Add($"{path}: 지정한 카메라에 CinemachineFollow가 없습니다.");
            }

            if (targetFieldOfView <= 0f || targetFieldOfView >= 180f)
            {
                errors.Add($"{path}: 목표 FOV는 0보다 크고 180보다 작아야 합니다.");
            }

            if (camera != null && camera.Lens.Orthographic)
            {
                errors.Add($"{path}: FOV 줌에는 원근 카메라가 필요합니다.");
            }

            ValidateNonNegative(zoomDuration, "줌 시간", path, errors);
            ValidateNonNegative(focusDuration, "구도 유지 시간", path, errors);
        }

        public override void CollectCameras(
            ICollection<CinemachineCamera> cameras)
        {
            if (camera != null)
            {
                cameras.Add(camera);
            }
        }

        public override string ToString()
        {
            string targetName = DescribeTarget(targetSource, explicitTarget);
            string cameraName = SequenceEffectText.ObjectName(
                camera,
                "카메라 미지정");
            string framing =
                $"Follow Offset을 {SequenceEffectText.Vector(targetCameraOffset)}로 맞추고";
            string zoom = zoomStyle == CameraMoveStyle.Cut || zoomDuration <= 0f
                ? $"FOV를 즉시 {SequenceEffectText.Number(targetFieldOfView)}°로 변경해 줌하고"
                : $"{SequenceEffectText.Seconds(zoomDuration)} 동안 "
                  + $"{DescribeZoomStyle()} 방식으로 FOV를 {SequenceEffectText.Number(targetFieldOfView)}°까지 조정해 줌하고";

            return $"{targetName}을(를) 추적하도록 {cameraName}의 {framing} {zoom} "
                   + $"{SequenceEffectText.Seconds(focusDuration)} 동안 유지합니다.";
        }

        private Ease GetZoomEase()
        {
            return zoomStyle switch
            {
                CameraMoveStyle.Quick => Ease.OutQuad,
                CameraMoveStyle.Smooth => Ease.InOutSine,
                CameraMoveStyle.Custom => customEase,
                _ => Ease.Linear
            };
        }

        private string DescribeZoomStyle()
        {
            return zoomStyle switch
            {
                CameraMoveStyle.Quick => "빠른 OutQuad",
                CameraMoveStyle.Smooth => "부드러운 InOutSine",
                CameraMoveStyle.Custom => $"{customEase}",
                _ => "즉시"
            };
        }

        private static void SetFocusTarget(
            CinemachineCamera targetCamera,
            Transform target)
        {
            targetCamera.Target.TrackingTarget = target;
            targetCamera.Target.LookAtTarget = target;
            targetCamera.Target.CustomLookAtTarget = true;
        }

        private static void SetFieldOfView(
            CinemachineCamera targetCamera,
            float fieldOfView)
        {
            LensSettings lens = targetCamera.Lens;
            lens.FieldOfView = Mathf.Clamp(fieldOfView, 0.01f, 179f);
            targetCamera.Lens = lens;
        }
    }
}
