using System;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 골드 싱글톤. 전 씬 공유.
    /// </summary>
    public class GoldManager : MonoBehaviour
    {
        public static GoldManager Instance { get; private set; }

        [SerializeField] int startingGold = 0;

        public int Gold { get; private set; }

        public event Action<int> OnGoldChanged;  // (newAmount)

        public void Init()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Gold = startingGold;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            if (Instance == null) Init();
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
