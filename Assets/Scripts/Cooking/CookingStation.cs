using System;
using System.Collections;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;
using System.Collections.Generic;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 주방의 조리대.
    /// 플레이어가 접근해 Interact 키 → CookingUI 열기 → 레시피 선택 → 요리.
    /// 요리 요청은 NetworkManager.RequestCook() 을 통해 서버에 전달된다.
    /// </summary>
    public class CookingStation : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] float m_CookDuration = 1.5f;

        [Header("VFX")]
        [SerializeField] ParticleSystem m_CookCompleteVFX;

        public event Action<RecipeData, FoodData> OnCookComplete;

        bool m_IsCooking;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract += TryOpenUI;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryOpenUI;
            UIManager.Instance?.Close("CookingUI");
        }

        void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnInteract -= TryOpenUI;

            if (PlayerDataManager.Instance?.Mastery != null)
                PlayerDataManager.Instance.Mastery.OnLevelUp -= OnMasteryLevelUp;
        }

        void OnEnable()
        {
            if (PlayerDataManager.Instance?.Mastery != null)
                PlayerDataManager.Instance.Mastery.OnLevelUp += OnMasteryLevelUp;
        }

        void OnMasteryLevelUp(uint recipeId, int newLevel)
        {
            var recipe = DataRegistry.Instance?.Recipes?.Get(recipeId);
            string name = recipe?.DisplayName ?? $"레시피({recipeId})";
            GameHUD.Instance?.ShowNotification($"{name} 마스터리 Lv{newLevel} 달성!", 2.5f);
        }

        void TryOpenUI()
        {
            if (m_IsCooking) return;

            var ui = UIManager.Instance?.GetPanel<CookingUI>("CookingUI");
            if (ui == null)
            {
                DebugUtil.LogWarning("[CookingStation] CookingUI 패널을 찾을 수 없습니다. HUD.prefab에 추가됐는지 확인하세요.");
                return;
            }
            ui.OpenFor(this);
        }

        /// <summary>CookingUI에서 선택된 레시피로 조리 시작. 등급은 현재 재료 품질에서 도출.</summary>
        public void CookRecipe(RecipeData recipe)
        {
            if (m_IsCooking || recipe == null) return;

            var mastery = PlayerDataManager.Instance?.Mastery;

            // 요리 전 인벤토리 품질로 등급 결정 (소모 전 미리 계산)
            FoodGrade grade = PlayerDataManager.Instance?.Inventory
                                  .CalculateCookingGrade(recipe) ?? FoodGrade.Normal;

            // 마스터리 등급 상향 확률 적용
            float gradeUpChance = mastery?.GetGradeUpChance(recipe.Id) ?? 0f;
            if (gradeUpChance > 0f && UnityEngine.Random.value < gradeUpChance)
            {
                grade = (FoodGrade)Mathf.Min((int)grade + 1, (int)FoodGrade.Legendary);
                DebugUtil.Log($"[CookingStation] 마스터리 등급 상향! → {grade}");
            }

            // 마스터리 조리 속도 적용
            float baseDuration  = recipe.CookTimeSeconds > 0 ? recipe.CookTimeSeconds : m_CookDuration;
            float speedMult     = mastery?.GetSpeedMultiplier(recipe.Id) ?? 1f;
            float finalDuration = baseDuration * speedMult;

            StartCoroutine(CookRoutine(recipe, grade, finalDuration));
        }

        IEnumerator CookRoutine(RecipeData recipe, FoodGrade grade, float duration)
        {
            m_IsCooking = true;
            DebugUtil.Log($"[CookingStation] 조리 시작: {recipe.DisplayName} [{grade}] ({duration:F1}s)");

            yield return new WaitForSeconds(duration);

            // 요리 요청 → 서버(스텁)에서 재료 검증·소모 + 음식 추가
            bool cookSuccess = false;
            FoodData resultFood = null;

            NetworkManager.Instance?.RequestCook(recipe, grade, (success, food) =>
            {
                cookSuccess = success;
                resultFood  = food;
            });

            if (!cookSuccess || resultFood == null)
            {
                GameHUD.Instance?.ShowNotification("재료가 부족합니다.", 2f);
                DebugUtil.LogWarning("[CookingStation] 요리 실패 또는 결과 음식 null.");
                m_IsCooking = false;
                yield break;
            }

            if (cookSuccess)
            {
                // 마스터리 기록
                PlayerDataManager.Instance?.Mastery?.RecordCook(recipe.Id);

                // 재료 절약 확률 — 성공 시 랜덤 재료 1개 환급
                float saveChance = PlayerDataManager.Instance?.Mastery?.GetIngredientSaveChance(recipe.Id) ?? 0f;
                if (saveChance > 0f && UnityEngine.Random.value < saveChance
                    && recipe.Ingredients?.Length > 0)
                {
                    int  idx     = UnityEngine.Random.Range(0, recipe.Ingredients.Length);
                    uint savedId = recipe.Ingredients[idx].IngredientId;
                    if (savedId != 0u)
                    {
                        NetworkManager.Instance?.RequestAddIngredient(savedId, 1);
                        DebugUtil.Log($"[CookingStation] 재료 절약! ID:{savedId} 1개 환급");
                    }
                }

                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayCookComplete(transform.position);
                else if (m_CookCompleteVFX != null)
                    m_CookCompleteVFX.Play();

                DebugUtil.Log($"[CookingStation] 완성! {resultFood?.DisplayName ?? "???"} [{grade}] → FoodInventory 추가");
                OnCookComplete?.Invoke(recipe, resultFood);

                ShowCookRewardPopup(resultFood);
            }

            m_IsCooking = false;
        }

        static void ShowCookRewardPopup(FoodData food)
        {
            var rewards = new List<RewardEntry>();
            if (food != null)
                rewards.Add(RewardEntry.Food(food.Id, 1));

            RewardPopup.Show("요리 완성!", rewards);
        }
    }
}
