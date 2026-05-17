using MonsterKitchen.Combat;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using MonsterKitchen.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// UI Toolkit 기반 HUD.
    ///
    /// ▶ 상단
    ///   gold-label / day-label : GoldManager / DayManager 이벤트 연동
    ///   hp-chip / hp-bar-fill / hp-text : Health 이벤트 연동
    ///
    /// ▶ 하단 (3-UI1 · 3-UI2 · 3-UI3)
    ///   weapon-slot     : 장착 무기 아이콘 표시
    ///   skill1/2-slot   : 스킬 아이콘 + 쿨타임 오버레이
    ///   ult-slot        : 궁극기 아이콘 + 게이지 바 + READY 표시
    ///
    /// 씬 전환 시 SceneManager.sceneLoaded 에서 플레이어를 재탐색한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        // ── 상단 HUD ─────────────────────────────────────────────────
        Label         _goldLabel;
        Label         _dayLabel;
        VisualElement _hpChip;
        VisualElement _hpFill;
        Label         _hpText;

        // ── 하단 스킬 바 ──────────────────────────────────────────────
        VisualElement _weaponIconBg;

        VisualElement _skill1IconBg;
        VisualElement _skill1CoolFill;
        Label         _skill1CoolText;

        VisualElement _skill2IconBg;
        VisualElement _skill2CoolFill;
        Label         _skill2CoolText;

        VisualElement _ultIconBg;
        VisualElement _ultCoolFill;
        VisualElement _ultGaugeFill;
        Label         _ultReadyLabel;

        // ── 미니맵 (v2 재구현 대기 — 현재 숨김) ─────────────────────
        VisualElement _minimapRoot;

        // ── 플레이어 컴포넌트 참조 ────────────────────────────────────
        Health      _playerHealth;
        PlayerStats _playerStats;

        // ================================================================
        //  Mono
        // ================================================================

        void Start()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var root = GetComponent<UIDocument>().rootVisualElement;

            // 상단 요소
            _goldLabel = root.Q<Label>("gold-label");
            _dayLabel  = root.Q<Label>("day-label");
            _hpChip    = root.Q<VisualElement>("hp-chip");
            _hpFill    = root.Q<VisualElement>("hp-bar-fill");
            _hpText    = root.Q<Label>("hp-text");

            // 미니맵 (Start 시점엔 던전 씬이 아닐 수 있으므로 요소만 캐시)
            _minimapRoot = root.Q<VisualElement>("minimap-root");

            // 하단 스킬 바
            _weaponIconBg   = root.Q<VisualElement>("weapon-icon-bg");

            _skill1IconBg   = root.Q<VisualElement>("skill1-icon-bg");
            _skill1CoolFill = root.Q<VisualElement>("skill1-cool-fill");
            _skill1CoolText = root.Q<Label>("skill1-cool-text");

            _skill2IconBg   = root.Q<VisualElement>("skill2-icon-bg");
            _skill2CoolFill = root.Q<VisualElement>("skill2-cool-fill");
            _skill2CoolText = root.Q<Label>("skill2-cool-text");

            _ultIconBg    = root.Q<VisualElement>("ult-icon-bg");
            _ultCoolFill  = root.Q<VisualElement>("ult-cool-fill");
            _ultGaugeFill = root.Q<VisualElement>("ult-gauge-fill");
            _ultReadyLabel = root.Q<Label>("ult-ready-label");

            // 매니저 이벤트 구독
            UpdateGold(GoldManager.Instance != null ? GoldManager.Instance.Gold : 0);
            UpdateDay(DayManager.Instance   != null ? DayManager.Instance.CurrentDay : 1);

            if (GoldManager.Instance != null)
                GoldManager.Instance.OnGoldChanged += UpdateGold;

            if (DayManager.Instance != null)
            {
                DayManager.Instance.OnDayStarted += UpdateDay;
                DayManager.Instance.OnDayEnded   += UpdateDay;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            FindAndBindPlayer();
            FindAndBindMinimap();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (GoldManager.Instance != null)
                GoldManager.Instance.OnGoldChanged -= UpdateGold;

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
        // ================================================================

        void FindAndBindPlayer()
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) { SetHpBarVisible(false); return; }

            // Health
            var health = pc.GetComponent<Health>();
            if (health != null)
            {
                _playerHealth = health;
                _playerHealth.OnHpChanged += UpdateHpBar;
                UpdateHpBar(_playerHealth.CurrentHp, _playerHealth.MaxHp);
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
                _playerStats = stats;
                _playerStats.OnWeaponChanged       += UpdateWeaponSlot;
                _playerStats.OnSkillChanged        += UpdateSkillSlot;
                _playerStats.OnCooldownChanged     += UpdateCooldown;
                _playerStats.OnUltimateGaugeChanged += OnUltimateGaugeChanged;
                _playerStats.OnUltimateReady       += ShowUltReady;

                // 초기 상태 반영
                UpdateWeaponSlot(stats.EquippedWeapon);
                UpdateSkillSlot(1, stats.SkillSlot1);
                UpdateSkillSlot(2, stats.SkillSlot2);
                UpdateSkillSlot(3, stats.UltimateSlot);
                UpdateUltGauge(stats.UltimateGauge, stats.MaxUltimateGauge);
            }
        }

        void UnbindPlayer()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHpChanged -= UpdateHpBar;
                _playerHealth = null;
            }

            if (_playerStats != null)
            {
                _playerStats.OnWeaponChanged        -= UpdateWeaponSlot;
                _playerStats.OnSkillChanged         -= UpdateSkillSlot;
                _playerStats.OnCooldownChanged      -= UpdateCooldown;
                _playerStats.OnUltimateGaugeChanged -= OnUltimateGaugeChanged;
                _playerStats.OnUltimateReady        -= ShowUltReady;
                _playerStats = null;
            }

            SetHpBarVisible(false);
            SetCooldownUI(_skill1CoolFill, _skill1CoolText, 0f, 1f);
            SetCooldownUI(_skill2CoolFill, _skill2CoolText, 0f, 1f);
        }

        // ================================================================
        //  미니맵 바인딩 (v2 구현 전 stub — 항상 숨김)
        // ================================================================

        void FindAndBindMinimap()
        {
            // MinimapManager v2 (플레이어 위치 마커) 구현 후 여기에 바인딩 로직 추가.
            if (_minimapRoot != null)
                _minimapRoot.style.display = DisplayStyle.None;
        }

        void UnbindMinimap()
        {
            if (_minimapRoot != null)
            {
                _minimapRoot.Clear();
                _minimapRoot.style.display = DisplayStyle.None;
            }
        }

        // ================================================================
        //  상단 HUD 갱신
        // ================================================================

        void UpdateGold(int gold)
        {
            if (_goldLabel != null) _goldLabel.text = $"Gold: {gold} G";
        }

        void UpdateDay(int day)
        {
            if (_dayLabel != null) _dayLabel.text = $"Day {day}";
        }

        void UpdateHpBar(int current, int max)
        {
            if (_hpFill == null || _hpText == null) return;

            float ratio = max > 0 ? (float)current / max : 0f;
            _hpFill.style.width = Length.Percent(ratio * 100f);

            Color barColor = ratio > 0.5f
                ? Color.Lerp(new Color(0.9f, 0.8f, 0f), new Color(0.2f, 0.8f, 0.2f), (ratio - 0.5f) * 2f)
                : Color.Lerp(new Color(0.86f, 0.23f, 0.23f), new Color(0.9f, 0.8f, 0f), ratio * 2f);
            _hpFill.style.backgroundColor = barColor;

            _hpText.text = $"{current}/{max}";
        }

        void SetHpBarVisible(bool visible)
        {
            if (_hpChip == null) return;
            _hpChip.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ================================================================
        //  하단 스킬 바 갱신
        // ================================================================

        /// <summary>무기 변경 시 아이콘 업데이트.</summary>
        void UpdateWeaponSlot(WeaponData weapon)
        {
            if (_weaponIconBg == null) return;

            if (weapon != null && weapon.normalAttackGroup?.skillIcon != null)
                _weaponIconBg.style.backgroundImage =
                    new StyleBackground(weapon.normalAttackGroup.skillIcon);
            else
                _weaponIconBg.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        }

        /// <summary>스킬 슬롯 변경 시 아이콘 업데이트. slot: 1·2·3(궁극기).</summary>
        void UpdateSkillSlot(int slot, SkillGroupData skill)
        {
            VisualElement iconBg = slot switch
            {
                1 => _skill1IconBg,
                2 => _skill2IconBg,
                3 => _ultIconBg,
                _ => null
            };
            if (iconBg == null) return;

            if (skill?.skillIcon != null)
                iconBg.style.backgroundImage = new StyleBackground(skill.skillIcon);
            else
                iconBg.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        }

        /// <summary>쿨타임 변경 시. slot: 1·2. remaining, total.</summary>
        void UpdateCooldown(int slot, float remaining, float total)
        {
            if (slot == 1)
                SetCooldownUI(_skill1CoolFill, _skill1CoolText, remaining, total);
            else if (slot == 2)
                SetCooldownUI(_skill2CoolFill, _skill2CoolText, remaining, total);
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
                    text.text = remaining > 1f
                        ? $"{remaining:F0}"
                        : $"{remaining:F1}";
            }
        }

        /// <summary>궁극기 게이지 변경 시 — 게이지 바 갱신 + 리셋 시 READY 숨김.</summary>
        void OnUltimateGaugeChanged(float current, float max)
        {
            UpdateUltGauge(current, max);
            // 궁극기 발동 후 게이지가 0으로 리셋되면 READY 표시 제거
            if (current <= 0f && _ultReadyLabel != null)
                _ultReadyLabel.style.display = DisplayStyle.None;
        }

        void UpdateUltGauge(float current, float max)
        {
            if (_ultGaugeFill == null) return;
            float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            _ultGaugeFill.style.width = Length.Percent(ratio * 100f);
        }

        /// <summary>궁극기 게이지 MAX 도달 시 READY 표시.</summary>
        void ShowUltReady()
        {
            if (_ultReadyLabel == null) return;
            _ultReadyLabel.text = "READY";
            _ultReadyLabel.style.display = DisplayStyle.Flex;
        }
    }
}
