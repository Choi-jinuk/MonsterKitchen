namespace MonsterKitchen.Core
{
    /// <summary>
    /// 전체 프로젝트에서 에셋을 식별하는 문자열 키 상수.
    ///
    /// 규칙: "카테고리/이름" 소문자 슬래시 구분.
    ///  - prefab/  : 인스턴스화할 GameObject 프리팹
    ///  - data/    : ScriptableObject 데이터
    ///
    /// Addressables 마이그레이션 시:
    ///   AssetManifest 대신 Addressables 레이블/주소를 동일 키로 등록하면
    ///   호출부(AssetLoadManager.Instance.Load<T>(AssetKeys.XXX))는 무변경.
    /// </summary>
    public static class AssetKeys
    {
        // ── Prefabs ──────────────────────────────────────────────────
        public const string PrefabHud         = "prefab/hud";
        public const string PrefabPlayer      = "prefab/player";
        public const string PrefabDamagePopup = "prefab/damage_popup";
        public const string PrefabCustomer    = "prefab/customer";

        // ── Data SO ──────────────────────────────────────────────────
        public const string DataPlayerSpawn   = "data/player_spawn";
        public const string DataDungeonSpawn  = "data/dungeon_spawn";
    }
}
