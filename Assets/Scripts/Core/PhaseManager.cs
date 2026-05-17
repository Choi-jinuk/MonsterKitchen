using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 하루를 세 페이즈로 관리한다: Morning(관리) → Dungeon(던전) → Evening(영업).
    /// 싱글톤, DontDestroyOnLoad.
    /// </summary>
    public class PhaseManager : MonoBehaviour
    {
        public static PhaseManager Instance { get; private set; }

        public enum Phase { Morning, Dungeon, Evening }

        public Phase CurrentPhase { get; private set; } = Phase.Morning;

        public event Action<Phase> OnPhaseChanged;

        public void Init()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (Instance == null) Init();
        }

        /// <summary>ManagementScene → DungeonScene 진입.</summary>
        public void EnterDungeon()
        {
            SetPhase(Phase.Dungeon);
            SceneLoader.Instance?.LoadScene("DungeonScene");
        }

        /// <summary>DungeonScene → ManagementScene 복귀.</summary>
        public void ReturnFromDungeon()
        {
            SetPhase(Phase.Morning);
            SceneLoader.Instance?.LoadScene("ManagementScene");
        }

        /// <summary>ManagementScene → KitchenScene (요리 준비) → RestaurantScene 순으로 진입.</summary>
        public void StartEvening()
        {
            SetPhase(Phase.Evening);
            SceneLoader.Instance?.LoadScene("KitchenScene");
        }

        /// <summary>영업 종료 후 주방으로 이동. 플레이어가 KitchenExit 통과 시 ManagementScene(다음 날)으로 전환.</summary>
        public void EndDay()
        {
            DayManager.Instance?.AdvanceToNextDay();
            SetPhase(Phase.Morning);
            SceneLoader.Instance?.LoadScene("KitchenScene");
        }

        void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
            Debug.Log($"[PhaseManager] Phase → {phase}");
        }
    }
}
