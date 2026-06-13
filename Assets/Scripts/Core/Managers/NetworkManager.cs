using MonsterKitchen.Core;
using System;
using System.Collections.Generic;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Core
{
    // ====================================================================
    //  NetworkManager — 서버↔클라이언트 패킷 관리자
    //
    //  ▶ 역할
    //    모든 PlayerData 변경 요청을 서버로 중계한다.
    //    클라이언트 코드는 Request*() 를 호출하고,
    //    서버 응답(콜백)이 오면 PlayerDataManager.Apply*() 로 상태를 반영한다.
    //    클라이언트는 서버 응답 전까지 PlayerData 를 직접 수정하지 않는다.
    //
    //  ▶ 현재 구현 (Server Stub)
    //    실제 서버 없이 로컬에서 연산을 수행해 즉시 콜백을 호출한다.
    //    "[SERVER STUB START] ~ [SERVER STUB END]" 블록을 실제
    //    HTTP/WebSocket 호출로 교체하면 나머지 코드는 변경이 없다.
    //
    //  ▶ 데이터 흐름
    //    Client → Request*()
    //               ↓ [SERVER STUB]
    //             연산 처리 (골드 검증, 재고 확인, 레벨 계산 등)
    //               ↓
    //    PlayerDataManager.Apply*() → 이벤트 발행 → UI 갱신
    //               ↓
    //    onResult 콜백 (성공 여부 + 결과 값)
    //
    //  ▶ 생명주기
    //    GlobalController.InitManagers() → Init()  (PlayerDataManager.Init 이후)
    // ====================================================================

    public class NetworkManager : INetworkManager
    {
        public static NetworkManager Instance { get; private set; }

        public void Init() => Instance = this;

        // ================================================================
        //  골드
        // ================================================================

        // ── 파티 편성 ────────────────────────────────────────────────────

        /// <summary>요청 파티를 정제: 리더 제외, 중복 제거, 무효 ID 드롭, 최대 2명.</summary>
        public static List<uint> SanitizeParty(uint leaderId, IReadOnlyList<uint> requested, Func<uint, bool> isValidId)
        {
            var result = new List<uint>(2);
            if (requested == null) return result;
            foreach (var id in requested)
            {
                if (result.Count >= 2)                   break;
                if (id == leaderId)                      continue;
                if (result.Contains(id))                 continue;
                if (isValidId != null && !isValidId(id)) continue;
                result.Add(id);
            }
            return result;
        }

        /// <summary>파티(동료) 구성을 요청한다. 검증 후 PlayerDataManager 반영 + dirty.</summary>
        public void RequestSetParty(IReadOnlyList<uint> ids, Action onResult = null)
        {
            var  pm     = PlayerDataManager.Instance;
            var  chars  = DataRegistry.Instance?.PlayerChars;
            uint leader = pm?.SelectedCharId ?? 9001;

            var clean = SanitizeParty(leader, ids,
                id => chars != null && chars.Get(id) != null);

            pm?.ApplyParty(clean);
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke();
        }

        /// <summary>
        /// 골드 획득을 서버에 요청한다.
        /// 성공 시 onResult(newGold) 호출.
        /// </summary>
        public void RequestEarnGold(int amount, Action<int> onResult = null)
        {
            if (amount <= 0)
            {
                onResult?.Invoke(PlayerDataManager.Instance.Coin.Gold);
                return;
            }

            // ── [SERVER STUB START] ──────────────────────────────────
            int newGold = PlayerDataManager.Instance.Coin.Gold + amount;
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplyGold(newGold);
            DebugUtil.Log(StringUtil.Format("[Network] EarnGold +{0}G → 총 {1}G", amount, newGold));
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(newGold);
        }

        /// <summary>
        /// 골드 소모를 서버에 요청한다.
        /// 잔액 부족 시 onResult(false, currentGold).
        /// 성공 시 onResult(true, newGold).
        /// </summary>
        public void RequestSpendGold(int amount, Action<bool, int> onResult = null)
        {
            int current = PlayerDataManager.Instance.Coin.Gold;

            // ── [SERVER STUB START] ──────────────────────────────────
            if (amount <= 0 || current < amount)
            {
                DebugUtil.Log(StringUtil.Format("[Network] SpendGold 실패 — 잔액 부족 ({0}G / {1}G 필요)", current, amount));
                onResult?.Invoke(false, current);
                return;
            }
            int newGold = current - amount;
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplyGold(newGold);
            DebugUtil.Log(StringUtil.Format("[Network] SpendGold -{0}G → 총 {1}G", amount, newGold));
            GlobalController.Instance?.SaveSched.ForceSave();
            onResult?.Invoke(true, newGold);
        }

        // ================================================================
        //  재료 인벤토리
        // ================================================================

        /// <summary>
        /// 재료 추가를 서버에 요청한다.
        /// 성공 시 onResult(ingredientId, newQty) 호출.
        /// </summary>
        public void RequestAddIngredient(uint ingredientId, int qty,
                                        Action<uint, int> onResult = null)
        {
            if (qty <= 0) return;

            // ── [SERVER STUB START] ──────────────────────────────────
            int cur    = PlayerDataManager.Instance.Inventory.GetIngredientCount(ingredientId);
            int newQty = cur + qty;
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplyIngredient(ingredientId, newQty);
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(ingredientId, newQty);
        }

        /// <summary>
        /// 품질 포함 재료 추가를 서버에 요청한다.
        /// 총 수량 업데이트 + 품질 카운트 업데이트.
        /// 성공 시 onResult(ingredientId, newQty) 호출.
        /// </summary>
        public void RequestAddIngredient(uint ingredientId, int qty, IngredientQuality quality,
                                         Action<uint, int> onResult = null)
        {
            if (qty <= 0) return;

            // ── [SERVER STUB START] ──────────────────────────────────
            int cur    = PlayerDataManager.Instance.Inventory.GetIngredientCount(ingredientId);
            int newQty = cur + qty;
            // ── [SERVER STUB END] ────────────────────────────────────

            // AddIngredientWithQuality 는 총 수량과 품질 카운트를 모두 갱신한다.
            PlayerDataManager.Instance.Inventory.AddIngredientWithQuality(ingredientId, qty, quality);
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(ingredientId, newQty);
        }

        // ================================================================
        //  요리 (재료 소모 + 음식 추가 원자 처리)
        // ================================================================

        /// <summary>
        /// 레시피를 요리한다. 서버에서 재료를 검증·소모하고 음식을 추가한다.
        /// onResult(success, resultFoodData) 호출.
        /// </summary>
        public void RequestCook(RecipeData recipe, FoodGrade grade,
                                Action<bool, FoodData> onResult = null)
        {
            if (recipe == null) { onResult?.Invoke(false, null); return; }

            var inv = PlayerDataManager.Instance.Inventory;

            // ── [SERVER STUB START] ──────────────────────────────────
            var reqCounts = new Dictionary<uint, int>();
            foreach (var req in recipe.Ingredients)
            {
                if (req.IngredientId == 0u) continue;
                reqCounts.TryGetValue(req.IngredientId, out int used);
                reqCounts[req.IngredientId] = used + req.Quantity;
            }

            foreach (var kv in reqCounts)
            {
                if (inv.GetIngredientCount(kv.Key) < kv.Value)
                {
                    DebugUtil.LogWarning(StringUtil.Format("[Network] RequestCook 실패 — 재료 부족 (id:{0} 필요:{1})", kv.Key, kv.Value));
                    onResult?.Invoke(false, null);
                    return;
                }
            }

            var ingredientChanges = new Dictionary<uint, int>();
            foreach (var kv in reqCounts)
            {
                int cur = inv.GetIngredientCount(kv.Key);
                ingredientChanges[kv.Key] = cur - kv.Value;
            }
            // ── [SERVER STUB END] ────────────────────────────────────

            foreach (var kv in ingredientChanges)
                PlayerDataManager.Instance.ApplyIngredient(kv.Key, kv.Value);

            foreach (var kv in reqCounts)
                PlayerDataManager.Instance.Inventory.ConsumeIngredientQuality(kv.Key, kv.Value);

            PlayerDataManager.Instance.ApplyFoodAdd(recipe.ResultFoodId, grade);

            FoodData food = DataRegistry.Instance?.Foods?.Get(recipe.ResultFoodId);
            DebugUtil.Log(StringUtil.Format("[Network] Cook 완료 → {0} [{1}]",
                food?.DisplayName ?? recipe.ResultFoodId.ToString(), grade));
            GlobalController.Instance?.SaveSched.ForceSave();
            onResult?.Invoke(true, food);
        }

        // ================================================================
        //  음식 서빙 (음식 소모 + 등급 반환)
        // ================================================================

        /// <summary>
        /// 음식 1개를 서빙한다. 서버에서 재고를 확인하고 등급과 잔여 수량을 반환한다.
        /// onResult(success, grade, remaining) 호출.
        /// </summary>
        public void RequestServeFood(uint foodId, Action<bool, FoodGrade, int> onResult = null)
        {
            // ── [SERVER STUB START] ──────────────────────────────────
            if (PlayerDataManager.Instance.Inventory.GetFoodCount(foodId) <= 0)
            {
                DebugUtil.Log(StringUtil.Format("[Network] RequestServeFood 실패 — 재고 없음 (id:{0})", foodId));
                onResult?.Invoke(false, FoodGrade.Normal, 0);
                return;
            }
            // ── [SERVER STUB END] ────────────────────────────────────

            var (grade, remaining) = PlayerDataManager.Instance.ApplyFoodConsume(foodId);
            DebugUtil.Log(StringUtil.Format("[Network] ServeFood id:{0} [{1}] 잔여:{2}", foodId, grade, remaining));
            GlobalController.Instance?.SaveSched.MarkDirty();
            onResult?.Invoke(true, grade, remaining);
        }

        // ================================================================
        //  업그레이드
        // ================================================================

        /// <summary>
        /// 업그레이드를 서버에 요청한다.
        /// 서버에서 골드를 차감하고 레벨을 올린 뒤 onResult(success) 호출.
        /// </summary>
        public void RequestUpgrade(PlayerUpgradeType type, Action<bool> onResult = null)
        {
            var upg  = PlayerDataManager.Instance.Upgrades;
            var coin = PlayerDataManager.Instance.Coin;

            // ── [SERVER STUB START] ──────────────────────────────────
            int cost = type switch
            {
                PlayerUpgradeType.ToolDamage   => upg.ToolDamageUpgradeCost,
                PlayerUpgradeType.ToolRange    => upg.ToolRangeUpgradeCost,
                PlayerUpgradeType.ToolCooldown => upg.ToolCooldownUpgradeCost,
                PlayerUpgradeType.ShopSeats    => upg.ShopSeatUpgradeCost,
                PlayerUpgradeType.ShopTip      => upg.ShopTipUpgradeCost,
                PlayerUpgradeType.BagCapacity  => upg.BagUpgradeCost,
                _                              => int.MaxValue,
            };

            if (coin.Gold < cost)
            {
                DebugUtil.Log(StringUtil.Format("[Network] Upgrade 실패 — 골드 부족 ({0}G / {1}G 필요)", coin.Gold, cost));
                onResult?.Invoke(false);
                return;
            }

            int newGold  = coin.Gold - cost;
            int newLevel = type switch
            {
                PlayerUpgradeType.ToolDamage   => upg.ToolDamageLevel   + 1,
                PlayerUpgradeType.ToolRange    => upg.ToolRangeLevel    + 1,
                PlayerUpgradeType.ToolCooldown => upg.ToolCooldownLevel + 1,
                PlayerUpgradeType.ShopSeats    => upg.ShopSeatLevel     + 1,
                PlayerUpgradeType.ShopTip      => upg.ShopTipLevel      + 1,
                PlayerUpgradeType.BagCapacity  => upg.BagCapacityLevel  + 1,
                _                              => 0,
            };
            // ── [SERVER STUB END] ────────────────────────────────────

            PlayerDataManager.Instance.ApplyGold(newGold);
            PlayerDataManager.Instance.ApplyUpgradeLevel(type, newLevel);
            DebugUtil.Log(StringUtil.Format("[Network] Upgrade {0} Lv{1} 완료 (골드: {2}G)", type, newLevel, newGold));
            GlobalController.Instance?.SaveSched.ForceSave();
            onResult?.Invoke(true);
        }
    }
}
