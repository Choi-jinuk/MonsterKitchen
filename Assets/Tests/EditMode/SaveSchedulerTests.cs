using NUnit.Framework;
using MonsterKitchen.Core;

namespace MonsterKitchen.Tests.EditMode
{
    // ====================================================================
    //  SaveSchedulerTests — SaveScheduler 순수 함수 동작 검증
    //
    //  SaveScheduler(Action saveAction) 생성자로 저장 액션을 주입한다.
    //  MonoBehaviour runner 없이 동기 경로만 테스트한다.
    // ====================================================================
    public class SaveSchedulerTests
    {
        SaveScheduler m_Sched;
        int           m_SaveCallCount;

        [SetUp]
        public void SetUp()
        {
            m_SaveCallCount = 0;
            m_Sched = new SaveScheduler(() => m_SaveCallCount++);
        }

        [TearDown]
        public void TearDown()
        {
            SaveScheduler.ResetInstanceForTest();
        }

        [Test]
        public void MarkDirty_SetsIsDirtyTrue()
        {
            m_Sched.MarkDirty();
            Assert.IsTrue(m_Sched.IsDirty);
        }

        [Test]
        public void ForceSave_CallsSaveAction()
        {
            m_Sched.ForceSave();
            Assert.AreEqual(1, m_SaveCallCount);
        }

        [Test]
        public void ForceSave_ClearsDirtyFlag()
        {
            m_Sched.MarkDirty();
            m_Sched.ForceSave();
            Assert.IsFalse(m_Sched.IsDirty);
        }

        [Test]
        public void ForceSave_InvokesOnSaveComplete()
        {
            bool completed = false;
            m_Sched.OnSaveComplete += () => completed = true;
            m_Sched.ForceSave();
            Assert.IsTrue(completed);
        }

        [Test]
        public void ForceSave_WhileInProgress_SkipsSaveAction()
        {
            SaveScheduler sched = null;
            int callCount = 0;
            sched = new SaveScheduler(() =>
            {
                callCount++;
                sched.ForceSave();
            });
            sched.ForceSave();
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void ForceSave_WithOnCompleteCallback_InvokesCallback()
        {
            bool cbCalled = false;
            m_Sched.ForceSave(onComplete: () => cbCalled = true);
            Assert.IsTrue(cbCalled);
        }
    }
}
