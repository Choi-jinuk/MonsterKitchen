using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// MVP 로더용 에셋 매니페스트 ScriptableObject.
    ///
    /// Inspector에서 키(AssetKeys 상수)와 에셋(Object)을 1:1 매핑한다.
    /// AssetLoadManager 하나에만 연결하면 되며, 다른 컴포넌트는 Inspector
    /// 참조 없이 AssetLoadManager.Instance.Load<T>(key)로 에셋을 조회한다.
    ///
    /// ▶ Addressables 마이그레이션 시
    ///   - 이 SO는 더 이상 사용하지 않는다.
    ///   - AssetLoadManager의 로더를 AddressableLoader로 교체하면 완료.
    ///   - 키 문자열은 Addressables 주소와 동일하게 맞춰 두면 변경 최소화.
    /// </summary>
    [CreateAssetMenu(menuName = "MonsterKitchen/Asset Manifest", fileName = "AssetManifest")]
    public class AssetManifest : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("AssetKeys 클래스의 상수 값과 정확히 일치해야 합니다.")]
            public string Key;

            [Tooltip("프리팹, SO 등 모든 Unity 에셋 타입 가능.")]
            public UnityEngine.Object Asset;
        }

        [SerializeField] List<Entry> m_Entries = new();

        /// <summary>
        /// 내부 딕셔너리를 빌드해 반환한다. AssetLoadManager.Awake()에서 1회 호출.
        /// </summary>
        public Dictionary<string, UnityEngine.Object> BuildDictionary()
        {
            var dict = new Dictionary<string, UnityEngine.Object>(m_Entries.Count);
            foreach (var e in m_Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Key) || e.Asset == null)
                {
                    if (!string.IsNullOrWhiteSpace(e.Key))
                        Debug.LogWarning($"[AssetManifest] 키 '{e.Key}' 에 에셋이 없습니다.");
                    continue;
                }

                if (!dict.TryAdd(e.Key, e.Asset))
                    Debug.LogWarning($"[AssetManifest] 중복 키 감지: '{e.Key}' — 첫 번째 항목을 사용합니다.");
            }
            return dict;
        }

#if UNITY_EDITOR
        /// <summary>Editor Only: 항목 수 노출 (Inspector 헤더 표시용).</summary>
        public int EntryCount => m_Entries.Count;
#endif
    }
}
