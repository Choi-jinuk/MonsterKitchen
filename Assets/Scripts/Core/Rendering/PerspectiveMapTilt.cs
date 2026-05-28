using UnityEngine;
using MonsterKitchen.Data;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PerspectiveMapTilt — 맵/배경 GO 의 X 회전을 0 으로 고정 (평면 유지)
    //
    //  ▶ 역할
    //    1) [SerializeField] _config 로 GameConfig SO 를 메모리에 올린다
    //       → GameConfig.OnEnable → Current 자동 등록 → 전체 시스템 동작
    //    2) X 회전을 항상 0 으로 고정 (에디터에서 실수로 회전시켜도 되돌림)
    //
    //  ▶ 새로운 2.5D 방식
    //    카메라가 tiltAngleDeg 만큼 기울어져 있으므로
    //    맵/배경은 평면(X=0)을 유지해도 자연스러운 2.5D 바닥으로 보인다.
    //
    //  ▶ 배치 대상
    //    Background, DungeonGrid, GroundTilemap 등 바닥 평면 GO.
    // ====================================================================

    [ExecuteAlways]
    public class PerspectiveMapTilt : MonoBehaviour
    {
        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable() => Apply();

        // X=0 고정을 Update() 로 계속 감시 — 에디터에서 실수 회전 방지
        void Update()
        {
            if (!Mathf.Approximately(transform.eulerAngles.x, 0f))
                Apply();
        }

        // ================================================================
        //  적용
        // ================================================================

        public void Apply()
        {
            var e = transform.eulerAngles;
            e.x = 0f;
            transform.eulerAngles = e;
        }
    }
}
