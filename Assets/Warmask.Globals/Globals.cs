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

    public UnityEvent<eMask> OnMaskChanged = new UnityEvent<eMask>();

    public enum eType
    {
        Red,
        Gray,
        Blue,
        Unknown
    }
    
    public enum eMask
    {
        None,
        Mask1, //red
        Mask2, //blue 
        Unknown = -1
    }
    
    public enum ePlayer
    {
        None,
        Player1,
        Player2,
    }
    
    // Configurable colors for each eType
    public Color[] typeColors = new Color[4]
    {
        Color.red,    // Default for Red
        Color.gray,   // Default for Gray
        Color.blue,    // Default for Blue
        Color.blueViolet // Default for Unknown
    };

    public float planetProductionFactor = 1.0f;
}