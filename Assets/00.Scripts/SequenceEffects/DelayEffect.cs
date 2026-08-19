using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class DelayEffect : SequenceEffect
    {
        [SerializeField]
        [Min(0f)]
        private float duration = 0.5f;

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            sequence.AppendInterval(Mathf.Max(0f, duration));
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            ValidateNonNegative(duration, "대기 시간", path, errors);
        }

        public override string ToString()
        {
            return $"{SequenceEffectText.Seconds(duration)}초 동안 대기합니다.";
        }
    }
}
