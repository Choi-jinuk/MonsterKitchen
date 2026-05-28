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
        public string      Key         = "";
        public BBValueType ValueType   = BBValueType.Float;
        public float       FloatValue;
        public int         IntValue;
        public bool        BoolValue;
        public string      StringValue = "";
        public Vector2     Vector2Value;
        public Vector3     Vector3Value;
    }

    // ====================================================================
    //  BTAsset — 행동 트리 구조를 저장하는 ScriptableObject
    //
    //  ▶ 노드들은 이 에셋의 sub-asset 으로 저장된다.
    //  ▶ BlackboardDefaults 는 BTRunner.Start() 에서 IBTBlackboardInitializer
    //    보다 먼저 적용된다.
    // ====================================================================

    [CreateAssetMenu(menuName = "MonsterKitchen/BehaviorTree/BT Asset", fileName = "NewBTAsset")]
    public class BTAsset : ScriptableObject
    {
        [SerializeField] public BTNode      Root;

        [Tooltip("BTRunner 시작 시 블랙보드에 미리 채울 기본값 목록.\n" +
                 "IBTBlackboardInitializer 보다 먼저 적용되므로 코드에서 덮어쓸 수 있다.")]
        [SerializeField] public List<BBEntry> BlackboardDefaults = new();

#if UNITY_EDITOR
        // ================================================================
        //  에디터 전용 — sub-asset 관리
        // ================================================================

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

        public void RemoveNode(BTNode node)
        {
            if (node == null) return;
            if (Root == node) Root = null;

            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            foreach (var sub in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            {
                // BTComposite (Sequence, Selector) — 다자식
                if (sub is BTComposite composite)
                    composite.RemoveChild(node);
                // BTDecorator (Condition, Service, Inverter 등) — 단일 자식
                else if (sub is BTDecorator decorator && decorator.Child == node)
                    decorator.ClearChild();
            }

            UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
            DestroyImmediate(node, true);
            UnityEditor.EditorUtility.SetDirty(this);
        }

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
