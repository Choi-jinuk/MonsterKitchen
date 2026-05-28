
namespace MonsterKitchen.Core
{
    /// <summary>
    /// 전체 프로젝트에서 에셋을 식별하는 문자열 키 상수.
    ///
    /// ── 카테고리 규칙 ────────────────────────────────────────────────
    ///   prefab/   인스턴스화할 GameObject 프리팹
    ///   sprite/   Sprite 텍스처
    ///   anim/     RuntimeAnimatorController
    ///   data/     ScriptableObject 데이터
    ///
    /// ── 데이터 주도 키 형식 (uint id 기반) ───────────────────────────
    ///   prefab/monster/{id}      → MonsterData.prefabAddress
    ///   sprite/monster/{id}      → MonsterData.spriteAddress
    ///   sprite/ingredient/{id}   → IngredientData.spriteAddress
    ///   sprite/food/{id}         → FoodData.spriteAddress
    ///   sprite/weapon/{id}       → WeaponData.weaponSpriteAddress
    ///   anim/weapon/{id}         → WeaponData.weaponAnimAddress
    ///   sprite/skill/{groupId}   → SkillGroupData.skillIconAddress
    ///
    /// ── Addressables 마이그레이션 ────────────────────────────────────
    ///   AssetManifest 대신 Addressables 레이블/주소를 동일 키로 등록하면
    ///   호출부(AssetLoadManager.Instance.Load<T>(key))는 무변경.
    /// </summary>
    public static class AssetKeys
    {
        // ── 고정 프리팹 ──────────────────────────────────────────────
        public const string PREFAB_HUD          = "prefab/hud";
        public const string PREFAB_PLAYER       = "prefab/player";
        public const string PREFAB_DAMAGE_POPUP = "prefab/damage_popup";
        public const string PREFAB_CUSTOMER     = "prefab/customer";

        // ── 고정 데이터 SO ───────────────────────────────────────────
        /// <summary>[사용 중단] PlayerSpawnData SO 키. Players.csv + DataRegistry 로 대체됨.</summary>
        [System.Obsolete("Use DataRegistry.GetPlayer(id) instead.")]
        public const string DATA_PLAYER_SPAWN  = "data/player_spawn";
        public const string DATA_DUNGEON_SPAWN = "data/dungeon_spawn";
        public const string DATA_TABLE_DATA    = "data/table_data";

        // ── 데이터 주도 키 생성 헬퍼 — ZString.Format 으로 boxing 최소화 ─
        public static string MonsterPrefab     (uint id)      => StringUtil.Format("prefab/monster/{0}",     id);
        public static string MonsterSprite     (uint id)      => StringUtil.Format("sprite/monster/{0}",     id);
        public static string MonsterBT         (uint id)      => StringUtil.Format("bt/monster/{0}",         id);
        public static string PlayerBT          (uint id)      => StringUtil.Format("bt/player/{0}",           id);
        public static string IngredientSprite  (uint id)      => StringUtil.Format("sprite/ingredient/{0}",  id);
        public static string FoodSprite        (uint id)      => StringUtil.Format("sprite/food/{0}",        id);
        public static string WeaponSprite      (uint id)      => StringUtil.Format("sprite/weapon/{0}",      id);
        public static string WeaponAnim        (uint id)      => StringUtil.Format("anim/weapon/{0}",        id);
        public static string SkillIcon         (string gid)   => StringUtil.Format("sprite/skill/{0}",       gid);
        public static string ProjectilePrefab  (string sid)   => StringUtil.Format("prefab/projectile/{0}",  sid);
        public static string PlayerCharPrefab  (uint id)      => StringUtil.Format("prefab/player/{0}",      id);
    }
}
