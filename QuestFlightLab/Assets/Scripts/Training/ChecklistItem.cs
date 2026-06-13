using System;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class ChecklistItem
    {
        public string label = "";
        public bool required = true;
        public bool completed;
        public string state = "pending";

        public ChecklistItem() { }

        public ChecklistItem(string label, bool required = true)
        {
            this.label = label;
            this.required = required;
        }

        public void SetCompleted(bool value)
        {
            completed = value;
            state = value ? "complete" : "pending";
        }
    }
}
