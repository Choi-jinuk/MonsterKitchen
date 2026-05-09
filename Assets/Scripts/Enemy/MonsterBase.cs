using MonsterKitchen.Combat;
using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Enemy
{
    // ====================================================================
    //  MonsterBase — 모든 몬스터의 공통 기반
    //
    //  ▶ Awake() 없음
    //    - GetComponent 는 Init() 에서만 한다.
    //    - 프리팹을 비활성 상태로 Instantiate → Init() → SetActive(true) 순서로
    //      SpawnManager 가 호출하므로, Awake 가 먼저 실행되는 일이 없다.
    //
    //  ▶ OnEnable / OnDisable 만 남김
    //    - HP 사망 이벤트 구독/해제를 SetActive 와 연동한다.
    //    - 화면 밖 몬스터를 SetActive(false) 로 비활성화할 때 자동 정리된다.
    // ====================================================================

    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(DropResolver))]
    public class MonsterBase : MonoBehaviour
    {
        [SerializeField] protected MonsterData monsterData;

        public MonsterData Data => monsterData;

        protected Health       HP;
        protected DropResolver Dropper;
        protected bool         _initialized;

        // ----------------------------------------------------------------
        //  Init — SpawnManager 가 SetActive(true) 전에 호출
        // ----------------------------------------------------------------

        /// <summary>
        /// GetComponent + 체력 초기화.
        /// SpawnManager.SpawnRoom() → Init() → SetActive(true) 순으로 호출된다.
        /// </summary>
        public virtual void Init(MonsterData data, Transform player, Vector3 spawnPos)
        {
            HP      = GetComponent<Health>();
            Dropper = GetComponent<DropResolver>();

            monsterData = data;
            if (data != null)
                HP.SetMaxHp(data.hp);

            _initialized = true;
        }

        // ----------------------------------------------------------------
        //  OnEnable / OnDisable — 이벤트 구독 수명 관리
        // ----------------------------------------------------------------

        protected virtual void OnEnable()
        {
            // 안전망: Init 전에 OnEnable 이 실행된 경우(씬 직접 배치 등)에도 동작
            HP ??= GetComponent<Health>();
            if (HP != null) HP.OnDeath += OnDied;
        }

        protected virtual void OnDisable()
        {
            if (HP != null) HP.OnDeath -= OnDied;
        }

        // ----------------------------------------------------------------
        //  Override point
        // ----------------------------------------------------------------

        protected virtual void OnDied(AttributeType killAttr) { }
    }
}
