using System.Collections;
using System.Collections.Generic;
using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Dungeon;
using MonsterKitchen.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  GameHUD — UI Toolkit 기반 HUD.
    //
    //  ▶ 상단
    //    gold-label / day-label : GoldManager / DayManager 이벤트 연동
    //    hp-chip / hp-bar-fill / hp-text : Health 이벤트 연동
    //
    //  ▶ 하단 (3-UI1 · 3-UI2 · 3-UI3)
    //    weapon-slot     : 장착 무기 아이콘 표시
    //    skill1/2-slot   : 스킬 아이콘 + 쿨타임 오버레이
    //    ult-slot        : 궁극기 아이콘 + 게이지 바 + READY 표시
    //
    //  ▶ 던전 전용 (씬 진입 시 활성화)
    //    minimap-root    : 플레이어 도트 + 스폰 존 마커
    //    bag-weight-bar  : 현재 무게 / 최대 무게 바
    //    notification-label : 토스트 알림 (DungeonBag 가득 참 등)
    //
    //  씬 전환 시 SceneManager.sceneLoaded 에서 플레이어·미니맵을 재탐색한다.
    // ====================================================================
    [RequireComponent(typeof(UIDocument))]
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        // ── 상단 HUD ─────────────────────────────────────────────────
        Label         m_GoldLabel;
        Label         m_DayLabel;
        VisualElement m_HpChip;
        VisualElement m_HpFill;
        Label         m_HpText;

        // ── 하단 스킬 바 ──────────────────────────────────────────────
        VisualElement m_WeaponIconBg;

        VisualElement m_Skill1IconBg;
        VisualElement m_Skill1CoolFill;
        Label         m_Skill1CoolText;

        VisualElement m_Skill2IconBg;
        VisualElement m_Skill2CoolFill;
        Label         m_Skill2CoolText;

        VisualElement m_UltIconBg;
        VisualElement m_UltCoolFill;
        VisualElement m_UltGaugeFill;
        Label         m_UltReadyLabel;

        // ── 저장 인디케이터 ───────────────────────────────────────────
        VisualElement m_SaveIndicator;

        // ── 미니맵 ───────────────────────────────────────────────────
        VisualElement        m_MinimapRoot;
        VisualElement        m_MinimapBg;
        VisualElement        m_MinimapPlayerDot;
        readonly List<VisualElement> m_SpawnMarkers = new();

        // ── 가방 무게 바 ──────────────────────────────────────────────
        VisualElement m_BagWeightBar;
        Label         m_BagWeightLabel;
        VisualElement m_BagWeightFill;

        // ── 알림 토스트 ───────────────────────────────────────────────
        Label         m_NotificationLabel;
        Coroutine     m_NotificationCoroutine;

        // ── 플레이어 컴포넌트 참조 ────────────────────────────────────
        Health           m_PlayerHealth;
        PlayerStats      m_PlayerStats;
        Transform        m_PlayerTransform;
        PlayerController m_PlayerController;

        // ── 스폰 존 캐시 (던전 씬 진입 시 이벤트 구독용) ──────────────
        DungeonSpawnZone[] m_CachedSpawnZones;

        // ── 미니맵 월드 바운드 캐시 ───────────────────────────────────
        Rect m_WorldBounds;

        // ================================================================
        //  Mono
        // ================================================================

        void OnEnable()
        {
            if (SaveScheduler.Instance != null)
                SaveScheduler.Instance.OnSaveStateChanged += OnSaveStateChanged;
        }

        void OnDisable()
        {
            if (SaveScheduler.Instance != null)
                SaveScheduler.Instance.OnSaveStateChanged -= OnSaveStateChanged;
        }

        void Start()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var root = GetComponent<UIDocument>().rootVisualElement;

            // 상단 요소
            m_GoldLabel = root.Q<Label>("gold-label");
            m_DayLabel  = root.Q<Label>("day-label");
            m_HpChip    = root.Q<VisualElement>("hp-chip");
            m_HpFill    = root.Q<VisualElement>("hp-bar-fill");
            m_HpText    = root.Q<Label>("hp-text");

            // 미니맵 요소 캐시 (던전 씬 아닐 때는 숨김 유지)
            m_MinimapRoot      = root.Q<VisualElement>("minimap-root");
            m_MinimapBg        = root.Q<VisualElement>("minimap-bg");
            m_MinimapPlayerDot = root.Q<VisualElement>("minimap-player-dot");

            // 가방 무게 바 요소 캐시
            m_BagWeightBar   = root.Q<VisualElement>("bag-weight-bar");
            m_BagWeightLabel = root.Q<Label>("bag-weight-label");
            m_BagWeightFill  = root.Q<VisualElement>("bag-weight-fill");

            // 알림 토스트
            m_NotificationLabel = root.Q<Label>("notification-label");
            if (m_NotificationLabel != null)
                m_NotificationLabel.style.display = DisplayStyle.None;

            // 하단 스킬 바
            m_WeaponIconBg   = root.Q<VisualElement>("weapon-icon-bg");

            m_Skill1IconBg   = root.Q<VisualElement>("skill1-icon-bg");
            m_Skill1CoolFill = root.Q<VisualElement>("skill1-cool-fill");
            m_Skill1CoolText = root.Q<Label>("skill1-cool-text");

            m_Skill2IconBg   = root.Q<VisualElement>("skill2-icon-bg");
            m_Skill2CoolFill = root.Q<VisualElement>("skill2-cool-fill");
            m_Skill2CoolText = root.Q<Label>("skill2-cool-text");

            m_UltIconBg     = root.Q<VisualElement>("ult-icon-bg");
            m_UltCoolFill   = root.Q<VisualElement>("ult-cool-fill");
            m_UltGaugeFill  = root.Q<VisualElement>("ult-gauge-fill");
            m_UltReadyLabel = root.Q<Label>("ult-ready-label");

            // 저장 인디케이터
            m_SaveIndicator = root.Q<VisualElement>("save-indicator");
            if (m_SaveIndicator != null)
                m_SaveIndicator.style.display = DisplayStyle.None;

            // 매니저 이벤트 구독
            UpdateGold(PlayerDataManager.Instance?.Coin.Gold ?? 0);
            UpdateDay(DayManager.Instance != null ? DayManager.Instance.CurrentDay : 1);

            if (PlayerDataManager.Instance?.Coin != null)
                PlayerDataManager.Instance.Coin.OnGoldChanged += UpdateGold;

            if (DayManager.Instance != null)
            {
                DayManager.Instance.OnDayStarted += UpdateDay;
                DayManager.Instance.OnDayEnded   += UpdateDay;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            FindAndBindPlayer();
            FindAndBindMinimap();
        }

        // Update() 제거 — 미니맵 도트/스폰 마커는 이벤트 구동 (Section D)

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (PlayerDataManager.Instance?.Coin != null)
                PlayerDataManager.Instance.Coin.OnGoldChanged -= UpdateGold;

            if (DayManager.Instance != null)
            {
                DayManager.Instance.OnDayStarted -= UpdateDay;
                DayManager.Instance.OnDayEnded   -= UpdateDay;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindPlayer();
            UnbindMinimap();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnbindPlayer();
            FindAndBindPlayer();
            UnbindMinimap();
            FindAndBindMinimap();
        }

        // ================================================================
        //  플레이어 탐색 & 바인딩
        //  PlayerController 는 런타임 스폰 오브젝트 → 태그 검색 허용 (캐시됨)
        // ================================================================

        void FindAndBindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            var pc = go != null ? go.GetComponent<PlayerController>() : null;
            if (pc == null) { SetHpBarVisible(false); return; }

            m_PlayerTransform  = pc.transform;
            m_PlayerController = pc;
            pc.OnMoved += OnPlayerMoved;

            // Health
            var health = pc.GetComponent<Health>();
            if (health != null)
            {
                m_PlayerHealth = health;
                m_PlayerHealth.OnHpChanged += UpdateHpBar;
                UpdateHpBar(m_PlayerHealth.CurrentHp, m_PlayerHealth.MaxHp);
                SetHpBarVisible(true);
            }
            else
            {
                SetHpBarVisible(false);
            }

            // PlayerStats
            var stats = pc.GetComponent<PlayerStats>();
            if (stats != null)
            {
                m_PlayerStats = stats;
                m_PlayerStats.OnWeaponChanged        += UpdateWeaponSlot;
                m_PlayerStats.OnSkillChanged         += UpdateSkillSlot;
                m_PlayerStats.OnCooldownChanged      += UpdateCooldown;
                m_PlayerStats.OnUltimateGaugeChanged += OnUltimateGaugeChanged;
                m_PlayerStats.OnUltimateReady        += ShowUltReady;

                UpdateWeaponSlot(stats.EquippedWeapon);
                UpdateSkillSlot(1, stats.SkillSlot1);
                UpdateSkillSlot(2, stats.SkillSlot2);
                UpdateSkillSlot(3, stats.UltimateSlot);
                UpdateUltGauge(stats.UltimateGauge, stats.MaxUltimateGauge);
            }
        }

        void UnbindPlayer()
        {
            m_PlayerTransform = null;

            if (m_PlayerController != null)
            {
                m_PlayerController.OnMoved -= OnPlayerMoved;
                m_PlayerController = null;
            }

            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnHpChanged -= UpdateHpBar;
                m_PlayerHealth = null;
            }

            if (m_PlayerStats != null)
            {
                m_PlayerStats.OnWeaponChanged        -= UpdateWeaponSlot;
                m_PlayerStats.OnSkillChanged         -= UpdateSkillSlot;
                m_PlayerStats.OnCooldownChanged      -= UpdateCooldown;
                m_PlayerStats.OnUltimateGaugeChanged -= OnUltimateGaugeChanged;
                m_PlayerStats.OnUltimateReady        -= ShowUltReady;
                m_PlayerStats = null;
            }

            SetHpBarVisible(false);
            SetCooldownUI(m_Skill1CoolFill, m_Skill1CoolText, 0f, 1f);
            SetCooldownUI(m_Skill2CoolFill, m_Skill2CoolText, 0f, 1f);
        }

        // ================================================================
        //  미니맵 바인딩 (던전 씬 전용)
        // ================================================================

        void FindAndBindMinimap()
        {
            var map = DungeonMapController.Instance;
            if (map == null || m_MinimapRoot == null)
            {
                if (m_MinimapRoot != null) m_MinimapRoot.style.display = DisplayStyle.None;
                if (m_BagWeightBar != null) m_BagWeightBar.style.display = DisplayStyle.None;
                return;
            }

            // 미니맵 배경 스프라이트
            if (m_MinimapBg != null && map.MinimapSprite != null)
                m_MinimapBg.style.backgroundImage = new StyleBackground(map.MinimapSprite);

            // 월드 바운드 캐시 (UV 변환에 사용)
            m_WorldBounds = map.WorldBounds;

            // 스폰 존 마커 생성 + 이벤트 구독
            var zones = map.SpawnZones;
            m_CachedSpawnZones = new DungeonSpawnZone[zones.Count];
            for (int i = 0; i < zones.Count; i++)
            {
                var zone   = zones[i];
                var marker = new VisualElement();
                marker.AddToClassList("minimap-spawn-marker");
                m_MinimapRoot.Add(marker);
                m_SpawnMarkers.Add(marker);
                PositionMarkerForZone(marker, zone);
                m_CachedSpawnZones[i] = zone;
                if (zone != null) zone.OnMonsterCountChanged += UpdateSpawnMarkers;
            }

            m_MinimapRoot.style.display = DisplayStyle.Flex;

            // 가방 무게 바 구독 및 초기화
            if (DungeonBag.Current != null)
            {
                DungeonBag.Current.OnBagChanged += UpdateBagBar;
                UpdateBagBar();
                if (m_BagWeightBar != null) m_BagWeightBar.style.display = DisplayStyle.Flex;
            }
        }

        void UnbindMinimap()
        {
            if (DungeonBag.Current != null)
                DungeonBag.Current.OnBagChanged -= UpdateBagBar;

            if (m_CachedSpawnZones != null)
            {
                foreach (var zone in m_CachedSpawnZones)
                    if (zone != null) zone.OnMonsterCountChanged -= UpdateSpawnMarkers;
                m_CachedSpawnZones = null;
            }

            m_SpawnMarkers.Clear();
            if (m_MinimapRoot != null)
            {
                m_MinimapRoot.Clear();
                m_MinimapRoot.style.display = DisplayStyle.None;
            }
            if (m_BagWeightBar != null)
                m_BagWeightBar.style.display = DisplayStyle.None;

            m_WorldBounds = default;
        }

        // ── 미니맵 갱신 ──────────────────────────────────────────────

        void OnPlayerMoved(Vector3 pos) => UpdateMinimapDot();

        void UpdateMinimapDot()
        {
            if (m_MinimapRoot == null || m_MinimapPlayerDot == null) return;
            if (m_MinimapRoot.style.display == DisplayStyle.None) return;
            if (m_PlayerTransform == null) return;

            var uv = WorldToMinimapUV(m_PlayerTransform.position);
            var rootW = m_MinimapRoot.resolvedStyle.width;
            var rootH = m_MinimapRoot.resolvedStyle.height;
            var dotW  = m_MinimapPlayerDot.resolvedStyle.width;
            var dotH  = m_MinimapPlayerDot.resolvedStyle.height;

            m_MinimapPlayerDot.style.left = uv.x * rootW - dotW * 0.5f;
            m_MinimapPlayerDot.style.top  = (1f - uv.y) * rootH - dotH * 0.5f;
        }

        void UpdateSpawnMarkers()
        {
            if (m_MinimapRoot == null || m_MinimapRoot.style.display == DisplayStyle.None) return;

            var map = DungeonMapController.Instance;
            if (map == null) return;

            var zones = map.SpawnZones;
            int count = Mathf.Min(m_SpawnMarkers.Count, zones.Count);
            for (int i = 0; i < count; i++)
            {
                bool alive = zones[i].HasAliveMonsters;
                if (alive)
                    m_SpawnMarkers[i].RemoveFromClassList("minimap-spawn-marker--inactive");
                else
                    m_SpawnMarkers[i].AddToClassList("minimap-spawn-marker--inactive");
            }
        }

        void PositionMarkerForZone(VisualElement marker, DungeonSpawnZone zone)
        {
            var uv    = WorldToMinimapUV(zone.ZoneCenter);
            var rootW = m_MinimapRoot.resolvedStyle.width;
            var rootH = m_MinimapRoot.resolvedStyle.height;
            marker.style.left = uv.x * rootW - 5f;
            marker.style.top  = (1f - uv.y) * rootH - 5f;
        }

        Vector2 WorldToMinimapUV(Vector3 worldPos)
        {
            if (m_WorldBounds.width == 0f || m_WorldBounds.height == 0f)
                return new Vector2(0.5f, 0.5f);

            float u = (worldPos.x - m_WorldBounds.xMin) / m_WorldBounds.width;
            float v = (worldPos.y - m_WorldBounds.yMin) / m_WorldBounds.height;
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        // ================================================================
        //  가방 무게 바 갱신
        // ================================================================

        void UpdateBagBar()
        {
            var bag = DungeonBag.Current;
            if (bag == null || m_BagWeightBar == null) return;

            float ratio = bag.MaxWeight > 0 ? (float)bag.CurrentWeight / bag.MaxWeight : 0f;

            if (m_BagWeightFill != null)
            {
                m_BagWeightFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);

                if (bag.IsFull)
                {
                    m_BagWeightFill.AddToClassList("bag-weight-fill--full");
                }
                else
                {
                    m_BagWeightFill.RemoveFromClassList("bag-weight-fill--full");
                }
            }

            if (m_BagWeightLabel != null)
            {
                m_BagWeightLabel.text = $"{bag.CurrentWeight}/{bag.MaxWeight}";

                if (bag.IsFull)
                    m_BagWeightLabel.AddToClassList("bag-weight-label--full");
                else
                    m_BagWeightLabel.RemoveFromClassList("bag-weight-label--full");
            }
        }

        // ================================================================
        //  알림 토스트
        // ================================================================

        /// <summary>HUD 상단에 토스트 메시지 표시. duration 초 후 자동 숨김.</summary>
        public void ShowNotification(string message, float duration = 2f)
        {
            if (m_NotificationLabel == null) return;

            if (m_NotificationCoroutine != null)
                StopCoroutine(m_NotificationCoroutine);

            m_NotificationCoroutine = StartCoroutine(NotificationRoutine(message, duration));
        }

        IEnumerator NotificationRoutine(string message, float duration)
        {
            m_NotificationLabel.text = message;
            m_NotificationLabel.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(duration);
            m_NotificationLabel.style.display = DisplayStyle.None;
            m_NotificationCoroutine = null;
        }

        // ================================================================
        //  상단 HUD 갱신
        // ================================================================

        void UpdateGold(int gold)
        {
            if (m_GoldLabel != null) m_GoldLabel.text = $"Gold: {gold} G";
        }

        void UpdateDay(int day)
        {
            if (m_DayLabel != null) m_DayLabel.text = $"Day {day}";
        }

        void UpdateHpBar(int current, int max)
        {
            if (m_HpFill == null || m_HpText == null) return;

            float ratio = max > 0 ? (float)current / max : 0f;
            m_HpFill.style.width = Length.Percent(ratio * 100f);

            Color barColor = ratio > 0.5f
                ? Color.Lerp(new Color(0.9f, 0.8f, 0f), new Color(0.2f, 0.8f, 0.2f), (ratio - 0.5f) * 2f)
                : Color.Lerp(new Color(0.86f, 0.23f, 0.23f), new Color(0.9f, 0.8f, 0f), ratio * 2f);
            m_HpFill.style.backgroundColor = barColor;

            m_HpText.text = $"{current}/{max}";
        }

        void SetHpBarVisible(bool visible)
        {
            if (m_HpChip == null) return;
            m_HpChip.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ================================================================
        //  하단 스킬 바 갱신
        // ================================================================

        /// <summary>무기 변경 시 아이콘 업데이트.</summary>
        void UpdateWeaponSlot(WeaponData weapon)
        {
            if (m_WeaponIconBg == null) return;

            Sprite icon = null;
            if (weapon?.NormalAttackGroup != null && !string.IsNullOrEmpty(weapon.NormalAttackGroup.SkillIconAddress))
                icon = AssetLoadManager.Instance?.Load<Sprite>(weapon.NormalAttackGroup.SkillIconAddress);

            m_WeaponIconBg.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);
        }

        /// <summary>스킬 슬롯 변경 시 아이콘 업데이트. slot: 1·2·3(궁극기).</summary>
        void UpdateSkillSlot(int slot, SkillGroupData skill)
        {
            VisualElement iconBg = slot switch
            {
                1 => m_Skill1IconBg,
                2 => m_Skill2IconBg,
                3 => m_UltIconBg,
                _ => null
            };
            if (iconBg == null) return;

            Sprite icon = null;
            if (skill != null && !string.IsNullOrEmpty(skill.SkillIconAddress))
                icon = AssetLoadManager.Instance?.Load<Sprite>(skill.SkillIconAddress);

            iconBg.style.backgroundImage = icon != null
                ? new StyleBackground(icon)
                : new StyleBackground(StyleKeyword.None);
        }

        /// <summary>쿨타임 변경 시. slot: 1·2. remaining, total.</summary>
        void UpdateCooldown(int slot, float remaining, float total)
        {
            if (slot == 1)
                SetCooldownUI(m_Skill1CoolFill, m_Skill1CoolText, remaining, total);
            else if (slot == 2)
                SetCooldownUI(m_Skill2CoolFill, m_Skill2CoolText, remaining, total);
        }

        void SetCooldownUI(VisualElement fill, Label text, float remaining, float total)
        {
            if (fill == null) return;

            bool onCooldown = remaining > 0f && total > 0f;

            fill.style.display = onCooldown ? DisplayStyle.Flex : DisplayStyle.None;
            if (text != null)
                text.style.display = onCooldown ? DisplayStyle.Flex : DisplayStyle.None;

            if (onCooldown)
            {
                float ratio = remaining / total;
                fill.style.height = Length.Percent(ratio * 100f);
                if (text != null)
                    text.text = remaining > 1f ? $"{remaining:F0}" : $"{remaining:F1}";
            }
        }

        /// <summary>궁극기 게이지 변경 시 — 게이지 바 갱신 + 리셋 시 READY 숨김.</summary>
        void OnUltimateGaugeChanged(float current, float max)
        {
            UpdateUltGauge(current, max);
            if (current <= 0f && m_UltReadyLabel != null)
                m_UltReadyLabel.style.display = DisplayStyle.None;
        }

        void UpdateUltGauge(float current, float max)
        {
            if (m_UltGaugeFill == null) return;
            float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            m_UltGaugeFill.style.width = Length.Percent(ratio * 100f);
        }

        /// <summary>궁극기 게이지 MAX 도달 시 READY 표시.</summary>
        void ShowUltReady()
        {
            if (m_UltReadyLabel == null) return;
            m_UltReadyLabel.text = "READY";
            m_UltReadyLabel.style.display = DisplayStyle.Flex;
        }

        // ================================================================
        //  저장 인디케이터
        // ================================================================

        void OnSaveStateChanged(bool isSaving)
        {
            if (m_SaveIndicator == null) return;
            m_SaveIndicator.style.display = isSaving ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
