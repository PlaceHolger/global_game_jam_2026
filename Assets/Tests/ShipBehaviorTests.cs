using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Warmask.Ship;
using Warmask.Shipyard;

namespace Warmask.Tests
{
    /// <summary>
    /// Behaviour tests for the manager-driven ship refactor. These run at a far
    /// corner of the map so the scene's own ships cannot interfere.
    /// </summary>
    public class ShipBehaviorTests
    {
        private static readonly Vector2 TestOrigin = new Vector2(1000f, 1000f);

        private ShipyardInstance yard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetProfiling();
            ShipProfiling.EnableDamage = true;

            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            ShipyardInstance[] yards = Object.FindObjectsByType<ShipyardInstance>();
            Assert.IsNotEmpty(yards, "SampleScene has no ShipyardInstance.");
            yard = yards[0];

            foreach (ShipyardInstance other in yards)
                other.enabled = false;

            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            ResetProfiling();
        }

        [UnityTest]
        public IEnumerator ManagerTicksShips_WithoutPerShipUpdateMethods()
        {
            Assert.IsNull(
                typeof(ShipInstance).GetMethod("Update",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public),
                "ShipInstance must not have its own Update; the manager drives it.");
            Assert.IsNull(
                typeof(ShipInstance).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public),
                "ShipInstance must not have its own LateUpdate; the manager drives it.");
            Assert.IsNull(
                typeof(ShipMaskFader).GetMethod("Update",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public),
                "ShipMaskFader must not have its own Update; ShipInstance.Tick drives it.");

            ShipInstance ship = SpawnShip(TestOrigin, playerId: 0, aiInterval: 999f);
            Vector2 startPosition = ship.transform.position;
            int countBefore = ShipManagerInstance.Instance.ShipCount;

            yield return new WaitForSeconds(0.5f);

