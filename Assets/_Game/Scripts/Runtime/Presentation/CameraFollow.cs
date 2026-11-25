using UnityEngine;

namespace ShadowRPG.Runtime.Presentation
{
    /// <summary>플레이어를 부드럽게 따라가며 화면 비율에 맞춰 시야를 조정합니다.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] float minHalfHeight = 10f;
        [Tooltip("세로 화면에서도 좌우로 이만큼(유닛)은 보이도록 시야를 넓힙니다.")]
        [SerializeField] float minHalfWidth = 7.5f;

        Camera _camera;
        Transform _target;
        Vector3 _velocity;

        public void SetTarget(Transform target)
        {
            _target = target;
            if (target != null) transform.position = new Vector3(target.position.x, target.position.y + 0.5f, -10f);
        }

        void Awake() => _camera = GetComponent<Camera>();

        void LateUpdate()
        {
            _camera.orthographicSize = Mathf.Max(minHalfHeight, minHalfWidth / Mathf.Max(0.1f, _camera.aspect));
            if (_target == null) return;
            var goal = new Vector3(_target.position.x, _target.position.y + 0.5f, -10f);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, smoothTime);
        }
    }
}
