using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.Core
{
    /// <summary>
    /// 에셋 로더 추상화 인터페이스.
    ///
    /// MVP  : ManifestLoader  — AssetManifest SO 딕셔너리에서 동기 반환.
    /// Phase2: AddressableLoader — Addressables.LoadAssetAsync 비동기 로드.
    ///
    /// 호출부는 항상 이 인터페이스(또는 AssetLoadManager)를 통해서만 에셋에 접근.
    /// 백엔드를 교체해도 호출부 변경이 없다.
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// key에 해당하는 에셋을 동기 반환.
        /// ManifestLoader는 항상 즉시 반환. AddressableLoader에서는 캐시 히트 시 즉시 반환.
        /// 없으면 null.
        /// </summary>
        T Load<T>(string key) where T : Object;

        /// <summary>
        /// 타입 T에 해당하는 모든 에셋을 열거.
        /// DataRegistry의 MVP 일괄 로드에 사용.
        /// </summary>
        IEnumerable<T> LoadAll<T>() where T : Object;

        /// <summary>
        /// Addressables 마이그레이션 시 핸들 해제.
        /// ManifestLoader에서는 no-op (GC가 처리).
        /// </summary>
        void Release(string key);
    }
}
