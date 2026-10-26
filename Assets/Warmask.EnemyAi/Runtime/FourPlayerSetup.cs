using UnityEngine;
using Warmask.Planet;

/// <summary>
/// Makes a map playable by four slots: the two spare corner homes change hands and
/// the single scene AI grows into three, one per opponent slot.
/// Lives in Assembly-CSharp so it can see both Warmask.Scenes and EnemyAi.
/// </summary>
public class FourPlayerSetup : MonoBehaviour
{
    [SerializeField] private PlanetInstance p3Home;
    [SerializeField] private PlanetInstance p4Home;
    [SerializeField] private EnemyAi prototype;

    private void Awake()
    {
        if (!Globals.Instance || Globals.Instance.playerCount < 4)
            return;

        if (p3Home) p3Home.SetOwner(Globals.ePlayer.Player3);
        if (p4Home) p4Home.SetOwner(Globals.ePlayer.Player4);

        AddAi(Globals.ePlayer.Player3);
        AddAi(Globals.ePlayer.Player4);
    }

    private void AddAi(Globals.ePlayer owner)
    {
        if (!prototype)
            return;

        EnemyAi clone = Instantiate(prototype, prototype.transform.parent);
        clone.name = "EnemyAI P" + (int)owner;
        clone.ConfigurePlayer(owner);
    }
}
