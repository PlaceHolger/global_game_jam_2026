using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Warmask.Planet.Runtime
{
    public class PlanetInstance : MonoBehaviour, IPointerClickHandler
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
        
        private float unitSpawnInterval;
        private float timer = 0f;

        void Start()
        {
            InitializePlanet();
            // Adjust spawn interval based on size and global production factor
            unitSpawnInterval = Mathf.Max(0.1f, Globals.Instance.planetProductionFactor / planet_size);
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
        }

        void Update()
        {
            if(owner == Globals.ePlayer.None)
                return; // No production if no owner
            
            timer += Time.deltaTime;
            if (timer >= unitSpawnInterval)
            {
                timer = 0f;
                unitCount++;
                UpdateDebugLabel(unitCount.ToString());
                onUnitCreated.Invoke((int)planet_type);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var lineHelper = MouseLineHelper.Instance; //todo: make this a real manager for ship movement
            if (!lineHelper.startTransform)
            {
                //no start pos, so we can be the start
                lineHelper.SetLineType(planet_type);
                lineHelper.SetStartPos(transform);
                UpdateDebugLabel("Start");
            }
            else if (lineHelper.startTransform == transform)
            {
                //clicked the same planet, so clear the line
                lineHelper.SetStartPos(null);
                UpdateDebugLabel("Cleared");
                if (selection_highlight)
                    selection_highlight.SetActive(false);
                return;
            }
            else if (!lineHelper.endTransform)  //okay, we have a start pos already but the end is empty, so the command is from somewhere to us, we can clear the line
            {
                lineHelper.SetLineType(Globals.eType.Unknown);
                lineHelper.SetEndPos(transform);
                // show the line for 1 second then clear it
                DOVirtual.DelayedCall(0.5f, () =>
                {
                    lineHelper.SetStartPos(null);
                    lineHelper.SetEndPos(null);
                    UpdateDebugLabel("Cleared");
                });
                //disable selection highlight on start and end
                if (selection_highlight)
                    selection_highlight.SetActive(false);
                if (lineHelper.startTransform.TryGetComponent(out PlanetInstance startPlanet))
                {
                    if (startPlanet.selection_highlight)
                        startPlanet.selection_highlight.SetActive(false);
                }
                return;
            }
            else //we have both start and end already, so ignore this click (should never happen)
            {
                Debug.Log("should never happen");
                return;
            }
            
            //toggle selection_highlight 
            if (selection_highlight)
            {
                bool isActive = selection_highlight.activeSelf;
                selection_highlight.SetActive(!isActive);
                
                if(!isActive)
                    MouseLineHelper.Instance.SetStartPos(transform);
                else 
                    MouseLineHelper.Instance.SetStartPos(null);
            }
            
            UpdateDebugLabel("Click");
            Debug.Log("clicked");
        }
        
        void OnValidate()
        {
            // Update color and scale in edit mode when values change
            InitializePlanet();
        }

        void UpdateDebugLabel(string text = null)
        {
            if (text_label)
                text_label.text = text;
        }
    }
}