using System.Collections.Generic;
using Milkfrog.CombatDemo.PCG;
using NUnit.Framework;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public sealed class WfcTests
    {
        WfcConfig config;
        [SetUp] public void Setup() { config = ScriptableObject.CreateInstance<WfcConfig>(); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(config); }
        [Test] public void RotationsAndStateDeduplication()
        {
            Assert.AreEqual(2, WfcConfig.Rotate(1)); Assert.AreEqual(1, WfcConfig.Rotate(8));
            Assert.AreEqual(31, config.modules.Count);
            var seen = new HashSet<string>(); foreach (var m in config.modules) Assert.IsTrue(seen.Add(m.room + ":" + m.ports));
        }
        [Test] public void WeightedEntropyHasExpectedLimits()
        {
            var solver = new WfcSolver(config, 1);
            Assert.AreEqual(0, solver.Entropy(1), 1e-8); Assert.Greater(solver.Entropy(3), 0);
            config.modules[0].weight = config.modules[1].weight; Assert.AreEqual(System.Math.Log(2), solver.Entropy(3), 1e-6);
        }
        [Test] public void SameSeedReproducesTopologyAndBudget()
        {
            var a = new WfcSolver(config, -190).Solve(); var b = new WfcSolver(config, -190).Solve();
            Assert.IsTrue(a.success, a.error); Assert.IsTrue(b.success, b.error); CollectionAssert.AreEqual(a.tiles, b.tiles); CollectionAssert.AreEqual(a.encounters, b.encounters);
            Assert.AreEqual(a.stats.backtracks, b.stats.backtracks);
        }
        [Test] public void EveryDefaultSeedIsConnectedWithFourRoomsAndLoop()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var r = new WfcSolver(config, seed).Solve(); Assert.IsTrue(r.success, $"Seed {seed}: {r.error}");
                Assert.AreEqual(r.stats.walkable, r.stats.reachable); Assert.GreaterOrEqual(r.stats.rooms, 4); Assert.GreaterOrEqual(r.stats.loops, 1);
                Assert.IsTrue(WfcSolver.Inspect(config, r.tiles, r.start, r.goal).success);
            }
        }
        [Test] public void ImpossibleTilesetTerminatesWithDiagnostic()
        {
            config.maxAttempts = 2; config.maxBacktracks = 0;
            config.modules = new List<WfcModule> { new WfcModule { id = "OnlyCrossRoom", ports = 15, room = true, weight = 1 } };
            var r = new WfcSolver(config, 0).Solve(); Assert.IsFalse(r.success); Assert.AreEqual(2, r.stats.attempts); Assert.IsNotEmpty(r.error);
        }
        [Test] public void InvalidWeightFailsBeforeObservation()
        {
            config.modules[0].weight = float.NaN;
            var r = new WfcSolver(config, 0).Solve(); Assert.IsFalse(r.success); Assert.AreEqual(0, r.stats.attempts);
            config.modules[0].weight = 4; config.width = -100;
            r = new WfcSolver(config, 0).Solve(); Assert.IsFalse(r.success); Assert.AreEqual(0, r.stats.attempts); Assert.IsNotEmpty(r.error);
        }
        [Test] public void ContradictionsReallyExerciseBacktrackingAndEveryStepHasValidDomains()
        {
            int total = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                var solver = new WfcSolver(config, seed);
                foreach (var step in solver.Steps())
                {
                    Assert.AreEqual(config.width * config.height, step.domains.Length);
                    if (step.kind == WfcStepKind.Complete) foreach (ulong domain in step.domains) Assert.AreEqual(1, WfcSolver.Bits(domain));
                }
                Assert.IsTrue(solver.Result.success); total += solver.Result.stats.backtracks;
                Assert.LessOrEqual(solver.Result.stats.backtracks, config.maxAttempts * config.maxBacktracks);
            }
            Assert.Greater(total, 0, "Expected the connectivity constraint to force at least one alternative decision.");
        }
        [Test] public void ValidatorRejectsUnmatchedBoundarySocket()
        {
            var r = new WfcSolver(config, 18).Solve(); Assert.IsTrue(r.success);
            r.tiles[0] = config.modules.FindIndex(m => m.ports == 15);
            Assert.IsFalse(WfcSolver.Inspect(config, r.tiles, r.start, r.goal).success);
        }
    }
}
