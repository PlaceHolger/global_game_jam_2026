using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Warmask.Planet;

namespace Warmask.Planet
{
    public class PlanetInstance : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("Event triggered when a new unit is created. Passes the planet type as an integer.")]
        private UnityEvent<int> onUnitCreated;
        [SerializeField, Tooltip("Label which displays debug information on the planet")]
        private TMP_Text text_label;
        [SerializeField, Tooltip("Label which displays the planet name")]
        private TMP_Text planet_name_label;
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
        [SerializeField, Tooltip("if enemy ships are in orbit, the stored units will be damaged over time")]
        private float unitReductionIntervalWhenAttacked = 1f;
        [SerializeField, Tooltip("Maximum units that can be stored on the planet")]
        private int maxUnitCount = 100;
        
        public Globals.eType PlanetType => planet_type;
        public int DefendingShipsInOrbit //these are the ships in orbit, not the ships on the planet
        {
            get => numOwnShipsInOrbit;
            //set { unitCount = value;  UpdateDebugLabel(); }
        }

        public int DefendingUnityOnGround
        {
            get => unitCount;
        }

        public Globals.ePlayer OwnedBy => owner;
        public float PlanetSize => planet_size;
        
        private float unitSpawnInterval;
        private float timer = 0f;
        private float maskModifier = 1f;

        private int numEnemyShipsInOrbit = 0;
        private int numOwnShipsInOrbit = 0;
        private Globals.ePlayer lastAttacker = Globals.ePlayer.None;
        
        void Start()
        {
            InitializePlanet();
            
            Globals.Instance.OnMaskChanged.AddListener(OnMaskChanged);
        }

        private void OnMaskChanged(Globals.eType arg0)
        {
            //if the mask matches the planet type, double the production speed
            if(arg0 == planet_type ) 
            {
                maskModifier = 1.5f;
            }
            else
            {
                maskModifier = 0.66f;
            }
            InitializePlanet(); //reinitialize to apply new spawn interval
        }

        public void SetOwner(Globals.ePlayer newOwner)
        {
            owner = newOwner;
            int newOwnerIndex = (int)owner - 1;
            
            //we also update the color of the name label based on owner
            if(planet_name_label)
            {
                planet_name_label.color = Globals.Instance.GetPlayerColor(owner);
            }
            
            if(text_label)
            {
                text_label.color = Globals.Instance.GetPlayerColor(owner);
            }
            
            // Update owner indicators gameobjects (from ownerIndicators)
            for (int i = 0; i < ownerIndicators.Length; i++)
            {
                ownerIndicators[i].SetActive(i == newOwnerIndex);
                if (ownerIndicators[i].TryGetComponent<Image>(out var uiImage))
                {
                    uiImage.color = Globals.Instance.GetPlayerColor(owner);
                    uiImage.color = new Color(uiImage.color.r, uiImage.color.g, uiImage.color.b, 0.5f);
                }
            }
        }

        private void InitializePlanet()
        {
            // Set planet color based on type using Globals
            Color planetColor = Globals.Instance.GetTypeColor(planet_type);
            if(TryGetComponent(out SpriteRenderer sr))
                sr.color = planetColor;

            float adjustedSize = planet_size * maskModifier;
            // Scale the object based on planet_size (uniform x and y scaling)
            transform.localScale = new Vector3(0.6f + 0.4f * adjustedSize, 0.6f + 0.4f * adjustedSize, 1f);
            
            UpdateDebugLabel(unitCount.ToString());
            
            SetOwner(owner);
            
            // Adjust spawn interval based on size and global production factor
            unitSpawnInterval = Mathf.Max(0.1f, Globals.Instance.planetProductionFactor / adjustedSize);
            
            planet_name_label.text = gameObject.name;
        }

        void Update()
        {
            if(numEnemyShipsInOrbit > 0 && numOwnShipsInOrbit > 0)
            {
                // Under attack, do not produce units
                UpdateDebugLabel("Under Attack! (" + numOwnShipsInOrbit + " vs " + numEnemyShipsInOrbit + ")");
                return;
            }

            if (numEnemyShipsInOrbit > 0 && numOwnShipsInOrbit <= 0)
            {
                // Under attack, reduce unit count over time
                timer += Time.deltaTime;
                if (timer >= unitReductionIntervalWhenAttacked)
                {
                    timer = 0f;
                    unitCount = Mathf.Max(0, unitCount - 1);
                    UpdateDebugLabel("Falling: " + unitCount.ToString());
                }
                
                if(unitCount == 0)
                {
                    // Change ownership to the attacking player
                    SetOwner(lastAttacker);
                    lastAttacker = Globals.ePlayer.None;
                    numEnemyShipsInOrbit = 0; //reset enemy ships count
                    UpdateDebugLabel("Victory");
                }
                return;
            }
            
            if(owner == Globals.ePlayer.None)
                return; // No production if no owner
            
            timer += Time.deltaTime;
            if (timer >= unitSpawnInterval)
            {
                timer = 0f;
                if (unitCount <= maxUnitCount)
                {
                    unitCount++;
                    UpdateDebugLabel(unitCount.ToString());
                }
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

        public void OnShipCountChanged(Dictionary<int, int> shipCountCache)
        {
            numOwnShipsInOrbit = 0;
            numEnemyShipsInOrbit = 0;
            
            foreach (var kvp in shipCountCache)
            {
                if ((Globals.ePlayer)kvp.Key == owner)
                    numOwnShipsInOrbit += kvp.Value;
                else
                {
                    lastAttacker = (Globals.ePlayer)kvp.Key;
                    numEnemyShipsInOrbit += kvp.Value;
                }
            }

            if (numEnemyShipsInOrbit > 0)
            {
                //UpdateDebugLabel("Under Attack!");
            }
            else
            {
                UpdateDebugLabel(unitCount.ToString());
                lastAttacker = Globals.ePlayer.None;
            }
        }
    }
}