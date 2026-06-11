# 던전 리워크 + 가방 시스템 + 미니맵 설계 문서

> 최종 갱신: 2026-06-03

---

## Goal

선형 다방(Room1→Room2) 구조를 단일 오픈맵으로 교체한다.  
몬스터는 처치 후 자동 리스폰되고, 플레이어는 무게 기반 던전 가방이 차면 귀환한다.  
미니맵은 단일 맵 위 플레이어 위치 + 스폰 존 마커로 표시한다.

---

## Architecture

**접근법 B — 신규 클래스로 완전 교체.**  
DungeonSceneController·DungeonRoom·DungeonDoor·MonsterRespawnManager를 제거하고  
DungeonMapController·DungeonSpawnZone으로 재작성한다.

---

## 섹션 1: 던전 구조

### 신규 클래스

| 클래스 | 파일 | 역할 |
|---|---|---|
| `DungeonMapController` | `Assets/Scripts/Dungeon/DungeonMapController.cs` | 씬 총괄. 스폰 존 초기화, 가방 바인딩, 출구 활성화. `SceneControllerBase` 상속 유지. |
| `DungeonSpawnZone` | `Assets/Scripts/Dungeon/DungeonSpawnZone.cs` | 맵에 여러 개 배치. spawnTableId + spawnPoints + respawnDelay. 처치 감지 → 타이머 → 재스폰 자체 관리. |

### 제거 클래스

| 파일 | 이유 |
|---|---|
| `DungeonSceneController.cs` | `DungeonMapController`로 교체 |
| `DungeonRoom.cs` | `DungeonSpawnZone`으로 교체 |
| `DungeonDoor.cs` | 방 이동 개념 제거 |
| `MonsterRespawnManager.cs` | `DungeonSpawnZone` 내부로 통합 |

### DungeonMapController 책임

```
Start()
  → 각 DungeonSpawnZone.Init(player) 호출
  → DungeonExit 활성화 (항상)
  → DungeonBag 초기화
  → NavGrid.Bake()
  → CameraConfiner.Refresh()
```

### DungeonSpawnZone 책임

```
Init(player)
  → SpawnTable 로드
  → 초기 스폰 (프레임 분산)
  → 각 몬스터 Health.OnDeath 구독

OnMonsterDied(position)
  → respawnDelay 후 동일 위치에 동일 테이블에서 재스폰
```

**respawnDelay**: `[SerializeField] float m_RespawnDelay = 8f` (Inspector 조정 가능)

### DungeonExit 변경

- 씬 시작부터 활성
- E키 상호작용 시:
  - 가방 미달(여유 있음) → 확인 팝업 "아직 공간이 있습니다. 귀환하시겠습니까?" [귀환 / 취소]
  - 가방 꽉 참 → 즉시 귀환
- 귀환: `DungeonBag.FlushToInventory()` → `SceneLoader.LoadScene("ManagementScene")`

---

## 섹션 2: DungeonBag 시스템

### 데이터

| 항목 | 위치 | 내용 |
|---|---|---|
| `IngredientData.Weight` | `Assets/Scripts/Data/Table/IngredientTable.cs` | `int Weight = 1` 신규 필드 |
| `Ingredients.csv` | `Assets/Data/CSV/Ingredients.csv` | `Weight` 컬럼 추가 (기본값 1) |
| `DungeonBag` | `Assets/Scripts/Dungeon/DungeonBag.cs` | 순수 C# 클래스. 던전 세션 전용 임시 컨테이너. |
| `PlayerUpgradeType.BagCapacity` | `GameEnums.cs` | 가방 용량 업그레이드 타입 추가 |
| `PlayerUpgradeData.BagCapacityLevel` | `PlayerUpgradeData.cs` | Lv1=10, Lv2=15, Lv3=20, Lv4=25, Lv5=30 |

### DungeonBag API

```csharp
public class DungeonBag
{
    public int CurrentWeight { get; }
    public int MaxWeight     { get; }   // PlayerUpgradeData.BagCapacityLevel에서 도출
    public bool IsFull       => CurrentWeight >= MaxWeight;

    // 재료 추가 시도. 무게 초과 시 false 반환.
    public bool TryAdd(uint ingredientId, int qty, IngredientQuality quality, int weight);

    // 특정 재료 제거 (버리기).
    public void Remove(uint ingredientId, int qty);

    // 귀환 시 NetworkManager.RequestAddIngredient(quality overload)로 일괄 전송 후 클리어.
    public void FlushToInventory();

    // 현재 내용물 열람 (UI용).
    public IReadOnlyDictionary<uint, (int qty, IngredientQuality quality)> Contents { get; }

    // 이벤트
    public event Action OnBagChanged;   // 무게/내용물 변경 시
}
```

### ItemDrop 변경

