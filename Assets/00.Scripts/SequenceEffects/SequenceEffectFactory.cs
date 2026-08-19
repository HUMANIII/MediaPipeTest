using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

namespace MediaPipeTest.SequenceEffects
{
    [DisallowMultipleComponent]
    public sealed class SequenceEffectFactory : MonoBehaviour
    {
        [Header("Cinemachine")]
        [SerializeField]
        private CinemachineBrain brain;

        [SerializeField]
        [Tooltip("연출 완료 후 카메라 관련 값을 재생 전 상태로 복구합니다.")]
        private bool restoreCameraStateOnComplete = true;

        [SerializeField]
        [Tooltip("시작 카메라로 복귀할 때 사용할 Cinemachine 블렌드입니다.")]
        private CinemachineBlendDefinition restoreCameraBlend =
            new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                0.5f);

        [Header("Events")]
        [SerializeField]
        private UnityEvent onInitialize = new UnityEvent();

        [SerializeField]
        private UnityEvent onCompleted = new UnityEvent();

        [Header("Effects")]
        [SerializeReference]
        private List<SequenceEffect> effects = new List<SequenceEffect>();

        private Sequence activeSequence;
        private CameraStateSnapshot activeCameraSnapshot;
        private readonly List<Action> activeCleanupActions = new List<Action>();
        private Coroutine cameraRestoreCoroutine;
        private CinemachineBrain blendOverrideBrain;
        private CinemachineBlendDefinition previousBrainDefaultBlend;
        private CinemachineBlenderSettings previousBrainCustomBlends;
        private bool isDestroying;

        public bool IsPlaying { get; private set; }

        public UnityEvent OnInitialize => onInitialize;

        public UnityEvent OnCompleted => onCompleted;

        public bool TryPlay()
        {
            if (IsPlaying)
            {
                return false;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogError(
                    "SequenceEffectFactory가 활성 상태가 아니어서 연출을 재생할 수 없습니다.",
                    this);
                return false;
            }

            var validationErrors = ValidateEffects();
            if (validationErrors.Count > 0)
            {
                Debug.LogError(
                    "Sequence Effect 설정이 올바르지 않아 재생하지 않습니다.\n- "
                    + string.Join("\n- ", validationErrors),
                    this);
                return false;
            }

            var cameras = new HashSet<CinemachineCamera>();
            foreach (var effect in effects)
            {
                effect.CollectCameras(cameras);
            }

            CameraStateSnapshot cameraSnapshot = null;
            if (restoreCameraStateOnComplete)
            {
                cameraSnapshot = CameraStateSnapshot.Capture(brain, cameras);
            }

            Sequence sequence = DOTween.Sequence();
            sequence.Pause();
            activeCleanupActions.Clear();

            try
            {
                var context = new SequenceEffectContext(this, brain, null, false);
                foreach (var effect in effects)
                {
                    effect.AppendTo(sequence, context);
                }
            }
            catch (Exception exception)
            {
                sequence.Kill(false);
                RunCleanupActions();
                cameraSnapshot?.Restore();
                Debug.LogException(exception, this);
                return false;
            }

            sequence.SetAutoKill(true);
            sequence.OnComplete(() => CompleteSequence(sequence));
            sequence.OnKill(() => HandleSequenceKilled(sequence));

            activeSequence = sequence;
            activeCameraSnapshot = cameraSnapshot;
            IsPlaying = true;

            try
            {
                onInitialize?.Invoke();
            }
            catch (Exception exception)
            {
                activeSequence = null;
                activeCameraSnapshot = null;
                IsPlaying = false;
                sequence.Kill(false);
                RunCleanupActions();
                cameraSnapshot?.Restore();
                Debug.LogException(exception, this);
                return false;
            }

            sequence.Play();
            return true;
        }

        internal void WaitUntilCameraIsLive(
            Sequence sequence,
            CinemachineCamera targetCamera,
            TweenCallback onCameraReady)
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

        internal void PrioritizeCamera(CinemachineCamera targetCamera)
        {
            if (targetCamera == null)
            {
                return;
            }

            if (brain != null
                && brain.ActiveVirtualCamera is CinemachineVirtualCameraBase liveCamera)
            {
                targetCamera.Priority = Mathf.Max(
                    targetCamera.Priority.Value,
                    liveCamera.Priority.Value);
            }

            targetCamera.Prioritize();
        }

        internal void RegisterCleanup(Action cleanup)
        {
            if (cleanup != null)
            {
                activeCleanupActions.Add(cleanup);
            }
        }

