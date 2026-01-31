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
    public Color[] typeColors = new Color[5]
    {
        Color.red,
        Color.blue,
        Color.gray,
        Color.blueViolet,
        Color.white // Default for Unknown
    };

    public Color GetTypeColor(eType type) => typeColors[(int)type];
    
    public bool IsPlayer(ePlayer player) => player == ePlayer.Player1;

    public float planetProductionFactor = 1.0f;

    public eType currentMask = eType.TypeA;
}