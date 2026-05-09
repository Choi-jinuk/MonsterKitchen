using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 메인 카메라가 플레이어를 부드럽게 추적한다.
    /// 방 경계(roomMin/roomMax)를 설정하면 카메라가 방 밖으로 나가지 않는다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [Tooltip("카메라 추적 지연 (초). 클수록 부드럽고 느슨하게 따라온다.")]
        [SerializeField] private float smoothTime = 0.25f;

        [Header("Room Bounds (월드 좌표)")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private Vector2 roomMin = new Vector2(-9f, -5f);
        [SerializeField] private Vector2 roomMax = new Vector2(9f, 5f);

        private Vector3 _velocity;
        private Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void Start()
        {
            // 타겟이 Inspector에 연결돼 있지 않으면 태그로 탐색
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            var smoothed = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);

            if (useBounds && _cam != null && _cam.orthographic)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                smoothed.x = Mathf.Clamp(smoothed.x, roomMin.x + halfW, roomMax.x - halfW);
                smoothed.y = Mathf.Clamp(smoothed.y, roomMin.y + halfH, roomMax.y - halfH);
            }

            transform.position = smoothed;
        }

        /// <summary>방 크기를 타일맵 크기에 맞춰 자동 계산 (Editor 또는 런타임에서 호출)</summary>
        public void SetRoomBounds(Vector2 min, Vector2 max)
        {
            roomMin = min;
            roomMax = max;
        }
    }
}
