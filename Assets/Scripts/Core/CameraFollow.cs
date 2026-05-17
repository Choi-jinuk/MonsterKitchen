using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 메인 카메라가 플레이어를 부드럽게 추적한다.
    ///
    /// ▶ 방 경계(Room Bounds)
    ///   roomMin / roomMax 범위 안에서만 카메라가 이동한다.
    ///
    /// ▶ 통로 자동 감지
    ///   플레이어가 방 경계 밖으로 corridorDetectThreshold 이상 벗어나면
    ///   카메라가 bounds 제한 없이 플레이어를 자유 추적한다.
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

        [Header("Corridor Detection")]
        [Tooltip("플레이어가 방 경계를 이 거리(units) 이상 벗어나면 통로로 판단해 카메라가 자유 추적한다.")]
        [SerializeField] private float corridorDetectThreshold = 1.5f;

        private Vector3 _velocity;
        private Camera  _cam;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var desired  = new Vector3(target.position.x, target.position.y, transform.position.z);
            var smoothed = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);

            if (useBounds && _cam != null && _cam.orthographic)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                float clampedX = Mathf.Clamp(smoothed.x, roomMin.x + halfW, roomMax.x - halfW);
                float clampedY = Mathf.Clamp(smoothed.y, roomMin.y + halfH, roomMax.y - halfH);

                // 자동 통로 감지: 플레이어가 bounds 밖으로 threshold 이상 벗어났으면 자유 추적
                float overflowX = Mathf.Abs(smoothed.x - clampedX);
                float overflowY = Mathf.Abs(smoothed.y - clampedY);
                bool autoCorridor = overflowX > corridorDetectThreshold ||
                                    overflowY > corridorDetectThreshold;

                if (!autoCorridor)
                {
                    smoothed.x = clampedX;
                    smoothed.y = clampedY;
                }
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
