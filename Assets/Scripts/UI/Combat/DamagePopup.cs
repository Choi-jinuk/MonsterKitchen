using System.Collections;
using MonsterKitchen.Core;
using TMPro;
using UnityEngine;

namespace MonsterKitchen.UI
{
    /// <summary>
    /// 피격 위치에 생성되어 위로 떠오르며 페이드 아웃하는 데미지 숫자 팝업.
    /// DamagePopupManager 의 ObjectPool 에서 Get/Return 된다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] float m_RiseSpeed   = 1.5f;
        [SerializeField] float m_Lifetime    = 0.8f;
        [SerializeField] Color m_NormalColor = Color.white;
        [SerializeField] Color m_CritColor   = new Color(1f, 0.4f, 0f); // 주황
        [SerializeField] float m_NormalScale = 0.4f;
        [SerializeField] float m_CritScale   = 0.55f;

        TMP_Text            m_Text;
        DamagePopupManager  m_Manager;

        void Awake()
        {
            m_Text = GetComponent<TMP_Text>();
        }

        /// <summary>팝업을 초기화하고 애니메이션을 시작한다.</summary>
        public void Show(int amount, bool isCrit, DamagePopupManager manager)
        {
            m_Manager       = manager;
            m_Text.text     = amount.ToString();
            m_Text.color    = isCrit ? m_CritColor : m_NormalColor;

            float s = isCrit ? m_CritScale : m_NormalScale;
            transform.localScale = Vector3.one * s;

            StopAllCoroutines();
            StartCoroutine(AnimateRoutine());
        }

        IEnumerator AnimateRoutine()
        {
            float   elapsed   = 0f;
            Vector3 startPos  = transform.position;
            Color   startColor = m_Text.color;

            while (elapsed < m_Lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / m_Lifetime;

                // 위로 이동
                transform.position = startPos + Vector3.up * (m_RiseSpeed * elapsed);

                // 후반 50% 구간에서 페이드 아웃
                float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
                m_Text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                yield return null;
            }

            m_Manager?.ReturnToPool(this);
        }
    }
}
