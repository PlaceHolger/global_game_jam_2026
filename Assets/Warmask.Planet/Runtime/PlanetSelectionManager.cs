namespace Warmask.Planet.Runtime{
    using DG.Tweening;
    using UnityEngine;

    public class PlanetSelectionManager : MonoBehaviour 
    {
        [SerializeField] 
        private float clearDelay = 0.5f;

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
        
        public void HandlePlanetHoverStart(PlanetInstance planet)
        {
            var lineHelper = MouseLineHelper.Instance;
            if (!lineHelper || !planet) 
                return;
            //update line color on hover
            if (_startPlanet)
                lineHelper.SetLineType(_startPlanet.PlanetType, planet.PlanetType);
        }
        
        public void HandlePlanetHoverEnd(PlanetInstance planet)
        {
            var lineHelper = MouseLineHelper.Instance;
            if (!lineHelper || !planet) 
                return;
            //update line color on hover
            if (_startPlanet)
                lineHelper.SetLineType(_startPlanet.PlanetType);
        }

        public void HandlePlanetClick(PlanetInstance planet)
        {
            var lineHelper = MouseLineHelper.Instance;
            if (!lineHelper || !planet) return;

            // No start set -> set start
            if (!_startPlanet)
            {
                if (planet.OwnedBy != Globals.ePlayer.Player1) //we are only allowing player 1 to select planets for now
                    return;
                
                _startPlanet = planet;
                lineHelper.SetLineType(planet.PlanetType);
                lineHelper.SetStartPos(planet.transform);
                planet.SetSelection(true);
                planet.UpdateDebugLabel("Start");
                return;
            }

            // Clicked same planet -> clear
            if (_startPlanet == planet)
            {
                lineHelper.SetStartPos(null);
                planet.SetSelection(false);
                planet.UpdateDebugLabel(null);
                _startPlanet = null;
                return;
            }

            // Start set, end empty -> set end and clear after delay
            lineHelper.SetLineType(Globals.eType.Unknown);
            lineHelper.SetEndPos(planet.transform);

            planet.UpdateDebugLabel("End");
            planet.SetSelection(true);
            _startPlanet.SetSelection(false);
            
            TroopMovementManager.GetInstance().MoveTroops(_startPlanet, planet, _startPlanet.UnitCount / 2, _startPlanet.OwnedBy);

            var capturedStart = _startPlanet;
            DOVirtual.DelayedCall(clearDelay, () =>
            {
                lineHelper.SetStartPos(null);
                lineHelper.SetEndPos(null);
                planet.UpdateDebugLabel(null);
                planet.SetSelection(false);
                capturedStart.SetSelection(false);
                capturedStart.UpdateDebugLabel(null);
            });

            _startPlanet = null;
        }
    }
}