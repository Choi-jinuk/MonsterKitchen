// DungeonDoor — D-02
using MonsterKitchen.Core;
using UnityEngine;

namespace MonsterKitchen.Dungeon
{
    /// <summary>
    /// 방과 방 사이의 문. 이전 방이 클리어되면 활성화된다.
    /// 플레이어가 트리거에 닿으면 다음 방의 스폰 포인트로 텔레포트하고
    /// CameraFollow의 경계를 다음 방 범위로 갱신한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DungeonDoor : MonoBehaviour
    {
        [Header("다음 방 정보")]
        [Tooltip("다음 방 스폰 포인트 (플레이어 텔레포트 목적지)")]
        [SerializeField] Transform destinationSpawnPoint;

        [Tooltip("다음 방의 CameraFollow 경계 (Min X,Y)")]
        [SerializeField] Vector2 nextRoomBoundsMin = new Vector2(14f, -6f);

        [Tooltip("다음 방의 CameraFollow 경계 (Max X,Y)")]
        [SerializeField] Vector2 nextRoomBoundsMax = new Vector2(33f, 5f);

        [Header("다음 방 활성화")]
        [Tooltip("문 통과 시 활성화할 DungeonRoom GameObject (씬에서 비활성으로 배치)")]
        [SerializeField] GameObject nextRoomObject;

        private void Awake()
        {
            // 기본 비활성 — DungeonRoom.OnRoomCleared 이벤트가 활성화
            gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // 1. 플레이어 텔레포트
            if (destinationSpawnPoint != null)
                other.transform.position = destinationSpawnPoint.position;

            // 2. CameraFollow 경계 갱신
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow != null)
                    follow.SetRoomBounds(nextRoomBoundsMin, nextRoomBoundsMax);
            }

            // 3. 다음 방 활성화 (DungeonRoom.Start → 몬스터 스폰)
            if (nextRoomObject != null)
                nextRoomObject.SetActive(true);

            // 4. 문 닫기 (1회성)
            gameObject.SetActive(false);
        }
    }
}
