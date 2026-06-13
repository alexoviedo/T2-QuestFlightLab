using System;

namespace QuestFlightLab.Training
{
    [Serializable]
    public class ChecklistItem
    {
        public string label = "";
        public bool completed;

        public ChecklistItem() { }

        public ChecklistItem(string label)
        {
            this.label = label;
        }
    }
}
