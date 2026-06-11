using MonsterKitchen.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonsterKitchen.Data;
using MonsterKitchen.Restaurant;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 날 카운터 + 식당 영업 오픈/마감.
    /// GlobalController 가 new DayManager(this) 로 생성하고 Init() 를 호출한다.
    /// 코루틴은 주입받은 runner(GlobalController) 를 통해 실행한다.
    /// </summary>
    public class DayManager
    {
        public static DayManager Instance { get; private set; }

        readonly MonoBehaviour m_Runner;

        // 영업 설정 — RestaurantSceneController 가 SetRestaurantConfig() 로 주입
        Transform         m_GuestSpawnPoint;
        CustomerAI        m_CustomerPrefab;
        RestaurantTable[] m_Tables;

        const float TIME_BETWEEN_GUESTS = 8f;

        public int  CurrentDay { get; private set; }
        public bool IsOpen     { get; private set; }

        public event Action<int> OnDayStarted;
        public event Action<int> OnDayEnded;
        /// <summary>모든 손님이 퇴장 완료 시 발행. SettlementUI 표시 트리거.</summary>
        public event Action OnAllGuestsLeft;

        // ── 일일 통계 ────────────────────────────────────────────────
        int       m_DayRevenue;
        int       m_DayTips;
        int       m_GuestsServed;
        int       m_GuestsArrived;
        int       m_GuestsForToday;
        List<int> m_SatisfactionScores = new();

        public int   DayRevenue    => m_DayRevenue;
        public int   DayTips       => m_DayTips;
        public int   GuestsServed  => m_GuestsServed;
        public int   GuestsArrived => m_GuestsArrived;
        public float AvgSatisfaction => m_SatisfactionScores.Count > 0
            ? (float)m_SatisfactionScores.Average() : 50f;

        int m_GuestsFinished;

        public DayManager(MonoBehaviour runner) => m_Runner = runner;

        public void Init()
        {
            Instance   = this;
            CurrentDay = 1;
        }

        /// <summary>PhaseManager 에서 호출. 다음 날로 카운터만 증가.</summary>
        public void AdvanceToNextDay()
        {
            CurrentDay++;
            DebugUtil.Log($"[DayManager] Day {CurrentDay} 시작.");
        }

        public void StartDay()
        {
            if (IsOpen) return;
            IsOpen           = true;
            m_GuestsFinished = 0;
            m_GuestsArrived  = 0;
            m_GuestsForToday = GetGuestCountForDay();
            ResetDailyStats();
            OnDayStarted?.Invoke(CurrentDay);
            DebugUtil.Log($"[DayManager] Day {CurrentDay} 영업 시작! (손님 {m_GuestsForToday}명)");
            m_Runner.StartCoroutine(SpawnGuestsRoutine());
        }

        // ── 일일 통계 기록 ───────────────────────────────────────────

        /// <summary>CustomerAI 가 서빙 완료 또는 인내심 만료 시 호출.</summary>
        public void RecordServing(int payment, int tip, int satisfaction)
        {
            m_DayRevenue += payment;
            m_DayTips    += tip;
            if (payment > 0) m_GuestsServed++;
            m_SatisfactionScores.Add(satisfaction);
        }

        void ResetDailyStats()
        {
            m_DayRevenue   = 0;
            m_DayTips      = 0;
            m_GuestsServed = 0;
            m_SatisfactionScores.Clear();
        }

        // ── 손님 수 계산 (명성 기반) ─────────────────────────────────

        int GetGuestCountForDay()
            => PlayerDataManager.Instance?.Fame?.MaxGuestsPerDay() ?? 3;

        // ── 스폰 ────────────────────────────────────────────────────

        IEnumerator SpawnGuestsRoutine()
        {
            while (m_GuestsArrived < m_GuestsForToday)
            {
                yield return new WaitForSeconds(m_GuestsArrived == 0 ? 1f : TIME_BETWEEN_GUESTS);

                var table = FindFreeTable();
                if (table == null) { yield return new WaitForSeconds(2f); continue; }

                SpawnGuest(table);
                m_GuestsArrived++;
            }
        }

        void SpawnGuest(RestaurantTable table)
        {
            if (m_CustomerPrefab == null || m_GuestSpawnPoint == null) return;

            var go = UnityEngine.Object.Instantiate(
                m_CustomerPrefab, m_GuestSpawnPoint.position, Quaternion.identity);
            go.gameObject.SetActive(true);
            go.Init(table);
            go.OnGuestFinished += HandleGuestFinished;
        }

        void HandleGuestFinished()
        {
            m_GuestsFinished++;
            if (m_GuestsFinished >= m_GuestsForToday)
                OnAllGuestsLeft?.Invoke();
        }

        // ── 영업 종료 ────────────────────────────────────────────────

        /// <summary>SettlementUI "다음 날로" 버튼이 호출한다.</summary>
        public void CompleteDay()
        {
            m_Runner.StartCoroutine(EndDayRoutine());
        }

        IEnumerator EndDayRoutine()
        {
            yield return new WaitForSeconds(1f);
            IsOpen = false;
            OnDayEnded?.Invoke(CurrentDay);
            DebugUtil.Log($"[DayManager] Day {CurrentDay} 영업 마감.");

            yield return new WaitForSeconds(0.5f);
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EndDay();
            else
                SceneLoader.Instance?.LoadScene("ManagementScene");
        }

        RestaurantTable FindFreeTable()
        {
            if (m_Tables == null) return null;
            foreach (var t in m_Tables)
                if (t != null && !t.IsOccupied) return t;
            return null;
        }

        public void SetTables(RestaurantTable[] t) => m_Tables = t;

        /// <summary>ServerDBManager 가 저장 파일 로드 시 날짜를 복원할 때 호출한다.</summary>
        public void RestoreDay(int day)
        {
            if (day > 0) CurrentDay = day;
        }

        /// <summary>RestaurantSceneController 가 씬 로드 시 호출해 로컬 레퍼런스를 주입한다.</summary>
        public void SetRestaurantConfig(Transform spawnPoint, CustomerAI prefab, RestaurantTable[] tables)
        {
            if (prefab == null)
            {
                DebugUtil.LogError("[DayManager] SetRestaurantConfig — customerPrefab is null. 식당 손님 스폰 불가.");
                return;
            }
            m_GuestSpawnPoint = spawnPoint;
            m_CustomerPrefab  = prefab;
            m_Tables          = tables;
        }
    }
}
