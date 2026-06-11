namespace MonsterKitchen.UI.Mobile
{
    // ====================================================================
    //  MobileContext — 씬/상황별 모바일 버튼 배치 컨텍스트
    //
    //  ▶ Dungeon         : [Dash][Skill1][Skill2]
    //  ▶ DungeonInteract : [Dash][Skill1][Interact]  ← Skill2 슬롯 교체
    //  ▶ Exploration     : [Dash][Interact]
    // ====================================================================

    public enum MobileContext
    {
        Dungeon,
        DungeonInteract,
        Exploration,
    }
}