            Vector2 endPosition = ship.transform.position;
            Assert.Greater(Vector2.Distance(startPosition, endPosition), 0.05f,
                "Manager tick did not move the ship.");
            Assert.GreaterOrEqual(ShipManagerInstance.Instance.ShipCount, countBefore);
        }

        [UnityTest]
        public IEnumerator SpatialHashTargeting_WorksWithoutColliders()
        {
            ShipProfiling.EnableMovement = false;

            ShipInstance shooter = SpawnShip(TestOrigin, playerId: 0, aiInterval: 0.02f);
            ShipInstance target = SpawnShip(TestOrigin + Vector2.up * 1.5f, playerId: 1, aiInterval: 999f);
            target.transform.rotation = Quaternion.identity;

            Assert.AreEqual(0, shooter.GetComponentsInChildren<Collider2D>(true).Length,
                "Ship prefab should no longer carry ship-to-ship colliders.");

            float startHealth = target.CurrentHealth;

            yield return new WaitForSeconds(0.8f);

            Assert.Less(target.CurrentHealth, startHealth,
                "Spatial-hash targeting/firing did not damage the enemy ship.");
        }

        [UnityTest]
        public IEnumerator FriendlyShip_BlocksLineOfFire()
        {
            ShipProfiling.EnableMovement = false;

            ShipInstance shooter = SpawnShip(TestOrigin, playerId: 0, aiInterval: 0.02f);
            ShipInstance friendly = SpawnShip(TestOrigin + Vector2.up * 0.8f, playerId: 0, aiInterval: 999f);
            ShipInstance enemy = SpawnShip(TestOrigin + Vector2.up * 1.8f, playerId: 1, aiInterval: 999f);

            Assert.Less(friendly.transform.position.y, enemy.transform.position.y);

            float enemyStartHealth = enemy.CurrentHealth;
            float friendlyStartHealth = friendly.CurrentHealth;

            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(enemyStartHealth, enemy.CurrentHealth, 0.001f,
                "Shots passed through a friendly ship and hit the enemy.");
            Assert.AreEqual(friendlyStartHealth, friendly.CurrentHealth, 0.001f,
                "Friendly ship should not be damaged by friendly fire.");
        }

        [UnityTest]
        public IEnumerator TrailsAndSpritesUsePlayerColor()
        {
            ShipProfiling.EnableMovement = false;

            ShipInstance ship = SpawnShip(TestOrigin, playerId: 1, aiInterval: 999f);
            yield return null;
            yield return null;

            SpriteRenderer sprite = ship.GetComponentInChildren<SpriteRenderer>(true);
            TrailRenderer trail = ship.GetComponentInChildren<TrailRenderer>(true);
            Assert.IsNotNull(sprite);
            Assert.IsNotNull(trail);

            Color expected = Globals.Instance.GetPlayerColor(Globals.ePlayer.Player1);
            Assert.IsNotNull(sprite.sharedMaterial,
                "Ship sprite has no material; URP 2D renders a null sprite material as magenta.");
            Assert.IsFalse(sprite.sharedMaterial.shader.name.Contains("Error"),
                "Ship sprite uses an error shader.");
            // Idle ships are dimmed uniformly, so compare hue rather than raw brightness.
            Color spriteColor = sprite.color;
            float peak = Mathf.Max(spriteColor.r, spriteColor.g, spriteColor.b, 0.0001f);
            spriteColor /= peak;
            Assert.AreEqual(expected.r, spriteColor.r, 0.01f, "Sprite tint is not the player colour.");
            Assert.AreEqual(expected.g, spriteColor.g, 0.01f, "Sprite tint is not the player colour.");
            Assert.AreEqual(expected.b, spriteColor.b, 0.01f, "Sprite tint is not the player colour.");

            Color start = trail.startColor;
            Assert.AreEqual(expected.r, start.r, 0.01f, "Trail startColor is not the player colour.");
            Assert.AreEqual(expected.g, start.g, 0.01f, "Trail startColor is not the player colour.");
            Assert.AreEqual(expected.b, start.b, 0.01f, "Trail startColor is not the player colour.");

            Color end = trail.endColor;
            Assert.AreEqual(expected.r, end.r, 0.01f, "Trail endColor is not the player colour.");
            Assert.AreEqual(expected.g, end.g, 0.01f, "Trail endColor is not the player colour.");
            Assert.AreEqual(expected.b, end.b, 0.01f, "Trail endColor is not the player colour.");
        }

        [UnityTest]
        public IEnumerator MaskFader_DisablesRenderersWhenFullyTransparent()
        {
            ShipProfiling.EnableMovement = false;
            ShipProfiling.EnableDamage = false;

            Globals.eType visibleType = Globals.Instance.currentMask;
            Globals.eType hiddenType = visibleType == Globals.eType.TypeA
                ? Globals.eType.TypeB
                : Globals.eType.TypeA;

            ShipInstance ship = SpawnShip(TestOrigin, playerId: 0, aiInterval: 999f);
            ship.SetType(visibleType);
            yield return null;
            yield return null;

            SpriteRenderer sprite = ship.GetComponentInChildren<SpriteRenderer>(true);
            TrailRenderer trail = ship.GetComponentInChildren<TrailRenderer>(true);
            Assert.IsNotNull(sprite);
            Assert.IsTrue(sprite.enabled, "Visible ship should have its SpriteRenderer enabled.");
            if (trail != null)
                Assert.IsTrue(trail.enabled, "Visible ship should have its TrailRenderer enabled.");

            ship.SetType(hiddenType);

            // Fade duration is 1.5s in the prefab.
            yield return new WaitForSeconds(2f);

            Assert.IsFalse(sprite.enabled, "Transparent ship should have its SpriteRenderer disabled.");
            if (trail != null)
                Assert.IsFalse(trail.enabled, "Transparent ship should have its TrailRenderer disabled.");
        }

        [UnityTest]
        public IEnumerator OffMaskShip_KeepsFighting_WithoutVisibleLaser()
        {
            ShipProfiling.EnableMovement = false;

            Globals.eType visibleType = Globals.Instance.currentMask;
            Globals.eType hiddenType = visibleType == Globals.eType.TypeA
                ? Globals.eType.TypeB
                : Globals.eType.TypeA;

            ShipInstance shooter = SpawnShip(TestOrigin, playerId: 0, aiInterval: 0.02f);
            ShipInstance enemy = SpawnShip(TestOrigin + Vector2.up * 1.5f, playerId: 1, aiInterval: 999f);
            shooter.SetType(hiddenType);
            enemy.SetType(visibleType);

            LineRenderer laser = shooter.GetComponentInChildren<LineRenderer>(true);
            Assert.IsNotNull(laser, "Ship prefab should carry a laser LineRenderer.");

            float enemyStartHealth = enemy.CurrentHealth;
            bool laserWasVisible = false;
            float deadline = Time.time + 0.8f;
            while (Time.time < deadline)
            {
                if (laser.enabled)
                    laserWasVisible = true;
                yield return null;
            }

            Assert.Less(enemy.CurrentHealth, enemyStartHealth,
                "Off-mask ships must keep fighting (damage is gameplay, not visuals).");
            Assert.IsFalse(laserWasVisible,
                "Off-mask ships must not render lasers the player cannot see the ship for.");
        }

        [UnityTest]
        public IEnumerator OffMaskShip_MovesAtFullSpeed_WithCoarseTick()
        {
            ShipProfiling.EnableTargeting = false;

            GameObject moveTarget = new GameObject("CoarseTickMoveTarget");
            moveTarget.transform.position = TestOrigin + Vector2.up * 300f;

            ShipInstance visible = SpawnShip(TestOrigin, playerId: 0, aiInterval: 999f);
            ShipInstance hidden = SpawnShip(TestOrigin + Vector2.right * 60f, playerId: 1, aiInterval: 999f);
            hidden.SetType(visible.ShipType == Globals.eType.TypeA
                ? Globals.eType.TypeB
                : Globals.eType.TypeA);

            visible.SetMoveCommand(moveTarget.transform, 0f);
            hidden.SetMoveCommand(moveTarget.transform, 0f);

            // Let the move-command speed boost expire before measuring cruise speed.
            yield return new WaitForSeconds(1.5f);

            Vector2 visibleStart = visible.transform.position;
            Vector2 hiddenStart = hidden.transform.position;
            yield return new WaitForSeconds(1.5f);

            float visibleDistance = Vector2.Distance(visibleStart, visible.transform.position);
            float hiddenDistance = Vector2.Distance(hiddenStart, hidden.transform.position);

            Assert.Greater(hiddenDistance, 0.5f,
                "Off-mask ship did not move at all under coarse ticking.");
            Assert.AreEqual(visibleDistance, hiddenDistance, visibleDistance * 0.15f,
                "Coarse off-mask ticking changed ship speed; accumulate dt instead of dropping it.");

            Object.Destroy(moveTarget);
        }

        [UnityTest]
        public IEnumerator IdleShip_UsesShortTrail_AssignedShip_UsesLongTrail()
        {
            ShipProfiling.EnableTargeting = false;

            ShipInstance ship = SpawnShip(TestOrigin, playerId: 0, aiInterval: 999f);
            TrailRenderer trail = ship.GetComponentInChildren<TrailRenderer>(true);
            Assert.IsNotNull(trail, "Ship has no TrailRenderer.");

            yield return null;
            Assert.Less(trail.time, 2f, "Idle ship should use the short trail.");
            Assert.Less(trail.widthMultiplier, 1f, "Idle ship should use the thin trail.");
            Assert.Less(trail.startColor.a, 0.5f, "Idle ship trail should be more transparent.");

            SpriteRenderer sprite = ship.GetComponentInChildren<SpriteRenderer>(true);
            Assert.IsNotNull(sprite, "Ship has no SpriteRenderer.");
            float idleRed = sprite.color.r;
            Assert.Less(idleRed, 1f, "Idle hull should stay below HDR bloom.");

            GameObject moveTarget = new GameObject("TrailMoveTarget");
            moveTarget.transform.position = TestOrigin + Vector2.up * 300f;
            ship.SetMoveCommand(moveTarget.transform, 0f);
            yield return null;

            Assert.Greater(trail.time, 2f, "Assigned ship should use the long trail.");
            Assert.AreEqual(1f, trail.widthMultiplier, 0.001f, "Assigned ship should use the full-width trail.");
            Assert.Greater(trail.startColor.a, 0.5f, "Assigned ship trail should be more intense.");

            Assert.Greater(sprite.color.r, 1f, "Assigned ship hull should be HDR-bright.");
            Assert.Greater(sprite.color.r, idleRed, "Assigned ship should be brighter than an idle ship.");
            Object.Destroy(moveTarget);
        }

        [UnityTest]
        public IEnumerator Death_IsDeferredAndUnregistersShip()
        {
            ShipProfiling.EnableMovement = false;
            ShipProfiling.EnableTargeting = false;

            ShipInstance ship = SpawnShip(TestOrigin, playerId: 0, aiInterval: 999f);
            int countBefore = ShipManagerInstance.Instance.ShipCount;

            ship.TakeDamage(ship.MaxHealth + 1f);

            Assert.IsTrue(ship.IsPendingDeath, "Lethal damage should defer death instead of mutating the queue.");
            Assert.IsFalse(ship.IsAlive);

            yield return null;
            yield return null;

            Assert.Less(ShipManagerInstance.Instance.ShipCount, countBefore,
                "Dead ship was not unregistered after the manager's death pass.");
        }

        private ShipInstance SpawnShip(Vector2 position, int playerId, float aiInterval)
        {
            yard.SpawnShipUnchecked();
            ShipInstance ship = yard.LastSpawnedShip;
            Assert.IsNotNull(ship, "Shipyard failed to spawn a ship.");

            ship.transform.SetPositionAndRotation(position, Quaternion.identity);
            ship.SetPlayerId(playerId);
            ship.SetType(Globals.Instance.currentMask);
            ship.AiUpdateInterval = aiInterval;
            return ship;
        }

        private static void ResetProfiling()
        {
            ShipProfiling.EnableBoidSteering = true;
            ShipProfiling.EnableMovement = true;
            ShipProfiling.EnableTrails = true;
            ShipProfiling.EnableLasers = true;
            ShipProfiling.EnableAudio = true;
            ShipProfiling.EnableRendering = true;
            ShipProfiling.EnableHitFlash = true;
            ShipProfiling.EnableTargeting = true;
            ShipProfiling.EnableFiring = true;
            ShipProfiling.EnableDamage = true;
            ShipProfiling.EnableOffMaskCoarseTick = true;
            ShipProfiling.LogPhaseTimings = false;
        }
    }
}
