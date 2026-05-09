using System.Collections.Generic;
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Management
{
    /// <summary>
    /// 씬 내 모든 FarmPlot을 수집하고 상태를 로깅한다.
    /// </summary>
    public class FarmManager : MonoBehaviour
    {
        [SerializeField] List<FarmPlot> plots = new();

        public IReadOnlyList<FarmPlot> Plots      => plots;
        public int                     ReadyCount  => plots.FindAll(p => p.IsReady).Count;
        public int                     PlantedCount=> plots.FindAll(p => p.IsPlanted && !p.IsReady).Count;
        public int                     EmptyCount  => plots.FindAll(p => !p.IsPlanted).Count;

        void Start()
        {
            if (plots.Count == 0)
                plots.AddRange(GetComponentsInChildren<FarmPlot>());

            if (DayManager.Instance != null)
                DayManager.Instance.OnDayStarted += HandleDayStarted;

            PrintStatus();
        }

        void OnDestroy()
        {
            if (DayManager.Instance != null)
                DayManager.Instance.OnDayStarted -= HandleDayStarted;
        }

        void HandleDayStarted(int day)
        {
            PrintStatus();
        }

        void PrintStatus()
        {
            Debug.Log($"[FarmManager] 수확가능:{ReadyCount}  성장중:{PlantedCount}  빈칸:{EmptyCount}");
        }
    }
}
