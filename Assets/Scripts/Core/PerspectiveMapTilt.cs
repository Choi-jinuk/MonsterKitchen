using UnityEngine;
using MonsterKitchen.Data;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PerspectiveMapTilt — 맵/배경 GO 의 X 회전을 GameConfig 와 동기화
    //
    //  ▶ 역할
    //    1) [SerializeField] _config 참조가 씬 로드 시 GameConfig SO 를 메모리에 올린다
    //       → GameConfig.OnEnable → Current 자동 등록 → 전체 시스템이 값을 읽을 수 있음
    //    2) transform.eulerAngles.x = GameConfig.Current.tiltAngleDeg 적용
    //
    //  ▶ 배치 대상
    //    Background, DungeonGrid 등 기울어진 평면 역할의 GO.
    //
    //  ▶ Inspector 연결
    //    _config 슬롯에 Assets/Data/GameConfig.asset 을 드래그하면 완료.
    //    연결하지 않으면 PerspectiveManager 기본값(5°)으로 동작한다.
    //
    //  ▶ 향후 런타임 튜닝
    //    GameConfig.Current.tiltAngleDeg 를 변경하면
    //    LateUpdate 가 없어도 OnValidate(에디터) 또는 별도 Apply() 호출로 반영된다.
    //    실시간 슬라이더 UI 는 Apply() 를 public 으로 노출해 호출하면 된다.
    // ====================================================================

    [ExecuteAlways]
    public class PerspectiveMapTilt : MonoBehaviour
    {
        [Tooltip("씬 공통 설정 에셋. Assets/Data/GameConfig.asset 을 연결한다.")]
        [SerializeField] GameConfig _config;

        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable() => Apply();

#if UNITY_EDITOR
        void OnValidate() => Apply();
#endif

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
