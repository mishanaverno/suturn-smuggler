using NUnit.Framework;
using OuterSpace.Sim.Objects;

public class ControlStepTest
{
    const double ParkingPeriod = 10757.0;
    const double TransferPeriod = 1000.0 * ParkingPeriod;

    [Test]
    public void TimeStep_IsTheSameFraction_OnOrbitsThousandTimesApart()
    {
        double parking = Ship.TimeStep(ParkingPeriod, 1.0);
        double transfer = Ship.TimeStep(TransferPeriod, 1.0);

        Assert.AreEqual(parking / ParkingPeriod, transfer / TransferPeriod, 1e-12);
        Assert.AreEqual(Ship.TimeStepFraction, parking / ParkingPeriod, 1e-12);
    }

    [Test]
    public void FineScale_ShrinksStep_ExactlyAsDeclared()
    {
        double scale = Ship.StepScale(coarse: false, fine: true);
        Assert.AreEqual(Ship.FineFactor, scale);

        Assert.AreEqual(Ship.FineFactor, Ship.TimeStep(ParkingPeriod, scale) / Ship.TimeStep(ParkingPeriod, 1.0), 1e-12);
        Assert.AreEqual(Ship.FineFactor, Ship.SpeedStep(1737.0, scale) / Ship.SpeedStep(1737.0, 1.0), 1e-12);
        Assert.AreEqual(Ship.CoarseFactor, Ship.StepScale(coarse: true, fine: false));
    }
}
