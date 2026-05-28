using UnityEngine;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  GameConfig — 전체 게임 공통 설정값 저장소 (ScriptableObject)
    //
    //  ▶ 목적
    //    코드에 하드코딩되던 "define-like" 상수들을 Inspector 에서 설정 가능한
    //    단일 에셋으로 관리한다. 씬마다 배치하지 않고 하나의 .asset 파일만 유지.
    //
    //  ▶ 로드 방식
    //    PerspectiveMapTilt (Background GO) 의 [SerializeField] 참조가
    //    씬 로드 시 이 SO 를 메모리에 올리고, OnEnable 이 Current 에 자동 등록한다.
    //    Resources.Load 사용 없음.
    //
    //  ▶ 에디터 튜닝
    //    Inspector 에서 tiltAngleDeg 를 수정하면 OnValidate → PerspectiveManager.NotifyTiltChanged
    //    → PerspectiveCameraSync / PerspectiveEntityTilt 가 즉시 반영한다.
    //    빌드에서는 인메모리 변경만 되고 에셋 파일은 수정되지 않는다.
    //
    //  ▶ 에셋 위치
    //    Assets/Data/GameConfig.asset (프로젝트에 하나만 존재)
    // ====================================================================

    [CreateAssetMenu(fileName = "GameConfig", menuName = "MonsterKitchen/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        // ── Singleton-like 접근 ────────────────────────────────────────
        static GameConfig s_Current;

        /// <summary>
        /// 현재 로드된 GameConfig 인스턴스.
        /// PerspectiveMapTilt 를 포함한 씬이 로드되면 OnEnable 에서 자동 등록된다.
        /// 미등록 시 null — 각 시스템은 기본값(fallback)으로 동작해야 한다.
        /// </summary>
        public static GameConfig Current => s_Current;

        // SO 가 메모리에 로드되는 순간(씬 로드 포함) 자동으로 등록된다.
        void OnEnable()
        {
            s_Current = this;
            Core.PerspectiveManager.NotifyTiltChanged();
        }

        // ── 2.5D Perspective ──────────────────────────────────────────
        [Header("2.5D Perspective")]
        [Tooltip("맵 기울기 각도 (도).\n" +
                 "PerspectiveCameraSync: 카메라 X 회전값.\n" +
                 "PerspectiveEntityTilt: 엔티티 X 회전값.")]
        [Range(-89f, 89f)]
        public float TiltAngleDeg = -20f;

#if UNITY_EDITOR
        void OnValidate() => Core.PerspectiveManager.NotifyTiltChanged();
#endif

        // ── 향후 확장 예정 (주석 해제해서 추가) ──────────────────────
        // [Header("Camera")]
        // [Tooltip("Perspective 카메라 FOV.")]
        // [Range(10f, 120f)]
        // public float cameraFov = 55f;

        // [Header("Player")]
        // [Tooltip("기본 이동 속도.")]
        // public float playerMoveSpeed = 5f;

        // [Header("Combat")]
        // [Tooltip("기본 데미지 배율.")]
        // public float baseDamageMultiplier = 1f;
    }
}
