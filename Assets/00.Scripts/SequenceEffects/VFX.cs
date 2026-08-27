using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.VFX;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class VFX : SequenceEffect
    {
        [SerializeField]
        [Tooltip("동적으로 생성할 VFX Graph Asset입니다.")]
        private VisualEffectAsset visualEffectAsset;

        [SerializeField]
        private EffectTargetSource targetSource = EffectTargetSource.Explicit;

        [SerializeField]
        private Transform explicitTarget;

        [SerializeField]
        [Tooltip("타깃을 기준으로 적용할 로컬 위치입니다.")]
        private Vector3 localPosition;

        [SerializeField]
        [Tooltip("타깃을 기준으로 적용할 로컬 회전값입니다.")]
        private Vector3 localEulerAngles;

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            VisualEffectAsset asset = visualEffectAsset;
            Transform target = ResolveTarget(
                targetSource,
                explicitTarget,
                context);
            if (asset == null || target == null)
            {
                return;
            }

            GameObject runtimeObject = null;
            VisualEffect runtimeEffect = null;

            context.RegisterCleanup(() =>
            {
                if (runtimeEffect != null)
                {
                    runtimeEffect.Stop();
                }

                if (runtimeObject != null)
                {
                    runtimeObject.SetActive(false);
                    UnityEngine.Object.Destroy(runtimeObject);
                }

                runtimeEffect = null;
                runtimeObject = null;
            });

            sequence.AppendCallback(() =>
            {
                if (asset == null || target == null)
                {
                    return;
                }

                runtimeObject = new GameObject($"{asset.name} VFX");
                runtimeObject.SetActive(false);
                runtimeObject.layer = target.gameObject.layer;

                Transform runtimeTransform = runtimeObject.transform;
                runtimeTransform.SetParent(target, false);
                runtimeTransform.localPosition = localPosition;
                runtimeTransform.localRotation = Quaternion.Euler(
                    localEulerAngles);

                runtimeEffect = runtimeObject.AddComponent<VisualEffect>();
                runtimeEffect.visualEffectAsset = asset;
                runtimeEffect.pause = false;

                runtimeObject.SetActive(true);
            });
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            if (visualEffectAsset == null)
            {
                errors.Add($"{path}: VFX Graph Asset이 지정되지 않았습니다.");
            }

            ValidateTarget(targetSource, explicitTarget, context, path, errors);
        }

        public override string ToString()
        {
            string assetName = SequenceEffectText.ObjectName(
                visualEffectAsset,
                "VFX Asset 미지정");
            string targetName = DescribeTarget(targetSource, explicitTarget);
            return $"{assetName}을(를) {targetName}에 생성해 재생하고 "
                   + "시퀀스 종료 시 제거합니다.";
        }
    }
}
