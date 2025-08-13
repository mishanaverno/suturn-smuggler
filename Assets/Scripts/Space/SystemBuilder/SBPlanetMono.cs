
using UnityEngine;

namespace OuterSpace.SystemBuilder
{
    public class SBPlanetMono : PlanetMono, ICentralBody
    {
        // Start is called before the first frame update
        void Awake()
        {
            spaceObject = new(M, new(transform.position), new(velocity), transform.parent.GetComponentInParent<ICentralBody>());
        }
        private void Start()
        {
            spaceObject.UpdateVelocity(RelativePosition, new(velocity));
        }
        void FixedUpdate()
        {
            _soi = spaceObject.orbitParams.semiMajorAxis * System.Math.Pow(spaceObject.mass / spaceObject.centralBody.Mass, 2.0 / 5.0);
            spaceObject.UpdateVelocity(RelativePosition, new(velocity));
            if (Input.GetKey(KeyCode.Space))
            {
                Log();
            }

        }

    }
}
