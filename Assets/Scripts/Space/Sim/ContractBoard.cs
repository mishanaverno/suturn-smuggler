using System.Collections.Generic;
using DoublePrecision;
using OuterSpace.Sim.Objects;

namespace OuterSpace.Sim
{
    /// <summary>
    /// Грузы, которые станция предлагает отвезти. Цена и срок выводятся из перелёта Гомана
    /// между лунами станций: это грубая, но честная мера того, насколько далеко везти, и
    /// считается она без прогноза траектории.
    /// </summary>
    public static class ContractBoard
    {
        const int OfferCount = 3;
        /// <summary>Плата за тонну на каждый км/с перелёта; подогнана так, чтобы рейс окупал топливо с запасом.</summary>
        const double RewardPerTonneKmS = 4000.0;
        const double MassStep = 500.0;      // кг
        const int MaxMassSteps = 24;
        static readonly string[] Goods =
        {
            "MEDICAL SUPPLIES", "SPARE PARTS", "ELECTRONICS", "SEED STOCK", "ISOTOPES", "WATER FILTERS",
        };
        static readonly System.Random random = new();

        public static List<Cargo> Generate(Station from, IReadOnlyList<Station> stations, double epoch)
        {
            List<Station> destinations = new();
            // У станций одной луны нет перелёта между лунами, а значит и меры цены.
            foreach (Station station in stations)
                if (station.centralBody != from.centralBody) destinations.Add(station);

            List<Cargo> offers = new();
            if (destinations.Count == 0) return offers;
            for (int i = 0; i < OfferCount; i++)
            {
                Station to = destinations[random.Next(destinations.Count)];
                double mass = MassStep * random.Next(1, MaxMassSteps + 1);
                (double deltaV, double transfer, double synodic) = Transfer(from.centralBody, to.centralBody);
                offers.Add(new Cargo
                {
                    name = Goods[random.Next(Goods.Length)],
                    mass = mass,
                    from = from,
                    to = to,
                    reward = Mathd.Round(mass / 1000.0 * deltaV / 1000.0 * RewardPerTonneKmS / 100.0) * 100.0,
                    // Окно может открыться только через синодический период, а сам перелёт —
                    // вдвое дольше гомановского: запас на подход, стыковку и неидеальный план.
                    deadline = epoch + synodic + 2.0 * transfer,
                });
            }
            return offers;
        }

        /// <summary>Перелёт Гомана между круговыми орбитами двух лун вокруг общего центра.</summary>
        static (double deltaV, double transfer, double synodic) Transfer(SpaceObject a, SpaceObject b)
        {
            double mu = a.centralBody.MU;
            double r1 = a.orbitParams.semiMajorAxis;
            double r2 = b.orbitParams.semiMajorAxis;
            double at = 0.5 * (r1 + r2);
            double deltaV = Mathd.Abs(Mathd.Sqrt(mu / r1) * (Mathd.Sqrt(r2 / at) - 1.0))
                + Mathd.Abs(Mathd.Sqrt(mu / r2) * (1.0 - Mathd.Sqrt(r1 / at)));
            double transfer = Mathd.PI * Mathd.Sqrt(at * at * at / mu);
            double t1 = 2.0 * Mathd.PI * Mathd.Sqrt(r1 * r1 * r1 / mu);
            double t2 = 2.0 * Mathd.PI * Mathd.Sqrt(r2 * r2 * r2 / mu);
            double synodic = 1.0 / Mathd.Abs(1.0 / t1 - 1.0 / t2);
            return (deltaV, transfer, synodic);
        }
    }
}
