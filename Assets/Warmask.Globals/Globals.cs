using UnityEngine;

[CreateAssetMenu(fileName = "Globals", menuName = "WarMaster/Globals", order = 1)]
public class Globals : ScriptableObject
{
    private static Globals _instance;
    public static Globals Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<Globals>("Globals");
                if (_instance == null)
                {
                    Debug.LogError("Globals asset not found in Resources folder.");
                }
            }
            return _instance;
        }
    }

    public enum eType
    {
        Red,
        Gray,
        Blue
    }
    
    // Configurable colors for each eType
    public Color[] typeColors = new Color[3]
    {
        Color.red,    // Default for Red
        Color.gray,   // Default for Gray
        Color.blue    // Default for Blue
    };

    public float planetProductionFactor = 1.0f;
}