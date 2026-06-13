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
                    new LessonStep("before_takeoff", "Before takeoff checklist placeholder", "Checklist acknowledged"),
                    new LessonStep("align_runway", "Align on Runway 08/26 centerline", "Small runway offset"),
                    new LessonStep("smooth_throttle", "Smoothly apply throttle", "Power above 80%"),
                    new LessonStep("maintain_centerline", "Maintain centerline with rudder", "Runway offset controlled"),
                    new LessonStep("rotate", "Rotate near placeholder Vr", "Pitch up near Vr"),
                    new LessonStep("climb_vy", "Climb near placeholder Vy", "Positive climb near Vy"),
                    new LessonStep("maintain_heading", "Maintain heading and positive climb", "Heading stable with climb")
                }
            };
        }
    }
}
