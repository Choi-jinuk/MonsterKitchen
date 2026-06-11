using System;
using System.Collections.Generic;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  PlayerCookingMasteryData — 레시피별 요리 숙련도 관리
    //
    //  ▶ 역할
    //    레시피별 요리 횟수를 추적하고 Lv1~5 숙련도와 판매가 보정치를 제공.
    //    PlayerDataManager 가 생성하고 Init() 을 호출한다.
    //
    //  ▶ 레벨 임계값
    //    Lv1=0회, Lv2=5회, Lv3=15회, Lv4=30회, Lv5=50회
    //
    //  ▶ 판매가 보정
    //    Lv1=×1.0, Lv2=×1.1, Lv3=×1.2, Lv4=×1.3, Lv5=×1.5
    // ====================================================================

    public class PlayerCookingMasteryData
    {
        static readonly int[]   s_Thresholds  = { 0, 5, 15, 30, 50 };
        static readonly float[] s_Multipliers = { 1.0f, 1.1f, 1.2f, 1.3f, 1.5f };

        // Lv1   Lv2    Lv3    Lv4    Lv5
        static readonly float[] s_SpeedMultipliers      = { 1.0f, 0.85f, 0.85f, 0.85f, 0.70f };
        static readonly float[] s_IngredientSaveChances = { 0.0f, 0.0f,  0.20f, 0.20f, 0.20f };
        static readonly float[] s_GradeUpChances        = { 0.0f, 0.0f,  0.0f,  0.0f,  0.30f };

        readonly Dictionary<uint, int> m_CookCounts = new();

        // ── 이벤트 ───────────────────────────────────────────────────

        /// <summary>레벨업 시 발행. (recipeId, newLevel)</summary>
        public event Action<uint, int> OnLevelUp;

        // ── 조회 API ─────────────────────────────────────────────────

        /// <summary>레시피 숙련도 레벨 (1~5).</summary>
        public int GetLevel(uint recipeId)
        {
            int count = GetCookCount(recipeId);
            int level = 1;
            for (int i = s_Thresholds.Length - 1; i >= 1; i--)
            {
                if (count >= s_Thresholds[i])
                {
                    level = i + 1;
                    break;
                }
            }
            return level;
        }

        /// <summary>레시피 판매가 배율 (1.0~1.5).</summary>
        public float GetPriceMultiplier(uint recipeId)
            => s_Multipliers[GetLevel(recipeId) - 1];

        /// <summary>조리 시간 배율 (1.0~0.7). CookingStation 에서 사용.</summary>
        public float GetSpeedMultiplier(uint recipeId)
            => s_SpeedMultipliers[GetLevel(recipeId) - 1];

        /// <summary>재료 1개 절약 확률 (0~0.20). 요리 성공 후 랜덤 재료 1개 환급.</summary>
        public float GetIngredientSaveChance(uint recipeId)
            => s_IngredientSaveChances[GetLevel(recipeId) - 1];

        /// <summary>등급 1단계 상향 확률 (0~0.30). Legendary 초과 없음.</summary>
        public float GetGradeUpChance(uint recipeId)
            => s_GradeUpChances[GetLevel(recipeId) - 1];

        /// <summary>레시피 총 요리 횟수.</summary>
        public int GetCookCount(uint recipeId)
            => m_CookCounts.TryGetValue(recipeId, out int c) ? c : 0;

        /// <summary>전체 카운트 읽기 전용 뷰 (저장용).</summary>
        public IReadOnlyDictionary<uint, int> CookCounts => m_CookCounts;

        // ── 변경 API ─────────────────────────────────────────────────

        /// <summary>요리 완료 시 호출. 레벨업 시 OnLevelUp 이벤트 발행.</summary>
        public void RecordCook(uint recipeId)
        {
            int prevLevel = GetLevel(recipeId);

            m_CookCounts.TryGetValue(recipeId, out int current);
            m_CookCounts[recipeId] = current + 1;

            int newLevel = GetLevel(recipeId);
            if (newLevel > prevLevel)
                OnLevelUp?.Invoke(recipeId, newLevel);
        }

        // ── 초기화 / 로드 ────────────────────────────────────────────

        public void Init() { }

        /// <summary>ServerDBManager 가 저장 파일 복원 시 호출한다.</summary>
        public void LoadCounts(Dictionary<uint, int> data)
        {
            m_CookCounts.Clear();
            if (data == null) return;
            foreach (var kv in data)
                m_CookCounts[kv.Key] = kv.Value;
        }
    }
}
