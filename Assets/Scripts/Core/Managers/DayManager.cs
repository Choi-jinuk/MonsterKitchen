using System;
using System.Collections;
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

        // 영업 설정 — RestaurantSetup 이 SetRestaurantConfig() 로 주입
        Transform         m_GuestSpawnPoint;
        CustomerAI        m_CustomerPrefab;
        RestaurantTable[] m_Tables;

        const int   INITIAL_DAY          = 1;
        const float TIME_BETWEEN_GUESTS  = 8f;
        const int   GUESTS_PER_DAY       = 3;

        public int  CurrentDay { get; private set; }
        public bool IsOpen     { get; private set; }

        public event Action<int> OnDayStarted;
        public event Action<int> OnDayEnded;

        int m_GuestsSpawned;
        int m_GuestsFinished;

        public DayManager(MonoBehaviour runner) => m_Runner = runner;

        public void Init()
        {
            Instance   = this;
            CurrentDay = INITIAL_DAY;
        }

        /// <summary>PhaseManager 에서 호출. 다음 날로 카운터만 증가.</summary>
        public void AdvanceToNextDay()
        {
            CurrentDay++;
            Debug.Log($"[DayManager] Day {CurrentDay} 시작.");
        }

        public void StartDay()
        {
            if (IsOpen) return;
            IsOpen           = true;
            m_GuestsSpawned  = 0;
            m_GuestsFinished = 0;
            OnDayStarted?.Invoke(CurrentDay);
            Debug.Log($"[DayManager] Day {CurrentDay} 영업 시작!");
            m_Runner.StartCoroutine(SpawnGuestsRoutine());
        }

        IEnumerator SpawnGuestsRoutine()
        {
            while (m_GuestsSpawned < GUESTS_PER_DAY)
            {
                yield return new WaitForSeconds(m_GuestsSpawned == 0 ? 1f : TIME_BETWEEN_GUESTS);

                var table = FindFreeTable();
                if (table == null) { yield return new WaitForSeconds(2f); continue; }

                SpawnGuest(table);
                m_GuestsSpawned++;
            }
        }

        void SpawnGuest(RestaurantTable table)
        {
            if (m_CustomerPrefab == null || m_GuestSpawnPoint == null) return;

            var go = UnityEngine.Object.Instantiate(m_CustomerPrefab, m_GuestSpawnPoint.position, Quaternion.identity);
            go.gameObject.SetActive(true);
            go.Init(table);
            go.OnGuestFinished += HandleGuestFinished;
        }

        void HandleGuestFinished()
        {
            m_GuestsFinished++;
            if (m_GuestsFinished >= GUESTS_PER_DAY)
                m_Runner.StartCoroutine(EndDayRoutine());
        }

        IEnumerator EndDayRoutine()
        {
            yield return new WaitForSeconds(2f);
            IsOpen = false;
            OnDayEnded?.Invoke(CurrentDay);
            Debug.Log($"[DayManager] Day {CurrentDay} 영업 마감.");

            yield return new WaitForSeconds(1.5f);
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EndDay();
            else
                SceneLoader.Instance?.LoadScene("KitchenScene");
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

        /// <summary>RestaurantSetup 이 씬 로드 시 호출해 로컬 레퍼런스를 주입한다.</summary>
        public void SetRestaurantConfig(Transform spawnPoint, CustomerAI prefab, RestaurantTable[] tables)
        {
            m_GuestSpawnPoint = spawnPoint;
            m_CustomerPrefab  = prefab;
            m_Tables          = tables;
        }
    }
}
