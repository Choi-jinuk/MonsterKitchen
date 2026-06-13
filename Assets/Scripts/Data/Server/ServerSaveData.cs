using System;
using System.Collections.Generic;

namespace MonsterKitchen.Data
{
    // ====================================================================
    //  ServerSaveData — 영속 저장 데이터 DTO (JSON 직렬화 대상)
    //
    //  ▶ 역할
    //    PlayerData(런타임 상태)를 JSON 으로 직렬화/역직렬화하기 위한
    //    평탄한(flat) 직렬화 구조. Unity JsonUtility 호환.
    //
    //  ▶ Dictionary 제한
    //    Dictionary<uint, int> 는 JsonUtility 직렬화 불가.
    //    → InventoryEntry / FoodEntry List 로 변환 후 저장.
    //    FoodGrade Queue 는 휘발 정보 → 로드 시 Normal 로 복원.
    //
    //  ▶ 3-레이어 구조에서의 위치
    //    TableData(PlayerCharData) — 정적 캐릭터 정의 (읽기 전용)
    //    PlayerData                — 런타임 게임플레이 상태
    //    ServerSaveData            — 영속 직렬화 (이 클래스)              ← 현재
    // ====================================================================

    [Serializable]
    public class ServerSaveData
    {
        public int    Version       = 1;
        public long   SavedAtUtc;           // DateTime.UtcNow.Ticks
        public int    DayCount      = 1;
        public uint   SelectedCharId = 9001;
        public List<uint> PartyCompanionIds = new();   // AI 동료 (max 2)

        // ── 경제 ──────────────────────────────────────────────────────
        public int Gold;

        // ── 명성 ──────────────────────────────────────────────────────
        public int TotalFame;

        // ── 업그레이드 레벨 ────────────────────────────────────────────
        public int ToolDamageLevel;
        public int ToolRangeLevel;
        public int ToolCooldownLevel;
        public int ShopSeatLevel;
        public int ShopTipLevel;
        public int BagCapacityLevel;

        // ── 인벤토리 (List 직렬화) ────────────────────────────────────
        public List<InventoryEntry> Ingredients = new();
        public List<FoodEntry>      Foods       = new();

        // ── 품질/등급 (v2 추가 — v1 세이브는 빈 리스트로 로드되어 Normal/품질없음 처리) ──
        public List<IngredientQualityEntry> IngredientQualities = new();
        public List<FoodGradeEntry>         FoodGrades          = new();

        // ── 요리 마스터리 (List 직렬화 — JsonUtility는 Dictionary 불가) ──
        public List<CookCountEntry> RecipeCookCounts = new();
    }

    [Serializable]
    public class InventoryEntry
    {
        public uint Id;
        public int  Qty;
    }

    [Serializable]
    public class FoodEntry
    {
        public uint Id;
        public int  Qty;
        // 등급 상세는 FoodGradeEntry 리스트로 별도 저장 (v2)
    }

    /// <summary>재료 품질별 보유 수량. (Id, Quality) 조합당 1엔트리.</summary>
    [Serializable]
    public class IngredientQualityEntry
    {
        public uint Id;
        public int  Quality;   // (int)IngredientQuality
        public int  Count;
    }

    /// <summary>음식 등급별 보유 수량. (Id, Grade) 조합당 1엔트리.</summary>
    [Serializable]
    public class FoodGradeEntry
    {
        public uint Id;
        public int  Grade;     // (int)FoodGrade
        public int  Count;
    }

    [Serializable]
    public class CookCountEntry
    {
        public uint RecipeId;
        public int  Count;
    }
}
