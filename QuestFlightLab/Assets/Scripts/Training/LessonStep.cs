using System;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class LessonStep
    {
        public string id = "";
        public string prompt = "";
        public string successHint = "";
        public float minimumSeconds = 2f;

        public LessonStep() { }

        public LessonStep(string id, string prompt, string successHint, float minimumSeconds = 2f)
        {
            this.id = id;
            this.prompt = prompt;
            this.successHint = successHint;
            this.minimumSeconds = minimumSeconds;
        }
    }
}
