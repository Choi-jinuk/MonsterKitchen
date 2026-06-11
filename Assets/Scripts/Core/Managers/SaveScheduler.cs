using System;
using System.Collections;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  SaveScheduler — 자동 저장 주기 관리자
    //
    //  ▶ 역할
    //    dirty 플래그 + 주기 타이머로 ServerDBManager.Save() 호출 시점을 관리.
    //    중요 패킷은 NetworkManager에서 ForceSave()를 직접 호출한다.
    //
    //  ▶ 의존성 주입 (테스트 지원)
    //    생성자에 saveAction을 주입하면 ServerDBManager 없이 테스트 가능.
    //    런타임에는 기본값(null)을 사용해 ServerDBManager.Instance?.Save() 호출.
    //
    //  ▶ IsSaving 표시 시간
    //    실제 저장은 동기 (수 ms). UX를 위해 SaveIndicatorSeconds 동안 유지.
    //    m_IsSaveInProgress는 실제 파일 쓰기 중만 true → 재진입 방지.
    // ====================================================================

    public class SaveScheduler
    {
        public static SaveScheduler Instance { get; private set; }

        readonly Action   m_SaveAction;
        MonoBehaviour     m_Runner;
        bool              m_IsSaveInProgress;

        const float SaveIndicatorSeconds = 1f;

        // ── 공개 상태 ────────────────────────────────────────────────
        public bool IsDirty  { get; private set; }
        public bool IsSaving { get; private set; }

        public event Action<bool> OnSaveStateChanged;
        public event Action       OnSaveComplete;

        // ── 생성자 ───────────────────────────────────────────────────
        public SaveScheduler(Action saveAction = null)
        {
            m_SaveAction = saveAction ?? (() => ServerDBManager.Instance?.Save());
        }

        // ── 생명주기 ─────────────────────────────────────────────────

        public void Init() => Instance = this;

        /// <summary>GlobalController.Start() 에서 호출. 자동 저장 코루틴 시작.</summary>
        public void StartAutoSaveCycle(MonoBehaviour runner)
        {
            m_Runner = runner;
            runner.StartCoroutine(AutoSaveCycle());
        }

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>일반 패킷 후 호출. 다음 주기 tick 에서 저장된다.</summary>
        public void MarkDirty() => IsDirty = true;

        /// <summary>
        /// 즉시 저장. 중요 패킷 / 앱 이벤트 / 수동 호출.
        /// runner 없으면 (EditMode 테스트) 동기 완료 후 즉시 OnSaveComplete.
        /// </summary>
        public void ForceSave(Action onComplete = null)
        {
            if (m_IsSaveInProgress)
            {
                DebugUtil.LogWarning("[SaveScheduler] ForceSave 재진입 — 건너뜀.");
                onComplete?.Invoke();
                return;
            }

            m_IsSaveInProgress = true;
            SetSaving(true);

            try
            {
                m_SaveAction?.Invoke();
                IsDirty = false;
            }
            finally
            {
                m_IsSaveInProgress = false;
            }

            if (m_Runner != null)
            {
                m_Runner.StartCoroutine(HideSavingIndicator(onComplete));
            }
            else
            {
                // 테스트 / runner 없는 환경 — 즉시 완료
                SetSaving(false);
                onComplete?.Invoke();
                OnSaveComplete?.Invoke();
            }
        }

        // ── 내부 ─────────────────────────────────────────────────────

        IEnumerator AutoSaveCycle()
        {
            while (true)
            {
                int   interval = GameConfig.Current?.AutoSaveIntervalSeconds ?? 30;
                float wait     = interval > 0 ? interval : 30f;
                yield return new WaitForSeconds(wait);

                if (interval > 0 && IsDirty)
                    ForceSave();
            }
        }

        IEnumerator HideSavingIndicator(Action onComplete)
        {
            yield return new WaitForSeconds(SaveIndicatorSeconds);
            SetSaving(false);
            onComplete?.Invoke();
            OnSaveComplete?.Invoke();
        }

        void SetSaving(bool saving)
        {
            IsSaving = saving;
            OnSaveStateChanged?.Invoke(saving);
        }

#if UNITY_EDITOR
        /// <summary>EditMode 테스트에서 Instance 초기화 용도. 프로덕션 코드에서 호출 금지.</summary>
        public static void ResetInstanceForTest() => Instance = null;
#endif
    }
}
