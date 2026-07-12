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
        public void FlightStateFiniteContractCoversTelemetryAndRates()
        {
            FlightDynamicsState state = new FlightDynamicsState
            {
                rotationUnity = Quaternion.identity
            };
            Assert.That(state.IsFinite, Is.True);
            state.engineRpm = double.NaN;
            Assert.That(state.IsFinite, Is.False);
            state.engineRpm = 900.0;
            state.angularVelocityBodyDegreesPerSecond = new Vector3(float.PositiveInfinity, 0f, 0f);
            Assert.That(state.IsFinite, Is.False);
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
        public void NativePitchInputConvertsProjectNoseUpToStockC172xConvention()
        {
            Assert.That(JSBSimNativeFlightBackend.ContractPitchToJsbsim(0.25), Is.EqualTo(-0.25).Within(1e-12));
            Assert.That(JSBSimNativeFlightBackend.ContractPitchToJsbsim(-0.4), Is.EqualTo(0.4).Within(1e-12));
            Assert.That(JSBSimNativeFlightBackend.ContractPitchToJsbsim(2.0), Is.EqualTo(-1.0).Within(1e-12));
        }

        [Test]
        public void NativeRudderInputConvertsProjectRightYawToStockC172xConvention()
        {
            Assert.That(JSBSimNativeFlightBackend.ContractRudderToJsbsim(0.25), Is.EqualTo(-0.25).Within(1e-12));
            Assert.That(JSBSimNativeFlightBackend.ContractRudderToJsbsim(-0.4), Is.EqualTo(0.4).Within(1e-12));
            Assert.That(JSBSimNativeFlightBackend.ContractRudderToJsbsim(-2.0), Is.EqualTo(1.0).Within(1e-12));
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
        public void CoordinatorPublishesReadOnlySnapshotAtBackendControlBoundary()
        {
            GameObject root = new GameObject("CoordinatorControlSnapshotProbe");
            AircraftState state = root.AddComponent<AircraftState>();
            SimpleAircraftPhysics physics = root.AddComponent<SimpleAircraftPhysics>();
            physics.state = state;
            FlightDynamicsCoordinator coordinator = root.AddComponent<FlightDynamicsCoordinator>();
            coordinator.requestedBackend = FlightDynamicsBackendKind.UnityPrototype;
            coordinator.simulationRoot = root.transform;
            coordinator.presentationRoot = root.transform;
            coordinator.aircraftState = state;
            coordinator.unityPrototype = physics;
            try
            {
                Assert.That(coordinator.InitializeSelectedBackend(), Is.True, coordinator.LastError);
                AircraftControlState input = AircraftControlState.Neutral(0.64f);
                input.aileron = -0.21f;
                input.elevator = 0.17f;
                input.rudder = 0.06f;
                input.trim = 0.08f;
                input.flaps = 0.33f;
                Assert.That(coordinator.StepForTest(input, 1.0 / 120.0), Is.True, coordinator.LastError);

                FlightDynamicsControlSnapshot snapshot = coordinator.LastAppliedControls;
                Assert.That(snapshot.aileron, Is.EqualTo(-0.21f).Within(1e-6f));
                Assert.That(snapshot.elevator, Is.EqualTo(0.17f).Within(1e-6f));
                Assert.That(snapshot.throttle, Is.EqualTo(0.64f).Within(1e-6f));
                Assert.That(snapshot.trim, Is.EqualTo(0.08f).Within(1e-6f));
                Assert.That(snapshot.flaps, Is.EqualTo(0.33f).Within(1e-6f));

                snapshot.elevator = -1f;
                Assert.That(coordinator.LastAppliedControls.elevator, Is.EqualTo(0.17f).Within(1e-6f),
                    "Consumers receive a value copy and cannot mutate authoritative input.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CoordinatorUsesAndPersistsAuthoredInitialConditionsAcrossReset()
        {
            GameObject spawn = new GameObject("AuthoredFlightSpawn");
            FlightDynamicsInitialConditionProvider provider = spawn.AddComponent<FlightDynamicsInitialConditionProvider>();
            provider.derivePositionFromSpawnTransform = false;
            provider.deriveHeadingFromSpawnTransform = false;
            provider.latitudeDegrees = GeodeticReference.Kbdu.latitudeDegrees;
            provider.longitudeDegrees = GeodeticReference.Kbdu.longitudeDegrees;
            provider.altitudeMslMeters = GeodeticReference.Kbdu.altitudeMslMeters + 50.0;
            provider.terrainElevationMslMeters = GeodeticReference.Kbdu.altitudeMslMeters;
            provider.headingDegrees = 123.0;

            GameObject root = new GameObject("AuthoredInitialConditionsProbe");
            AircraftState state = root.AddComponent<AircraftState>();
            SimpleAircraftPhysics physics = root.AddComponent<SimpleAircraftPhysics>();
            physics.state = state;
            FlightDynamicsCoordinator coordinator = root.AddComponent<FlightDynamicsCoordinator>();
            coordinator.requestedBackend = FlightDynamicsBackendKind.UnityPrototype;
            coordinator.simulationRoot = root.transform;
            coordinator.presentationRoot = root.transform;
            coordinator.aircraftState = state;
            coordinator.unityPrototype = physics;
            coordinator.initialConditionProvider = provider;
            try
            {
                Assert.That(coordinator.InitializeSelectedBackend(), Is.True, coordinator.LastError);
                Assert.That(root.transform.position.y, Is.EqualTo(50f).Within(0.02f));
                Assert.That(Mathf.DeltaAngle(root.transform.eulerAngles.y, 123f), Is.EqualTo(0f).Within(0.01f));
                Assert.That(coordinator.LastResetInitialConditions.headingDegrees, Is.EqualTo(123.0).Within(1e-8));

                // The reset contract persists the state resolved at backend
                // initialization even if authoring fields or pose later move.
                provider.headingDegrees = 210.0;
                provider.altitudeMslMeters += 100.0;
                root.transform.SetPositionAndRotation(new Vector3(500f, 500f, 500f), Quaternion.identity);
                Assert.That(coordinator.ResetToConfiguredInitialConditions(), Is.True, coordinator.LastError);
                Assert.That(root.transform.position.y, Is.EqualTo(50f).Within(0.02f));
                Assert.That(Mathf.DeltaAngle(root.transform.eulerAngles.y, 123f), Is.EqualTo(0f).Within(0.01f));
                Assert.That(coordinator.LastAppliedControls.throttle, Is.EqualTo(0.10f).Within(1e-6f));
                Assert.That(coordinator.LastAppliedControls.mixture, Is.EqualTo(1f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(spawn);
            }
        }

        [Test]
        public void ProductionMarkerPreventsLaunchOptionFromMutatingAuthoredBackend()
        {
            string environmentKey = QuestLaunchOptions.FlightBackendKey.ToUpperInvariant();
            string previous = System.Environment.GetEnvironmentVariable(environmentKey);
            GameObject marker = new GameObject("ProductionVerticalSliceRootTestMarker");
            marker.AddComponent<ProductionVerticalSliceRoot>();
            GameObject aircraft = new GameObject("AuthoredProductionAircraft");
            AircraftState state = aircraft.AddComponent<AircraftState>();
            SimpleAircraftPhysics prototype = aircraft.AddComponent<SimpleAircraftPhysics>();
            prototype.state = state;
            FlightDynamicsCoordinator authored = aircraft.AddComponent<FlightDynamicsCoordinator>();
            authored.requestedBackend = FlightDynamicsBackendKind.UnityPrototype;
            try
            {
                System.Environment.SetEnvironmentVariable(environmentKey, "jsbsim_native");
                System.Reflection.MethodInfo bootstrap = typeof(FlightDynamicsRuntimeBootstrap).GetMethod(
                    "BootstrapRequestedBackend",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(bootstrap, Is.Not.Null);
                bootstrap.Invoke(null, null);
                Assert.That(aircraft.GetComponents<FlightDynamicsCoordinator>().Length, Is.EqualTo(1));
                Assert.That(authored.requestedBackend, Is.EqualTo(FlightDynamicsBackendKind.UnityPrototype));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(environmentKey, previous);
                Object.DestroyImmediate(aircraft);
                Object.DestroyImmediate(marker);
            }
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
