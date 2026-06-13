namespace MonsterKitchen.AI.Companion
{
    public enum CompanionState { Follow, Engage }

    // ====================================================================
    //  CompanionStateDecision — 동료 행동 상태 결정 (순수 함수)
    // ====================================================================
    public static class CompanionStateDecision
    {
        /// <summary>적이 감지 범위 내면 Engage, 아니면 Follow.</summary>
        public static CompanionState Decide(bool hasEnemy, float enemyDist, float detectRange)
        {
            return (hasEnemy && enemyDist <= detectRange)
                ? CompanionState.Engage
                : CompanionState.Follow;
        }
    }
}
