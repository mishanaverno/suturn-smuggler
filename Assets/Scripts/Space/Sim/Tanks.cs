using DoublePrecision;

namespace OuterSpace.Sim
{
    /// <summary>Баки корабля. Метан — горючее всех режимов и рабочее тело РСУ, поэтому баки общие.</summary>
    public class Tanks
    {
        /// <summary>Запасы, кг.</summary>
        public double methane;
        public double lox;

        public double Mass => methane + lox;

        /// <summary>Сколько топлива можно сжечь до того, как кончится метан или кислород, кг.</summary>
        public double Usable(double loxShare)
        {
            double byMethane = methane / (1.0 - loxShare);
            return loxShare > 0.0 ? Mathd.Min(byMethane, lox / loxShare) : byMethane;
        }

        /// <summary>
        /// Списывает топливо за dt и возвращает прирост скорости. Масса убывает внутри тика,
        /// поэтому прирост — по Циолковскому: exhaust·ln(m0/m1), где exhaust — скорость
        /// истечения вдоль тяги. Кончилось топливо посреди тика — сжигается сколько было.
        /// </summary>
        public Vector3d Expend(double shipMass, Vector3d exhaust, double flow, double loxShare, double dt)
        {
            double burned = Mathd.Min(flow * dt, Usable(loxShare));
            if (burned <= 0.0) return Vector3d.zero;

            methane = Mathd.Max(methane - burned * (1.0 - loxShare), 0.0);
            lox = Mathd.Max(lox - burned * loxShare, 0.0);
            return exhaust * Mathd.Log(shipMass / (shipMass - burned));
        }
    }
}
