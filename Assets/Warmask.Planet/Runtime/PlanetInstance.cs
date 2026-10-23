using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Warmask.Planet
{
    public class PlanetInstance : MonoBehaviour
    {
        [SerializeField, Tooltip("Event triggered when a new unit is created. Passes the planet type as an integer.")]
        private UnityEvent<int> onUnitCreated;

        [SerializeField, Tooltip("Label for status messages (under attack, falling, etc.)")]
        private TMP_Text statusText;

        [SerializeField, Tooltip("Defense strength value displayed inside the shield")]
        private TMP_Text defenseStrength;

        [SerializeField, Tooltip("Image shown when no one owns the planet")]
        private GameObject noOwner;

        [SerializeField, Tooltip("Shield badge image behind the defense value")]
        private UnityEngine.UI.Image shieldBadge;

        [SerializeField, Tooltip("Label which rotates when the planet is controlled.")]
        private Transform textTransform;

        [SerializeField, Tooltip("Label which displays the planet name")]
        private TMP_Text planet_name_label;

        [SerializeField, Range(0.2f, 5f),
         Tooltip("Size of the planet which affects its scale and unit production rate")]
        private float planet_size = 1f;

        [SerializeField, Tooltip("Type of the planet which determines its color and the units it produces")]
        private Globals.eType planet_type;

        [SerializeField, Tooltip("Current number of units on the planet")]
        private int unitCount = 5;

        [SerializeField] private Globals.ePlayer owner = Globals.ePlayer.None;

        [SerializeField, Tooltip("Explosion prefab spawned when planet is captured")]
        private GameObject explosionPrefab;

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

        public int TransferableShips { get; private set; }
        public void SetTransferableShipCount(int count) => TransferableShips = count;

        public int DefendingUnityOnGround
        {
            get => unitCount;
        }

        public Globals.ePlayer OwnedBy => owner;
        public float PlanetSize => planet_size;
        public event Action<Globals.ePlayer, Globals.ePlayer> OwnerChanged;

        private float unitSpawnInterval;
        private float timer = 0f;
        private float maskModifier = 1f;

        private SpriteRenderer planetRenderer;
        private SpriteRenderer shipTypeIcon;
        private Coroutine feedbackRoutine;
        private float feedbackUntil;
        private string feedbackText;
        private Color feedbackColor;
        private bool _hovered;
        private Vector3 _baseScale;

        private const float HoverScale = 1.1f;

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
            if (arg0 == planet_type)
                maskModifier = 1.0f;
            else
                maskModifier = 0.666f;

            InitializePlanet(); //reinitialize to apply new spawn interval
        }

        public void SetOwner(Globals.ePlayer newOwner)
        {
            Globals.ePlayer oldOwner = owner;
            owner = newOwner;
            int newOwnerIndex = (int)owner - 1;

            //we also update the color of the name label based on owner
            if (planet_name_label)
            {
                planet_name_label.color = Globals.Instance.GetPlayerColor(owner);
            }

            if (statusText)
            {
                statusText.color = Globals.Instance.GetPlayerColor(owner);
            }

            // Update owner indicators
            for (int i = 0; i < ownerIndicators.Length; i++)
            {
                ownerIndicators[i].SetActive(i == newOwnerIndex);
                if (ownerIndicators[i].TryGetComponent<Image>(out var uiImage))
                {
                    uiImage.color = Globals.Instance.GetPlayerColor(owner);
                    uiImage.color = new Color(uiImage.color.r, uiImage.color.g, uiImage.color.b, 0.5f);
                }
            }

            // Show NoOwner when no one owns the planet
            if (noOwner)
                noOwner.SetActive(owner == Globals.ePlayer.None);

            // Spawn explosion on capture
            if (explosionPrefab && oldOwner != newOwner && newOwner != Globals.ePlayer.None)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            if (oldOwner != newOwner)
                OwnerChanged?.Invoke(oldOwner, newOwner);
        }

        private void InitializePlanet()
        {
            // Set planet color based on type using Globals
            Color planetColor = Globals.Instance.GetTypeColor(planet_type);
            if (!planetRenderer)
                TryGetComponent(out planetRenderer);
            if (planetRenderer)
            {
                bool isCurrentMask = (Globals.Instance.currentMask == planet_type);
                if (isCurrentMask)
                    planetRenderer.color = planetColor;
                else //dim color if not matching mask
                    planetRenderer.color = Color.Lerp(planetColor, Color.gray, 0.825f);
            }

            UpdateShipTypeIcon(planetColor);

            EnsureShieldBadge();

            // The mask changes production speed, not the planet's visual size.
            float adjustedSize = planet_size * maskModifier;
            float visualScale = 0.6f + 0.4f * planet_size;
            _baseScale = new Vector3(visualScale, visualScale, 1f);
            transform.localScale = _hovered ? _baseScale * HoverScale : _baseScale;

            UpdateDebugLabel(unitCount.ToString());

            SetOwner(owner);

            // Adjust spawn interval based on size and global production factor
            unitSpawnInterval = Mathf.Max(0.1f, Globals.Instance.planetProductionFactor / adjustedSize);

            planet_name_label.text = gameObject.name;
        }

        private void UpdateShipTypeIcon(Color typeColor)
        {
            if (!Application.isPlaying)
                return;

            if (!shipTypeIcon)
            {
                Sprite shipSprite = Globals.Instance.ShipSprite;
                if (!shipSprite)
                    return;

                var icon = new GameObject("ShipTypeIcon");
                icon.transform.SetParent(transform, false);
                icon.transform.localPosition = new Vector3(-0.85f, -0.85f, 0f);
                icon.transform.localScale = Vector3.one * 0.4f;
                shipTypeIcon = icon.AddComponent<SpriteRenderer>();
                shipTypeIcon.sprite = shipSprite;
                shipTypeIcon.sortingLayerName = "UI";
                shipTypeIcon.sortingOrder = 20;
                if (planetRenderer)
                    shipTypeIcon.sharedMaterial = planetRenderer.sharedMaterial;
            }

            shipTypeIcon.color = typeColor;
        }

        private void EnsureShieldBadge()
        {
            if (!shieldBadge)
                return;
            if (Globals.Instance && Globals.Instance.ShieldSprite)
                shieldBadge.sprite = Globals.Instance.ShieldSprite;
            shieldBadge.color = new Color(1f, 1f, 1f, 0.45f);
        }

        private void SetShieldBadgeVisible(bool visible)
        {
            if (!shieldBadge)
                return;

            shieldBadge.gameObject.SetActive(visible);
        }

        void Update()
        {
            if (numEnemyShipsInOrbit > 0 && numOwnShipsInOrbit > 0)
            {
                // Under attack, do not produce units
                UpdateDebugLabel("Under Attack! (" + numOwnShipsInOrbit + " vs " + numEnemyShipsInOrbit + ")",
                    lastAttacker);
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
                    UpdateDebugLabel("Falling:", lastAttacker);
                }

                if (unitCount == 0)
                {
                    var lastOwner = owner;
                    // Change ownership to the attacking player
                    SetOwner(lastAttacker);
                    lastAttacker = Globals.ePlayer.None;
                    numEnemyShipsInOrbit = 0; //reset enemy ships count
                    UpdateDebugLabel("Victory", lastAttacker);
                    
                    PlanetsManager.Instance.CheckIfPlayerLostAllPlanets(lastOwner);
                }

                return;
            }

            if (owner == Globals.ePlayer.None)
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
            else
            {
                Vector3 dir = (owner == Globals.ePlayer.Player1) ? Vector3.forward : Vector3.back;
                textTransform.Rotate(dir, Time.deltaTime * unitCount);
            }
        }

        public void SetHovered(bool isHovered)
        {
            if (_hovered == isHovered)
                return;

            _hovered = isHovered;
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale;

            transform.DOKill();
            transform.DOScale(_baseScale * (isHovered ? HoverScale : 1f), 0.12f)
                .SetEase(Ease.OutQuad);
        }

        public void SetSelection(bool isSelected)
        {
            if (!selection_highlight)
                return;

            selection_highlight.SetActive(isSelected);
            Transform highlight = selection_highlight.transform;
            highlight.DOKill();
            highlight.localScale = Vector3.one;

            if (isSelected)
                highlight.DOScale(1.15f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }

        void OnValidate()
        {
            // Never run runtime setup on play-mode callbacks or prefab assets.
            if (Application.isPlaying)
                return;

            InitializePlanet();
        }

        public void UpdateDebugLabel(string text = null, Globals.ePlayer textOwner = Globals.ePlayer.None)
        {
            if (!statusText)
                return;

            if (Time.time < feedbackUntil)
            {
                statusText.text = feedbackText;
                statusText.color = feedbackColor;
                SetShieldBadgeVisible(false);
                return;
            }

            if (textOwner != Globals.ePlayer.None)
                statusText.color = Globals.Instance.GetPlayerColor(textOwner);
            else //reset to owner color
                statusText.color = Globals.Instance.GetPlayerColor(owner);

            // Under attack detection
            bool underAttack = numEnemyShipsInOrbit > 0;

            // Always update defense strength with current unit count
            if (defenseStrength)
            {
                defenseStrength.text = unitCount.ToString();
                defenseStrength.fontSize = 18 + Mathf.Clamp((float)unitCount / Mathf.Max(1, maxUnitCount), 0f, 1f) * 12f;
                defenseStrength.color = underAttack ? new Color(1f, 0.3f, 0.3f) : Color.white;
            }

            if (!string.IsNullOrEmpty(text) && !int.TryParse(text, out _))
            {
                // Status message (not a number) - show in statusText
                statusText.text = text;
                // Keep shield visible when under attack (defense value shown in red)
                SetShieldBadgeVisible(underAttack);
            }
            else
            {
                // Number (unit count) - update defense strength in shield only
                string countText = !string.IsNullOrEmpty(text) ? text : unitCount.ToString();

                statusText.text = "";

                SetShieldBadgeVisible(true);
            }
        }

        public void ShowNoShipsFeedback()
        {
            ShowFeedback("No ships to send", new Color(1f, 0.55f, 0.35f));
        }

        public void ShowNotYoursFeedback()
        {
            ShowFeedback("Not yours", new Color(1f, 0.8f, 0.3f));
        }

        public void ShowFeedback(string text, Color color, float duration = 0.9f)
        {
            feedbackText = text;
            feedbackColor = color;
            feedbackUntil = Time.time + duration;
            UpdateDebugLabel();

            if (feedbackRoutine != null)
                StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(ClearFeedbackAfter(duration));
        }

        private IEnumerator ClearFeedbackAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            feedbackUntil = 0f;
            feedbackRoutine = null;
            UpdateDebugLabel();
        }

        private void OnDestroy()
        {
            if (Globals.Instance != null)
                Globals.Instance.OnMaskChanged.RemoveListener(OnMaskChanged);
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