        private List<string> ValidateEffects()
        {
            var errors = new List<string>();
            if (effects == null || effects.Count == 0)
            {
                errors.Add("Effects 목록이 비어 있습니다.");
                return errors;
            }

            var context = new SequenceEffectValidationContext(brain, null, false);
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                string path = $"Effects[{i}]";
                if (effect == null)
                {
                    errors.Add($"{path}: 연출 요소가 비어 있거나 타입을 찾을 수 없습니다.");
                    continue;
                }

                effect.Validate(context, path, errors);
            }

            return errors.Distinct().ToList();
        }

        private IEnumerator ResumeWhenCameraIsLive(
            Sequence sequence,
            CinemachineCamera targetCamera,
            TweenCallback onCameraReady)
        {
            yield return null;

            while (IsCurrentSequence(sequence)
                   && sequence.IsActive()
                   && targetCamera != null
                   && !IsCameraLiveAndBlendComplete(targetCamera))
            {
                yield return null;
            }

            if (!IsCurrentSequence(sequence) || !sequence.IsActive())
            {
                yield break;
            }

            if (targetCamera == null)
            {
                AbortSequence(
                    sequence,
                    "카메라 전환 대기 중 CinemachineCamera가 제거되어 연출을 중단했습니다.");
                yield break;
            }

            onCameraReady?.Invoke();
            sequence.Play();
        }

        private bool IsCameraLiveAndBlendComplete(CinemachineCamera targetCamera)
        {
            return brain != null
                   && targetCamera != null
                   && !brain.IsBlending
                   && brain.IsLiveChild(targetCamera);
        }

        private bool IsCurrentSequence(Sequence sequence)
        {
            return ReferenceEquals(activeSequence, sequence) && IsPlaying;
        }

        private void CompleteSequence(Sequence sequence)
        {
            if (!IsCurrentSequence(sequence))
            {
                return;
            }

            CameraStateSnapshot snapshot = activeCameraSnapshot;
            activeSequence = null;
            activeCameraSnapshot = null;

            RunCleanupActions();

            if (restoreCameraStateOnComplete
                && snapshot != null
                && snapshot.CanBlendToInitialCamera(brain))
            {
                cameraRestoreCoroutine = StartCoroutine(
                    RestoreCameraWithBlend(snapshot));
                return;
            }

            if (restoreCameraStateOnComplete)
            {
                snapshot?.Restore();
            }

            FinishPlayback();
        }

        private IEnumerator RestoreCameraWithBlend(
            CameraStateSnapshot snapshot)
        {
            CinemachineVirtualCameraBase initialCamera =
                snapshot.InitialActiveCamera;

            ApplyRestoreBlendOverride();
            initialCamera.Prioritize();

            yield return null;

            while (!isDestroying
                   && brain != null
                   && initialCamera != null
                   && initialCamera.isActiveAndEnabled
                   && (brain.IsBlending || !brain.IsLiveChild(initialCamera)))
            {
                yield return null;
            }

            snapshot.RestoreCameraStates();
            RestoreBrainBlendSettings();
            cameraRestoreCoroutine = null;

            if (!isDestroying)
            {
                FinishPlayback();
            }
        }

        private void FinishPlayback()
        {
            IsPlaying = false;
            onCompleted?.Invoke();
        }

        private void ApplyRestoreBlendOverride()
        {
            RestoreBrainBlendSettings();

            if (brain == null)
            {
                return;
            }

            blendOverrideBrain = brain;
            previousBrainDefaultBlend = brain.DefaultBlend;
            previousBrainCustomBlends = brain.CustomBlends;

            CinemachineBlendDefinition blend = restoreCameraBlend;
            blend.Time = Mathf.Max(0f, blend.Time);
            brain.DefaultBlend = blend;
            brain.CustomBlends = null;
        }

        private void RestoreBrainBlendSettings()
        {
            CinemachineBrain targetBrain = blendOverrideBrain;
            blendOverrideBrain = null;

            if (targetBrain == null)
            {
                return;
            }

            targetBrain.DefaultBlend = previousBrainDefaultBlend;
            targetBrain.CustomBlends = previousBrainCustomBlends;
        }

        private void HandleSequenceKilled(Sequence sequence)
        {
            if (isDestroying || !ReferenceEquals(activeSequence, sequence))
            {
                return;
            }

            CameraStateSnapshot snapshot = activeCameraSnapshot;
            activeSequence = null;
            activeCameraSnapshot = null;
            IsPlaying = false;

            RunCleanupActions();

            if (restoreCameraStateOnComplete)
            {
                snapshot?.Restore();
            }
        }

        private void AbortSequence(Sequence sequence, string message)
        {
            if (!ReferenceEquals(activeSequence, sequence))
            {
                return;
            }

            Debug.LogError(message, this);
            sequence.Kill(false);
        }

        private void OnDestroy()
        {
            isDestroying = true;

            Sequence sequence = activeSequence;
            CameraStateSnapshot snapshot = activeCameraSnapshot;
            activeSequence = null;
            activeCameraSnapshot = null;
            IsPlaying = false;

            if (sequence != null && sequence.IsActive())
            {
                sequence.Kill(false);
            }

            if (cameraRestoreCoroutine != null)
            {
                StopCoroutine(cameraRestoreCoroutine);
                cameraRestoreCoroutine = null;
            }

            RestoreBrainBlendSettings();

            RunCleanupActions();

            if (restoreCameraStateOnComplete)
            {
                snapshot?.Restore();
            }
        }

        private void RunCleanupActions()
        {
            for (var i = activeCleanupActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    activeCleanupActions[i]?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            activeCleanupActions.Clear();
        }

        private sealed class CameraStateSnapshot
        {
            private readonly CinemachineVirtualCameraBase initialActiveCamera;
            private readonly List<CameraSnapshot> cameraSnapshots;

            private CameraStateSnapshot(
                CinemachineVirtualCameraBase initialActiveCamera,
                List<CameraSnapshot> cameraSnapshots)
            {
                this.initialActiveCamera = initialActiveCamera;
                this.cameraSnapshots = cameraSnapshots;
            }

            public CinemachineVirtualCameraBase InitialActiveCamera =>
                initialActiveCamera;

            public static CameraStateSnapshot Capture(
                CinemachineBrain brain,
                IEnumerable<CinemachineCamera> cameras)
            {
                var snapshots = new List<CameraSnapshot>();
                foreach (var camera in cameras)
                {
                    if (camera != null)
                    {
                        snapshots.Add(new CameraSnapshot(camera));
                    }
                }

                return new CameraStateSnapshot(
                    brain != null
                        ? brain.ActiveVirtualCamera as CinemachineVirtualCameraBase
                        : null,
                    snapshots);
            }

            public void Restore()
            {
                RestoreCameraStates();

                if (initialActiveCamera != null && initialActiveCamera.isActiveAndEnabled)
                {
                    initialActiveCamera.Prioritize();
                }
            }

            public bool CanBlendToInitialCamera(CinemachineBrain brain)
            {
                return brain != null
                       && brain.isActiveAndEnabled
                       && initialActiveCamera != null
                       && initialActiveCamera.isActiveAndEnabled;
            }

            public void RestoreCameraStates()
            {
                foreach (var snapshot in cameraSnapshots)
                {
                    snapshot.Restore();
                }
            }
        }

        private sealed class CameraSnapshot
        {
            private readonly CinemachineCamera camera;
            private readonly PrioritySettings priority;
            private readonly CinemachineCore.BlendHints blendHint;
            private readonly CameraTarget target;
            private readonly float fieldOfView;
            private readonly CinemachineFollow follow;
            private readonly Vector3 followOffset;
            private readonly CinemachineOrbitalFollow orbitalFollow;
            private readonly InputAxis horizontalAxis;
            private readonly InputAxis verticalAxis;

            public CameraSnapshot(CinemachineCamera camera)
            {
                this.camera = camera;
                priority = camera.Priority;
                blendHint = camera.BlendHint;
                target = camera.Target;
                fieldOfView = camera.Lens.FieldOfView;

                follow = camera.GetComponent<CinemachineFollow>();
                followOffset = follow != null ? follow.FollowOffset : default;

                orbitalFollow = camera.GetComponent<CinemachineOrbitalFollow>();
                horizontalAxis = orbitalFollow != null
                    ? orbitalFollow.HorizontalAxis
                    : default;
                verticalAxis = orbitalFollow != null
                    ? orbitalFollow.VerticalAxis
                    : default;
            }

            public void Restore()
            {
                if (camera == null)
                {
                    return;
                }

                camera.Priority = priority;
                camera.BlendHint = blendHint;
                camera.Target = target;

                LensSettings lens = camera.Lens;
                lens.FieldOfView = fieldOfView;
                camera.Lens = lens;

                if (follow != null)
                {
                    follow.FollowOffset = followOffset;
                }

                if (orbitalFollow != null)
                {
                    orbitalFollow.HorizontalAxis = horizontalAxis;
                    orbitalFollow.VerticalAxis = verticalAxis;
                    orbitalFollow.HorizontalAxis.CancelRecentering();
                    orbitalFollow.VerticalAxis.CancelRecentering();
                }
            }
        }
    }
}
