using System;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "Globals", menuName = "WarMaster/Globals", order = 1)]
public class Globals : ScriptableObject
{
    private static Globals _instance;
    public static Globals Instance
    {
        get
        {
            if (!_instance)
            {
                _instance = Resources.Load<Globals>("Globals");
                if (!_instance)
                {
                    Debug.LogError("Globals asset not found in Resources folder.");
                }
            }
            return _instance;
        }
    }

    public UnityEvent<eType> OnMaskChanged = new UnityEvent<eType>();

    public enum eType
    {
        TypeA,
        TypeB,
        TypeC,
        TypeD,
        Unknown
    }
    
    public enum ePlayer
    {
        None,
        Player1,
        Player2,
    }
    
    // Configurable colors for each eType
    [SerializeField]
    private Color[] typeColors = new Color[5]
    {
        Color.red,
        Color.blue,
        Color.gray,
        Color.softYellow,
        Color.white // Default for Unknown
    };

    [SerializeField] private Sprite[] playerSprites;

    [SerializeField] private Color[] playerColors = new Color[3]
    {
        Color.white,
        Color.paleGreen,
        Color.paleVioletRed
    };

    public Color GetPlayerColor(ePlayer player) => playerColors[(int)player];

    public Color GetTypeColor(eType type) => typeColors[(int)type];
    
    public bool EasyMode = false;
    
    public Sprite GetPlayerSprite(ePlayer player) => playerSprites[(int)player];

    [NonSerialized] public bool localMultiplayer;
    [NonSerialized] public float maskCooldownRemaining;

    public bool IsHuman(ePlayer player) => player == ePlayer.Player1 || (localMultiplayer && player == ePlayer.Player2);

    public bool IsPlayer(ePlayer player) => IsHuman(player);

    public float planetProductionFactor = 1.0f;

    public eType currentMask = eType.TypeA;

    [Range(0f, 1f), Tooltip("Alpha of ships that are not on the active mask. 0 = hidden, 0.25 = ghosted.")]
    public float inactiveShipAlpha = 0f;

    [Min(0f), Tooltip("Shared cooldown after a mask switch. 0 = unlimited (solo). Local MP sets this at match start.")]
    public float maskSwitchCooldown = 0f;

    [Range(0f, 1f), Tooltip("Local MP default alpha for ships on the inactive mask.")]
    public float localMultiplayerGhostAlpha = 0.15f;

    [Min(0f), Tooltip("Local MP default shared mask cooldown, in seconds.")]
    public float localMultiplayerMaskCooldown = 8f;

    public Sprite ShipSprite => playerSprites != null && playerSprites.Length > 0 ? playerSprites[0] : null;

    public const bool trailInPlayerColor = false;
}