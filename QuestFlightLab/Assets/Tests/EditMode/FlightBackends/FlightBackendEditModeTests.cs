#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Flight;
using QuestFlightLab.Flight.Backends;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Tests.EditMode
{
    public class FlightBackendEditModeTests
    {
        [Test]
        public void CentralUnitConversionsMatchDefinedUnits()
        {
            Assert.That(FlightFrameConversions.FeetToMeters * FlightFrameConversions.MetersToFeet, Is.EqualTo(1.0).Within(1e-12));
            Assert.That(FlightFrameConversions.KnotsToMetersPerSecond, Is.EqualTo(0.5144444444444445).Within(1e-12));
            Vector3 unity = FlightFrameConversions.NedFeetPerSecondToUnityMetersPerSecond(10.0, 20.0, 5.0);
            Assert.That(unity.x, Is.EqualTo(6.096f).Within(0.0001f));
            Assert.That(unity.y, Is.EqualTo(-1.524f).Within(0.0001f));
            Assert.That(unity.z, Is.EqualTo(3.048f).Within(0.0001f));
        }

        [Test]
        public void KbduGeodeticEnuRoundTripIsStable()
        {
            GeodeticReference origin = GeodeticReference.Kbdu;
            Assert.That(FlightFrameConversions.GeodeticToUnity(
                origin.latitudeDegrees,
                origin.longitudeDegrees,
                origin.altitudeMslMeters,
                origin).magnitude, Is.LessThan(0.001f));

            Vector3 expected = new Vector3(1250f, 340f, -2180f);
            GeodeticReference point = FlightFrameConversions.UnityToGeodetic(expected, origin);
            Vector3 roundTrip = FlightFrameConversions.GeodeticToUnity(
                point.latitudeDegrees,
                point.longitudeDegrees,
                point.altitudeMslMeters,
                origin);
            Assert.That(Vector3.Distance(expected, roundTrip), Is.LessThan(0.03f));
        }

        [Test]
        public void JsbsimAttitudeMapsHeadingPitchAndBankSignsToUnity()
        {
            Vector3 east = FlightFrameConversions.JsbsimAttitudeToUnity(90.0, 0.0, 0.0) * Vector3.forward;
            Assert.That(Vector3.Distance(east, Vector3.right), Is.LessThan(0.0001f));

            Vector3 pitchedForward = FlightFrameConversions.JsbsimAttitudeToUnity(0.0, 10.0, 0.0) * Vector3.forward;
            Assert.That(pitchedForward.y, Is.GreaterThan(0.17f));

            Vector3 rightWing = FlightFrameConversions.JsbsimAttitudeToUnity(0.0, 0.0, 30.0) * Vector3.right;
            Assert.That(rightWing.y, Is.LessThan(-0.49f));
        }

        [Test]
        public void NativePitchInputPreservesBackendContractNoseUpConvention()
        {
            Assert.That(JSBSimNativeFlightBackend.ContractPitchToJsbsim(0.25), Is.EqualTo(0.25).Within(1e-12));
            Assert.That(JSBSimNativeFlightBackend.ContractPitchToJsbsim(-0.4), Is.EqualTo(-0.4).Within(1e-12));
            Assert.That(JSBSimNativeFlightBackend.ContractPitchToJsbsim(2.0), Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void FixedStepAccumulatorRuns120HzAcross72HzTicks()
        {
            FixedStepAccumulator accumulator = new FixedStepAccumulator(1.0 / 120.0, 8);
            int steps = 0;
            for (int frame = 0; frame < 72; frame++)
            {
                accumulator.Consume(1.0 / 72.0, _ => steps++);
            }

            Assert.That(steps, Is.EqualTo(120));
            Assert.That(accumulator.DroppedSeconds, Is.EqualTo(0.0).Within(1e-10));
        }

        [Test]
        public void AuthorityGuardRejectsDoubleIntegration()
        {
            GameObject root = new GameObject("BackendAuthorityProbe");
            object firstOwner = new object();
            object secondOwner = new object();
            try
            {
                Assert.That(FlightDynamicsAuthority.TryAcquire(root.transform, firstOwner, out FlightDynamicsAuthorityLease first, out string firstError), Is.True, firstError);
                using (first)
                {
                    Assert.That(FlightDynamicsAuthority.TryAcquire(root.transform, secondOwner, out FlightDynamicsAuthorityLease second, out string error), Is.False);
                    Assert.That(second, Is.Null);
                    Assert.That(error, Does.Contain("authoritative"));
                }

                Assert.That(FlightDynamicsAuthority.TryAcquire(root.transform, secondOwner, out FlightDynamicsAuthorityLease afterRelease, out string releaseError), Is.True, releaseError);
                afterRelease.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ReferenceFrameRigExclusivelyOwnsVisualInterpolation()
        {
            GameObject simulation = new GameObject("SimulationRoot");
            GameObject visual = new GameObject("PresentationRoot");
            visual.transform.SetParent(simulation.transform, false);
            try
            {
                Assert.That(FlightDynamicsCoordinator.ShouldCoordinatorInterpolate(simulation.transform, visual.transform), Is.True);
                AircraftReferenceFrameRig rig = simulation.AddComponent<AircraftReferenceFrameRig>();
                Assert.That(rig.enabled, Is.True);
                Assert.That(FlightDynamicsCoordinator.ShouldCoordinatorInterpolate(simulation.transform, visual.transform), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(simulation);
            }
        }

        [Test]
        public void DemoEnvelopeCannotDoubleIntegrateAnInitializedCoordinator()
        {
            Assert.That(typeof(ShortPlaytestDemoPilot).GetMethod(
                "ApplyVisualFlightEnvelope",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
                Is.Null,
                "The runtime demo must only supply controls; it must never overwrite authoritative pose.");
        }

        [Test]
        public void UnityPrototypeBackendRemainsFunctionalFallback()
        {
            GameObject root = new GameObject("UnityBackendProbe");
            AircraftState state = root.AddComponent<AircraftState>();
            SimpleAircraftPhysics physics = root.AddComponent<SimpleAircraftPhysics>();
            physics.state = state;
            try
            {
                using UnityPrototypeFlightBackend backend = new UnityPrototypeFlightBackend();
                Assert.That(backend.Initialize(new FlightDynamicsBackendContext
                {
                    simulationRoot = root.transform,
                    aircraftState = state,
                    unityPrototype = physics,
                    localOrigin = GeodeticReference.Kbdu
                }), Is.True, backend.LastError);
                Assert.That(backend.Reset(FlightDynamicsInitialConditions.KbduRunway()), Is.True, backend.LastError);
                backend.SetControls(AircraftControlState.Neutral(0.8f));
                Assert.That(backend.Advance(1.0 / 120.0), Is.True, backend.LastError);
                Assert.That(backend.CurrentState.IsFinite, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StagedNativePluginLoadsC172xAndAdvances()
        {
            Assert.That(JSBSimNativeFlightBackend.ProbeLibrary(out string probeError), Is.True, probeError);
            GameObject root = new GameObject("NativeBackendProbe");
            try
            {
                using JSBSimNativeFlightBackend backend = new JSBSimNativeFlightBackend();
                Assert.That(backend.Initialize(new FlightDynamicsBackendContext
                {
                    simulationRoot = root.transform,
                    localOrigin = GeodeticReference.Kbdu,
                    jsbsimAircraft = "c172x",
                    jsbsimDataRoot = JSBSimRuntimeDataPaths.BundledDataRoot
                }), Is.True, backend.LastError);
                Assert.That(backend.Reset(FlightDynamicsInitialConditions.KbduRunway()), Is.True, backend.LastError);
                backend.SetControls(AircraftControlState.Neutral(0.25f));
                for (int i = 0; i < 120; i++)
                {
                    Assert.That(backend.Advance(1.0 / 120.0), Is.True, $"step {i}: {backend.LastError}");
                }
                Assert.That(backend.CurrentState.IsFinite, Is.True);
                Assert.That(backend.CurrentState.simulationTimeSeconds, Is.EqualTo(1.0).Within(0.001));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
