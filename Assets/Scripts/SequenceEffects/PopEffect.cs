using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class PopEffect : SequenceEffect
    {
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
            Vector3 destinationScale = Vector3.one;
            float progress = 0f;

            sequence.AppendCallback(() =>
            {
                if (target == null)
                {
                    return;
                }

                destinationScale = target.localScale;
                progress = 0f;
                target.gameObject.SetActive(true);
                target.localScale = Vector3.zero;
            });

            sequence.Append(
                DOTween.To(
                        () => progress,
                        value =>
                        {
                            progress = value;
                            if (target != null)
                            {
                                target.localScale = Vector3.LerpUnclamped(
                                    Vector3.zero,
                                    destinationScale,
                                    value);
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
            return $"{targetName}을(를) 활성화하고 {SequenceEffectText.Seconds(duration)} 동안 "
                   + $"{ease} 방식으로 팝한 뒤 {SequenceEffectText.Seconds(delayAfterPop)} 동안 대기합니다.";
        }
    }
}
