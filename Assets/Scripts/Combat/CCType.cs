namespace MonsterKitchen.Combat
{
    // ====================================================================
    //  CCType — 이동 제어(Crowd Control) 종류
    //
    //  None    : CC 없음 (정상 상태)
    //  Knockback : 특정 방향으로 밀려남
    //  Stun    : 이동 불가 (제자리 정지)
    //  PullIn  : 특정 지점으로 끌려감
    // ====================================================================

    public enum CCType
    {
        None      = 0,
        Knockback = 1,
        Stun      = 2,
        PullIn    = 3,
        Sleep     = 4,   // 수면 — 처치 시 재료 품질 +1
        Hypnosis  = 5,   // 최면 — 처치 시 재료 품질 +1
    }
}
