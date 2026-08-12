using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class PopEffect : SequenceEffect
    {
        private static readonly int PopScaleId = Shader.PropertyToID("_PopScale");
        private static readonly int PopCenterOffsetId =
            Shader.PropertyToID("_PopCenterOffset");

        [SerializeField]
        private EffectTargetSource targetSource = EffectTargetSource.Explicit;

        [SerializeField]
        private Transform explicitTarget;

        [SerializeField]
        [Min(0f)]
        private float duration = 0.2f;

        [SerializeField]
        [Min(0f)]
        private float delayAfterPop = 0.5f;

        [SerializeField]
        private Ease ease = Ease.OutQuad;

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            Transform target = ResolveTarget(targetSource, explicitTarget, context);
            Renderer[] renderers = Array.Empty<Renderer>();
            var propertyBlock = new MaterialPropertyBlock();
            float progress = 0f;

            sequence.AppendCallback(() =>
            {
                if (target == null)
                {
                    return;
                }

                progress = 0f;
                target.gameObject.SetActive(true);
                renderers = FindPopRenderers(target);
                ApplyPopProperties(
                    renderers,
                    propertyBlock,
                    Vector3.zero,
                    target.position);
            });

            sequence.Append(
                DOTween.To(
                        () => progress,
                        value =>
                        {
                            progress = value;
                            if (target != null && renderers.Length > 0)
                            {
                                ApplyPopProperties(
                                    renderers,
                                    propertyBlock,
                                    Vector3.LerpUnclamped(
                                        Vector3.zero,
                                        Vector3.one,
                                        value),
                                    target.position);
                            }
                        },
                        1f,
                        Mathf.Max(0f, duration))
                    .SetEase(ease));

            if (delayAfterPop > 0f)
            {
                sequence.AppendInterval(delayAfterPop);
            }
        }

        private static Renderer[] FindPopRenderers(Transform target)
        {
            Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>(true);
            var popRenderers = new List<Renderer>(childRenderers.Length);

            foreach (Renderer targetRenderer in childRenderers)
            {
                if (targetRenderer != null && HasPopProperties(targetRenderer))
                {
                    popRenderers.Add(targetRenderer);
                }
            }

            return popRenderers.ToArray();
        }

        private static bool HasPopProperties(Renderer targetRenderer)
        {
            Material[] materials = targetRenderer.sharedMaterials;
            foreach (Material material in materials)
            {
                if (material != null
                    && material.HasProperty(PopScaleId)
                    && material.HasProperty(PopCenterOffsetId))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyPopProperties(
            Renderer[] renderers,
            MaterialPropertyBlock propertyBlock,
            Vector3 scale,
            Vector3 centerOffset)
        {
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(PopCenterOffsetId, centerOffset);
                propertyBlock.SetVector(PopScaleId, scale);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            ValidateTarget(targetSource, explicitTarget, context, path, errors);
            ValidateNonNegative(duration, "팝 시간", path, errors);
            ValidateNonNegative(delayAfterPop, "팝 이후 대기 시간", path, errors);
        }

        public override string ToString()
        {
            string targetName = DescribeTarget(targetSource, explicitTarget);
            return $"{targetName}을(를) 활성화하고 셰이더의 _PopScale을 "
                   + $"{SequenceEffectText.Seconds(duration)} 동안 {ease} 방식으로 팝한 뒤 "
                   + $"{SequenceEffectText.Seconds(delayAfterPop)} 동안 대기합니다.";
        }
    }
}
