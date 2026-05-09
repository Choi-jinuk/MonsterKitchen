using System;
using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Data;
using MonsterKitchen.UI;
using UnityEngine;

namespace MonsterKitchen.Cooking
{
    /// <summary>
    /// 주방의 조리대. 플레이어가 접근해 Space키 → CookingUI 열기 → 레시피 선택 → 요리.
    /// allRecipes는 DataRegistry.AllRecipes에서 런타임 조회 (Inspector 참조 제거).
    /// </summary>
    public class CookingStation : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] float cookDuration = 1.5f;

        [Header("VFX")]
        [SerializeField] ParticleSystem cookCompleteVFX;

        public event Action<RecipeData, FoodData> OnCookComplete;

        bool _playerNearby;
        bool _isCooking;

        void Update()
        {
            if (!_playerNearby || _isCooking) return;

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                OpenCookingUI();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                _playerNearby = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerNearby = false;
                // 플레이어가 조리대 범위를 벗어나면 UI 닫기
                UIManager.Instance?.Close("CookingUI");
            }
        }

        void OpenCookingUI()
        {
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

            FoodData food = recipe.resultFood;
            if (food != null)
                FoodInventory.Instance?.AddWithGrade(food.id, grade);

            if (cookCompleteVFX != null)
                cookCompleteVFX.Play();

            Debug.Log($"[CookingStation] 완성! {food?.displayName ?? "???"} [{grade}] → FoodInventory 추가");

            OnCookComplete?.Invoke(recipe, food);
            _isCooking = false;
        }
    }
}
