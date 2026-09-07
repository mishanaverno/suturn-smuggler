using OuterSpace.Sim;
using UnityEngine;

namespace OuterSpace
{
    public class ShipMono
    {
        SimTransform simTransform;
       
        void Start ()
        {
            Debug.Log(SpaceMono.instance.transform.position);
            Debug.Log(SimMono.instance.transform.position);
        }
        void Update ()
        {
 
        }
    }
}
