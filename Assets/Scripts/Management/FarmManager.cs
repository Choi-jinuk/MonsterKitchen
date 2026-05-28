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
        [SerializeField] List<FarmPlot> m_Plots = new();

        public IReadOnlyList<FarmPlot> Plots      => m_Plots;
        public int                     ReadyCount  => m_Plots.FindAll(p => p.IsReady).Count;
        public int                     PlantedCount=> m_Plots.FindAll(p => p.IsPlanted && !p.IsReady).Count;
        public int                     EmptyCount  => m_Plots.FindAll(p => !p.IsPlanted).Count;

        void Start()
        {
            if (m_Plots.Count == 0)
                m_Plots.AddRange(GetComponentsInChildren<FarmPlot>());

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
