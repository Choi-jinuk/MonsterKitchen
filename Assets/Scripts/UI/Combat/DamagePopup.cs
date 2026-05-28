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
        [SerializeField] float  riseSpeed    = 1.5f;
        [SerializeField] float  lifetime     = 0.8f;
        [SerializeField] Color  normalColor  = Color.white;
        [SerializeField] Color  critColor    = new Color(1f, 0.4f, 0f); // 주황
        [SerializeField] float  normalScale  = 0.4f;
        [SerializeField] float  critScale    = 0.55f;

        TMP_Text _text;
        DamagePopupManager _manager;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        /// <summary>팝업을 초기화하고 애니메이션을 시작한다.</summary>
        public void Show(int amount, bool isCrit, DamagePopupManager manager)
        {
            _manager = manager;
            _text.text  = amount.ToString();
            _text.color = isCrit ? critColor : normalColor;

            float s = isCrit ? critScale : normalScale;
            transform.localScale = Vector3.one * s;

            StopAllCoroutines();
            StartCoroutine(AnimateRoutine());
        }

        IEnumerator AnimateRoutine()
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Color startColor = _text.color;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                // 위로 이동
                transform.position = startPos + Vector3.up * (riseSpeed * elapsed);

                // 후반 50% 구간에서 페이드 아웃
                float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
                _text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                yield return null;
            }

            _manager?.ReturnToPool(this);
        }
    }
}
