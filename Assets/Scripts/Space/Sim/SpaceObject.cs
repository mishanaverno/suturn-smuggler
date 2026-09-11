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
        public enum SpaceObjectParts { SOI, ORBIT, TRAJECTORY };
        public List<SpaceObjectParts> parts = new();
        public OrbitElements orbitParams;
        public SpaceObject centralBody;
        public SimTransform simTransform;
        public double radius;
        public KnowledgeSource knowledge = KnowledgeSource.Database;
        public double SOI;
        public double MU;
        public bool IsRoot => centralBody == null;
        public Vector3d velocity => simTransform.RELATIVE_V;
        // Планеты и луны своих сфер влияния не покидают, проверять их каждый тик незачем.
        public virtual bool TracksSOITransitions => false;
        public SpaceObject Instance => this;

        public SpaceObject(Vector3d position, Vector3d velocity, double mu, GameObject prefab, List<SpaceObjectParts> parts)
        {
            this.parts = parts;
            this.MU = mu;
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
            if (IsRoot)
            {
                simTransform.SetGLOBAL_V(velocity);
                SOI = double.PositiveInfinity;
                return this;
            }
            simTransform.SetRELATIVE_V(velocity);
            CalculateOrbit(velocity);
            CalculateSOI();
            return this;
        }
        /// <summary>Ставит объект на орбиту, заданную элементами: положение и скорость выводятся из них.</summary>
        public SpaceObject SetOrbit(OrbitElements elements)
        {
            orbitParams = elements;
            (Vector3d position, Vector3d velocity) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(elements, GameMono.instance.Epoch);
            simTransform.SetRELATIVE_R(position);
            simTransform.SetRELATIVE_V(velocity);
            CalculateSOI();
            return this;
        }
        private SpaceObject Render(GameObject prefab)
        {
            InstatiateGameObject(prefab);
            return this;
        }
        public void CalculateSOI()
        {
            // Отношение масс равно отношению GM, поэтому масса тел нигде не нужна.
            SOI = orbitParams.semiMajorAxis * Mathd.Pow(MU / centralBody.MU, 2.0 / 5.0);
        }
        public void CalculateOrbit(Vector3d velocity)
        {
            GameObject.GetComponent<SpaceObjectMono>().I_V = velocity;
            this.orbitParams = AstroDynamic.CalculateOrbitElements(simTransform.RELATIVE_R, velocity, centralBody.MU, GameMono.instance.Epoch);
        }
        
        public virtual void OnCentralBodyChanged(SpaceObject previous) { }
        public virtual void Update() { }
        public virtual void FixedUpdate() {
            if (IsRoot) return;
            (Vector3d relPos, Vector3d relVel) = AstroDynamic.CalcRelativePositionAndVelocityAtEpoch(orbitParams, GameMono.instance.Epoch);
            simTransform.SetRELATIVE_R(relPos);
            simTransform.SetRELATIVE_V(relVel);
        }

    }
    
}
