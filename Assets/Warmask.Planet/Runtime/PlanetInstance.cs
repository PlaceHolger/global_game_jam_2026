using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Warmask.Planet.Runtime
{
    public class PlanetInstance : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("Event triggered when a new unit is created. Passes the planet type as an integer.")]
        private UnityEvent<int> onUnitCreated;
        [FormerlySerializedAs("debug_label")] [SerializeField, Tooltip("Label which displays debug information on the planet")]
        private TMP_Text text_label;
        [SerializeField, Range(0.2f, 5f), Tooltip("Size of the planet which affects its scale and unit production rate")]
        private float planet_size = 1f;
        [SerializeField, Tooltip("Type of the planet which determines its color and the units it produces")]
        private Globals.eType planet_type;
        [SerializeField, Tooltip("Current number of units on the planet")]
        private int unitCount = 5;
        [SerializeField]
        private Globals.ePlayer owner = Globals.ePlayer.None;
        [SerializeField, Tooltip("Highlight GameObject for selection indication")]
        private GameObject selection_highlight;
        [SerializeField, Tooltip("2 ui images showing the owner of the planet")]
        private GameObject[] ownerIndicators;
        
        public Globals.eType PlanetType => planet_type;
        
        private float unitSpawnInterval;
        private float timer = 0f;

        void Start()
        {
            InitializePlanet();
            // Adjust spawn interval based on size and global production factor
            unitSpawnInterval = Mathf.Max(0.1f, Globals.Instance.planetProductionFactor / planet_size);
        }
        
        public void SetOwner(Globals.ePlayer newOwner)
        {
            owner = newOwner;
            int newOwnerIndex = (int)owner - 1;
            
            // Update owner indicators gameobjects (from ownerIndicators)
            for (int i = 0; i < ownerIndicators.Length; i++)
            {
                ownerIndicators[i].SetActive(i == newOwnerIndex);
            }
        }


        private void InitializePlanet()
        {
            // Set planet color based on type using Globals
            Color planetColor = Globals.Instance.typeColors[(int)planet_type];
            if(TryGetComponent(out SpriteRenderer sr))
                sr.color = planetColor;

            // Scale the object based on planet_size (uniform x and y scaling)
            transform.localScale = new Vector3(planet_size, planet_size, 1f);
            
            UpdateDebugLabel(unitCount.ToString());
            
            SetOwner(owner);
        }

        void Update()
        {
            //if(owner == Globals.ePlayer.None)
               // return; // No production if no owner
            
            timer += Time.deltaTime;
            if (timer >= unitSpawnInterval)
            {
                timer = 0f;
                unitCount++;
                UpdateDebugLabel(unitCount.ToString());
                onUnitCreated.Invoke((int)planet_type);
            }
        }
        
        public void SetSelection(bool isSelected)
        {
            if (selection_highlight)
                selection_highlight.SetActive(isSelected);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlanetSelectionManager.Instance?.HandlePlanetClick(this);
        }
        
        void OnValidate()
        {
            // Update color and scale in edit mode when values change
            InitializePlanet();
        }

        public void UpdateDebugLabel(string text = null)
        {
            if (text_label)
                if(!string.IsNullOrEmpty(text))
                    text_label.text = text;
                else //by default show unit count
                    text_label.text = unitCount.ToString();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PlanetSelectionManager.Instance?.HandlePlanetHoverStart(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PlanetSelectionManager.Instance?.HandlePlanetHoverEnd(this);
        }
    }
}