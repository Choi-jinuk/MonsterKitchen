using System;
using System.Collections;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 주방의 조리대.
    /// 플레이어가 접근해 Interact 키 → CookingUI 열기 → 레시피 선택 → 요리.
    /// </summary>
    public class CookingStation : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] float cookDuration = 1.5f;

        [Header("VFX")]
        [SerializeField] ParticleSystem cookCompleteVFX;

        public event Action<RecipeData, FoodData> OnCookComplete;

        bool _isCooking;

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
        }

        void TryOpenUI()
        {
            if (_isCooking) return;

            var ui = UIManager.Instance?.GetPanel<CookingUI>("CookingUI");
            if (ui == null)
            {
                Debug.LogWarning("[CookingStation] CookingUI 패널을 찾을 수 없습니다. HUD.prefab에 추가됐는지 확인하세요.");
                return;
            }
            ui.OpenFor(this);
        }

        /// <summary>CookingUI에서 선택된 레시피와 등급으로 조리 시작.</summary>
        public void CookRecipe(RecipeData recipe, FoodGrade grade = FoodGrade.Normal)
        {
            if (_isCooking || recipe == null) return;
            StartCoroutine(CookRoutine(recipe, grade));
        }

        IEnumerator CookRoutine(RecipeData recipe, FoodGrade grade)
        {
            _isCooking = true;
            Debug.Log($"[CookingStation] 조리 시작: {recipe.displayName} [{grade}] ({cookDuration}s)");

            yield return new WaitForSeconds(cookDuration);

            if (!RecipeMatcher.ConsumeIngredients(recipe, Inventory.Instance))
            {
                Debug.LogWarning("[CookingStation] 재료 소모 실패.");
                _isCooking = false;
                yield break;
            }

            FoodData food = DataRegistry.Instance?.GetFood(recipe.resultFoodId);
            if (food != null)
                FoodInventory.Instance?.AddWithGrade(recipe.resultFoodId, grade);

            if (cookCompleteVFX != null)
                cookCompleteVFX.Play();

            Debug.Log($"[CookingStation] 완성! {food?.displayName ?? "???"} [{grade}] → FoodInventory 추가");

            OnCookComplete?.Invoke(recipe, food);
            _isCooking = false;
        }
    }
}
