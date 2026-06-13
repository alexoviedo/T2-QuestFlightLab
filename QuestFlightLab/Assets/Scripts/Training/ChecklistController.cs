using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuestFlightLab.Training
{
    public class ChecklistController : MonoBehaviour
    {
        public List<ChecklistItem> beforeTakeoff = new List<ChecklistItem>();

        public bool IsComplete => beforeTakeoff.Count > 0 && beforeTakeoff.All(item => item.completed);
        public string StatusSummary => $"{beforeTakeoff.Count(item => item.completed)}/{beforeTakeoff.Count} before-takeoff items";

        private void Awake()
        {
            if (beforeTakeoff.Count == 0)
            {
                foreach (string label in DefaultBeforeTakeoffItemLabels())
                {
                    beforeTakeoff.Add(new ChecklistItem(label));
                }
            }
        }

        public void SetAllCompleteForScenario()
        {
            foreach (ChecklistItem item in beforeTakeoff)
            {
                item.SetCompleted(true);
            }
        }

        public static string[] DefaultBeforeTakeoffItemLabels()
        {
            return new[]
            {
                "Flight controls free and correct placeholder",
                "Elevator trim set for takeoff placeholder",
                "Flaps set for takeoff placeholder",
                "Mixture rich placeholder",
                "Carb heat cold placeholder",
                "Engine instruments in green placeholder",
                "Doors/windows secure placeholder",
                "Runway heading and departure plan briefed placeholder"
            };
        }
    }
}
