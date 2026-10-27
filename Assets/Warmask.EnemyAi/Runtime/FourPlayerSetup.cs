using UnityEngine;
using Warmask.Planet;

/// <summary>
/// Gives the spare corner homes to the extra slots and grows the scene AI into one
/// per opponent. Lives in Assembly-CSharp so it can see both Globals and EnemyAi.
/// </summary>
public class FourPlayerSetup : MonoBehaviour
{
    [SerializeField] private PlanetInstance[] spareHomes; // P3 then P4
    [SerializeField] private EnemyAi prototype;

    private void Awake()
    {
        if (!Globals.Instance || Globals.Instance.playerCount <= 2)
            return;

        int extraPlayers = Globals.Instance.playerCount - 2;
        if (spareHomes == null || spareHomes.Length < extraPlayers || !prototype)
        {
            Debug.LogError("[FourPlayerSetup] Needs one spare home per extra player and the AI prototype; match will be incomplete.");
            return;
        }

        for (int slot = 2; slot < Globals.Instance.playerCount; slot++)
        {
            PlanetInstance home = spareHomes[slot - 2];
            if (!home)
            {
                Debug.LogError("[FourPlayerSetup] Spare home for slot " + slot + " is missing.");
                continue;
            }

            Globals.ePlayer owner = (Globals.ePlayer)(slot + 1);

            // Silent hand-off: a neutral home becoming owned is setup, not a capture.
            home.SetOwner(owner, spawnCaptureEffect: false);
            AddAi(owner);
        }
    }

    private void AddAi(Globals.ePlayer owner)
    {
        EnemyAi clone = Instantiate(prototype, prototype.transform.parent);
        clone.name = "EnemyAI P" + (int)owner;
        clone.ConfigurePlayer(owner);
    }
}
