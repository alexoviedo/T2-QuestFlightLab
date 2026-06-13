using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestFlightLab.Training
{
    public class ChecklistController : MonoBehaviour
    {
        public List<ChecklistItem> beforeTakeoff = new List<ChecklistItem>();

        public bool IsComplete => beforeTakeoff.Count > 0 && beforeTakeoff.All(item => item.completed);

        private void Awake()
        {
            if (beforeTakeoff.Count == 0)
            {
                beforeTakeoff.Add(new ChecklistItem("Flight controls free and correct placeholder"));
                beforeTakeoff.Add(new ChecklistItem("Trim set for takeoff placeholder"));
                beforeTakeoff.Add(new ChecklistItem("Flaps set for takeoff placeholder"));
                beforeTakeoff.Add(new ChecklistItem("Mixture rich placeholder"));
                beforeTakeoff.Add(new ChecklistItem("Carb heat cold placeholder"));
            }
        }

        public void SetAllCompleteForScenario()
        {
            foreach (ChecklistItem item in beforeTakeoff)
            {
                item.completed = true;
            }
        }
    }
}
