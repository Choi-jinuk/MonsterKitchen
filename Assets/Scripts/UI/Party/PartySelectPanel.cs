using System.Collections.Generic;
using MonsterKitchen.Core;
using MonsterKitchen.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace MonsterKitchen.UI
{
    // ====================================================================
    //  PartySelectPanel — ManagementScene 파티 편성 UI
    //
    //  로스터(리더 제외)에서 동료 최대 2명 토글 → RequestSetParty 저장.
    // ====================================================================
    [RequireComponent(typeof(UIDocument))]
    public class PartySelectPanel : MonoBehaviour
    {
        const int MaxCompanions = 2;

        readonly List<uint> m_Selected = new();
        Label      m_CountLabel;
        ScrollView m_Roster;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;   // UIDocument 가 아직 트리를 빌드하지 않음

            m_CountLabel = root.Q<Label>("count-label");
            m_Roster     = root.Q<ScrollView>("roster");

            var  pm       = PlayerDataManager.Instance;
            uint leaderId = pm?.SelectedCharId ?? 9001;

            m_Selected.Clear();
            if (pm != null) m_Selected.AddRange(pm.PartyCompanionIds);

            var leaderData = DataRegistry.Instance?.PlayerChars?.Get(leaderId);
            var leaderLabel = root.Q<Label>("leader-label");
            if (leaderLabel != null)
                leaderLabel.text = $"리더: {(leaderData != null ? leaderData.DisplayName : leaderId.ToString())} (고정)";

            BuildRoster(leaderId);
            UpdateCount();

            var saveBtn  = root.Q<Button>("save-btn");
            var closeBtn = root.Q<Button>("close-btn");
            if (saveBtn  != null) saveBtn.clicked  += OnSave;
            if (closeBtn != null) closeBtn.clicked += () => gameObject.SetActive(false);
        }

        void BuildRoster(uint leaderId)
        {
            if (m_Roster == null) return;
            m_Roster.Clear();

            var chars = DataRegistry.Instance?.PlayerChars;
            if (chars == null) return;

            foreach (var data in chars.All)
            {
                if (data.Id == leaderId) continue;
                uint id   = data.Id;
                var  card = new Button { text = $"{data.DisplayName}\n★{data.NatalStars} {data.CharClass}" };
                card.AddToClassList("card");
                if (m_Selected.Contains(id)) card.AddToClassList("selected");
                card.clicked += () => ToggleCard(id, card);
                m_Roster.Add(card);
            }
        }

        void ToggleCard(uint id, Button card)
        {
            if (m_Selected.Contains(id))
            {
                m_Selected.Remove(id);
                card.RemoveFromClassList("selected");
            }
            else
            {
                if (m_Selected.Count >= MaxCompanions) return;   // 상한 막기
                m_Selected.Add(id);
                card.AddToClassList("selected");
            }
            UpdateCount();
        }

        void UpdateCount()
        {
            if (m_CountLabel != null)
                m_CountLabel.text = $"동료 선택 ({m_Selected.Count}/{MaxCompanions})";
        }

        void OnSave()
        {
            NetworkManager.Instance?.RequestSetParty(new List<uint>(m_Selected));
            gameObject.SetActive(false);
        }
    }
}
