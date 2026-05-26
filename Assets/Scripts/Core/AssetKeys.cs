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
    ///   형식 예시: $"prefab/monster/{data.id}"
    ///
    /// ── Addressables 마이그레이션 ────────────────────────────────────
    ///   AssetManifest 대신 Addressables 레이블/주소를 동일 키로 등록하면
    ///   호출부(AssetLoadManager.Instance.Load<T>(key))는 무변경.
    /// </summary>
    public static class AssetKeys
    {
        // ── 고정 프리팹 ──────────────────────────────────────────────
        public const string PrefabHud         = "prefab/hud";
        public const string PrefabPlayer      = "prefab/player";
        public const string PrefabDamagePopup = "prefab/damage_popup";
        public const string PrefabCustomer    = "prefab/customer";

        // ── 고정 데이터 SO ───────────────────────────────────────────
        public const string DataPlayerSpawn   = "data/player_spawn";
        public const string DataDungeonSpawn  = "data/dungeon_spawn";

        // ── 데이터 주도 키 생성 헬퍼 ─────────────────────────────────
        public static string MonsterPrefab(uint id)    => $"prefab/monster/{id}";
        public static string MonsterSprite(uint id)    => $"sprite/monster/{id}";
        public static string IngredientSprite(uint id) => $"sprite/ingredient/{id}";
        public static string FoodSprite(uint id)       => $"sprite/food/{id}";
        public static string WeaponSprite(uint id)     => $"sprite/weapon/{id}";
        public static string WeaponAnim(uint id)       => $"anim/weapon/{id}";
        public static string SkillIcon(string groupId) => $"sprite/skill/{groupId}";
    }
}
