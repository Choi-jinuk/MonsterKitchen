using MonsterKitchen.Data;
using UnityEngine;

namespace MonsterKitchen.Player
{
    // ====================================================================
    //  WeaponSocketController — 무기 소켓 시각 담당
    //
    //  ▶ 역할
    //    - 무기 교체 시 SpriteRenderer.sprite + AnimatorController 런타임 교체
    //    - _facingDir 기반 360도 방향 회전 (world space Z 축)
    //    - 왼쪽 반구 진입 시 flipY 처리 (스프라이트 뒤집힘 방지)
    //    - 활 등 애니메이션 있는 무기의 트리거 발동 (TriggerWeaponAnim)
    //
    //  ▶ 배치
    //    Player.prefab 의 자식 GameObject "WeaponSocket" 에 추가.
    //    2D Animation 리깅 완료 후 이 GO를 손 본(Hand_R)에 연결한다.
    //    PlayerController 에서 [SerializeField] WeaponSocketController _weaponSocket 로 참조.
    //
    //  ▶ 스킨 교체 연동
    //    캐릭터 본체 스킨은 SpriteLibraryAsset 교체로 처리한다.
    //    무기 스킨은 WeaponData.weaponSprite 를 바꾸거나 SetWeapon() 을 재호출한다.
    //
    //  ▶ 무기 애니메이션 (활 시위 등)
    //    WeaponData.weaponAnimController 가 있으면 Animator 활성화.
    //    PlayerController 공격 트리거 발동 시 TriggerWeaponAnim(triggerHash) 을 호출해
    //    무기 전용 애니메이션 클립을 재생한다.
    //    무기 AnimatorController 가 없으면 Animator 비활성 상태 유지.
    // ====================================================================

    public class WeaponSocketController : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Animator       _anim;

        /// <summary>PlayerController 의 _sprites flipX 루프 제외 판별에 사용.</summary>
        public SpriteRenderer SR => _sr;

        void Awake()
        {
            if (_sr   == null) _sr   = GetComponent<SpriteRenderer>();
            if (_anim == null) _anim = GetComponent<Animator>();

            // 초기 상태: 무기 없음
            if (_anim != null) _anim.enabled = false;
        }

        // ================================================================
        //  무기 교체
        // ================================================================

        /// <summary>
        /// 무기를 장착한다. null 을 넣으면 스프라이트·애니메이터 초기화(해제).
        /// PlayerController.Init() 및 PlayerStats.OnWeaponChanged 에서 호출된다.
        /// </summary>
        public void SetWeapon(WeaponData weapon)
        {
            if (_sr != null)
                _sr.sprite = weapon?.weaponSprite;

            if (_anim != null)
            {
                bool hasAnim = weapon?.weaponAnimController != null;
                _anim.runtimeAnimatorController = hasAnim ? weapon.weaponAnimController : null;
                _anim.enabled = hasAnim;
            }
        }

        // ================================================================
        //  방향 회전
        // ================================================================

        /// <summary>
        /// 페이싱 방향 갱신. FixedUpdate 및 공격 직전에 호출.
        /// World space Z 회전으로 360도 자유 방향 지원.
        /// 왼쪽 반구(dir.x &lt; 0) 에서 flipY = true 로 스프라이트 뒤집힘 방지.
        /// </summary>
        public void SetFacingDirection(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.01f) return;

            // World space 기준 회전 — 부모 flipX 영향 없음
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // 왼쪽 반구에서 무기 스프라이트 수직 반전 (뒤집힌 무기 방지)
            if (_sr != null)
                _sr.flipY = dir.x < 0f;
        }

        // ================================================================
        //  무기 애니메이션 트리거
        // ================================================================

        /// <summary>
        /// 무기 Animator 트리거를 발동한다.
        /// 활의 Draw/Release, 마법봉의 Cast 등 무기별 모션에 사용.
        /// weaponAnimController 가 없거나 비활성이면 무시.
        /// </summary>
        public void TriggerWeaponAnim(int triggerHash)
        {
            if (_anim == null || !_anim.enabled) return;
            _anim.SetTrigger(triggerHash);
        }

        /// <summary>string 오버로드. 빈 문자열은 무시.</summary>
        public void TriggerWeaponAnim(string triggerName)
        {
            if (_anim == null || !_anim.enabled || string.IsNullOrEmpty(triggerName)) return;
            _anim.SetTrigger(triggerName);
        }

        // ================================================================
        //  가시성
        // ================================================================

        /// <summary>소켓 스프라이트 표시 여부 (사망·씬 전환 시 숨김 등).</summary>
        public void SetVisible(bool visible)
        {
            if (_sr != null) _sr.enabled = visible;
        }

#if UNITY_EDITOR
        void Reset()
        {
            _sr   = GetComponent<SpriteRenderer>();
            _anim = GetComponent<Animator>();
        }
#endif
    }
}
