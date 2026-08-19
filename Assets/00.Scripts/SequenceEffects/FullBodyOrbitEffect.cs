using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class FullBodyOrbitEffect : SequenceEffect
    {
        [SerializeField]
        private CinemachineCamera camera;

        [SerializeField]
        [Min(0)]
        private int orbitCount = 2;

        [SerializeField]
        [Min(0f)]
        private float duration = 5f;

        [SerializeField]
        private bool clockwise = true;

        [SerializeField]
        private Ease horizontalEase = Ease.Linear;

        [SerializeField]
        private Ease verticalEase = Ease.InOutSine;

        [SerializeField]
        private bool inheritPosition = true;

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            var orbitalFollow = camera.GetComponent<CinemachineOrbitalFollow>();
            float startAngle = 0f;
            float bottomVertical = 0f;
            float topVertical = 0f;
            float orbitProgress = 0f;
            float riseProgress = 0f;
            bool horizontalRecenteringWasEnabled = false;
            bool verticalRecenteringWasEnabled = false;
            bool recenteringWasCaptured = false;
            float signedOrbitAngle =
                (clockwise ? 1f : -1f) * 360f * orbitCount;

            void RestoreRecentering()
            {
                if (!recenteringWasCaptured || orbitalFollow == null)
                {
                    return;
                }

                orbitalFollow.HorizontalAxis.Recentering.Enabled =
                    horizontalRecenteringWasEnabled;
                orbitalFollow.VerticalAxis.Recentering.Enabled =
                    verticalRecenteringWasEnabled;
                orbitalFollow.HorizontalAxis.CancelRecentering();
                orbitalFollow.VerticalAxis.CancelRecentering();
                recenteringWasCaptured = false;
            }

            context.RegisterCleanup(RestoreRecentering);

            sequence.AppendCallback(() =>
            {
                horizontalRecenteringWasEnabled =
                    orbitalFollow.HorizontalAxis.Recentering.Enabled;
                verticalRecenteringWasEnabled =
                    orbitalFollow.VerticalAxis.Recentering.Enabled;
                recenteringWasCaptured = true;

                orbitalFollow.HorizontalAxis.Recentering.Enabled = false;
                orbitalFollow.VerticalAxis.Recentering.Enabled = false;
                orbitalFollow.HorizontalAxis.CancelRecentering();
                orbitalFollow.VerticalAxis.CancelRecentering();

                if (inheritPosition)
                {
                    camera.BlendHint |= CinemachineCore.BlendHints.InheritPosition;
                }

                context.WaitUntilCameraIsLive(
                    sequence,
                    camera,
                    () =>
                    {
                        startAngle = orbitalFollow.HorizontalAxis.Value;
                        bottomVertical = orbitalFollow.VerticalAxis.Value;
                        topVertical = orbitalFollow.VerticalAxis.Range.y;
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
                            orbitalFollow.HorizontalAxis.Value = startAngle + value;
                        },
                        signedOrbitAngle,
                        Mathf.Max(0f, duration))
                    .SetEase(horizontalEase));

            sequence.Join(
                DOTween.To(
                        () => riseProgress,
                        value =>
                        {
                            riseProgress = value;
                            orbitalFollow.VerticalAxis.Value =
                                Mathf.LerpUnclamped(bottomVertical, topVertical, value);
                        },
                        1f,
                        Mathf.Max(0f, duration))
                    .SetEase(verticalEase));

            sequence.AppendCallback(() =>
            {
                orbitalFollow.HorizontalAxis.Value = startAngle;
                orbitalFollow.VerticalAxis.Value = topVertical;
                RestoreRecentering();
            });
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            ValidateCamera(camera, context, path, errors);

            if (camera != null
                && camera.GetComponent<CinemachineOrbitalFollow>() == null)
            {
                errors.Add(
                    $"{path}: 지정한 카메라에 CinemachineOrbitalFollow가 없습니다.");
            }

            if (orbitCount < 0)
            {
                errors.Add($"{path}: 회전 횟수는 0 이상이어야 합니다.");
            }

            ValidateNonNegative(duration, "회전 시간", path, errors);
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
            string cameraName = SequenceEffectText.ObjectName(
                camera,
                "카메라 미지정");
            string direction = clockwise ? "시계 방향" : "반시계 방향";
            return $"{cameraName}로 전환하여 {SequenceEffectText.Seconds(duration)} 동안 "
                   + $"{direction}으로 {orbitCount}회 회전하면서 하단에서 상단까지 전신을 훑습니다.";
        }
    }
}
