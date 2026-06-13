using System;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class LessonStep
    {
        public string id = "";
        public string prompt = "";
        public string successHint = "";
        public string warningHint = "";
        public float targetAirspeedKts;
        public float airspeedToleranceKts;
        public float targetHeadingDeg;
        public float headingToleranceDeg;
        public float minimumSeconds = 2f;

        public LessonStep() { }

        public LessonStep(string id, string prompt, string successHint, float minimumSeconds = 2f, string warningHint = "")
        {
            this.id = id;
            this.prompt = prompt;
            this.successHint = successHint;
            this.minimumSeconds = minimumSeconds;
            this.warningHint = warningHint;
        }
    }
}
