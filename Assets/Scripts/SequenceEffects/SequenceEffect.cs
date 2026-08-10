using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    public enum EffectTargetSource
    {
        Explicit,
        CurrentRepeatTarget
    }

    public enum CameraMoveStyle
    {
        Cut,
        Quick,
        Smooth,
        Custom
    }

    /// <summary>
    /// Base data object for a single section of a DOTween sequence.
    /// Concrete effects are stored by SequenceEffectFactory with SerializeReference.
    /// </summary>
    [Serializable]
    public abstract class SequenceEffect
    {
        public abstract void AppendTo(
            Sequence sequence,
            SequenceEffectContext context);

        public abstract void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors);

        public virtual void CollectCameras(
            ICollection<CinemachineCamera> cameras)
        {
        }

        protected static Transform ResolveTarget(
            EffectTargetSource targetSource,
            Transform explicitTarget,
            SequenceEffectContext context)
        {
            return targetSource == EffectTargetSource.CurrentRepeatTarget
                ? context.CurrentTarget
                : explicitTarget;
        }

        protected static void ValidateTarget(
            EffectTargetSource targetSource,
            Transform explicitTarget,
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            if (targetSource == EffectTargetSource.Explicit)
            {
                if (explicitTarget == null)
                {
                    errors.Add($"{path}: Explicit Target이 지정되지 않았습니다.");
                }

                return;
            }

            if (!context.HasCurrentTarget)
            {
                errors.Add(
                    $"{path}: Current Repeat Target은 RepeatEffect 내부에서만 사용할 수 있습니다.");
            }
            else if (context.CurrentTarget == null)
            {
                errors.Add($"{path}: 현재 Repeat Target이 비어 있습니다.");
            }
        }

        protected static void ValidateCamera(
            CinemachineCamera camera,
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            if (context.Brain == null)
            {
                errors.Add($"{path}: CinemachineBrain이 지정되지 않았습니다.");
            }
            else if (!context.Brain.isActiveAndEnabled)
            {
                errors.Add($"{path}: CinemachineBrain이 활성 상태가 아닙니다.");
            }

            if (camera == null)
            {
                errors.Add($"{path}: CinemachineCamera가 지정되지 않았습니다.");
            }
            else if (!camera.isActiveAndEnabled)
            {
                errors.Add($"{path}: CinemachineCamera가 활성 상태가 아닙니다.");
            }
        }

        protected static void ValidateNonNegative(
            float value,
            string valueName,
            string path,
            List<string> errors)
        {
            if (value < 0f)
            {
                errors.Add($"{path}: {valueName}은(는) 0 이상이어야 합니다.");
            }
        }

        protected static string DescribeTarget(
            EffectTargetSource targetSource,
            Transform explicitTarget)
        {
            return targetSource == EffectTargetSource.CurrentRepeatTarget
                ? "현재 반복 대상"
                : SequenceEffectText.ObjectName(explicitTarget, "대상 미지정");
        }
    }

    public readonly struct SequenceEffectContext
    {
        internal SequenceEffectContext(
            SequenceEffectFactory factory,
            CinemachineBrain brain,
            Transform currentTarget,
            bool hasCurrentTarget)
        {
            Factory = factory;
            Brain = brain;
            CurrentTarget = currentTarget;
            HasCurrentTarget = hasCurrentTarget;
        }

        public SequenceEffectFactory Factory { get; }

        public CinemachineBrain Brain { get; }

        public MonoBehaviour CoroutineHost => Factory;

        public Transform CurrentTarget { get; }

        public bool HasCurrentTarget { get; }

        public SequenceEffectContext WithCurrentTarget(Transform target)
        {
            return new SequenceEffectContext(Factory, Brain, target, true);
        }

        public void WaitUntilCameraIsLive(
            Sequence sequence,
            CinemachineCamera camera,
            TweenCallback onCameraReady = null)
        {
            Factory.WaitUntilCameraIsLive(sequence, camera, onCameraReady);
        }

        public void PrioritizeCamera(CinemachineCamera camera)
        {
            Factory.PrioritizeCamera(camera);
        }

        public void RegisterCleanup(Action cleanup)
        {
            Factory.RegisterCleanup(cleanup);
        }
    }

    public readonly struct SequenceEffectValidationContext
    {
        internal SequenceEffectValidationContext(
            CinemachineBrain brain,
            Transform currentTarget,
            bool hasCurrentTarget)
        {
            Brain = brain;
            CurrentTarget = currentTarget;
            HasCurrentTarget = hasCurrentTarget;
        }

        public CinemachineBrain Brain { get; }

        public Transform CurrentTarget { get; }

        public bool HasCurrentTarget { get; }

        public SequenceEffectValidationContext WithCurrentTarget(Transform target)
        {
            return new SequenceEffectValidationContext(Brain, target, true);
        }
    }

    internal static class SequenceEffectText
    {
        public static string Seconds(float value)
        {
            return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}초";
        }

        public static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string Vector(Vector3 value)
        {
            return $"({Number(value.x)}, {Number(value.y)}, {Number(value.z)})";
        }

        public static string ObjectName(UnityEngine.Object value, string fallback)
        {
            return value == null ? fallback : value.name;
        }
    }
}
