
using UnityEngine;

namespace OuterSpace.SystemBuilder
{
    public class SBPlanetMono : PlanetMono, ICentralBody
    {
        
        OrbitRenderer orbitRenderer;

        // Start is called before the first frame update
        void Awake()
        {
            spaceObject = new(M, transform.parent.GetComponentInParent<ICentralBody>());
        }
        private void Start()
        {
            spaceObject.UpdateVelocity(RelativePostion, new(velocity));
            orbitRenderer = GetComponent<OrbitRenderer>();
            Debug.Log(spaceObject.centralbody);

        }
        void FixedUpdate()
        {
            _soi = spaceObject.orbitParams.semiMajorAxis * System.Math.Pow(spaceObject.mass / spaceObject.centralbody.Mass, 2.0 / 5.0);
            spaceObject.UpdateVelocity(RelativePostion, new(velocity));
            if (spaceObject.orbitParams.eccentricity < 1)
                orbitRenderer.DrawOrbit(spaceObject.centralbody.RelativePostion.CastToVector3(), spaceObject.orbitParams);
            if (Input.GetKey(KeyCode.Space))
            {
                Log();
                Debug.Log(RelativePostion);
                
            }

        }

    }
}
