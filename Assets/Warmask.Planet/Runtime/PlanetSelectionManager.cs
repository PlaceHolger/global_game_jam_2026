using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    public class PlanetSelectionManager : MonoBehaviour
    {
        [SerializeField] private float clearDelay = 0.5f;
        [SerializeField] private Globals.ePlayer player = Globals.ePlayer.Player1;

        public Globals.ePlayer Player => player;

        public int SelectedCount => _selected.Count;
        public IReadOnlyList<PlanetInstance> Selected => _selected;

        private static PlanetSelectionManager _instance;

        public static PlanetSelectionManager Instance
        {
            get
            {
                if (!_instance)
                {
                    var managers = FindObjectsByType<PlanetSelectionManager>(FindObjectsInactive.Exclude);
                    foreach (var manager in managers)
                    {
                        if (manager.player == Globals.ePlayer.Player1)
                        {
                            _instance = manager;
                            break;
                        }
                    }

                    if (!_instance && managers.Length > 0)
                        _instance = managers[0];
                }
                return _instance;
            }
        }

        private readonly List<PlanetInstance> _selected = new List<PlanetInstance>();
        private PlanetInstance _primary;
        private PlanetInstance _hoverPlanet;
        private PlanetInstance _flashStart;
        private PlanetInstance _flashTarget;
        private MouseLineHelper _mouseLineHelper;
        private IPointerSource _pointerSource;
        private Tween _clearTween;

        private const float RightClickDragThreshold = 12f;
        private const float RightClickMaxSeconds = 0.5f;
        private const float BoxDragThreshold = 8f;

        private Vector2 _rightPressScreen;
        private PlanetInstance _rightPressPlanet;
        private float _rightPressTime;
        private bool _rightDragged;

        private Vector2 _leftPressScreen;
        private PlanetInstance _leftPressPlanet;
        private bool _leftBoxing;
        private bool _boxVisible;
        private Rect _boxRect;

        private int _planetLayerMask;
        private CursorLight _cursorLight;

        private static bool ShiftHeld => Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        private static bool CtrlHeld => Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

        /// <summary>Keyboard/gamepad pointers bring their own add-to-selection button.</summary>
        private bool AdditiveHeld => _pointerSource != null ? _pointerSource.AddToSelectionHeld : ShiftHeld;

        private void Awake()
        {
            _planetLayerMask = LayerMask.GetMask("Planets");

            if (player == Globals.ePlayer.None)
                player = Globals.ePlayer.Player1;

            _mouseLineHelper = GetComponentInChildren<MouseLineHelper>();
            SetLinePlayerColor();

            if (player == Globals.ePlayer.Player1 && !GetComponent<LocalMultiplayerBootstrap>())
                gameObject.AddComponent<LocalMultiplayerBootstrap>();
        }

        public void Initialize(Globals.ePlayer newPlayer, IPointerSource pointerSource)
        {
            player = newPlayer;
            _pointerSource = pointerSource;
            SetLinePlayerColor();
        }

        private void SetLinePlayerColor()
        {
            if (_mouseLineHelper && Globals.Instance)
                _mouseLineHelper.SetPlayerColor(Globals.Instance.GetPlayerColor(player));
        }

        private void Update()
        {
            // The prefab lives in Resources, so wait for the map scene before spawning the light.
            if (!_cursorLight && PlanetsManager.Instance)
                _cursorLight = CursorLight.TryGetOrCreate(transform, player, _pointerSource);

            if (_pointerSource != null)
            {
                PlanetInstance hoveredPlanet = FindPlanetAt(_pointerSource.ScreenPosition);
                SetHoveredPlanet(hoveredPlanet);
                if (_primary)
                    UpdateOrderPreview(hoveredPlanet);

                if (_pointerSource.CancelPressed)
                {
                    ClearSelection();
                    return;
                }

                if (!_pointerSource.ConfirmPressed)
                    return;

                if (!hoveredPlanet)
                {
                    ClearSelection();
                    return;
                }

                HandlePlanetClick(hoveredPlanet, _pointerSource.SendAllHeld);
                return;
            }

            if (Mouse.current == null)
                return;

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            bool overClock = MatchClock.BlocksPointer(screenPosition);
            PlanetInstance mousePlanet = overClock ? null : FindPlanetAt(screenPosition);
            SetHoveredPlanet(mousePlanet);
            if (_primary)
                UpdateOrderPreview(mousePlanet);

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                _rightPressScreen = screenPosition;
                _rightPressPlanet = overClock ? null : mousePlanet;
                _rightPressTime = Time.unscaledTime;
                _rightDragged = false;
            }

            if (Mouse.current.rightButton.isPressed && !_rightDragged &&
                Vector2.Distance(screenPosition, _rightPressScreen) > RightClickDragThreshold)
                _rightDragged = true;

            if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                // Right-click is a command on the selection, or a select when nothing is selected.
                // The planet from press time is used so a tiny cursor drift cannot lose the target.
                bool quick = !_rightDragged && !overClock &&
                             Time.unscaledTime - _rightPressTime <= RightClickMaxSeconds;
                if (quick && _rightPressPlanet)
                {
                    if (_selected.Count > 0)
                        SendTo(_rightPressPlanet, sendAll: true);
                    else
                        HandlePlanetClick(_rightPressPlanet, sendAll: true);
                }

                _rightPressPlanet = null;
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame && !overClock)
            {
                _leftPressScreen = screenPosition;
                _leftPressPlanet = mousePlanet;
                _leftBoxing = !_leftPressPlanet;
                _boxVisible = false;

                if (_leftPressPlanet)
                    HandlePlanetClick(_leftPressPlanet, CtrlHeld);
            }

            if (_leftBoxing && Mouse.current.leftButton.isPressed &&
                Vector2.Distance(screenPosition, _leftPressScreen) > BoxDragThreshold)
            {
                _boxVisible = true;
                _boxRect = ScreenBox(screenPosition);
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame && _leftBoxing)
            {
                if (_boxVisible)
                    SelectInBox(_boxRect, ShiftHeld);
                else
                    ClearSelection();

                _leftBoxing = false;
                _boxVisible = false;
            }
        }

        private void SetHoveredPlanet(PlanetInstance planet)
        {
            if (_hoverPlanet == planet)
                return;

            if (_hoverPlanet)
                _hoverPlanet.SetHovered(false);

            _hoverPlanet = planet;

            if (_hoverPlanet)
                _hoverPlanet.SetHovered(true);
        }

        private PlanetInstance FindPlanetAt(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (!camera)
                return null;

            Vector3 worldPoint;
            if (_pointerSource != null)
            {
                if (!_pointerSource.TryGetWorldPoint(camera, out worldPoint))
                    return null;
            }
            else
            {
                worldPoint = camera.ScreenToWorldPoint(
                    new Vector3(screenPosition.x, screenPosition.y, camera.nearClipPlane));
                worldPoint.z = 0f;
            }

            Collider2D hit = Physics2D.OverlapPoint(worldPoint, _planetLayerMask);
            return hit ? hit.GetComponentInParent<PlanetInstance>() : null;
        }

        public void ClearSelection()
        {
            ClearOrderFlash();

            foreach (PlanetInstance planet in _selected)
            {
                if (planet)
                    planet.SetSelection(false);
            }

            _selected.Clear();
            _primary = null;
            _leftBoxing = false;
            _boxVisible = false;

            if (_mouseLineHelper)
            {
                _mouseLineHelper.SetStartPos(null);
                _mouseLineHelper.SetEndPos(null);
                _mouseLineHelper.ClearOrderPreview();
            }
        }

        private void ClearOrderFlash()
        {
            if (_clearTween != null)
            {
                _clearTween.Kill();
                _clearTween = null;
            }

            if (_flashStart)
                _flashStart.SetSelection(false);
            if (_flashTarget)
                _flashTarget.SetSelection(false);

            _flashStart = null;
            _flashTarget = null;

            if (_primary == null && !_leftBoxing && _mouseLineHelper)
            {
                _mouseLineHelper.SetStartPos(null);
                _mouseLineHelper.SetEndPos(null);
                _mouseLineHelper.ClearOrderPreview();
            }
        }

        public void HandlePlanetClick(PlanetInstance planet, bool sendAll = false)
        {
            if (!_mouseLineHelper || !planet) return;

            ClearOrderFlash();

            if (planet.OwnedBy == player)
            {
                // A plain click on an unselected own planet is an order (reinforce it).
                // Shift-click, or a click on an already selected planet, edits the selection instead.
                if (AdditiveHeld || _selected.Count == 0 || _selected.Contains(planet))
                    ToggleSelected(planet);
                else
                    SendTo(planet, sendAll);
                return;
            }

            if (_selected.Count == 0)
            {
                planet.ShowNotYoursFeedback();
                return;
            }

            SendTo(planet, sendAll);
        }

        private void ToggleSelected(PlanetInstance planet)
        {
            if (_selected.Contains(planet))
            {
                RemoveSelected(planet);
                return;
            }

            AddSelected(planet);
            if (GetAvailableShips(planet) <= 0)
                planet.ShowNoShipsFeedback();
            UpdateOrderPreview(_hoverPlanet);
        }

        private void AddSelected(PlanetInstance planet)
        {
            if (!planet || _selected.Contains(planet))
                return;

            _selected.Add(planet);
            planet.SetSelection(true, player);
            _primary = planet;

            if (_mouseLineHelper)
            {
                _mouseLineHelper.SetLineType(planet.PlanetType);
                _mouseLineHelper.SetStartPos(planet.transform);
            }
        }

        private void RemoveSelected(PlanetInstance planet)
        {
            if (!_selected.Remove(planet))
                return;

            planet.SetSelection(false);

            if (_primary != planet)
                return;

            _primary = _selected.Count > 0 ? _selected[_selected.Count - 1] : null;
            if (_primary && _mouseLineHelper)
            {
                _mouseLineHelper.SetLineType(_primary.PlanetType);
                _mouseLineHelper.SetStartPos(_primary.transform);
            }
            else if (!_primary)
            {
                ClearSelection();
            }
        }

        private Rect ScreenBox(Vector2 currentScreenPosition)
        {
            return Rect.MinMaxRect(
                Mathf.Min(_leftPressScreen.x, currentScreenPosition.x),
                Mathf.Min(_leftPressScreen.y, currentScreenPosition.y),
                Mathf.Max(_leftPressScreen.x, currentScreenPosition.x),
                Mathf.Max(_leftPressScreen.y, currentScreenPosition.y));
        }

        private void SelectInBox(Rect screenRect, bool additive)
        {
            Camera camera = Camera.main;
            if (!camera)
                return;

            if (!additive)
                ClearSelection();

            foreach (PlanetInstance planet in FindObjectsByType<PlanetInstance>(FindObjectsInactive.Exclude))
            {
                if (planet.OwnedBy != player)
                    continue;

                Vector3 screenPoint = camera.WorldToScreenPoint(planet.transform.position);
                if (screenPoint.z < 0f || !screenRect.Contains(screenPoint))
                    continue;

                AddSelected(planet);
            }
        }

        private void SendTo(PlanetInstance target, bool sendAll)
        {
            if (!target || _selected.Count == 0)
                return;

            ClearOrderFlash();

            PlanetInstance source = _primary ? _primary : _selected[0];
            var counts = new int[_selected.Count];
            bool anySource = false;

            for (int i = 0; i < _selected.Count; i++)
            {
                PlanetInstance from = _selected[i];
                if (!from)
                    continue;

                if (GetAvailableShips(from) <= 0)
                {
                    from.ShowNoShipsFeedback();
                    continue;
                }

                counts[i] = GetShipsToSend(from, target, sendAll);
                if (counts[i] > 0)
                    anySource = true;
            }

            if (!anySource)
            {
                source.ShowNoShipsFeedback();
                ClearSelection();
                return;
            }

            int total = 0;
            for (int i = 0; i < _selected.Count; i++)
            {
                if (counts[i] <= 0)
                    continue;

                PlanetInstance from = _selected[i];
                if (!from)
                    continue;

                if (TroopMovementManager.GetInstance().MoveTroops(from, target, counts[i], from.OwnedBy) > 0)
                    total += counts[i];
            }

            if (total <= 0)
            {
                source.ShowNoShipsFeedback();
                ClearSelection();
                return;
            }

            if (_mouseLineHelper)
            {
                _mouseLineHelper.SetLineType(Globals.eType.TypeD, Globals.eType.TypeD); // player-colored send flash
                _mouseLineHelper.SetEndPos(target.transform);
                _mouseLineHelper.SetOrderCount(total);
            }

            target.SetSelection(true, player);
            foreach (PlanetInstance planet in _selected)
            {
                if (planet)
                    planet.SetSelection(false);
            }

            _selected.Clear();
            _primary = null;
            _flashStart = source;
            _flashTarget = target;

            _clearTween = DOVirtual.DelayedCall(clearDelay, () =>
            {
                _clearTween = null;

                // A new selection/order owns the line now; its own timer will clean up.
                if (_primary != null)
                    return;

                ClearOrderFlash();
            });
        }

        private void UpdateOrderPreview(PlanetInstance target)
        {
            if (!_primary || !_mouseLineHelper)
                return;

            bool sendAll = _pointerSource != null ? _pointerSource.SendAllHeld : CtrlHeld;

            int total = 0;
            foreach (PlanetInstance from in _selected)
                total += GetShipsToSend(from, target, sendAll);

            _mouseLineHelper.SetOrderCount(total);
        }

        private void OnGUI()
        {
            if (!_boxVisible || Event.current.type != EventType.Repaint)
                return;

            Rect rect = new Rect(
                _boxRect.xMin,
                Screen.height - _boxRect.yMax,
                _boxRect.width,
                _boxRect.height);

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = Globals.Instance ? Globals.Instance.GetPlayerColor(player) : Color.white;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 1f, rect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.yMin, 1f, rect.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static int GetAvailableShips(PlanetInstance planet)
        {
            if (!planet) return 0;
            // MoveTroops caps at the orbit count, so the preview must use the same cap.
            return Mathf.Min(planet.TransferableShips, planet.DefendingShipsInOrbit);
        }

        private static int GetShipsToSend(PlanetInstance from, PlanetInstance to, bool sendAll)
        {
            if (!from) return 0;

            int availableShips = GetAvailableShips(from);
            if (availableShips <= 0)
                return 0;

            if (sendAll)
                return availableShips;

            int numTroopsToSend = (from.DefendingShipsInOrbit + 1) / 2; // round half up
            // but we are trying to be clever, if the target planet has more defense troops, we send more troops
            if (to && to != from && to.OwnedBy != from.OwnedBy && to.DefendingShipsInOrbit >= numTroopsToSend)
            {
                numTroopsToSend = Mathf.Min(to.DefendingShipsInOrbit + 2, from.DefendingShipsInOrbit); //send enough troops to conquer
            }

            return Mathf.Clamp(numTroopsToSend, 0, availableShips);
        }
    }
}
