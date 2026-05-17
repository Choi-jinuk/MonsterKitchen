using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 골드 싱글톤. 전 씬 공유.
    /// GlobalController 가 new 로 생성하고 Init() 를 호출한다.
    /// </summary>
    public class GoldManager
    {
        public static GoldManager Instance { get; private set; }

        public int Gold { get; private set; }

        public event Action<int> OnGoldChanged;  // (newAmount)

        public void Init()
        {
            Instance = this;
            Gold     = 0;
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
            Debug.Log($"[Gold] +{amount}G  총: {Gold}G");
        }

        public bool Spend(int amount)
        {
            if (amount <= 0 || Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }
    }
}
