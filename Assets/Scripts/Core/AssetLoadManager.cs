using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 전체 에셋 로딩을 단일 창구로 관리하는 싱글톤.
    ///
    /// ┌─ MVP (현재) ────────────────────────────────────────────────────────┐
    /// │ ManifestLoader: AssetManifest SO 딕셔너리에서 동기 반환.            │
    /// │ Inspector 참조: AssetManifest 하나만 이 컴포넌트에 연결.            │
    /// └─────────────────────────────────────────────────────────────────────┘
    ///
    /// ┌─ Phase 2 (Addressables 마이그레이션) ──────────────────────────────┐
    /// │ 1. AssetLoadManager.loaderMode = LoaderMode.Addressable 로 전환.   │
    /// │ 2. Addressables 패키지에서 동일 키(AssetKeys.XXX)로 주소 등록.      │
    /// │ 3. 호출부(Load<T>(key)) 는 변경 없음.                              │
    /// └─────────────────────────────────────────────────────────────────────┘
    ///
    /// 사용법:
    ///   var prefab = AssetLoadManager.Instance.Load<GameObject>(AssetKeys.PrefabHud);
    ///   var data   = AssetLoadManager.Instance.Load<PlayerSpawnData>(AssetKeys.DataPlayerSpawn);
    /// </summary>
    [DisallowMultipleComponent]
    public class AssetLoadManager : MonoBehaviour
    {
        // ── 로더 모드 ────────────────────────────────────────────────
        public enum LoaderMode { Manifest, Addressable }

        // ── 싱글톤 ───────────────────────────────────────────────────
        public static AssetLoadManager Instance { get; private set; }

        // ── Inspector ────────────────────────────────────────────────
        [Header("로더 모드 선택")]
        [SerializeField] LoaderMode loaderMode = LoaderMode.Manifest;

        [Header("MVP — Manifest 모드 전용")]
        [Tooltip("AssetManifest SO. Manifest 모드일 때만 사용. Addressable 모드 시 비워도 됨.")]
        [SerializeField] AssetManifest manifest;

        IAssetLoader _loader;

        // ── Unity ────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _loader = CreateLoader();
        }

        // ── Public API ───────────────────────────────────────────────

        /// <summary>
        /// key에 해당하는 에셋을 로드한다. 없으면 null + 경고.
        /// </summary>
        public T Load<T>(string key) where T : Object
        {
            var asset = _loader.Load<T>(key);
            if (asset == null)
                Debug.LogWarning($"[AssetLoadManager] 키 '{key}' (타입: {typeof(T).Name}) 를 찾을 수 없습니다.");
            return asset;
        }

        /// <summary>
        /// 타입 T에 해당하는 모든 에셋을 열거. DataRegistry MVP 로드용.
        /// </summary>
        public IEnumerable<T> LoadAll<T>() where T : Object
            => _loader.LoadAll<T>();

        /// <summary>
        /// Addressables 마이그레이션 후 핸들 수동 해제. MVP에서는 no-op.
        /// </summary>
        public void Release(string key) => _loader.Release(key);

        // ── 내부 ─────────────────────────────────────────────────────

        IAssetLoader CreateLoader()
        {
            switch (loaderMode)
            {
                case LoaderMode.Manifest:
                    if (manifest == null)
                    {
                        Debug.LogError("[AssetLoadManager] Manifest 모드인데 AssetManifest가 없습니다!");
                        return new NullLoader();
                    }
                    Debug.Log("[AssetLoadManager] ManifestLoader 초기화.");
                    return new ManifestLoader(manifest);

                case LoaderMode.Addressable:
                    // Phase 2 구현 자리. 현재는 NullLoader로 폴백.
                    Debug.LogWarning("[AssetLoadManager] AddressableLoader는 Phase 2에서 구현됩니다. NullLoader 사용 중.");
                    return new NullLoader();

                default:
                    return new NullLoader();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ManifestLoader — MVP 구현
        // ════════════════════════════════════════════════════════════

        sealed class ManifestLoader : IAssetLoader
        {
            readonly Dictionary<string, Object> _cache;

            public ManifestLoader(AssetManifest manifest)
                => _cache = manifest.BuildDictionary();

            public T Load<T>(string key) where T : Object
            {
                _cache.TryGetValue(key, out var obj);
                if (obj is T typed) return typed;

                // Object 필드에 프리팹을 드래그하면 root GameObject가 저장된다.
                // T가 Component라면 GetComponent로 자동 추출한다.
                if (obj is GameObject go)
                    return go.GetComponent<T>();

                return null;
            }

            public IEnumerable<T> LoadAll<T>() where T : Object
            {
                foreach (var obj in _cache.Values)
                    if (obj is T typed)
                        yield return typed;
            }

            public void Release(string key) { /* GC가 처리 */ }
        }

        // ════════════════════════════════════════════════════════════
        //  NullLoader — 안전 폴백 (매니페스트 미연결 등)
        // ════════════════════════════════════════════════════════════

        sealed class NullLoader : IAssetLoader
        {
            public T                 Load<T>(string key) where T : Object => null;
            public IEnumerable<T>    LoadAll<T>() where T : Object         => System.Array.Empty<T>();
            public void              Release(string key)                    { }
        }
    }
}
