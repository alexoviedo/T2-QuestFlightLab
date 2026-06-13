#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using QuestFlightLab.Input;
using QuestFlightLab.Runtime;
using UnityEngine;

namespace QuestFlightLab.Tests.PlayMode
{
    public class QuestFlightLabPlayModeTests
    {
        [Test]
        public void DeterministicInputSourcePublishesMappedControls()
        {
            GameObject go = new GameObject("DeterministicInputPlayModeProbe");
            DeterministicGamepadInputSource source = go.AddComponent<DeterministicGamepadInputSource>();
            Usb2BleInputMapper mapper = go.AddComponent<Usb2BleInputMapper>();
            mapper.deterministicInput = source;
            mapper.preferDeterministicInput = true;

            source.SetControls(new AircraftControlState
            {
                aileron = 0.75f,
                elevator = 0.25f,
                rudder = -0.5f,
                throttle = 0.8f,
                mixture = 1f,
                trim = 0.2f,
                flaps = 0.5f,
                leftToeBrake = 0.3f,
                rightToeBrake = 0.4f
            });

            mapper.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            mapper.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            Assert.That(mapper.Current.aileron, Is.EqualTo(0.75f).Within(0.01f));
            Assert.That(mapper.Current.elevator, Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(mapper.Current.rudder, Is.EqualTo(-0.5f).Within(0.01f));
            Assert.That(mapper.Current.throttle, Is.EqualTo(0.8f).Within(0.01f));
            Assert.That(mapper.Current.leftToeBrake, Is.EqualTo(0.3f).Within(0.01f));

            Object.Destroy(go);
        }
    }
}
#endif
