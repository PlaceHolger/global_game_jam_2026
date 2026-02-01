using DG.Tweening;
using UnityEngine;

namespace Warmask.Planet
{
    public class PlanetSelectionManager : MonoBehaviour
    {
        [SerializeField] private float clearDelay = 0.5f;

        private static PlanetSelectionManager _instance;

        public static PlanetSelectionManager Instance
        {
            get
            {
                if (!_instance)
                    _instance = FindFirstObjectByType<PlanetSelectionManager>();
                return _instance;
            }
        }

        private PlanetInstance _startPlanet;
        
        private MouseLineHelper _mouseLineHelper;

        private void Awake()
        {
            _mouseLineHelper = GetComponentInChildren<MouseLineHelper>();
        }
        
        public void HandlePlanetHoverStart(PlanetInstance planet)
        {
            if (!_mouseLineHelper || !planet)
                return;
            //update line color on hover
            if (_startPlanet)
                _mouseLineHelper.SetLineType(_startPlanet.PlanetType, planet.PlanetType);
        }

        public void HandlePlanetHoverEnd(PlanetInstance planet)
        {
            if (!_mouseLineHelper || !planet)
                return;
            //update line color on hover
            if (_startPlanet)
                _mouseLineHelper.SetLineType(_startPlanet.PlanetType);
        }

        public void HandlePlanetClick(PlanetInstance planet)
        {
            if (!_mouseLineHelper || !planet) return;

            // No start set -> set start
            if (!_startPlanet)
            {
                if (!Globals.Instance.IsPlayer(planet.OwnedBy))
                    return;

                _startPlanet = planet;
                _mouseLineHelper.SetLineType(planet.PlanetType);
                _mouseLineHelper.SetStartPos(planet.transform);
                planet.SetSelection(true);
                planet.UpdateDebugLabel("Start");
                return;
            }

            // Clicked same planet -> clear
            if (_startPlanet == planet || _startPlanet.UnitCount <= 0)
            {
                _mouseLineHelper.SetStartPos(null);
                planet.SetSelection(false);
                planet.UpdateDebugLabel(null);
                _startPlanet = null;
                return;
            }

            // Start set, end empty -> set end and clear after delay
            _mouseLineHelper.SetLineType(Globals.eType.TypeD, Globals.eType.TypeD); // special color to show "locked in" attack
            _mouseLineHelper.SetEndPos(planet.transform);

            planet.UpdateDebugLabel("End");
            planet.SetSelection(true);
            _startPlanet.SetSelection(false);
            
            int numTroopsToSend = _startPlanet.UnitCount / 2; // Send half of the troops by default
            // but we are trying to be clever, if the target planet has more defense troops, we send more troops
            if(planet.OwnedBy != _startPlanet.OwnedBy && planet.UnitCount >= numTroopsToSend)
            {
                numTroopsToSend = Mathf.Min(planet.UnitCount + 2, _startPlanet.UnitCount); //send enough troops to conquer
            }

            TroopMovementManager.GetInstance().MoveTroops(_startPlanet, planet, numTroopsToSend, _startPlanet.OwnedBy);

            var capturedStart = _startPlanet;
            DOVirtual.DelayedCall(clearDelay, () =>
            {
                _mouseLineHelper.SetStartPos(null);
                _mouseLineHelper.SetEndPos(null);
                planet.UpdateDebugLabel(null);
                planet.SetSelection(false);
                capturedStart.SetSelection(false);
                capturedStart.UpdateDebugLabel(null);
            });

            _startPlanet = null;
        }
    }
}