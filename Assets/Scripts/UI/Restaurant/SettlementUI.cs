using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  SettlementUI — 영업 결산 오버레이
    //
    //  ▶ RestaurantSceneController 가 DayManager.OnAllGuestsLeft 에서 열기.
    //  ▶ 명성 계산 + 적용 후 결과 표시.
    //  ▶ "다음 날로" 버튼 → DayManager.CompleteDay().
    // ====================================================================

    public class SettlementUI : UIPanel
    {
        Label m_DayLabel;
        Label m_GuestsLabel;
        Label m_RevenueLabel;
        Label m_SatisfactionLabel;
        Label m_FameLabel;

        // ── UIPanel 훅 ────────────────────────────────────────────────

        protected override void OnFirstOpen()
        {
            m_DayLabel          = Root?.Q<Label>("settlement-day");
            m_GuestsLabel       = Root?.Q<Label>("settlement-guests");
            m_RevenueLabel      = Root?.Q<Label>("settlement-revenue");
            m_SatisfactionLabel = Root?.Q<Label>("settlement-satisfaction");
            m_FameLabel         = Root?.Q<Label>("settlement-fame");

            Root?.Q<Button>("settlement-next-btn")
                ?.RegisterCallback<ClickEvent>(_ => HandleNextDay());
        }

        // ── 표시 ─────────────────────────────────────────────────────

        public override void OnOpen()
        {
            base.OnOpen();
            PopulateStats();
        }

        void PopulateStats()
        {
            var day  = DayManager.Instance;
            var fame = PlayerDataManager.Instance?.Fame;
            if (day == null) return;

            // 명성 계산 + 적용
            int fameDelta = fame?.GetDailyFame(day.AvgSatisfaction) ?? 0;
            if (fameDelta != 0)
                fame?.AddFame(fameDelta);

            if (m_DayLabel != null)
                m_DayLabel.text = $"Day {day.CurrentDay}";

            if (m_GuestsLabel != null)
                m_GuestsLabel.text = $"👥 손님   {day.GuestsServed} / {day.GuestsArrived}명";

            int totalRevenue = day.DayRevenue + day.DayTips;
            if (m_RevenueLabel != null)
                m_RevenueLabel.text = $"💰 매출   {totalRevenue} G" +
                    $"  (요리 {day.DayRevenue} + 팁 {day.DayTips})";

            int satStars = SatisfactionToStars(day.AvgSatisfaction);
            if (m_SatisfactionLabel != null)
                m_SatisfactionLabel.text =
                    $"⭐ 평균 만족도   {day.AvgSatisfaction:F0}점  {BuildStars(satStars)}";

            string fameSign = fameDelta >= 0 ? "+" : "";
            if (m_FameLabel != null)
                m_FameLabel.text =
                    $"🏅 명성   {fameSign}{fameDelta}  (누적 {fame?.TotalFame ?? 0})";
        }

        // ── "다음 날로" ──────────────────────────────────────────────

        void HandleNextDay()
        {
            UIManager.Instance?.Close(PanelId);
            DayManager.Instance?.CompleteDay();
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        static int SatisfactionToStars(float sat)
        {
            if (sat >= 90f) return 5;
            if (sat >= 75f) return 4;
            if (sat >= 60f) return 3;
            if (sat >= 40f) return 2;
            return 1;
        }

        static string BuildStars(int count)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 5; i++) sb.Append(i < count ? "★" : "☆");
            return sb.ToString();
        }
    }
}
