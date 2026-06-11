using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Combat
{
    // ====================================================================
    //  KillQualityEvaluator — 처치 조건 → 재료 품질 계산기
    //
    //  ▶ 점수 계산
    //    속성 매칭 : killAttribute == monsterAttribute (None 제외) → +1
    //    CC 처치   : hadSoftCC(Sleep·Hypnosis) → +1
    //    무기 등급 : gap = weaponTier - ceil(monsterRarity / 2)
    //               gap < 0 → 0, gap == 0 → +1, gap == 1 → +2, gap ≥ 2 → +3
    //    total = min(3, 합산)
    //
    //  ▶ 품질 매핑
    //    0-1점 → IngredientQuality.I
    //    2점   → IngredientQuality.II
    //    3점   → IngredientQuality.III
    // ====================================================================

    public static class KillQualityEvaluator
    {
        /// <summary>
        /// 처치 조건으로 재료 품질을 결정한다.
        /// </summary>
        /// <param name="monsterRarity">MonsterData.Rarity 를 int 캐스트한 값 (0=Common … 4=Legendary)</param>
        /// <param name="monsterAttribute">몬스터 속성</param>
        /// <param name="killAttribute">처치에 사용된 속성</param>
        /// <param name="hadSoftCC">처치 시점에 Sleep 또는 Hypnosis CC 가 걸려 있었는지</param>
        /// <param name="weaponTier">장착 무기 Tier (WeaponData.Tier)</param>
        public static IngredientQuality Evaluate(
            int           monsterRarity,
            AttributeType monsterAttribute,
            AttributeType killAttribute,
            bool          hadSoftCC,
            int           weaponTier)
        {
            int score = 0;

            // 속성 매칭
            if (monsterAttribute != AttributeType.None && killAttribute == monsterAttribute)
                score += 1;

            // CC 처치
            if (hadSoftCC)
                score += 1;

            // 무기 등급
            int recommended = Mathf.CeilToInt(monsterRarity / 2f);
            int gap         = weaponTier - recommended;
            score += gap < 0 ? 0 : gap == 0 ? 1 : gap == 1 ? 2 : 3;

            score = Mathf.Min(3, score);

            return score switch
            {
                3 => IngredientQuality.III,
                2 => IngredientQuality.II,
                _ => IngredientQuality.I,
            };
        }
    }
}
