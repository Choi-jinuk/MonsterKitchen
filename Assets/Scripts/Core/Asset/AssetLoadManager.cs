using MonsterKitchen.Core;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 전체 에셋 로딩을 단일 창구로 관리하는 싱글톤.
    /// GlobalController 가 new AssetLoadManager(manifest) 로 생성하고 Init() 를 호출한다.
    ///
    /// ┌─ MVP (현재) ────────────────────────────────────────────────────────┐
    /// │ ManifestLoader: AssetManifest SO 딕셔너리에서 동기 반환.               │
    /// └────────────────────────────────────────────────────────────────────┘
    ///
    /// 사용법:
    ///   var data = AssetLoadManager.Instance.Load<TableData>(AssetKeys.DATA_TABLE_DATA);
    /// </summary>
    public class AssetLoadManager
    {
        public static AssetLoadManager Instance { get; private set; }

        readonly AssetManifest m_Manifest;
        IAssetLoader           m_Loader;

        public AssetLoadManager(AssetManifest manifest) => m_Manifest = manifest;

        public void Init()
        {
            Instance = this;
            if (m_Manifest != null)
            {
                m_Loader = new ManifestLoader(m_Manifest);
                DebugUtil.Log("[AssetLoadManager] ManifestLoader 초기화.");
            }
            else
            {
                m_Loader = new NullLoader();
                DebugUtil.LogError("[AssetLoadManager] AssetManifest 가 없습니다! GlobalController Inspector 를 확인하세요.");
            }
        }

        public T Load<T>(string key) where T : Object
        {
            var asset = m_Loader.Load<T>(key);
            if (asset == null)
                DebugUtil.LogWarning(StringUtil.Format("[AssetLoadManager] 키 '{0}' (타입: {1}) 를 찾을 수 없습니다.", key, typeof(T).Name));
            return asset;
        }

        public IEnumerable<T> LoadAll<T>() where T : Object
            => m_Loader.LoadAll<T>();

        public void Release(string key) => m_Loader.Release(key);

        // ── ManifestLoader ────────────────────────────────────────────────

        sealed class ManifestLoader : IAssetLoader
        {
            readonly Dictionary<string, Object> m_Cache;

            public ManifestLoader(AssetManifest manifest)
                => m_Cache = manifest.BuildDictionary();

            public T Load<T>(string key) where T : Object
            {
                m_Cache.TryGetValue(key, out var obj);
                if (obj is T typed) return typed;
                if (obj is GameObject go) return go.GetComponent<T>();
                return null;
            }

            public IEnumerable<T> LoadAll<T>() where T : Object
            {
                foreach (var obj in m_Cache.Values)
                    if (obj is T typed)
                        yield return typed;
            }

            public void Release(string key) { }
        }

        // ── NullLoader ────────────────────────────────────────────────────

        sealed class NullLoader : IAssetLoader
        {
            public T              Load<T>(string key) where T : Object => null;
            public IEnumerable<T> LoadAll<T>() where T : Object        => System.Array.Empty<T>();
            public void           Release(string key)                   { }
        }
    }
}
