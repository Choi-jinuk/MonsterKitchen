namespace MonsterKitchen.UI
{
    /// <summary>
    /// UI 드로우 순서 레이어.
    /// UIDocument.sortingOrder 에 이 값을 그대로 사용한다.
    /// </summary>
    public enum UILayer
    {
        Background = 0,   // 배경 UI (씬 뒤)
        HUD        = 10,  // 항상 표시되는 인게임 정보 (HP, Gold, Day)
        Panel      = 20,  // 씬 위에 뜨는 패널 (인벤토리, 상점)
        Popup      = 30,  // 모달 팝업 (확인 다이얼로그, 알림)
        Overlay    = 100, // 전체 화면 오버레이 (로딩, 페이드)
    }
}
