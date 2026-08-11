// Feature: player-core-gameplay
// Infrastructure smoke test for task 1.2. This is NOT one of the 24 numbered
// correctness properties in the design; it exists only to prove that the
// CsCheck assembly in Assets/Plugins/CsCheck resolves from the edit-mode test
// assembly and that a Gen sample actually runs inside the editor.

using System;
using CsCheck;
using NUnit.Framework;

namespace Game.Player.Tests.EditMode
{
    [TestFixture]
    public sealed class CsCheckSmokeTest
    {
        /// <summary>
        /// Runs the design's minimum of 100 iterations over a bounded integer
        /// generator with an always-true property. Single-threaded so the
        /// editor test runner reports failures deterministically.
        /// </summary>
        [Test]
        public void CsCheck_Resolves_And_Samples_In_The_Editor()
        {
            Func<int, bool> alwaysTrue = i => i >= -1000 && i <= 1000;

            Check.Sample(Gen.Int[-1000, 1000], alwaysTrue, iter: 100, threads: 1);
        }

        /// <summary>
        /// Confirms the generator is actually producing values rather than
        /// silently sampling nothing, so a future green property test cannot be
        /// green for the wrong reason.
        /// </summary>
        [Test]
        public void CsCheck_Generator_Produces_Values()
        {
            var count = 0;

            Check.Sample(Gen.Int[0, 10], _ => count++, iter: 100, threads: 1);

            Assert.That(count, Is.EqualTo(100));
        }
    }
}
