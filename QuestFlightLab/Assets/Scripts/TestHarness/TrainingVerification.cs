using System;
using System.Collections.Generic;
using System.Linq;
using QuestFlightLab.Training;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class TrainingVerificationSnapshot
    {
        public string lessonTitle = "";
        public bool checklistModelPresent;
        public int checklistItemCount;
        public bool allRequiredStepsPresent;
        public string matchedScenarioStep = "";
        public List<string> missingStepIds = new List<string>();
        public string summary = "";
    }

    public static class TrainingVerification
    {
        private static readonly string[] RequiredStepIds =
        {
            "before_takeoff",
            "line_up",
            "smooth_throttle",
            "maintain_centerline",
            "rotate",
            "climb_vy",
            "maintain_runway_heading",
            "after_takeoff_cleanup"
        };

        public static TrainingVerificationSnapshot Capture(string scenarioId)
        {
            LessonSequence lesson = LessonSequence.BasicTakeoffFamiliarization();
            HashSet<string> available = new HashSet<string>(lesson.steps.Select(step => step.id));
            List<string> missing = RequiredStepIds.Where(id => !available.Contains(id)).ToList();
            string matched = MapScenarioToStep(scenarioId);

            return new TrainingVerificationSnapshot
            {
                lessonTitle = lesson.title,
                checklistModelPresent = true,
                checklistItemCount = ChecklistController.DefaultBeforeTakeoffItemLabels().Length,
                allRequiredStepsPresent = missing.Count == 0,
                matchedScenarioStep = matched,
                missingStepIds = missing,
                summary = missing.Count == 0
                    ? $"PASS lesson scaffold present; scenario maps to {matched}"
                    : $"MISSING lesson steps: {string.Join(";", missing)}"
            };
        }

        private static string MapScenarioToStep(string scenarioId)
        {
            return scenarioId switch
            {
                "before_takeoff_checklist" => "before_takeoff",
                "takeoff_roll_to_vr" => "smooth_throttle",
                "rotation_climb_to_altitude" => "rotate",
                "vy_climb_stabilization" => "climb_vy",
                "pattern_leg_heading_change" => "maintain_runway_heading",
                _ => "not lesson-gated"
            };
        }
    }
}
