using UnityEngine;
using MonsterKitchen.Core;

namespace MonsterKitchen.Navigation
{
    // ====================================================================
    //  NavPolyObstacle — 폴리곤 형태의 이동 불가 영역 컴포넌트
    //
    //  사용 방법 (두 가지 중 하나 선택):
    //
    //  [방법 A] PolygonCollider2D 사용 (권장)
    //    1. 장애물 오브젝트에 이 컴포넌트 + PolygonCollider2D 를 추가한다.
    //    2. PolygonCollider2D.isTrigger = true 로 설정 (물리 충돌 비활성화).
    //    3. 씬 뷰에서 PolygonCollider2D 꼭짓점을 스프라이트 윤곽에 맞게 편집한다.
    //
    //  [방법 B] Inspector 직접 입력
    //    1. 이 컴포넌트만 추가한다.
    //    2. Points 배열에 로컬 좌표 꼭짓점을 입력한다 (반시계 방향 권장).
    //
    //  → NavGrid.PointIsValid() 가 이 장애물을 자동으로 고려한다.
    //    별도 코드 없이 배치만 하면 된다.
    // ====================================================================

    [AddComponentMenu("Navigation/NavPolyObstacle")]
    public class NavPolyObstacle : MonoBehaviour
    {
        [Tooltip("로컬 좌표 폴리곤 꼭짓점.\n" +
                 "PolygonCollider2D 가 있으면 이 값은 무시된다.")]
        [SerializeField] Vector2[] m_Points;

        PolygonCollider2D m_Poly;
        Vector2[]         m_CachedWorld;
        bool              m_Dirty = true;

        // ── Mono ──────────────────────────────────────────────────────────

        void Awake()
        {
            m_Poly = GetComponent<PolygonCollider2D>();
        }

        void OnEnable()
        {
            NavObstacleLayer.Register(this);
            m_Dirty = true;
        }

        void OnDisable()
        {
            NavObstacleLayer.Unregister(this);
        }

        void LateUpdate()
        {
            if (transform.hasChanged)
            {
                m_Dirty = true;
                transform.hasChanged = false;
            }
        }

        void OnValidate()
        {
            m_Dirty = true;
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>월드 좌표 pos 가 이 장애물 폴리곤 내부이면 true 를 반환한다.</summary>
        public bool ContainsPoint(Vector2 worldPos)
        {
            // PolygonCollider2D 가 있으면 Unity 내장 메서드 사용 (서브-픽셀 정밀도)
            if (m_Poly != null)
                return m_Poly.OverlapPoint(worldPos);

            // m_Points 기반 수동 판정
            RefreshCache();
            if (m_CachedWorld == null || m_CachedWorld.Length < 3) return false;
            return PointInPolygon(worldPos, m_CachedWorld);
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        void RefreshCache()
        {
            if (!m_Dirty && m_CachedWorld != null) return;

            if (m_Points == null || m_Points.Length < 3)
            {
                m_CachedWorld = null;
                m_Dirty = false;
                return;
            }

            if (m_CachedWorld == null || m_CachedWorld.Length != m_Points.Length)
                m_CachedWorld = new Vector2[m_Points.Length];

            for (int i = 0; i < m_Points.Length; i++)
                m_CachedWorld[i] = transform.TransformPoint(m_Points[i]);

            m_Dirty = false;
        }

        /// <summary>
        /// 레이캐스팅(Jordan 곡선 정리) 방식 점-폴리곤 포함 판정.
        /// 반직선이 폴리곤 변과 홀수 번 교차하면 내부.
        /// </summary>
        static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            int cross = 0;
            int n     = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                // p 에서 오른쪽으로 수평 반직선 교차 여부
                if ((a.y <= p.y && b.y > p.y) || (b.y <= p.y && a.y > p.y))
                {
                    float t = (p.y - a.y) / (b.y - a.y);
                    if (p.x < a.x + t * (b.x - a.x))
                        cross++;
                }
            }
            return (cross & 1) == 1;
        }

        // ── Gizmo ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // PolygonCollider2D 가 있으면 그 경로, 없으면 m_Points 사용
            Vector2[] src = null;
            if (m_Poly != null && m_Poly.pathCount > 0)
                src = m_Poly.GetPath(0);
            else
                src = m_Points;

            if (src == null || src.Length < 2) return;

            // 2.5D 기울기 행렬 적용
            float     tiltDeg    = PerspectiveManager.TiltAngleDeg;
            Matrix4x4 savedMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.Rotate(Quaternion.Euler(tiltDeg, 0f, 0f));

            var t = transform;

            // 외곽선
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.9f);
            for (int i = 0; i < src.Length; i++)
            {
                Vector3 a = t.TransformPoint(src[i]);
                Vector3 b = t.TransformPoint(src[(i + 1) % src.Length]);
                Gizmos.DrawLine(a, b);
            }

            // 내부 중심 방사선 (영역 인식용)
            if (src.Length >= 3)
            {
                Vector2 center = Vector2.zero;
                foreach (var pt in src) center += pt;
                center /= src.Length;
                Vector3 worldCenter = t.TransformPoint(center);

                Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.18f);
                for (int i = 0; i < src.Length; i++)
                    Gizmos.DrawLine(worldCenter, t.TransformPoint(src[i]));
            }

            Gizmos.matrix = savedMatrix;
        }
#endif
    }
}
