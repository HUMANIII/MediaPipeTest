using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class RepeatEffect : SequenceEffect
    {
        [SerializeField]
        private List<Transform> targets = new List<Transform>();

        [SerializeReference]
        private List<SequenceEffect> children = new List<SequenceEffect>();

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            foreach (var target in targets)
            {
                SequenceEffectContext childContext = context.WithCurrentTarget(target);
                foreach (var child in children)
                {
                    child.AppendTo(sequence, childContext);
                }
            }
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            if (targets == null || targets.Count == 0)
            {
                errors.Add($"{path}: 반복 대상 목록이 비어 있습니다.");
            }

            Transform validationTarget = null;
            if (targets != null)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i] == null)
                    {
                        errors.Add($"{path}/Targets[{i}]: 반복 대상이 비어 있습니다.");
                    }
                    else if (validationTarget == null)
                    {
                        validationTarget = targets[i];
                    }
                }
            }

            if (children == null || children.Count == 0)
            {
                errors.Add($"{path}: 반복할 자식 연출 목록이 비어 있습니다.");
                return;
            }

            var childContext = context.WithCurrentTarget(validationTarget);
            for (var i = 0; i < children.Count; i++)
            {
                SequenceEffect child = children[i];
                string childPath = $"{path}/Children[{i}]";
                if (child == null)
                {
                    errors.Add(
                        $"{childPath}: 연출 요소가 비어 있거나 타입을 찾을 수 없습니다.");
                }
                else if (child is RepeatEffect)
                {
                    errors.Add($"{childPath}: RepeatEffect는 중첩할 수 없습니다.");
                }
                else
                {
                    child.Validate(childContext, childPath, errors);
                }
            }
        }

        public override void CollectCameras(
            ICollection<CinemachineCamera> cameras)
        {
            if (children == null)
            {
                return;
            }

            foreach (var child in children)
            {
                child?.CollectCameras(cameras);
            }
        }

        public override string ToString()
        {
            string targetNames = targets == null || targets.Count == 0
                ? "대상 미지정"
                : string.Join(
                    ", ",
                    targets.Select(target =>
                        SequenceEffectText.ObjectName(target, "위 요소를")));

            string childDescription = children == null || children.Count == 0
                ? "등록된 자식 연출이 없습니다."
                : string.Join(
                    " → ",
                    children.Select(child =>
                        child == null ? "비어 있는 연출 요소입니다." : child.ToString()));

            return $"{targetNames} 각각에 대해 ({childDescription}) 과정을 반복합니다.";
        }
    }
}
