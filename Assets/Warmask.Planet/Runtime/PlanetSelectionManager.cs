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
        public bool UsesPointerSource => _pointerSource != null;

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
        private MouseLineHelper _mouseLineHelper;
        private IPointerSource _pointerSource;

        private void Awake()
        {
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
            if (_pointerSource == null)
            {
                if (_startPlanet)
                    UpdateOrderPreview(_hoverPlanet);

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && _startPlanet)
                {
                    if (!FindPlanetAt(Mouse.current.position.ReadValue()))
                        ClearSelection();
                }
                return;
            }

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

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            foreach (Collider2D hit in hits)
            {
                PlanetInstance planet = hit.GetComponentInParent<PlanetInstance>();
                if (planet)
                    return planet;
            }

            return null;
        }

        public void ClearSelection()
        {
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

        public void HandlePlanetHoverStart(PlanetInstance planet)
        {
            if (!_mouseLineHelper || !planet)
                return;

            SetHoveredPlanet(planet);
            if (_startPlanet)
            {
                _mouseLineHelper.SetLineType(_startPlanet.PlanetType, planet.PlanetType);
                UpdateOrderPreview(planet);
            }
        }

        public void HandlePlanetHoverEnd(PlanetInstance planet)
        {
            if (!_mouseLineHelper || !planet)
                return;

            if (_hoverPlanet == planet)
                SetHoveredPlanet(null);
            if (_startPlanet)
            {
                _mouseLineHelper.SetLineType(_startPlanet.PlanetType);
                UpdateOrderPreview(null);
            }
        }

        public void HandlePlanetClick(PlanetInstance planet, bool sendAll = false)
        {
            if (!_mouseLineHelper || !planet) return;

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

            TroopMovementManager.GetInstance().MoveTroops(_startPlanet, planet, numTroopsToSend, _startPlanet.OwnedBy);

            var capturedStart = _startPlanet;
            DOVirtual.DelayedCall(clearDelay, () =>
            {
                _mouseLineHelper.SetStartPos(null);
                _mouseLineHelper.SetEndPos(null);
                _mouseLineHelper.ClearOrderPreview();
                //planet.UpdateDebugLabel(null);
                planet.SetSelection(false);
                capturedStart.SetSelection(false);
                //capturedStart.UpdateDebugLabel(null);
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
