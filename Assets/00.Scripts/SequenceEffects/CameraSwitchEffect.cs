using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects
{
    [Serializable]
    public sealed class CameraSwitchEffect : SequenceEffect
    {
        [SerializeField]
        private CinemachineCamera camera;

        public override void AppendTo(
            Sequence sequence,
            SequenceEffectContext context)
        {
            sequence.AppendCallback(() => context.PrioritizeCamera(camera));
        }

        public override void Validate(
            SequenceEffectValidationContext context,
            string path,
            List<string> errors)
        {
            ValidateCamera(camera, context, path, errors);
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
            return $"{SequenceEffectText.ObjectName(camera, "카메라 미지정")}로 전환합니다.";
        }
    }
}
