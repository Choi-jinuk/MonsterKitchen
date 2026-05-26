using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen.AI.BehaviorTree
{
    // ====================================================================
    //  블랙보드 기본값 항목
    // ====================================================================

    public enum BBValueType { Float, Int, Bool, String, Vector2, Vector3 }

    [Serializable]
    public class BBEntry
    {
        public string      key         = "";
        public BBValueType valueType   = BBValueType.Float;
        public float       floatValue;
        public int         intValue;
        public bool        boolValue;
        public string      stringValue = "";
        public Vector2     vector2Value;
        public Vector3     vector3Value;
    }

    // ====================================================================
    //  BTAsset — 행동 트리 구조를 저장하는 ScriptableObject
    //
    //  ▶ 노드들은 이 에셋의 sub-asset 으로 저장된다.
    //  ▶ blackboardDefaults 는 BTRunner.Start() 에서 IBTBlackboardInitializer
    //    보다 먼저 적용된다.
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/BehaviorTree/BT Asset", fileName = "NewBTAsset")]
    public class BTAsset : ScriptableObject
    {
        [SerializeField] public BTNode      root;

        [Tooltip("BTRunner 시작 시 블랙보드에 미리 채울 기본값 목록.\n" +
                 "IBTBlackboardInitializer 보다 먼저 적용되므로 코드에서 덮어쓸 수 있다.")]
        [SerializeField] public List<BBEntry> blackboardDefaults = new();

#if UNITY_EDITOR
        // ================================================================
        //  에디터 전용 — sub-asset 관리
        // ================================================================

        /// <summary>새 노드 SO 를 sub-asset 으로 추가하고 반환한다.</summary>
        public T AddNode<T>() where T : BTNode
        {
            var node = CreateInstance<T>();
            node.name = typeof(T).Name;
            UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
            UnityEditor.EditorUtility.SetDirty(this);
            return node;
        }

        public BTNode AddNode(Type type)
        {
            var node = (BTNode)CreateInstance(type);
            node.name = type.Name;
            UnityEditor.AssetDatabase.AddObjectToAsset(node, this);
            UnityEditor.EditorUtility.SetDirty(this);
            return node;
        }

        /// <summary>노드 SO 를 sub-asset 에서 제거하고 파괴한다.</summary>
        public void RemoveNode(BTNode node)
        {
            if (node == null) return;

            if (root == node) root = null;

            // 모든 Composite 의 자식 목록에서 제거
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            foreach (var sub in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is BTComposite composite)
                    composite.RemoveChild(node);
            }

            UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
            DestroyImmediate(node, true);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>이 BTAsset 에 속한 모든 BTNode sub-asset 목록을 반환한다.</summary>
        public List<BTNode> GetAllNodes()
        {
            var result = new List<BTNode>();
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is BTNode n) result.Add(n);
            return result;
        }
#endif
    }
}
