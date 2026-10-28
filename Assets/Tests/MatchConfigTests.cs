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

        [Test]
        public void Home_AllowsAnyPlanetOnTheSelectedMap()
        {
            MatchSlot[] slots = FourHumans();
            slots[3].home = MapLayout.Count("LargeMap") - 1;
            Assert.IsTrue(MatchConfig.IsValid(slots, "LargeMap", out _),
                "the last planet on the map is a legal start position");

            slots[3].home = MapLayout.Count("LargeMap");
            Assert.IsFalse(MatchConfig.IsValid(slots, "LargeMap", out _));
        }

        [Test]
        public void Badge_IsAlwaysPresent()
        {
            MatchConfig.Reset();
            foreach (MatchSlot slot in MatchConfig.Current)
                Assert.GreaterOrEqual(slot.badge, 0, "a badge slot never means None");

            MatchSlot[] slots = FourHumans();
            slots[0].badge = -1;
            MatchConfig.Apply(slots, "LargeMap");
            Assert.AreEqual(0, MatchConfig.Current[0].badge);
        }

        [Test]
        public void Teams_MustLeaveAtLeastTwoSides()
        {
            MatchSlot[] slots = FourHumans();
            foreach (MatchSlot slot in slots)
                slot.team = 1;

            Assert.IsFalse(MatchConfig.IsValid(slots, out string error));
            Assert.AreEqual("Choose at least two teams.", error);
        }

        [Test]
        public void Teams_SplitAlliesFromEnemies()
        {
            MatchSlot[] slots =
            {
                new MatchSlot { mode = MatchSlotMode.Human, home = 0, input = MatchInput.MouseKeyboard, team = 1 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 1, team = 1 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 2, team = 2 },
                new MatchSlot { mode = MatchSlotMode.Off, home = 3, team = 3 }
            };
            MatchConfig.Apply(slots, "LargeMap");

            Assert.IsTrue(MatchConfig.AreAllied(Globals.ePlayer.Player1, Globals.ePlayer.Player2));
            Assert.IsFalse(MatchConfig.AreEnemies(Globals.ePlayer.Player1, Globals.ePlayer.Player2));
            Assert.IsTrue(MatchConfig.AreEnemies(Globals.ePlayer.Player1, Globals.ePlayer.Player3));
            CollectionAssert.AreEquivalent(
                new[] { Globals.ePlayer.Player1, Globals.ePlayer.Player2 },
                MatchConfig.TeamMembers(1));
            Assert.IsTrue(MatchConfig.TeamHasHuman(1));
        }

        [Test]
        public void Difficulty_TracksStrengthTeamsAndBonuses()
        {
            MatchSlot[] slots = TwoSides();
            Assert.AreEqual(1f, MatchConfig.EvaluateDifficulty(slots), 0.001f);

            slots[1].strength = MatchAiStrength.Hard;
            Assert.AreEqual(1.4f, MatchConfig.EvaluateDifficulty(slots), 0.001f);

            slots[1].strength = MatchAiStrength.Easy;
            Assert.AreEqual(0.7f, MatchConfig.EvaluateDifficulty(slots), 0.001f);

            slots[1].strength = MatchAiStrength.Normal;
            slots[1].startingShips = 10;
            Assert.AreEqual(1.2f, MatchConfig.EvaluateDifficulty(slots), 0.001f);

            slots[1].startingShips = 0;
            slots[1].productionBoost = 200;
            Assert.AreEqual(2f, MatchConfig.EvaluateDifficulty(slots), 0.001f);

            slots[1].productionBoost = 100;
            slots[0].startingShips = 10;
            Assert.AreEqual(1f / 1.2f, MatchConfig.EvaluateDifficulty(slots), 0.001f);

            slots[0].startingShips = 0;
            slots[1].team = 1;
            Assert.AreEqual(0f, MatchConfig.EvaluateDifficulty(slots), 0.001f,
                "an AI on the human's team is not hostile opposition");
        }

        [Test]
        public void Difficulty_PunishesAiTeamingUp()
        {
            MatchSlot[] slots =
            {
                new MatchSlot { mode = MatchSlotMode.Human, home = 0, input = MatchInput.MouseKeyboard, team = 1 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 1, team = 2 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 2, team = 2 },
                new MatchSlot { mode = MatchSlotMode.Off, home = 3, team = 3 }
            };

            Assert.AreEqual(2.5f, MatchConfig.EvaluateDifficulty(slots), 0.001f,
                "two normal AIs on one team are harder than two solo AIs");
        }

        [Test]
        public void Apply_KeepsStartingShipsAndProduction()
        {
            MatchSlot[] slots = TwoSides();
            slots[0].startingShips = 10;
            slots[1].productionBoost = 150;
            slots[1].startingShips = -5;
            MatchConfig.Apply(slots, "LargeMap");

            Assert.AreEqual(10, MatchConfig.StartingShips(Globals.ePlayer.Player1));
            Assert.AreEqual(1.5f, MatchConfig.ProductionBoost(Globals.ePlayer.Player2), 0.001f);
            Assert.AreEqual(0, MatchConfig.StartingShips(Globals.ePlayer.Player2));
        }

        private static MatchSlot[] TwoSides()
        {
            return new[]
            {
                new MatchSlot { mode = MatchSlotMode.Human, home = 0, input = MatchInput.MouseKeyboard, team = 1 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 1, team = 2 },
                new MatchSlot { mode = MatchSlotMode.Off, home = 2, team = 3 },
                new MatchSlot { mode = MatchSlotMode.Off, home = 3, team = 4 }
            };
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
