using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 플레이어를 SmoothDamp 로 부드럽게 따라다니는 카메라.
    /// Cinemachine 없이 동작하며 Z 축은 고정한다.
    ///
    /// ▶ Inspector 설정
    ///   m_Target     : 따라갈 Transform (Player). 비우면 Start() 에서 태그 조회.
    ///   m_SmoothTime : 반응 시간(초). 작을수록 빠름 (기본 0.12)
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform m_Target;

        [Tooltip("target 과 카메라 사이의 보정값")]
        [SerializeField] private Vector3 m_Correction = new Vector3(0, -3, 0);
        [Tooltip("SmoothDamp 반응 시간(초). 작을수록 더 빠르게 따라간다.")]
        [SerializeField] float m_SmoothTime = 0.12f;

        Vector3 m_Velocity;
        float   m_FixedZ;

        // ================================================================
        //  공개 API
        // ================================================================

        public void SetTarget(Transform t) => m_Target = t;

        // ================================================================
        //  Mono
        // ================================================================

        void Start()
        {
            m_FixedZ = transform.position.z;

            if (m_Target == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) m_Target = go.transform;
                else Debug.LogWarning("[CameraFollow] 'Player' 태그 오브젝트를 찾을 수 없습니다.", this);
            }
        }

        void LateUpdate()
        {
            if (m_Target == null) return;

            Vector3 desired = new Vector3(m_Target.position.x, m_Target.position.y, m_FixedZ);
            desired += m_Correction;

            Vector3 smoothed = Vector3.SmoothDamp(
                transform.position, desired, ref m_Velocity, m_SmoothTime);
            smoothed.z = m_FixedZ;

            transform.position = smoothed;
        }
    }
}
