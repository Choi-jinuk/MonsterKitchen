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
    //  ▶ 에디터 전용 옵저버
    //    GameConfig.OnValidate() → NotifyTiltChanged() → OnTiltChanged 이벤트.
    //    PerspectiveCameraSync, PerspectiveEntityTilt 가 이 이벤트를 구독해
    //    Inspector 에서 값 변경 시 즉시 반영된다.
    //    런타임에서는 값이 바뀌지 않으므로 이벤트를 사용하지 않는다.
    // ====================================================================

    public static class PerspectiveManager
    {
        const float DefaultTilt = 5f;

        /// <summary>
        /// 현재 맵 기울기 (도). GameConfig.Current 에서 읽는다.
        /// GameConfig 미로드 시 기본값 5° 반환.
        /// </summary>
        public static float TiltAngleDeg =>
            GameConfig.Current != null ? GameConfig.Current.TiltAngleDeg : DefaultTilt;

#if UNITY_EDITOR
        /// <summary>
        /// [에디터 전용] TiltAngleDeg 값이 변경될 때 발행된다.
        /// GameConfig.OnValidate() 에서 NotifyTiltChanged() 를 호출해 구독자에게 알린다.
        /// </summary>
        public static event System.Action OnTiltChanged;

        /// <summary>
        /// [에디터 전용] GameConfig 가 변경됐을 때 호출한다.
        /// </summary>
        internal static void NotifyTiltChanged() => OnTiltChanged?.Invoke();
#endif
    }
}
