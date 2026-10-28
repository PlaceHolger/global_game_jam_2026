using NUnit.Framework;
using UnityEngine;

namespace Warmask.Tests
{
    public class MatchConfigTests
    {
        [TearDown]
        public void ResetConfig()
        {
            MatchConfig.Reset();
            Globals.Instance.playerCount = 2;
        }

        [Test]
        public void FourHumans_NeedUniqueHomesAndInputs()
        {
            MatchSlot[] slots = FourHumans();
            Assert.IsTrue(MatchConfig.IsValid(slots, out _));

            slots[3].home = slots[0].home;
            Assert.IsFalse(MatchConfig.IsValid(slots, out string homeError));
            Assert.AreEqual("Two players share a start position.", homeError);

            slots = FourHumans();
            slots[3].input = slots[1].input;
            Assert.IsFalse(MatchConfig.IsValid(slots, out string inputError));
            Assert.AreEqual("Two humans share an input device.", inputError);

            slots = FourHumans();
            slots[1].input = MatchInput.MouseKeyboard;
            Assert.IsFalse(MatchConfig.IsValid(slots, out string mouseError));
            Assert.AreEqual("Only Player 1 can use the mouse.", mouseError);
        }

        [Test]
        public void OneActiveSlot_CannotStart()
        {
            MatchSlot[] slots = FourHumans();
            slots[1].mode = MatchSlotMode.Off;
            slots[2].mode = MatchSlotMode.Off;
            slots[3].mode = MatchSlotMode.Off;

            Assert.IsFalse(MatchConfig.IsValid(slots, out string error));
            Assert.AreEqual("Choose at least two players.", error);
        }

        [Test]
        public void Apply_WritesSlotsMapAndCosmetics()
        {
            MatchSlot[] slots = FourHumans();
            slots[0].color = 2;
            slots[1].mode = MatchSlotMode.Ai;
            slots[2].mode = MatchSlotMode.Off;
            slots[3].home = 2;

            MatchConfig.Apply(slots, "SampleScene");

            Assert.AreEqual(3, Globals.Instance.playerCount);
            Assert.AreEqual("SampleScene", MatchConfig.MapScene);
            Assert.IsTrue(Globals.Instance.IsHuman(Globals.ePlayer.Player1));
            Assert.IsFalse(Globals.Instance.IsHuman(Globals.ePlayer.Player2));
            Assert.IsTrue(Globals.Instance.IsHuman(Globals.ePlayer.Player4));
            Assert.AreEqual(MatchConfig.Palette[2], Globals.Instance.GetPlayerColor(Globals.ePlayer.Player1));

            MatchConfig.Reset();
            Assert.IsFalse(MatchConfig.IsConfigured);
            Assert.IsTrue(Globals.Instance.IsHuman(Globals.ePlayer.Player1),
                "without a config the legacy P1-human default still applies");
            Assert.IsFalse(Globals.Instance.IsHuman(Globals.ePlayer.Player4));
        }

        private static MatchSlot[] FourHumans()
        {
            return new[]
            {
                new MatchSlot { mode = MatchSlotMode.Human, home = 0, input = MatchInput.MouseKeyboard },
                new MatchSlot { mode = MatchSlotMode.Human, home = 1, input = MatchInput.Keyboard },
                new MatchSlot { mode = MatchSlotMode.Human, home = 2, input = MatchInput.Gamepad1 },
                new MatchSlot { mode = MatchSlotMode.Human, home = 3, input = MatchInput.Gamepad2 }
            };
        }
    }
}
