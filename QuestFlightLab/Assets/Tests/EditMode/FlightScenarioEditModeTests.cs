#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.TestHarness;

namespace QuestFlightLab.Tests.EditMode
{
    public class FlightScenarioEditModeTests
    {
        [Test]
        public void DefaultScenarioSuitePassesAcceptanceChecks()
        {
            FlightScenarioSuiteResult suite = FlightScenarioRunner.RunDefaultSuite();
            Assert.AreEqual(0, suite.failedCount, "All deterministic flight-core scenarios should pass.");
        }
    }
}
#endif
