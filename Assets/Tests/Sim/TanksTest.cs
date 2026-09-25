using NUnit.Framework;
using OuterSpace.Sim;

public class TanksTest
{
    [Test]
    public void Refill_StopsAtCapacity()
    {
        Tanks tanks = new() { methane = 100.0, lox = 50.0, methaneCapacity = 400.0, loxCapacity = 150.0 };
        tanks.Refill(1000.0, 60.0);
        Assert.AreEqual(400.0, tanks.methane);
        Assert.AreEqual(110.0, tanks.lox);
    }
}
