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
        }

        void TryOpenUI()
        {
            if (m_IsCooking) return;

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
            if (m_IsCooking || recipe == null) return;
            StartCoroutine(CookRoutine(recipe, grade));
        }

        IEnumerator CookRoutine(RecipeData recipe, FoodGrade grade)
        {
            m_IsCooking = true;
            Debug.Log($"[CookingStation] 조리 시작: {recipe.DisplayName} [{grade}] ({m_CookDuration}s)");

            yield return new WaitForSeconds(m_CookDuration);

            // 요리 요청 → 서버(스텁)에서 재료 검증·소모 + 음식 추가
            bool cookSuccess = false;
            FoodData resultFood = null;

            NetworkManager.Instance?.RequestCook(recipe, grade, (success, food) =>
            {
                cookSuccess = success;
                resultFood  = food;
            });

            if (cookSuccess)
            {
                if (m_CookCompleteVFX != null)
                    m_CookCompleteVFX.Play();

                Debug.Log($"[CookingStation] 완성! {resultFood?.DisplayName ?? "???"} [{grade}] → FoodInventory 추가");
                OnCookComplete?.Invoke(recipe, resultFood);
            }
            else
            {
                Debug.LogWarning("[CookingStation] 요리 실패 (재료 부족).");
            }

            m_IsCooking = false;
        }
    }
}
