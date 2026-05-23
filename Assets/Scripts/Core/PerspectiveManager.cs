using MonsterKitchen.Data;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  PerspectiveManager — Perspective 시스템 공통 값 접근 창구
    //
    //  ▶ pure static class — 씬에 배치 불필요, MonoBehaviour 없음.
    //  ▶ 모든 값은 GameConfig.Current 에서 읽는다.
    //  ▶ GameConfig 미로드 시 각 프로퍼티는 안전한 기본값을 반환한다.
    //
    //  ▶ 향후 맵 스폰 방식 전환 시
    //    맵 프리팹의 PerspectiveMapTilt.OnEnable 이 GameConfig 를 로드하므로
    //    스폰 직후 자동으로 전체 시스템이 갱신된다.
    // ====================================================================

    public static class PerspectiveManager
    {
        const float DefaultTilt = 5f;

        /// <summary>
        /// 현재 맵 기울기 (도). GameConfig.Current 에서 읽는다.
        /// GameConfig 미로드 시 기본값 5° 반환.
        /// </summary>
        public static float TiltAngleDeg =>
            GameConfig.Current != null ? GameConfig.Current.tiltAngleDeg : DefaultTilt;
    }
}
