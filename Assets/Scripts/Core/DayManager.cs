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

        readonly MonoBehaviour _runner;

        // 영업 설정 — RestaurantSetup 이 SetRestaurantConfig() 로 주입
        Transform        _guestSpawnPoint;
        CustomerAI       _customerPrefab;
        RestaurantTable[] _tables;

        const int   InitialDay        = 1;
        const float TimeBetweenGuests = 8f;
        const int   GuestsPerDay      = 3;

        public int  CurrentDay { get; private set; }
        public bool IsOpen     { get; private set; }

        public event Action<int> OnDayStarted;
        public event Action<int> OnDayEnded;

        int _guestsSpawned;
        int _guestsFinished;

        public DayManager(MonoBehaviour runner) => _runner = runner;

        public void Init()
        {
            Instance   = this;
            CurrentDay = InitialDay;
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
            IsOpen          = true;
            _guestsSpawned  = 0;
            _guestsFinished = 0;
            OnDayStarted?.Invoke(CurrentDay);
            Debug.Log($"[DayManager] Day {CurrentDay} 영업 시작!");
            _runner.StartCoroutine(SpawnGuestsRoutine());
        }

        IEnumerator SpawnGuestsRoutine()
        {
            while (_guestsSpawned < GuestsPerDay)
            {
                yield return new WaitForSeconds(_guestsSpawned == 0 ? 1f : TimeBetweenGuests);

                var table = FindFreeTable();
                if (table == null) { yield return new WaitForSeconds(2f); continue; }

                SpawnGuest(table);
                _guestsSpawned++;
            }
        }

        void SpawnGuest(RestaurantTable table)
        {
            if (_customerPrefab == null || _guestSpawnPoint == null) return;

            var go = UnityEngine.Object.Instantiate(_customerPrefab, _guestSpawnPoint.position, Quaternion.identity);
            go.gameObject.SetActive(true);
            go.Init(table);
            go.OnGuestFinished += HandleGuestFinished;
        }

        void HandleGuestFinished()
        {
            _guestsFinished++;
            if (_guestsFinished >= GuestsPerDay)
                _runner.StartCoroutine(EndDayRoutine());
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
            if (_tables == null) return null;
            foreach (var t in _tables)
                if (t != null && !t.IsOccupied) return t;
            return null;
        }

        public void SetTables(RestaurantTable[] t) => _tables = t;

        /// <summary>RestaurantSetup 이 씬 로드 시 호출해 로컬 레퍼런스를 주입한다.</summary>
        public void SetRestaurantConfig(Transform spawnPoint, CustomerAI prefab, RestaurantTable[] tables)
        {
            _guestSpawnPoint = spawnPoint;
            _customerPrefab  = prefab;
            _tables          = tables;
        }
    }
}
