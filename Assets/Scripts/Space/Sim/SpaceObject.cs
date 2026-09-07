using UnityEngine;
using DoublePrecision;
using OuterSpace.Sim;
using Utilities;
using Game;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace OuterSpace
{
    public class SpaceObject : ObjectWithMono<SpaceObjectMono, SpaceObject>
    {
        public enum SpaceObjectParts { SOI, ORBIT };
        public List<SpaceObjectParts> parts = new();
        public Vector3d velocity;
        public OrbitElements orbitParams;
        public SpaceObject centralBody;
        public double mass;
        public SimTransform simTransform;
        public double SOI;
        public double MU;
        public bool IsStar => centralBody == null;
        public SpaceObject Instance => this;

        public SpaceObject(Vector3d position, Vector3d velocity, double mass, GameObject prefab, List<SpaceObjectParts> parts)
        {
            this.parts = parts;
            this.mass = mass;
            this.MU = Constants.realG * mass;
            Render(prefab);
            SetSimTransform(position, velocity);
        }
        private SpaceObject SetSimTransform(Vector3d position, Vector3d velocity)
        {
            this.simTransform = new(position, velocity, GameObject.transform);
            return this;
        }
        public SpaceObject SetCentralBody(SpaceObject centralBody)
        {
            this.centralBody = centralBody;
            this.simTransform.RelativeTo = centralBody.simTransform;
            return this;
        }

        public SpaceObject SetVelocity(Vector3d velocity)
        {
            this.velocity = velocity;
            if (!IsStar)
            {
                CalculateOrbit(velocity);
                CalculateSOI();
            }
            else
            {
                SOI = double.PositiveInfinity;
            }
            return this;
        }
        private SpaceObject Render(GameObject prefab)
        {
            InstatiateGameObject(prefab);
            return this;
        }
        public void CalculateSOI()
        {
            SOI = orbitParams.semiMajorAxis * Mathd.Pow((mass) / (centralBody.mass), 2.0 / 5.0);
        }
        public void CalculateOrbit(Vector3d velocity)
        {
            GameObject.GetComponent<SpaceObjectMono>().I_V = velocity;
            this.orbitParams = AstroDynamic.CalculateOrbitElements(simTransform.RELATIVE_R, velocity, centralBody.MU, GameMono.instance.Epoch);
        }
        
        public virtual void Update() { }
        public virtual void FixedUpdate() {
            if (!IsStar)
            {
                (Vector3d ECI_POS, _) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, GameMono.instance.Epoch);
                if (ECI_POS != Vector3d.zero)
                {
                    simTransform.SetRELATIVE_R(ECI_POS);
                }
            }
        }

    }
    
}
