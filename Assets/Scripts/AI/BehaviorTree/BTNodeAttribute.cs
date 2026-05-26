using System;

namespace MonsterKitchen.AI.BehaviorTree
{
    /// <summary>
    /// BTGraphEditor 에 노드를 등록하기 위한 어트리뷰트.
    /// Path 는 슬래시로 구분된 카테고리/이름 형식. 예: "Monster/Attack"
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class BTNodeAttribute : Attribute
    {
        /// <summary>에디터 메뉴 경로. 예: "Composite/Sequence", "Monster/Attack"</summary>
        public string Path { get; }

        public BTNodeAttribute(string path) => Path = path;
    }
}