- `OnTriggerEnter2D` → `DungeonBag.TryAdd()` 시도
  - 성공: Destroy(gameObject)
  - 실패(무게 초과): 아이템 그대로 유지 + GameHUD에 "가방 가득" 알림 (2초)

### 버리기 UI

- `DungeonBagUI` — UIPanel(isPopup=true). B키로 토글 (던전 씬 한정).
- 내용: 가방 내 재료 목록 (아이콘 + 수량 + 무게) + 버리기 버튼
- 버리기 → `DungeonBag.Remove()` → 완전 소실

### 가방 HUD

- `minimap-root` 옆에 `bag-weight-bar` 추가 (HUD.uxml)
- `현재무게 / 최대무게` 텍스트 + 채움 바
- 꽉 찼을 때 빨간색 강조

---

## 섹션 3: 미니맵

### 렌더링 방식

UIToolkit `VisualElement` 기반. RenderTexture 없음.

### 구성 요소

| 요소 | 구현 |
|---|---|
| 맵 배경 | `DungeonMapController`의 `[SerializeField] Sprite m_MinimapSprite` — 던전별 탑뷰 이미지 |
| 월드 바운드 | `[SerializeField] Rect m_WorldBounds` — 맵 월드 영역 (Inspector 설정) |
| 플레이어 도트 | 흰색 원형 VisualElement. `Update()`마다 월드→UV 변환 후 위치 갱신 |
| 스폰 존 마커 | 각 DungeonSpawnZone 위치 → 작은 삼각형. 몬스터 생존 시 빨간색, 전부 사망 시 회색 (리스폰 대기 중) |
| HUD 연동 | `GameHUD.FindAndBindMinimap()` — 던전 씬 로드 시 `DungeonMapController.Instance` 탐색 → 미니맵 활성화 |

### 좌표 변환

```
uvX = (worldPos.x - bounds.xMin) / bounds.width
uvY = (worldPos.y - bounds.yMin) / bounds.height
minimapPixelX = uvX * minimapWidth
minimapPixelY = (1 - uvY) * minimapHeight   // Y축 반전
```

### 비던전 씬

`minimap-root` display=None 유지 (기존 동작 그대로).

---

## 파일 변경 요약

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Dungeon/DungeonMapController.cs` | **신규** |
| `Assets/Scripts/Dungeon/DungeonSpawnZone.cs` | **신규** |
| `Assets/Scripts/Dungeon/DungeonBag.cs` | **신규** |
| `Assets/Scripts/UI/HUD/DungeonBagUI.cs` | **신규** |
| `Assets/Scripts/Dungeon/DungeonSceneController.cs` | **삭제** |
| `Assets/Scripts/Dungeon/DungeonRoom.cs` | **삭제** |
| `Assets/Scripts/Dungeon/DungeonDoor.cs` | **삭제** |
| `Assets/Scripts/Enemy/MonsterRespawnManager.cs` | **삭제** |
| `Assets/Scripts/Data/Table/IngredientTable.cs` | `Weight` 필드 추가 |
| `Assets/Data/CSV/Ingredients.csv` | `Weight` 컬럼 추가 |
| `Assets/Scripts/Data/Table/GameEnums.cs` | `PlayerUpgradeType.BagCapacity` 추가 |
| `Assets/Scripts/Data/Player/PlayerUpgradeData.cs` | `BagCapacityLevel` + 업그레이드 비용 추가 |
| `Assets/Scripts/Combat/ItemDrop.cs` | DungeonBag 경유 픽업으로 변경 |
| `Assets/Scripts/Dungeon/DungeonExit.cs` | 항상 활성 + 확인 팝업 추가 |
| `Assets/Scripts/UI/HUD/GameHUD.cs` | `FindAndBindMinimap` 실구현 + bag-weight-bar 바인딩 |
| `Assets/UI/HUD.uxml` | `minimap-root` + `bag-weight-bar` 요소 추가 |
| `Assets/UI/HUD.uss` | 미니맵 + 가방 바 CSS 추가 |
| `Assets/Scenes/DungeonScene.unity` | 씬 재배선 (구 컨트롤러 제거, 신규 배치) |

---

## 테스트 기준 (모듈 5-5 완료 조건)

1. DungeonScene 진입 → 몬스터 스폰 확인
2. 몬스터 처치 → 8초 후 재스폰 확인
3. 재료 픽업 → 가방 무게 증가 확인
4. 가방 꽉 참 → 픽업 거부 + 알림 확인
5. B키 → 가방 UI 열기 → 재료 버리기 → 무게 감소 확인
6. DungeonExit 가방 미달 상태 E키 → 확인 팝업 확인
7. 귀환 → PlayerInventory 재료 전송 확인
8. 미니맵 플레이어 도트 이동 확인
9. 스폰 존 마커 색상 변화 확인
