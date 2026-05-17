using System;
using System.Collections;
using MonsterKitchen.Restaurant;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 날 카운터 + 식당 영업 오픈/마감.
    /// 영업 시작 → 손님 순차 스폰 → 전원 처리 완료 → OnDayEnded.
    /// </summary>
    public class DayManager : MonoBehaviour
    {
        public static DayManager Instance { get; private set; }

        [Header("Day Settings")]
        [SerializeField] int   startDay         = 1;
        [SerializeField] float timeBetweenGuests = 8f;   // 손님 등장 간격(초)
        [SerializeField] int   guestsPerDay      = 3;

        [Header("Spawn")]
        [SerializeField] Transform        guestSpawnPoint;
        [SerializeField] CustomerAI       customerPrefab;
        [SerializeField] RestaurantTable[] tables;

        public int  CurrentDay    { get; private set; }
        public bool IsOpen        { get; private set; }

        /// <summary>PhaseManager에서 호출. 다음 날로 카운터만 증가.</summary>
        public void AdvanceToNextDay()
        {
            CurrentDay++;
            Debug.Log($"[DayManager] Day {CurrentDay} 시작.");
        }

        public event Action<int>  OnDayStarted;   // (day)
        public event Action<int>  OnDayEnded;     // (day)

        int _guestsSpawned;
        int _guestsFinished;

        public void Init()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentDay = startDay;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (Instance == null) Init();
        }

        public void StartDay()
        {
            if (IsOpen) return;
            IsOpen          = true;
            _guestsSpawned  = 0;
            _guestsFinished = 0;
            OnDayStarted?.Invoke(CurrentDay);
            Debug.Log($"[DayManager] Day {CurrentDay} 영업 시작!");
            StartCoroutine(SpawnGuestsRoutine());
        }

        IEnumerator SpawnGuestsRoutine()
        {
            while (_guestsSpawned < guestsPerDay)
            {
                yield return new WaitForSeconds(_guestsSpawned == 0 ? 1f : timeBetweenGuests);

                var table = FindFreeTable();
                if (table == null) { yield return new WaitForSeconds(2f); continue; }

                SpawnGuest(table);
                _guestsSpawned++;
            }
        }

        void SpawnGuest(RestaurantTable table)
        {
            if (customerPrefab == null || guestSpawnPoint == null) return;

            var go = Instantiate(customerPrefab, guestSpawnPoint.position, Quaternion.identity);
            go.gameObject.SetActive(true);   // prefab이 비활성 상태여도 스폰 후 활성화
            go.Init(table);
            go.OnGuestFinished += HandleGuestFinished;
        }

        void HandleGuestFinished()
        {
            _guestsFinished++;
            if (_guestsFinished >= guestsPerDay)
                StartCoroutine(EndDayRoutine());
        }

        IEnumerator EndDayRoutine()
        {
            yield return new WaitForSeconds(2f);
            IsOpen = false;
            OnDayEnded?.Invoke(CurrentDay);
            Debug.Log($"[DayManager] Day {CurrentDay} 영업 마감.");

            yield return new WaitForSeconds(1.5f);
            // PhaseManager.EndDay()가 AdvanceToNextDay()로 CurrentDay를 올린다.
            // 여기서 CurrentDay++ 하면 이중 증가 버그 발생 → 제거.
            if (PhaseManager.Instance != null)
                PhaseManager.Instance.EndDay();
            else
                SceneLoader.Instance?.LoadScene("KitchenScene");
        }

        RestaurantTable FindFreeTable()
        {
            foreach (var t in tables)
                if (t != null && !t.IsOccupied) return t;
            return null;
        }

        public void SetTables(RestaurantTable[] t) => tables = t;

        /// <summary>
        /// RestaurantSetup이 씬 로드 시 호출해 로컬 레퍼런스를 주입한다.
        /// ManagementScene에서 DontDestroyOnLoad된 인스턴스가 참조를 잃지 않도록 한다.
        /// </summary>
        public void SetRestaurantConfig(Transform spawnPoint, CustomerAI prefab, RestaurantTable[] restaurantTables)
        {
            guestSpawnPoint = spawnPoint;
            customerPrefab  = prefab;
            tables          = restaurantTables;
        }
    }
}
