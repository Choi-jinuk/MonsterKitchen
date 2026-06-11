namespace MonsterKitchen.Core
{
    // ====================================================================
    //  IServerDBManager — ServerDBManager 테스트 격리용 인터페이스
    // ====================================================================

    public interface IServerDBManager
    {
        void Save();
        void Load();
    }
}
