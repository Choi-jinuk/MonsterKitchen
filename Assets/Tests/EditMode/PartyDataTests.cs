using System.Collections.Generic;
using NUnit.Framework;
using MonsterKitchen.Data;

namespace MonsterKitchen.Tests
{
    // ====================================================================
    //  PartyDataTests — 파티 저장/검증/상태결정
    // ====================================================================
    public class PartyDataTests
    {
        // ── ServerSaveData ───────────────────────────────────────────────

        [Test]
        public void ServerSaveData_PartyCompanionIds_DefaultsEmptyNonNull()
        {
            var s = new ServerSaveData();
            Assert.IsNotNull(s.PartyCompanionIds);
            Assert.AreEqual(0, s.PartyCompanionIds.Count);
        }

        [Test]
        public void ServerSaveData_PartyCompanionIds_HoldsValues()
        {
            var s = new ServerSaveData();
            s.PartyCompanionIds.Add(9002);
            s.PartyCompanionIds.Add(9003);
            Assert.AreEqual(new List<uint> { 9002, 9003 }, s.PartyCompanionIds);
        }

        // ── NetworkManager.SanitizeParty ─────────────────────────────────

        [Test]
        public void SanitizeParty_CapsAtTwo_ExcludesLeader_Distinct()
        {
            var result = MonsterKitchen.Core.NetworkManager.SanitizeParty(
                leaderId: 9001,
                requested: new uint[] { 9001, 9002, 9002, 9003, 9004 },
                isValidId: id => id >= 9002 && id <= 9005);

            Assert.AreEqual(2, result.Count, "최대 2명");
            Assert.IsFalse(result.Contains(9001u), "리더 제외");
            Assert.AreEqual(new List<uint> { 9002, 9003 }, result, "중복 제거 + 순서 유지");
        }

        [Test]
        public void SanitizeParty_DropsInvalidIds()
        {
            var result = MonsterKitchen.Core.NetworkManager.SanitizeParty(
                leaderId: 9001,
                requested: new uint[] { 9999, 9002 },
                isValidId: id => id == 9002);
            Assert.AreEqual(new List<uint> { 9002 }, result);
        }

        // ── CompanionStateDecision ───────────────────────────────────────

        [Test]
        public void CompanionState_EnemyWithinDetect_Engage()
        {
            var st = MonsterKitchen.AI.Companion.CompanionStateDecision.Decide(
                hasEnemy: true, enemyDist: 4f, detectRange: 6f);
            Assert.AreEqual(MonsterKitchen.AI.Companion.CompanionState.Engage, st);
        }

        [Test]
        public void CompanionState_NoEnemy_Follow()
        {
            var st = MonsterKitchen.AI.Companion.CompanionStateDecision.Decide(
                hasEnemy: false, enemyDist: 0f, detectRange: 6f);
            Assert.AreEqual(MonsterKitchen.AI.Companion.CompanionState.Follow, st);
        }

        [Test]
        public void CompanionState_EnemyBeyondDetect_Follow()
        {
            var st = MonsterKitchen.AI.Companion.CompanionStateDecision.Decide(
                hasEnemy: true, enemyDist: 9f, detectRange: 6f);
            Assert.AreEqual(MonsterKitchen.AI.Companion.CompanionState.Follow, st);
        }
    }
}
