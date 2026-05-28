using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// [BTNode] 어트리뷰트가 붙은 모든 BTNode 서브클래스를 리플렉션으로 수집한다.
    /// BTGraphEditor 의 "Add Node" 메뉴에서 사용한다.
    /// </summary>
    public static class BTNodeRegistry
    {
        static Dictionary<string, Type> s_ByPath;

        static void EnsureLoaded()
        {
            if (s_ByPath != null) return;
            s_ByPath = new Dictionary<string, Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type.IsAbstract || !type.IsSubclassOf(typeof(BTNode))) continue;
                    var attr = type.GetCustomAttribute<BTNodeAttribute>();
                    if (attr != null)
                        s_ByPath[attr.Path] = type;
                }
            }
        }

        /// <summary>[BTNode] 어트리뷰트가 있는 타입 — Path → Type.</summary>
        public static IReadOnlyDictionary<string, Type> AllByPath
        {
            get { EnsureLoaded(); return s_ByPath; }
        }

        /// <summary>도메인 리로드 후 캐시를 초기화한다.</summary>
        public static void Invalidate() => s_ByPath = null;
    }
}
