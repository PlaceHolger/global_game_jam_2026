using UnityEngine;
using Warmask.Planet;

/// <summary>
/// Applies the match-setup slots to a map's four start positions and grows the
/// scene AI into one per AI slot. Lives in Assembly-CSharp so it sees EnemyAi.
/// </summary>
public class FourPlayerSetup : MonoBehaviour
{
    [SerializeField] private PlanetInstance[] homes; // start position 1..4
    [SerializeField] private EnemyAi prototype;

    private void Awake()
    {
        if (homes == null || homes.Length < MatchConfig.MaxSlots)
        {
            Debug.LogError("[FourPlayerSetup] Needs four start-position planets.");
            return;
        }

        MatchSlot[] slots = MatchConfig.Current;
        for (int homeIndex = 0; homeIndex < homes.Length; homeIndex++)
        {
            PlanetInstance home = homes[homeIndex];
            if (!home)
            {
                Debug.LogError("[FourPlayerSetup] Start position " + (homeIndex + 1) + " is missing.");
                continue;
            }

            // Silent hand-off: a neutral home becoming owned is setup, not a capture.
            home.SetOwner(OwnerAt(slots, homeIndex), spawnCaptureEffect: false);
        }

        if (!prototype)
        {
            if (HasAi(slots))
                Debug.LogError("[FourPlayerSetup] No EnemyAi prototype; AI slots will not play.");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < MatchConfig.MaxSlots; i++)
        {
            if (!slots[i].IsAi)
                continue;

            EnemyAi ai = spawned == 0
                ? prototype
                : Instantiate(prototype, prototype.transform.parent);

            spawned++;
            ai.name = "EnemyAI P" + (i + 1);
            ai.gameObject.SetActive(true);
            ai.Configure((Globals.ePlayer)(i + 1), slots[i].strength, slots[i].aiType);
        }

        // No AI slot uses the scene prototype this match (P2 is human/off).
        if (spawned == 0)
            prototype.gameObject.SetActive(false);
    }

    private static Globals.ePlayer OwnerAt(MatchSlot[] slots, int home)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].mode != MatchSlotMode.Off && slots[i].home == home)
                return (Globals.ePlayer)(i + 1);
        return Globals.ePlayer.None;
    }

    private static bool HasAi(MatchSlot[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].IsAi)
                return true;
        return false;
    }
}
