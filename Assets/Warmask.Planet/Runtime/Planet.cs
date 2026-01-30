using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Planet : MonoBehaviour, IPointerClickHandler
{
    public UnityEvent onUnitCreated;
    
    public TMP_Text debug_label;
    public float planet_size = 1f;
    //public PlanetType type;
    public Globals.eType planet_type;

    private float unitSpawnInterval;
    private int unitCount = 0;
    private float timer = 0f;

    void Start()
    {
        // Set planet color based on type using Globals
        Color planetColor = Globals.Instance.typeColors[(int)planet_type];
        GetComponent<SpriteRenderer>().color = planetColor;

        // Scale the object based on planet_size (uniform x and y scaling)
        transform.localScale = new Vector3(planet_size, planet_size, 1f);
        
        // Adjust spawn interval based on size and global production factor
        unitSpawnInterval = Mathf.Max(0.1f, Globals.Instance.planetProductionFactor / planet_size);
        UpdateDebugLabel("0");
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= unitSpawnInterval)
        {
            timer = 0f;
            unitCount++;
            UpdateDebugLabel($"{unitCount}");
            onUnitCreated.Invoke();
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        UpdateDebugLabel("Click");
        Debug.Log("clicked");
    }

    void UpdateDebugLabel(string text = null)
    {
        if (debug_label)
            debug_label.text = text;
    }
}