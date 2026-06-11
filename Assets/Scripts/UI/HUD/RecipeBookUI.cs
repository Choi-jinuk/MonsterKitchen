using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  RecipeBookUI — 레시피 도감 패널. UIPanel(isPopup=true), Tab 키 토글.
    //
    //  ▶ 해금 조건
    //    Mastery.GetCookCount(recipeId) > 0 (한 번이라도 요리)
    //    OR PlayerInventoryData.GetIngredientCount(id) > 0 (재료 1종 이상 보유)
    //
    //  ▶ 카드 표시
    //    해금: 이름 + 재료 목록 + 결과 음식 + 마스터리 ★ + 요리 횟수
    //    미해금: ??? + 회색 처리
    // ====================================================================

    public class RecipeBookUI : UIPanel
    {
        ScrollView m_CardList;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_CardList = Root?.Q<ScrollView>("recipe-card-list");
            Root?.Q<Button>("recipe-book-close-btn")
                ?.RegisterCallback<ClickEvent>(_ => UIManager.Instance?.Close(PanelId));
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        // ── 카드 목록 갱신 ────────────────────────────────────────────

        void Refresh()
        {
            m_CardList?.Clear();

            var recipes = DataRegistry.Instance?.Recipes?.All;
            if (recipes == null) return;

            foreach (var recipe in recipes)
            {
                bool unlocked = IsUnlocked(recipe);
                var card = BuildCard(recipe, unlocked);
                m_CardList?.Add(card);
            }
        }

        bool IsUnlocked(RecipeData recipe)
        {
            var inv     = PlayerDataManager.Instance?.Inventory;
            var mastery = PlayerDataManager.Instance?.Mastery;

            // 한 번이라도 요리한 레시피 → 해금
            if (mastery != null && mastery.GetCookCount(recipe.Id) > 0) return true;

            // 재료 1종 이상 보유 → 해금
            if (inv != null && recipe.Ingredients != null)
            {
                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.IngredientId != 0u && inv.GetIngredientCount(ing.IngredientId) > 0)
                        return true;
                }
            }

            return false;
        }

        // ── 카드 빌더 ─────────────────────────────────────────────────

        VisualElement BuildCard(RecipeData recipe, bool unlocked)
        {
            var card = new VisualElement();
            card.AddToClassList("recipe-card");
            if (!unlocked) card.AddToClassList("recipe-card--locked");

            // 결과 음식 아이콘
            var icon = new VisualElement();
            icon.AddToClassList("recipe-card-icon");
            if (unlocked)
            {
                var foodData = DataRegistry.Instance?.Foods?.Get(recipe.ResultFoodId);
                if (foodData != null && !string.IsNullOrEmpty(foodData.SpriteAddress))
                {
                    var sprite = AssetLoadManager.Instance?.Load<Sprite>(foodData.SpriteAddress);
                    if (sprite != null)
                        icon.style.backgroundImage = new StyleBackground(sprite);
                }
            }
            card.Add(icon);

            // 정보 컬럼
            var info = new VisualElement();
            info.AddToClassList("recipe-card-info");

            var nameLabel = new Label(unlocked ? recipe.DisplayName : "???");
            nameLabel.AddToClassList("recipe-card-name");
            info.Add(nameLabel);

            // 재료 목록
            var ingLabel = new Label(BuildIngredientString(recipe, unlocked));
            ingLabel.AddToClassList("recipe-card-detail");
            info.Add(ingLabel);

            // 결과 음식
            string resultStr = unlocked
                ? (DataRegistry.Instance?.Foods?.Get(recipe.ResultFoodId)?.DisplayName ?? "???")
                : "???";
            var resultLabel = new Label($"결과: {resultStr}");
            resultLabel.AddToClassList("recipe-card-result");
            info.Add(resultLabel);

            // 마스터리 보너스 표시 (Lv2 이상만)
            if (unlocked)
            {
                var mastery     = PlayerDataManager.Instance?.Mastery;
                float speed     = mastery?.GetSpeedMultiplier(recipe.Id) ?? 1f;
                float save      = mastery?.GetIngredientSaveChance(recipe.Id) ?? 0f;
                float gradeUp   = mastery?.GetGradeUpChance(recipe.Id) ?? 0f;
                string bonusStr = BuildBonusString(speed, save, gradeUp);
                if (!string.IsNullOrEmpty(bonusStr))
                {
                    var bonusLabel = new Label(bonusStr);
                    bonusLabel.AddToClassList("recipe-card-bonus");
                    info.Add(bonusLabel);
                }
            }

            card.Add(info);

            // 마스터리 컬럼 (우측)
            var masteryCol = new VisualElement();
            masteryCol.AddToClassList("recipe-card-mastery");

            int    level     = PlayerDataManager.Instance?.Mastery?.GetLevel(recipe.Id) ?? 1;
            int    cookCount = PlayerDataManager.Instance?.Mastery?.GetCookCount(recipe.Id) ?? 0;
            string stars     = BuildStars(level);

            var starsLabel = new Label(stars);
            starsLabel.AddToClassList("recipe-card-stars");
            masteryCol.Add(starsLabel);

            var countLabel = new Label($"Lv{level}  {cookCount}회");
            countLabel.AddToClassList("recipe-card-cook-count");
            masteryCol.Add(countLabel);

            card.Add(masteryCol);

            return card;
        }

        string BuildIngredientString(RecipeData recipe, bool unlocked)
        {
            if (recipe.Ingredients == null || recipe.Ingredients.Length == 0)
                return "재료: 없음";

            var sb = new System.Text.StringBuilder("재료: ");
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                if (i > 0) sb.Append("  ");

                var ing = recipe.Ingredients[i];
                if (!unlocked || ing.IngredientId == 0u)
                {
                    sb.Append("?×?");
                }
                else
                {
                    var data = DataRegistry.Instance?.Ingredients?.Get(ing.IngredientId);
                    string name = data?.DisplayName ?? $"ID:{ing.IngredientId}";
                    sb.Append($"{name}×{ing.Quantity}");
                }
            }
            return sb.ToString();
        }

        static string BuildBonusString(float speedMult, float saveChance, float gradeUpChance)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (speedMult < 1f)
                parts.Add($"조리속도 -{Mathf.RoundToInt((1f - speedMult) * 100f)}%");
            if (saveChance > 0f)
                parts.Add($"재료절약 {Mathf.RoundToInt(saveChance * 100f)}%");
            if (gradeUpChance > 0f)
                parts.Add($"등급상향 {Mathf.RoundToInt(gradeUpChance * 100f)}%");
            return parts.Count > 0 ? string.Join("  ", parts) : string.Empty;
        }

        static string BuildStars(int level)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 5; i++) sb.Append(i < level ? "★" : "☆");
            return sb.ToString();
        }
    }
}
