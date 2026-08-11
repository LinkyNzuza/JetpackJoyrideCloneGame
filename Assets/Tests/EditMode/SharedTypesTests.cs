// Feature: player-core-gameplay
// Task 1.3 unit tests for the shared pure types. These are plain unit tests, not
// one of the 24 numbered correctness properties; they guard the value semantics
// the cores rely on and prove the test assembly resolves the Game.Player types.

using System;
using NUnit.Framework;

namespace Game.Player.Tests.EditMode
{
    [TestFixture]
    public sealed class SharedTypesTests
    {
        [Test]
        public void Vector2Core_Stores_Components_And_Compares_By_Value()
        {
            var a = new Vector2Core(1.5f, -2.25f);
            var b = new Vector2Core(1.5f, -2.25f);

            Assert.That(a.X, Is.EqualTo(1.5f));
            Assert.That(a.Y, Is.EqualTo(-2.25f));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != new Vector2Core(1.5f, 0f), Is.True);
        }

        [Test]
        public void Vector2Core_With_Helpers_Leave_The_Other_Component_Untouched()
        {
            var start = new Vector2Core(3f, 4f);

            Assert.That(start.WithX(9f), Is.EqualTo(new Vector2Core(9f, 4f)));
            Assert.That(start.WithY(9f), Is.EqualTo(new Vector2Core(3f, 9f)));
            Assert.That(Vector2Core.Zero, Is.EqualTo(new Vector2Core(0f, 0f)));
        }

        [Test]
        public void Diagnostic_Factories_Carry_Severity_Field_And_Message()
        {
            var warning = Diagnostic.Warning("thrustForce", "out of range");

            Assert.That(warning.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(warning.Field, Is.EqualTo("thrustForce"));
            Assert.That(warning.Message, Is.EqualTo("out of range"));
            Assert.That(Diagnostic.Error("Play_Bounds", "m").Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(Diagnostic.Info("thrustForce", "m").Severity, Is.EqualTo(DiagnosticSeverity.Info));
        }

        [Test]
        public void PowerUpType_Declares_Shield_And_Magnet()
        {
            var values = (PowerUpType[])Enum.GetValues(typeof(PowerUpType));

            Assert.That(values, Contains.Item(PowerUpType.Shield));
            Assert.That(values, Contains.Item(PowerUpType.Magnet));
        }
    }
}
