using MonsterKitchen.Core;
using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 하루를 세 페이즈로 관리한다: Morning → Dungeon → Evening.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class PhaseManager
    {
        public static PhaseManager Instance { get; private set; }

        public enum Phase { Morning, Dungeon, Evening }

        public Phase CurrentPhase { get; private set; } = Phase.Morning;

        public event Action<Phase> OnPhaseChanged;

        public void Init() => Instance = this;

        /// <summary>ManagementScene → DungeonScene 진입.</summary>
        public void EnterDungeon()
        {
            SetPhase(Phase.Dungeon);
            SceneLoader.Instance?.LoadScene(CommonString.SceneDungeon);
        }

        /// <summary>DungeonScene → ManagementScene 복귀.</summary>
        public void ReturnFromDungeon()
        {
            SetPhase(Phase.Morning);
            SceneLoader.Instance?.LoadScene(CommonString.SceneManagement);
        }

        /// <summary>ManagementScene → KitchenScene → RestaurantScene 진입.</summary>
        public void StartEvening()
        {
            SetPhase(Phase.Evening);
            SceneLoader.Instance?.LoadScene(CommonString.SceneKitchen);
        }

        /// <summary>영업 종료 후 다음 날 아침 — ManagementScene 복귀.</summary>
        public void EndDay()
        {
            DayManager.Instance?.AdvanceToNextDay();
            SetPhase(Phase.Morning);
            SceneLoader.Instance?.LoadScene(CommonString.SceneManagement);
        }

        void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
            DebugUtil.Log($"[PhaseManager] Phase → {phase}");
        }
    }
}
