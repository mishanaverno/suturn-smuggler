using OuterSpace.Sim;
using DoublePrecision;

namespace OuterSpace
{
    public interface ICentralBody
    {
        public SimTransform SimTransform { get; }
        public Vector3d Velocity { get; }
        public double Mass { get; }
        public double SOI { get; }
    }
}
