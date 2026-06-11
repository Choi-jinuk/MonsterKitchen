using MonsterKitchen.Core;
using System;
using System.IO;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  ServerDBManager — 영속 데이터 저장/로드 관리자
    //
    //  ▶ 역할
    //    ServerSaveData(DTO) ↔ JSON 파일 변환 + 파일 I/O.
    //    현재: Application.persistentDataPath 로컬 파일 저장.
    //    향후: HTTP 서버 API 로 교체 가능 (이 클래스만 수정).
    //
    //  ▶ 사용법
    //    ServerDBManager.Instance.Save()  → 씬 전환·앱 종료 시 호출
    //    ServerDBManager.Instance.Load()  → GameStartup.StepSaveLoad() 에서 호출
    //
    //  ▶ 4-레이어 구조에서의 위치
    //    TableData(DataRegistry) — 정적 캐릭터 정의 (읽기 전용)
    //    PlayerData 서브 클래스  — 런타임 게임플레이 상태
    //    NetworkManager          — 서버 패킷 중계
    //    ServerDBManager         — 영속 저장/로드 (이 클래스)    ← 현재
    // ====================================================================

    public class ServerDBManager : IServerDBManager
    {
        public static ServerDBManager Instance { get; private set; }

        const string SaveFileName   = "save.json";
        const int    CurrentVersion = 1;

        string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public void Init() => Instance = this;

        // ================================================================
        //  저장
        // ================================================================

        /// <summary>현재 PlayerDataManager 서브 클래스 + DayManager 상태를 JSON 파일로 저장한다.</summary>
        public void Save()
        {
            var    save = BuildSaveData();
            string json = JsonUtility.ToJson(save, prettyPrint: true);

            try
            {
                File.WriteAllText(SavePath, json);
                DebugUtil.Log(StringUtil.Format("[ServerDB] 저장 완료 → {0}", SavePath));
            }
            catch (Exception e)
            {
                DebugUtil.LogError(StringUtil.Format("[ServerDB] 저장 실패: {0}", e.Message));
            }
        }

        // ================================================================
        //  로드
        // ================================================================

        /// <summary>
        /// JSON 파일에서 읽어 PlayerDataManager 서브 클래스에 적용한다.
        /// 파일이 없으면 새 게임 초기 상태를 유지한다.
        /// GameStartup.StepSaveLoad() 에서 호출.
        /// </summary>
        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                DebugUtil.Log("[ServerDB] 저장 파일 없음 — 새 게임 시작.");
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var    save = JsonUtility.FromJson<ServerSaveData>(json);
                PlayerDataManager.Instance.LoadFrom(save);

                if (DayManager.Instance != null && save.DayCount > 1)
                    DayManager.Instance.RestoreDay(save.DayCount);

                DebugUtil.Log(StringUtil.Format("[ServerDB] 로드 완료 (v{0}, Day {1}, Gold {2}G)",
                    save.Version, save.DayCount, save.Gold));
            }
            catch (Exception e)
            {
                DebugUtil.LogError(StringUtil.Format("[ServerDB] 로드 실패 — 새 게임으로 진행합니다. 오류: {0}", e.Message));
            }
        }

        // ================================================================
        //  내부: 서브 클래스 → ServerSaveData
        // ================================================================

        ServerSaveData BuildSaveData()
        {
            var pm   = PlayerDataManager.Instance;
            var coin = pm?.Coin;
            var inv  = pm?.Inventory;
            var upg  = pm?.Upgrades;
            var day  = DayManager.Instance;

            var save = new ServerSaveData
            {
                Version           = CurrentVersion,
                SavedAtUtc        = DateTime.UtcNow.Ticks,
                DayCount          = day?.CurrentDay ?? 1,
                SelectedCharId    = pm?.SelectedCharId ?? 9001,
                Gold              = coin?.Gold ?? 0,
                ToolDamageLevel   = upg?.ToolDamageLevel   ?? 0,
                ToolRangeLevel    = upg?.ToolRangeLevel    ?? 0,
                ToolCooldownLevel = upg?.ToolCooldownLevel ?? 0,
                ShopSeatLevel     = upg?.ShopSeatLevel     ?? 0,
                ShopTipLevel      = upg?.ShopTipLevel      ?? 0,
                BagCapacityLevel  = upg?.BagCapacityLevel  ?? 0,
            };

            if (inv != null)
            {
                foreach (var kv in inv.AllIngredients)
                    save.Ingredients.Add(new InventoryEntry { Id = kv.Key, Qty = kv.Value });

                foreach (var kv in inv.AllFoods)
                    save.Foods.Add(new FoodEntry { Id = kv.Key, Qty = kv.Value });
            }

            var mastery = pm?.Mastery;
            if (mastery != null)
            {
                foreach (var kv in mastery.CookCounts)
                    save.RecipeCookCounts.Add(new CookCountEntry { RecipeId = kv.Key, Count = kv.Value });
            }

            save.TotalFame = pm?.Fame?.Save() ?? 0;

            return save;
        }
    }
}
