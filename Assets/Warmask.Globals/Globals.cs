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
        Player3,
        Player4,
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

    [SerializeField, Tooltip("Badge/logo choices on the match setup screen. Add new owner sprites here.")]
    private Sprite[] playerBadges;

    [SerializeField, Tooltip("Degrees to rotate each ship sprite counter-clockwise, index-aligned with playerSprites, so every hull is drawn nose-up. Art drawn nose-right needs +90, nose-left -90.")]
    private float[] playerSpriteRotations;

    [SerializeField, Tooltip("Plain shield icon for the defense badge shown beside a planet's ground-unit count")]
    private Sprite shieldIcon;

    [SerializeField] private Color[] playerColors = new Color[5]
    {
        Color.white,
        Color.paleGreen,
        Color.paleVioletRed,
        new Color(1f, 0.52f, 0.12f),   // Player3: orange
        new Color(1f, 0.45f, 0.72f)    // Player4: pink
    };

    private Color[] matchColors;
    private Sprite[] matchShips;
    private Sprite[] matchBadges;
    private float[] matchRotations;

    public Color GetPlayerColor(ePlayer player)
    {
        int index = (int)player;
        return matchColors != null && index < matchColors.Length ? matchColors[index] : playerColors[index];
    }

    public Color GetTypeColor(eType type) => typeColors[(int)type];
    
    public bool EasyMode = false;

    public int ShipTypeCount => playerSprites != null ? playerSprites.Length : 0;
    public int BadgeCount => playerBadges != null ? playerBadges.Length : 0;

    public Sprite GetPlayerSprite(ePlayer player)
    {
        int index = (int)player;
        return matchShips != null && index < matchShips.Length ? matchShips[index] : playerSprites[index];
    }

    public Sprite GetShipSpriteByIndex(int index) =>
        playerSprites != null && playerSprites.Length > 0
            ? playerSprites[Mathf.Clamp(index, 0, playerSprites.Length - 1)]
            : null;

    /// <summary>Degrees to rotate a ship sprite so its nose points along +Y.</summary>
    public float GetShipRotationByIndex(int index) =>
        playerSpriteRotations != null && index >= 0 && index < playerSpriteRotations.Length
            ? playerSpriteRotations[index]
            : 0f;

    public Sprite GetBadgeByIndex(int index) =>
        playerBadges != null && playerBadges.Length > 0
            ? playerBadges[Mathf.Clamp(index, 0, playerBadges.Length - 1)]
            : null;

    /// <summary>Null keeps the planet prefab's own badge sprite.</summary>
    public Sprite GetPlayerBadge(ePlayer player)
    {
        int index = (int)player;
        return matchBadges != null && index < matchBadges.Length ? matchBadges[index] : null;
    }

    public void ApplyMatchCosmetics(Color[] colors, Sprite[] ships, Sprite[] badges, float[] rotations)
    {
        matchColors = colors;
        matchShips = ships;
        matchBadges = badges;
        matchRotations = rotations;
    }

    public float GetPlayerRotation(ePlayer player)
    {
        int index = (int)player;
        return matchRotations != null && index >= 0 && index < matchRotations.Length ? matchRotations[index] : 0f;
    }

    [NonSerialized] public bool localMultiplayer;
    [NonSerialized] public float maskCooldownRemaining;
    [NonSerialized, Tooltip("Slots in the current match, 2 or 4. Set by the menu before the map loads.")]
    public int playerCount = 2;

    public bool IsHuman(ePlayer player) =>
        MatchConfig.IsConfigured
            ? MatchConfig.IsHuman(player)
            : player == ePlayer.Player1 || (localMultiplayer && player == ePlayer.Player2);

    public bool IsPlayer(ePlayer player) => IsHuman(player);

    public float planetProductionFactor = 1.0f;

    public eType currentMask = eType.TypeA;

    [Range(0f, 1f), Tooltip("Alpha of ships that are not on the active mask. 0 = hidden, 0.25 = ghosted.")]
    public float inactiveShipAlpha = 0f;

    [Min(0f), Tooltip("Shared cooldown after a mask switch, in seconds. Local MP overrides this at match start.")]
    public float maskSwitchCooldown = 2.5f;

    [Min(1), Tooltip("Extra ships a player may own beyond owned planets * per-planet ship cap, e.g. stacks in transit.")]
    public int fleetCapBuffer = 20;

    [Min(0f), Tooltip("Local MP default shared mask cooldown, in seconds.")]
    public float localMultiplayerMaskCooldown = 5f;

    public Sprite ShipSprite => playerSprites != null && playerSprites.Length > 0 ? playerSprites[0] : null;

    public Sprite ShieldSprite => shieldIcon;

    public const bool trailInPlayerColor = false;
}