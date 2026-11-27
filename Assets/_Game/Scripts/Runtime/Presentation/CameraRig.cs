using ShadowRPG.Core.Tuning;
using Unity.Cinemachine;
using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>
    /// 게임 카메라 (Cinemachine): 플레이어를 부드럽게 따라가고 이동 방향으로 살짝 앞을 봅니다.
    /// 시야 = 화면 가로 폭(유닛) 기준 — 세로 화면 기본 CameraViewWidth, 가로 화면 CameraViewWidthLandscape.
    /// 흔들림은 Impulse로 받습니다 (FeelDirector).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        Camera _camera;
        CinemachineCamera _virtual;
        CinemachinePositionComposer _composer;
        PresentationTuning _t;

        public static CameraRig Ensure(Camera camera, PresentationTuning tuning)
        {
            var rig = FindAnyObjectByType<CameraRig>();
            if (rig == null) rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            rig.Setup(camera, tuning);
            return rig;
        }

        void Setup(Camera camera, PresentationTuning tuning)
        {
            _camera = camera;
            _t = tuning;
            camera.orthographic = true;
            if (!camera.TryGetComponent<CinemachineBrain>(out var brain)) brain = camera.gameObject.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = false;
            var legacy = camera.GetComponent<CameraFollow>();
            if (legacy != null) legacy.enabled = false;

            if (_virtual != null) return;
            _virtual = gameObject.AddComponent<CinemachineCamera>();
            _virtual.Lens = new LensSettings { ModeOverride = LensSettings.OverrideModes.Orthographic, OrthographicSize = 10f, NearClipPlane = -50f, FarClipPlane = 100f };
            _composer = gameObject.AddComponent<CinemachinePositionComposer>();
            _composer.CameraDistance = 20f;
            _composer.Damping = new Vector3(tuning.CameraFollowDamping, tuning.CameraFollowDamping, 0f);
            _composer.Lookahead = new LookaheadSettings { Enabled = tuning.CameraLookAhead > 0f, Time = tuning.CameraLookAhead * 0.5f, Smoothing = 8f, IgnoreY = false };
            _composer.TargetOffset = new Vector3(0f, tuning.CameraVerticalOffset, 0f);
            var listener = gameObject.AddComponent<CinemachineImpulseListener>();
            listener.Gain = 1f;
            transform.position = new Vector3(0f, 0f, -20f);
        }

        public void Follow(Transform target)
        {
            if (_virtual == null) return;
            _virtual.Follow = target;
            if (target != null)
            {
                transform.position = new Vector3(target.position.x, target.position.y + _t.CameraVerticalOffset, -20f);
                _virtual.PreviousStateIsValid = false;
            }
        }

        void LateUpdate()
        {
            if (_camera == null || _virtual == null) return;
            bool portrait = Screen.height >= Screen.width;
            float width = portrait ? _t.CameraViewWidth : _t.CameraViewWidthLandscape;
            var lens = _virtual.Lens;
            lens.OrthographicSize = width * 0.5f / Mathf.Max(0.1f, _camera.aspect);
            _virtual.Lens = lens;
        }
    }
}
