namespace MonsterKitchen
{
    // ---------------------------------------------------------------
    //  무기 종류 — WeaponData.weaponType 에서 사용
    // ---------------------------------------------------------------
    public enum WeaponType
    {
        Sword     = 0,   // 검      (근거리 범위, 기본 밸런스)
        Axe       = 1,   // 도끼    (근거리 범위, 느리고 강함)
        DualSword = 2,   // 쌍검    (근거리, 빠름)
        Spear     = 3,   // 창      (근거리, 긴 범위)
        Dagger    = 4,   // 단검    (근거리, 매우 빠름)
        Bow       = 5,   // 활      (원거리 투사체)
        Gun       = 6,   // 총      (원거리 빠른 투사체)
        Staff     = 7,   // 지팡이  (원거리 AoE 투사체)
    }

    // ---------------------------------------------------------------
    //  능력치 종류 — AbilEntry.abilType 에서 사용
    //  장비가 플레이어 스탯에 더하는 수치의 종류를 정의한다.
    // ---------------------------------------------------------------
    public enum AbilType
    {
        Attack      = 0,   // 공격력
        Defense     = 1,   // 방어력
        MaxHp       = 2,   // 최대 체력
        Speed       = 3,   // 이동 속도
        AttackSpeed = 4,   // 공격 속도 (쿨타임 감소율)
        CritRate    = 5,   // 치명타 확률 (0~1)
        CritDamage  = 6,   // 치명타 데미지 배율
    }

    public enum AttributeType
    {
        None    = 0,
        Fire    = 1,
        Water   = 2,
        Wind    = 3,
        Earth   = 4,
        Magic   = 5,
        Poison  = 6,
    }

    public enum RarityType
    {
        Common    = 0,
        Uncommon  = 1,
        Rare      = 2,
        Epic      = 3,
        Legendary = 4,
    }

    public enum IngredientState
    {
        Raw       = 0,
        Cooked    = 1,
        Spoiled   = 2,
    }

    public enum CharacterRole
    {
        Warrior  = 0,
        Mage     = 1,
        Ranger   = 2,
        Support  = 3,
    }

    public enum FoodGrade
    {
        Normal    = 0,
        Good      = 1,
        Perfect   = 2,
        Legendary = 3,
    }
}
