using System;
using System.Collections.Generic;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class LessonSequence
    {
        public string id = "";
        public string title = "";
        public List<LessonStep> steps = new List<LessonStep>();

        public static LessonSequence BasicTakeoffFamiliarization()
        {
            return new LessonSequence
            {
                id = "basic_takeoff_familiarization",
                title = "Basic Takeoff Familiarization",
                steps = new List<LessonStep>
                {
                    new LessonStep("before_takeoff", "Complete before-takeoff checklist placeholders", "Checklist acknowledged", 1f),
                    new LessonStep("line_up", "Line up on Runway 08/26 centerline", "Small runway offset", 1f),
                    new LessonStep("smooth_throttle", "Smoothly apply full throttle", "Power above 85%", 1f, "Avoid abrupt idle-to-full oscillation"),
                    new LessonStep("maintain_centerline", "Maintain centerline with rudder", "Runway offset controlled", 2f, "Use rudder before large drift develops"),
                    new LessonStep("rotate", "Rotate near placeholder Vr", "Pitch up near Vr", 1f, "Do not over-rotate into stall warning") { targetAirspeedKts = 55f, airspeedToleranceKts = 7f },
                    new LessonStep("climb_vy", "Climb near placeholder Vy", "Positive climb near Vy", 2f, "Hold pitch for speed; avoid chasing vertical speed") { targetAirspeedKts = 74f, airspeedToleranceKts = 10f },
                    new LessonStep("maintain_runway_heading", "Maintain runway heading and positive climb", "Heading stable with climb", 2f) { targetHeadingDeg = 78f, headingToleranceDeg = 12f },
                    new LessonStep("after_takeoff_cleanup", "After-takeoff cleanup placeholder", "Positive climb, flaps checked, task complete", 1f)
                }
            };
        }
    }
}
