using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MonsterKitchen.AI;

namespace MonsterKitchen.Tests.PlayMode
{
    // ====================================================================
    //  FlockEncircleSmokeTests — 다수 에이전트 겹침 해소 스모크 (씬 비의존)
    // ====================================================================
    public class FlockEncircleSmokeTests
    {
        sealed class DummyAgent : MonoBehaviour, IFlockAgent
        {
            public Vector2   FlockPosition  => transform.position;
            public Transform FlockTransform => transform;
        }

        [UnityTest]
        public IEnumerator Agents_SeparateOverTime()
        {
            var mgr = FlockManager.GetOrCreate();

            var agents = new List<DummyAgent>();
            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject($"dummy{i}");
                go.transform.position = new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
                var a = go.AddComponent<DummyAgent>();
                mgr.Register(a);
                agents.Add(a);
            }

            var   buffer = new Vector2[16];
            float t      = 0f;
            while (t < 2f)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                foreach (var a in agents)
                {
                    int count   = mgr.QueryNeighbors(a.FlockPosition, 0.9f, a.FlockTransform, buffer);
                    Vector2 sep = FlockSteering.Separation(a.FlockPosition, buffer, count, 0.9f, 2f);
                    a.transform.position += (Vector3)(sep * Time.fixedDeltaTime);
                }
            }

            float sumMin = 0f;
            foreach (var a in agents)
            {
                float min = float.MaxValue;
                foreach (var b in agents)
                {
                    if (a == b) continue;
                    float d = Vector2.Distance(a.FlockPosition, b.FlockPosition);
                    if (d < min) min = d;
                }
                sumMin += min;
            }
            float avgMin = sumMin / agents.Count;

            // 정리: unregister 후 파괴, 매니저 GO 도 파괴 (후속 테스트 오염 방지)
            foreach (var a in agents)
            {
                mgr.Unregister(a);
                Object.Destroy(a.gameObject);
            }
            if (mgr != null) Object.Destroy(mgr.gameObject);
            yield return null;

            Assert.Greater(avgMin, 0.3f, "2초 후 평균 최근접 이웃거리가 겹침 수준보다 커야 함");
        }
    }
}
