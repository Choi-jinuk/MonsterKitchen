using System.Collections;
using NUnit.Framework;
using MonsterKitchen.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonsterKitchen.Tests.PlayMode
{
    // ====================================================================
    //  SceneFlowSmokeTests — 핵심 부팅 흐름 스모크 테스트
    //
    //  ▶ 검증 범위
    //    1. StartScene 부팅 → GameStartup 4단계 완료 (Abort 없음)
    //    2. ManagementScene 전환 → 플레이어 스폰
    //
    //  ▶ 에셋 미준비(아트 등) LogError 로 인한 거짓 실패를 막기 위해
    //    ignoreFailingMessages 를 사용한다 — 예외/타임아웃만 실패로 취급.
    // ====================================================================
    public class SceneFlowSmokeTests
    {
        const float StartupTimeout = 30f;

        [UnityTest]
        public IEnumerator StartScene_Boots_And_StartupCompletes()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene(CommonString.SceneStart);
            yield return null;

            Assert.IsNotNull(GlobalController.Instance,
                "StartScene 로드 후 GlobalController 가 생성되어야 한다.");

            float elapsed = 0f;
            var startup = GlobalController.Instance.Startup;
            while (!startup.IsComplete && !startup.IsAborted && elapsed < StartupTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsFalse(startup.IsAborted, "GameStartup 이 Abort 되면 안 된다 (DataLoad 실패?).");
            Assert.IsTrue(startup.IsComplete, $"GameStartup 이 {StartupTimeout}s 내에 완료되어야 한다.");
        }

        [UnityTest]
        public IEnumerator ManagementScene_Transition_SpawnsPlayer()
        {
            LogAssert.ignoreFailingMessages = true;

            // 부팅 (이전 테스트와 독립 실행 보장)
            SceneManager.LoadScene(CommonString.SceneStart);
            yield return null;

            float elapsed = 0f;
            var startup = GlobalController.Instance.Startup;
            while (!startup.IsComplete && !startup.IsAborted && elapsed < StartupTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(startup.IsComplete, "부팅이 선행되어야 한다.");

            // ManagementScene 전환
            SceneLoader.Instance.LoadScene(CommonString.SceneManagement);
            elapsed = 0f;
            while ((SceneLoader.Instance.IsLoading
                    || SceneManager.GetActiveScene().name != CommonString.SceneManagement)
                   && elapsed < StartupTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(CommonString.SceneManagement, SceneManager.GetActiveScene().name,
                "ManagementScene 으로 전환되어야 한다.");

            // SceneController.OnInit → PlayerManager.Start → 스폰까지 수 프레임 대기
            elapsed = 0f;
            while ((PlayerManager.Instance == null || PlayerManager.Instance.Player == null)
                   && elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(PlayerManager.Instance?.Player,
                "ManagementScene 첫 진입 시 플레이어가 스폰되어야 한다.");
        }

        [UnityTest]
        public IEnumerator DungeonScene_Loads_With4SpawnZones_AndBakedNavGrid()
        {
            LogAssert.ignoreFailingMessages = true;

            // 부팅 → Management (플레이어 스폰 선행)
            SceneManager.LoadScene(CommonString.SceneStart);
            yield return null;
            float elapsed = 0f;
            var startup = GlobalController.Instance.Startup;
            while (!startup.IsComplete && !startup.IsAborted && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }
            Assert.IsTrue(startup.IsComplete);

            SceneLoader.Instance.LoadScene(CommonString.SceneManagement);
            elapsed = 0f;
            while ((SceneLoader.Instance.IsLoading
                    || SceneManager.GetActiveScene().name != CommonString.SceneManagement)
                   && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }

            // Dungeon 전환
            SceneLoader.Instance.LoadScene(CommonString.SceneDungeon);
            elapsed = 0f;
            while ((SceneLoader.Instance.IsLoading
                    || SceneManager.GetActiveScene().name != CommonString.SceneDungeon)
                   && elapsed < StartupTimeout)
            { elapsed += Time.deltaTime; yield return null; }

            // 컨트롤러 Init 완료 대기
            elapsed = 0f;
            while ((MonsterKitchen.Dungeon.DungeonMapController.Instance == null
                    || MonsterKitchen.Dungeon.DungeonMapController.Instance.State
                       != SceneControllerBase.SceneState.Running)
                   && elapsed < 10f)
            { elapsed += Time.deltaTime; yield return null; }

            var ctrl = MonsterKitchen.Dungeon.DungeonMapController.Instance;
            Assert.IsNotNull(ctrl, "DungeonMapController 초기화 실패");
            Assert.AreEqual(4, ctrl.SpawnZones.Count, "스폰 존 4개가 연결되어야 한다");
            Assert.IsNotNull(MonsterKitchen.Navigation.NavGrid.Instance);
            Assert.Greater(MonsterKitchen.Navigation.NavGrid.Instance.Width, 50,
                "NavGrid 가 확장 맵(60셀 폭)을 커버해야 한다");
        }
    }
}
