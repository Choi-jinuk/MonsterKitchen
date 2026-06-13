namespace MonsterKitchen.AI
{
    // ====================================================================
    //  FlockWeights — 군집 스티어링 가중치/반경 설정값 (몬스터·동료 공용)
    // ====================================================================
    [System.Serializable]
    public struct FlockWeights
    {
        public float SepWeight;
        public float SeekWeight;
        public float ArrivalWeight;
        public float TangentWeight;
        public float SepRadius;
        public float SlowRadius;
        public float RingBand;

        public static FlockWeights Default => new FlockWeights
        {
            SepWeight     = 2.0f,
            SeekWeight    = 1.0f,
            ArrivalWeight = 1.5f,
            TangentWeight = 1.2f,
            SepRadius     = 0.9f,
            SlowRadius    = 1.0f,
            RingBand      = 1.5f,
        };
    }
}
