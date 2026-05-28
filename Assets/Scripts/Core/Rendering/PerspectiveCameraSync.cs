using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PerspectiveCameraSync — 카메라 X 회전을 GameConfig 와 동기화
    //
    //  ▶ 배치 대상: Main Camera (또는 Cinemachine Brain 이 붙은 카메라 GO)
    //
    //  ▶ 역할
    //    1) [SerializeField] _config 참조가 씬 로드 시 GameConfig SO 를 메모리에 올린다
    //       → GameConfig.OnEnable → Current 자동 등록 → 전체 시스템이 값을 읽을 수 있음
    //    2) transform.eulerAngles.x = GameConfig.Current.tiltAngleDeg 적용
    //
    //  ▶ 에디터 옵저버
    //    Update() 폴링 대신 PerspectiveManager.OnTiltChanged 이벤트를 구독한다.
    //    GameConfig Inspector 에서 tiltAngleDeg 를 수정하면
    //    OnValidate → NotifyTiltChanged → Apply() 순으로 즉시 반영된다.
    //    런타임에서는 이벤트 없이 OnEnable 시 1회 적용한다.
    // ====================================================================

    [ExecuteAlways]
    public class PerspectiveCameraSync : MonoBehaviour
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
            var e = transform.eulerAngles;
            e.x = PerspectiveManager.TiltAngleDeg;
            transform.eulerAngles = e;
        }
    }
}
