using NUnit.Framework;
using MonsterKitchen.Core;
using MonsterKitchen.UI.Mobile;
using UnityEngine;

namespace MonsterKitchen.Tests
{
    public class MobileInputTests
    {
        [Test]
        public void InjectMove_FiresOnMoveEvent()
        {
            var mgr = new InputManager();
            mgr.Init();

            Vector2 received = Vector2.zero;
            mgr.OnMove += v => received = v;

            mgr.InjectMove(new Vector2(0.5f, 0.3f));

            Assert.AreEqual(new Vector2(0.5f, 0.3f), received);
        }

        [Test]
        public void InjectDash_FiresOnDashEvent()
        {
            var mgr = new InputManager();
            mgr.Init();

            bool fired = false;
            mgr.OnDash += () => fired = true;

            mgr.InjectDash();

            Assert.IsTrue(fired);
        }

        [Test]
        public void InjectSkill1_FiresOnSkill1Event()
        {
            var mgr = new InputManager();
            mgr.Init();

            bool fired = false;
            mgr.OnSkill1 += () => fired = true;

            mgr.InjectSkill1();

            Assert.IsTrue(fired);
        }

        [Test]
        public void InjectInteract_FiresOnInteractEvent()
        {
            var mgr = new InputManager();
            mgr.Init();

            bool fired = false;
            mgr.OnInteract += () => fired = true;

            mgr.InjectInteract();

            Assert.IsTrue(fired);
        }

        [Test]
        public void MobileButton_SetAction_UpdatesAction()
        {
            var go  = new GameObject();
            var btn = go.AddComponent<MobileButton>();

            btn.SetAction(MobileAction.Interact);

            Assert.AreEqual(MobileAction.Interact, btn.Action);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MobileHUD_SetContext_DungeonInteract_SetsSlot2ToInteract()
        {
            var hudGO = new GameObject();
            var hud   = hudGO.AddComponent<MobileHUD>();

            var slot0GO = new GameObject(); var s0 = slot0GO.AddComponent<MobileButton>();
            var slot1GO = new GameObject(); var s1 = slot1GO.AddComponent<MobileButton>();
            var slot2GO = new GameObject(); var s2 = slot2GO.AddComponent<MobileButton>();

            hud.SetSlotsForTest(new[] { s0, s1, s2 });
            hud.SetConfigsForTest(new[]
            {
                new MobileHUD.ContextConfig
                {
                    Context     = MobileContext.DungeonInteract,
                    SlotActions = new[]
                    {
                        MobileAction.Dash,
                        MobileAction.Skill1,
                        MobileAction.Interact,
                    },
                },
            });

            hud.SetContext(MobileContext.DungeonInteract);

            Assert.AreEqual(MobileAction.Interact, s2.Action);

            Object.DestroyImmediate(slot0GO);
            Object.DestroyImmediate(slot1GO);
            Object.DestroyImmediate(slot2GO);
            Object.DestroyImmediate(hudGO);
        }

        [Test]
        public void MobileHUD_SetContext_Exploration_HidesSlot2()
        {
            var hudGO = new GameObject();
            var hud   = hudGO.AddComponent<MobileHUD>();

            var slot0GO = new GameObject(); var s0 = slot0GO.AddComponent<MobileButton>();
            var slot1GO = new GameObject(); var s1 = slot1GO.AddComponent<MobileButton>();
            var slot2GO = new GameObject(); var s2 = slot2GO.AddComponent<MobileButton>();

            hud.SetSlotsForTest(new[] { s0, s1, s2 });
            hud.SetConfigsForTest(new[]
            {
                new MobileHUD.ContextConfig
                {
                    Context     = MobileContext.Exploration,
                    SlotActions = new[]
                    {
                        MobileAction.Dash,
                        MobileAction.Interact,
                    },
                },
            });

            hud.SetContext(MobileContext.Exploration);

            Assert.IsTrue(slot0GO.activeSelf);
            Assert.IsTrue(slot1GO.activeSelf);
            Assert.IsFalse(slot2GO.activeSelf);

            Object.DestroyImmediate(slot0GO);
            Object.DestroyImmediate(slot1GO);
            Object.DestroyImmediate(slot2GO);
            Object.DestroyImmediate(hudGO);
        }
    }
}
