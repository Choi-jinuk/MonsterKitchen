// ====================================================================
//  RewardEntry — 보상 항목 값 타입
//
//  ▶ Unity Object 참조 없음 — 순수 데이터
//  ▶ 새 타입 추가: RewardType 열거 → RewardPopup.BuildEntryElement switch 처리
// ====================================================================

namespace MonsterKitchen.UI
{
    // ── 보상 종류 ────────────────────────────────────────────────────
    public enum RewardType
    {
        Gold,
        Ingredient,
        Food,
        Exp,
        Item,       // 범용 아이템 (향후)
    }

    /// <summary>
    /// 보상 항목 하나를 나타내는 불변 값 타입.
    /// RewardPopup.Show() 에 리스트로 전달한다.
    ///
    /// 팩토리 메서드 사용:
    ///   RewardEntry.Gold(500)
    ///   RewardEntry.Ingredient(ING_001, 3)
    ///   RewardEntry.Food(FOOD_001, 1)
    ///   RewardEntry.Exp(120)
    ///   RewardEntry.Custom(RewardType.Item, id:42, amount:1, label:"전설의 검")
    /// </summary>
    public readonly struct RewardEntry
    {
        // ── 필드 ─────────────────────────────────────────────────────
        public RewardType Type        { get; }
        public uint       Id          { get; }     // Ingredient / Food / Item ID (Gold·Exp는 0)
        public int        Amount      { get; }
        public string     LabelOverride { get; }   // null → DataRegistry 에서 표시명 조회

        // ── 생성자 ────────────────────────────────────────────────────
        RewardEntry(RewardType type, uint id, int amount, string label)
        {
            Type          = type;
            Id            = id;
            Amount        = amount;
            LabelOverride = label;
        }

        // ── 팩토리 ────────────────────────────────────────────────────

        /// <summary>골드 보상.</summary>
        public static RewardEntry Gold(int amount)
            => new RewardEntry(RewardType.Gold, 0, amount, null);

        /// <summary>재료 보상.</summary>
        public static RewardEntry Ingredient(uint ingredientId, int qty)
            => new RewardEntry(RewardType.Ingredient, ingredientId, qty, null);

        /// <summary>음식 보상.</summary>
        public static RewardEntry Food(uint foodId, int qty)
            => new RewardEntry(RewardType.Food, foodId, qty, null);

        /// <summary>경험치 보상.</summary>
        public static RewardEntry Exp(int amount)
            => new RewardEntry(RewardType.Exp, 0, amount, null);

        /// <summary>커스텀 — 표시명 직접 지정.</summary>
        public static RewardEntry Custom(RewardType type, uint id, int amount, string label)
            => new RewardEntry(type, id, amount, label);
    }
}
