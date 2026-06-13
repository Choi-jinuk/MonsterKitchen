using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MonsterKitchen.AI;

namespace MonsterKitchen.Tests.PlayMode
{
    // ====================================================================
    //  CompanionSpawnTests — 동료 flock 비겹침 스모크 (씬 비의존 더미)
    // ====================================================================
    public class CompanionSpawnTests
    {
        sealed class Agent : MonoBehaviour, IFlockAgent
        {
            public Vector2   FlockPosition  => transform.position;
            public Transform FlockTransform => transform;
        }

        [UnityTest]
        public IEnumerator Companions_SeparateAndChaseLeader()
        {
            var mgr    = FlockManager.GetOrCreate();
            var leader = new GameObject("leader");
            leader.transform.position = Vector3.zero;
            var la = leader.AddComponent<Agent>();
            mgr.Register(la);

            var comps = new List<Agent>();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"comp{i}");
                go.transform.position = new Vector2(0.1f * i, 0.1f);
                var a = go.AddComponent<Agent>();
                mgr.Register(a);
                comps.Add(a);
            }

            var   buffer = new Vector2[16];
            float t      = 0f;
            while (t < 1.5f)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                foreach (var a in comps)
                {
                    Vector2 self     = a.FlockPosition;
                    Vector2 toLeader = (Vector2)leader.transform.position - self;
                    int n   = mgr.QueryNeighbors(self, 0.9f, a.FlockTransform, buffer);
                    Vector2 vel = FlockSteering.ComputeChase(self, toLeader.normalized, buffer, n, 2f, FlockWeights.Default);
                    a.transform.position += (Vector3)(vel * Time.fixedDeltaTime);
                }
            }

            float d = Vector2.Distance(comps[0].FlockPosition, comps[1].FlockPosition);

            mgr.Unregister(la); Object.Destroy(leader.gameObject);
            foreach (var a in comps) { mgr.Unregister(a); Object.Destroy(a.gameObject); }
            Object.Destroy(mgr.gameObject);
            yield return null;

            Assert.Greater(d, 0.3f, "동료끼리 겹치지 않아야 함");
        }
    }
}
