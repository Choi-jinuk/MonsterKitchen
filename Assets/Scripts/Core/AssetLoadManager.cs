using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 전체 에셋 로딩을 단일 창구로 관리하는 싱글톤.
    /// GlobalController 가 new AssetLoadManager(manifest) 로 생성하고 Init() 를 호출한다.
    ///
    /// ┌─ MVP (현재) ────────────────────────────────────────────────────────┐
    /// │ ManifestLoader: AssetManifest SO 딕셔너리에서 동기 반환.            │
    /// └─────────────────────────────────────────────────────────────────────┘
    ///
    /// 사용법:
    ///   var data = AssetLoadManager.Instance.Load<PlayerSpawnData>(AssetKeys.DataPlayerSpawn);
    /// </summary>
    public class AssetLoadManager
    {
        public static AssetLoadManager Instance { get; private set; }

        readonly AssetManifest _manifest;
        IAssetLoader           _loader;

        public AssetLoadManager(AssetManifest manifest) => _manifest = manifest;

        public void Init()
        {
            Instance = this;
            if (_manifest != null)
            {
                _loader = new ManifestLoader(_manifest);
                Debug.Log("[AssetLoadManager] ManifestLoader 초기화.");
            }
            else
            {
                _loader = new NullLoader();
                Debug.LogError("[AssetLoadManager] AssetManifest 가 없습니다! GlobalController Inspector 를 확인하세요.");
            }
        }

        public T Load<T>(string key) where T : Object
        {
            var asset = _loader.Load<T>(key);
            if (asset == null)
                Debug.LogWarning($"[AssetLoadManager] 키 '{key}' (타입: {typeof(T).Name}) 를 찾을 수 없습니다.");
            return asset;
        }

        public IEnumerable<T> LoadAll<T>() where T : Object
            => _loader.LoadAll<T>();

        public void Release(string key) => _loader.Release(key);

        // ── ManifestLoader ────────────────────────────────────────────────

        sealed class ManifestLoader : IAssetLoader
        {
            readonly Dictionary<string, Object> _cache;

            public ManifestLoader(AssetManifest manifest)
                => _cache = manifest.BuildDictionary();

            public T Load<T>(string key) where T : Object
            {
                _cache.TryGetValue(key, out var obj);
                if (obj is T typed) return typed;
                if (obj is GameObject go) return go.GetComponent<T>();
                return null;
            }

            public IEnumerable<T> LoadAll<T>() where T : Object
            {
                foreach (var obj in _cache.Values)
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
