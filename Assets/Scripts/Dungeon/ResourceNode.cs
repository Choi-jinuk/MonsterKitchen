using System.Collections;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    // ====================================================================
    //  ResourceNode — 던전 채집 노드 (나무 / 돌 / 광석)
    //
    //  ▶ 채집 방식
    //    플레이어가 **무기로 공격**하면 채집된다 (전투 우선, 전투 대상 없을 때 채집).
    //    공격을 받을 때마다 데미지가 내구도(currentHp)에서 차감된다.
    //    currentHp ≤ 0 → 채집 완료: dropIngredient 를 랜덤 수량 Inventory.Add.
    //
    //  ▶ 채집 도구 보너스
    //    PlayerStats.GatheringTool 에 등록된 도구가
    //    이 노드의 nodeType 을 compatibleNodeTypes 에 포함하면
    //    gatherMultiplier(드롭량 배율) + speedMultiplier(피해 배율) 적용.
    //    도구 내구도 1 소모.
    //
    //  ▶ 리스폰
    //    data.respawnSeconds > 0 이면 채집 완료 후 오브젝트를 비활성화하고
    //    respawnSeconds 초 뒤 원래 자리에 재활성화한다.
    //    respawnSeconds == 0 이면 영구 소멸.
    //
    //  ▶ 레이어
    //    Awake() 에서 "ResourceNode" 레이어로 자동 배정된다.
    //    Inspector 에서 수동 설정 불필요.
    // ====================================================================

    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ResourceNode : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] ResourceNodeData m_Data;

        // ── 런타임 상태 ─────────────────────────────────────────────────
        int  m_CurrentHp;
        bool m_Depleted;

        // 스프라이트가 없을 때 공유하는 플레이스홀더 (1×1 흰 텍스처)
        static Sprite s_Placeholder;

        // ================================================================
        //  Mono
        // ================================================================

        void Awake()
        {
            // "ResourceNode" 레이어 자동 배정 — Inspector 수동 설정 불필요
            int layer = LayerMask.NameToLayer("ResourceNode");
            if (layer < 0)
                Debug.LogWarning("[ResourceNode] 'ResourceNode' 레이어가 없습니다. " +
                                 "Project Settings → Tags & Layers 에서 추가하세요.");
            else
                gameObject.layer = layer;

            // 스프라이트 / 틴트 자동 적용
            if (m_Data != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var loadedSprite = !string.IsNullOrEmpty(m_Data.NodeSpriteAddress)
                        ? AssetLoadManager.Instance?.Load<Sprite>(m_Data.NodeSpriteAddress)
                        : null;
                    sr.sprite = loadedSprite != null ? loadedSprite : GetPlaceholder();
                    sr.color  = m_Data.NodeTint;
                }
            }
        }

        void OnEnable()
        {
            if (m_Data == null)
            {
                Debug.LogWarning($"[ResourceNode] {name}: ResourceNodeData 가 연결되지 않았습니다.");
                return;
            }
            m_CurrentHp = m_Data.MaxHp;
            m_Depleted  = false;

            // 콜라이더 활성화
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        // ================================================================
        //  피격 처리 (PlayerController 의 공격 OverlapCircle 에서 호출)
        // ================================================================

        /// <summary>
        /// 플레이어 공격 적중 시 PlayerController 가 직접 호출한다.
        /// damage : 실제 가한 데미지
        /// harvester : 공격한 플레이어의 PlayerStats (채집 도구 보너스 계산용)
        /// </summary>
        public void TakeHarvestDamage(int damage, Player.PlayerStats harvester = null)
        {
            if (m_Depleted || m_Data == null) return;

            // 채집 도구 speedMultiplier (호환 노드일 때만 적용)
            float speedMult = GetSpeedMultiplier(harvester);
            int   effective = Mathf.Max(1, Mathf.RoundToInt(damage * speedMult));

            m_CurrentHp -= effective;

            if (m_CurrentHp <= 0)
                Harvest(harvester);
        }

        // ================================================================
        //  채집 완료
        // ================================================================

        void Harvest(Player.PlayerStats harvester)
        {
            if (m_Depleted) return;
            m_Depleted = true;

            if (m_Data.DropIngredientId == 0u)
            {
                Debug.LogWarning($"[ResourceNode] {name}: dropIngredientId 가 설정되지 않았습니다.");
            }
            else
            {
                // 채집 도구 gatherMultiplier 적용
                float gatherMult = GetGatherMultiplier(harvester);
                int baseCount = RandomUtil.Range(m_Data.DropMin, m_Data.DropMax + 1);
                int count     = Mathf.Max(1, Mathf.RoundToInt(baseCount * gatherMult));

                NetworkManager.Instance?.RequestAddIngredient(m_Data.DropIngredientId, count);

                var ingredientData = DataRegistry.Instance?.GetIngredient(m_Data.DropIngredientId);
                string ingName = ingredientData != null ? ingredientData.DisplayName : m_Data.DropIngredientId.ToString();
                Debug.Log($"[ResourceNode] '{m_Data.DisplayName}' 채집 완료 → {ingName} ×{count}");
            }

            // 채집 도구 내구도 소모
            ConsumeToolDurability(harvester);

            // 리스폰 처리
            if (m_Data.RespawnSeconds > 0f)
                StartCoroutine(RespawnRoutine(m_Data.RespawnSeconds));
            else
                gameObject.SetActive(false); // 영구 소멸
        }

        // ================================================================
        //  리스폰
        // ================================================================

        IEnumerator RespawnRoutine(float delay)
        {
            // 콜라이더 비활성 + 스프라이트 숨김 (오브젝트는 유지해 코루틴 실행)
            var col = GetComponent<Collider2D>();
            var sr  = GetComponent<SpriteRenderer>();

            if (col != null) col.enabled = false;
            if (sr  != null) sr.enabled  = false;

            yield return new WaitForSeconds(delay);

            // 리스폰
            m_CurrentHp = m_Data.MaxHp;
            m_Depleted  = false;
            if (col != null) col.enabled = true;
            if (sr  != null) sr.enabled  = true;
        }

        // ================================================================
        //  채집 도구 보너스 계산 헬퍼
        // ================================================================

        bool IsCompatibleTool(Player.PlayerStats harvester, out GatheringToolData tool)
        {
            tool = null;
            if (harvester == null || m_Data == null) return false;

            tool = harvester.GatheringTool;
            if (tool == null) return false;

            foreach (var t in tool.CompatibleNodeTypes)
                if (t == m_Data.NodeType) return true;

            return false;
        }

        float GetSpeedMultiplier(Player.PlayerStats harvester)
        {
            return IsCompatibleTool(harvester, out var tool) ? tool.SpeedMultiplier : 1f;
        }

        float GetGatherMultiplier(Player.PlayerStats harvester)
        {
            return IsCompatibleTool(harvester, out var tool) ? tool.GatherMultiplier : 1f;
        }

        void ConsumeToolDurability(Player.PlayerStats harvester)
        {
            if (!IsCompatibleTool(harvester, out _)) return;
            harvester?.ConsumeGatheringToolDurability();
        }

        // ================================================================
        //  프로퍼티
        // ================================================================

        public ResourceNodeData Data      => m_Data;
        public int              CurrentHp => m_CurrentHp;
        public bool             Depleted  => m_Depleted;

        // ================================================================
        //  플레이스홀더 스프라이트 (스프라이트 미설정 시 사용)
        // ================================================================

        static Sprite GetPlaceholder()
        {
            if (s_Placeholder != null) return s_Placeholder;

            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp
            };
            var pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            s_Placeholder = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            return s_Placeholder;
        }
    }
}
