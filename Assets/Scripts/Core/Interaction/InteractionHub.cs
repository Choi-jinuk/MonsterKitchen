using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  InteractionHub — E키 상호작용 단일 진입점
    //
    //  ▶ 배경
    //    기존에는 8개 클래스가 각자 InputManager.OnInteract 를 구독해
    //    E키 한 번에 서빙+청소가 동시 실행되는 등 충돌이 있었다.
    //
    //  ▶ 동작
    //    후보(IInteractable)가 Register/Unregister 로 등록되고,
    //    E키 입력 시 CanInteract == true 인 후보 중 플레이어와
    //    가장 가까운 하나만 Interact() 를 실행한다.
    //
    //  ▶ 구독 수명
    //    첫 Register 시 InputManager.OnInteract 에 1회 구독.
    //    InputManager 는 GlobalController 수명(DontDestroyOnLoad)과 같다.
    // ====================================================================
    public static class InteractionHub
    {
        static readonly List<IInteractable> s_Candidates = new();
        static bool s_Subscribed;

        public static int CandidateCount => s_Candidates.Count;

        // ── 등록 ─────────────────────────────────────────────────────

        /// <summary>중복 등록 안전 (콜라이더 2개 트리거 등).</summary>
        public static void Register(IInteractable candidate)
        {
            if (candidate == null) return;
            if (!s_Candidates.Contains(candidate))
                s_Candidates.Add(candidate);
            EnsureSubscribed();
        }

        public static void Unregister(IInteractable candidate)
        {
            s_Candidates.Remove(candidate);
        }

        // ── 실행 ─────────────────────────────────────────────────────

        static void Execute()
        {
            if (s_Candidates.Count == 0) return;

            var player = PlayerManager.Instance?.Player;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            bool    hasPlayer = player != null;

            IInteractable best     = null;
            float         bestDist = float.MaxValue;

            // 역순 순회 — Interact() 중 Unregister 가 일어나도 안전하도록 스냅샷 없이 탐색만
            for (int i = s_Candidates.Count - 1; i >= 0; i--)
            {
                var c = s_Candidates[i];
                if (c == null || !c.CanInteract) continue;

                if (!hasPlayer) { best = c; break; }   // 플레이어 없으면 첫 유효 후보

                float d = (c.InteractPosition - playerPos).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best     = c;
                }
            }

            best?.Interact();
        }

        // ── 내부 ─────────────────────────────────────────────────────

        static void EnsureSubscribed()
        {
            if (s_Subscribed || InputManager.Instance == null) return;
            InputManager.Instance.OnInteract += Execute;
            s_Subscribed = true;
        }

        // Enter Play Mode Options (도메인 리로드 비활성) 대비 정적 상태 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Candidates.Clear();
            s_Subscribed = false;
        }
    }
}
