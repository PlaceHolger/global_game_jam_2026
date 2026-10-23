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

        private PlanetInstance _startPlanet;
        private PlanetInstance _hoverPlanet;
        private PlanetInstance _flashStart;
        private PlanetInstance _flashTarget;
        private MouseLineHelper _mouseLineHelper;
        private IPointerSource _pointerSource;
        private Tween _clearTween;
        private int _planetLayerMask;
        private CursorLight _cursorLight;
        private bool _lightSearchDone;

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
            if (!_cursorLight && !_lightSearchDone)
            {
                _cursorLight = CursorLight.TryGetOrCreate(transform, player, _pointerSource);
                _lightSearchDone = true;
            }

            if (_pointerSource != null)
            {
                PlanetInstance hoveredPlanet = FindPlanetAt(_pointerSource.ScreenPosition);
                SetHoveredPlanet(hoveredPlanet);
                if (_startPlanet)
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

            PlanetInstance mousePlanet = FindPlanetAt(Mouse.current.position.ReadValue());
            SetHoveredPlanet(mousePlanet);
            if (_startPlanet)
                UpdateOrderPreview(mousePlanet);

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearSelection();
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (!mousePlanet)
            {
                ClearSelection();
                return;
            }

            bool sendAll = Keyboard.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
            HandlePlanetClick(mousePlanet, sendAll);
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

            if (_startPlanet)
            {
                _startPlanet.SetSelection(false);
                _startPlanet = null;
            }

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

            if (_startPlanet == null && _mouseLineHelper)
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

            // No start set -> set start
            if (!_startPlanet)
            {
                if (planet.OwnedBy != player)
                {
                    planet.ShowNotYoursFeedback();
                    return;
                }

                _startPlanet = planet;
                SetHoveredPlanet(planet);
                _mouseLineHelper.SetLineType(planet.PlanetType);
                _mouseLineHelper.SetStartPos(planet.transform);
                planet.SetSelection(true);
                UpdateOrderPreview(null);

                if (GetAvailableShips(planet) <= 0)
                    planet.ShowNoShipsFeedback();
                return;
            }

            // Clicked same planet -> clear
            if (_startPlanet == planet)
            {
                ClearSelection();
                return;
            }

            int availableShips = GetAvailableShips(_startPlanet);
            if (availableShips <= 0)
            {
                _startPlanet.ShowNoShipsFeedback();
                return;
            }

            int numTroopsToSend = GetShipsToSend(_startPlanet, planet, sendAll);

            // Start set, end empty -> set end and clear after delay
            _mouseLineHelper.SetLineType(Globals.eType.TypeD, Globals.eType.TypeD); // player-colored send flash
            _mouseLineHelper.SetEndPos(planet.transform);
            _mouseLineHelper.SetOrderCount(numTroopsToSend);
            planet.SetSelection(true);
            _startPlanet.SetSelection(false);

            _flashStart = _startPlanet;
            _flashTarget = planet;

            TroopMovementManager.GetInstance().MoveTroops(_startPlanet, planet, numTroopsToSend, _startPlanet.OwnedBy);

            _clearTween = DOVirtual.DelayedCall(clearDelay, () =>
            {
                _clearTween = null;

                // A new selection/order owns the line now; its own timer will clean up.
                if (_startPlanet != null)
                    return;

                ClearOrderFlash();
            });

            _startPlanet = null;
        }

        private void UpdateOrderPreview(PlanetInstance target)
        {
            if (!_startPlanet || !_mouseLineHelper)
                return;

            bool sendAll = _pointerSource != null
                ? _pointerSource.SendAllHeld
                : Keyboard.current != null &&
                  (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

            _mouseLineHelper.SetOrderCount(GetShipsToSend(_startPlanet, target, sendAll));
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
