using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PerspectiveEntityTilt — 엔티티 X 회전을 카메라 틸트 각도에 동기화
    //
    //  ▶ 배치 대상
    //    Player, Enemy, NPC, SceneObject 등 2.5D 평면 위에 서 있는 오브젝트.
    //    스프라이트 렌더러가 카메라를 향하도록 기울어진다.
    //
    //  ▶ 원리
    //    카메라가 X = +tiltAngleDeg 로 아래를 바라볼 때
    //    엔티티도 X = +tiltAngleDeg 로 기울이면 스프라이트가 카메라 시선과 수직이 되어
    //    "기울어진 바닥 위에 서 있는" 자연스러운 2.5D 표현이 만들어진다.
    //
    //  ▶ 에디터 옵저버
    //    Update() 폴링 대신 PerspectiveManager.OnTiltChanged 이벤트를 구독한다.
    //    GameConfig Inspector 에서 tiltAngleDeg 를 수정하면
    //    OnValidate → NotifyTiltChanged → Apply() 순으로 즉시 반영된다.
    //    런타임에서는 이벤트 없이 OnEnable 시 1회 적용한다.
    //
    //  ▶ 회전 충돌 없음
    //    Unity 2D 물리(Rigidbody2D)는 Z 축 회전만 사용하므로 X 회전 변경과 무관하다.
    // ====================================================================

    [ExecuteAlways]
    public class PerspectiveEntityTilt : MonoBehaviour
    {
        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable()
        {
            Apply();
#if UNITY_EDITOR
            PerspectiveManager.OnTiltChanged += Apply;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            PerspectiveManager.OnTiltChanged -= Apply;
#endif
        }

        // ================================================================
        //  적용
        // ================================================================

        public void Apply()
        {
            float tilt = PerspectiveManager.TiltAngleDeg;
            var e = transform.eulerAngles;
            if (Mathf.Approximately(e.x, tilt)) return;
            e.x = tilt;
            transform.eulerAngles = e;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
