using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// UnityEngine.Random 래퍼 유틸리티.
    /// 프로젝트 내 모든 난수 생성은 이 클래스를 통한다.
    /// </summary>
    public static class RandomUtil
    {
        /// <summary>[0, 1) 균등 분포 float.</summary>
        public static float Value => Random.value;

        /// <summary>[min, max] 균등 분포 float.</summary>
        public static float Range(float min, float max) => Random.Range(min, max);

        /// <summary>[min, max) 균등 분포 int. (max 제외)</summary>
        public static int Range(int min, int max) => Random.Range(min, max);

        /// <summary>probability(0~1) 확률로 true 반환.</summary>
        public static bool Chance(float probability)
        {
            if (probability < 0f || probability > 1f)
                DebugUtil.LogError($"[RandomUtil] Chance의 probability 값이 유효 범위(0~1)를 벗어났습니다: {probability}");
            return Random.value <= probability;
        }

        /// <summary>반지름 radius 원 내부 랜덤 Vector2 오프셋.</summary>
        public static Vector2 InCircle(float radius = 1f) => Random.insideUnitCircle * radius;

        /// <summary>반지름 radius 원 내부 랜덤 Vector3 오프셋 (z = 0).</summary>
        public static Vector3 InCircle3D(float radius = 1f) => Random.insideUnitCircle * radius;
    }
}
