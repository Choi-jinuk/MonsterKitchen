using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PerspectivePlaneSync — 기울어진 맵 평면에 Z 위치 동기화
    //
    //  ▶ 원리
    //    PerspectiveManager.TiltAngleDeg (static) 를 읽어
    //    Z = Y * sin(tiltAngle) 으로 엔티티 Z 를 맞춘다.
    //    Perspective 카메라가 Z 거리 차이를 자연스러운 원근감으로 변환한다.
    //
    //  ▶ 스케일 조작 없음
    //    크기 변화는 오직 Perspective 카메라의 Z 거리에서만 발생한다.
    //
    //  ▶ 2D 물리 영향 없음
    //    Physics2D / Rigidbody2D 는 XY 평면만 사용하므로 Z 변경과 무관하다.
    //
    //  ▶ isStatic 플래그
    //    true  = 씬에 고정 배치된 오브젝트 (ToolObject, ResourceNode 등).
    //            플레이 모드 Start() 에서 Z 를 1회 적용 후 컴포넌트 비활성화.
    //            에디터 모드에서는 비활성화하지 않아 씬 프리뷰를 지원한다.
    //    false = 런타임에 이동하는 엔티티 (Player, 몬스터, 손님).
    //            LateUpdate() 마다 Z 를 갱신한다.
    //
    //  ▶ 틸트 값
    //    PerspectiveManager.TiltAngleDeg static 을 참조.
    //    PerspectiveManager 가 없는 씬에서는 기본값 5° 를 사용한다.
    // ====================================================================

    [ExecuteAlways]
    public class PerspectivePlaneSync : MonoBehaviour
    {
        [Tooltip("true = 씬 고정 오브젝트. 플레이 모드에서 Start() 1회 적용 후 비활성화.")]
        [SerializeField] bool _isStatic = false;

        public bool IsStatic => _isStatic;

        // ================================================================
        //  Mono
        // ================================================================

        void Start()
        {
            ApplyZ();
            // 플레이 모드에서만 정적 오브젝트를 비활성화.
            // 에디터 모드에서는 LateUpdate 유지 → 씬 프리뷰 지원.
            if (Application.isPlaying && _isStatic)
                enabled = false;
        }

        void LateUpdate() => ApplyZ();

        // ================================================================
        //  Z 동기화
        // ================================================================

        void ApplyZ()
        {
            var pos = transform.position;
            // tan 을 사용해야 기울어진 평면 표면과 정확히 일치한다.
            // sin 은 소각도(5° 이하)에서만 근사값으로 허용되며,
            // 45° 에서는 sin=0.707 vs tan=1.0 → 오차가 커서 오브젝트가 떠 보인다.
            pos.z = pos.y * Mathf.Tan(PerspectiveManager.TiltAngleDeg * Mathf.Deg2Rad);
            transform.position = pos;
        }

        // ================================================================
        //  공개 API
        // ================================================================

        /// <summary>isStatic 여부를 런타임에서 설정한다 (스폰 직후 호출 가능).</summary>
        public void SetStatic(bool isStatic)
        {
            _isStatic = isStatic;
            ApplyZ();
            if (Application.isPlaying && isStatic) enabled = false;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            float expectedZ = transform.position.y
                              * Mathf.Tan(PerspectiveManager.TiltAngleDeg * Mathf.Deg2Rad);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position,
                            transform.position + new Vector3(0f, 0f, expectedZ - transform.position.z + 0.5f));
        }
#endif
    }
}
