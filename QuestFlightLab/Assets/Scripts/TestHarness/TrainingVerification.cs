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
            "takeoff_roll",
            "rotate_vr",
            "upwind_climb",
            "crosswind_turn",
            "downwind_level_configure",
            "abeam_power_reduction",
            "base_turn",
            "final_alignment",
            "flare_or_go_around",
            "after_landing_reset"
        };

        public static TrainingVerificationSnapshot Capture(string scenarioId)
        {
            LessonSequence lesson = LessonSequence.BasicTrafficPatternFamiliarization();
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
                "traffic_pattern_phase_progression" => "crosswind_turn",
                "traffic_pattern_scoring_debrief" => "final_alignment",
                "basic_traffic_pattern_full" => "basic_traffic_pattern",
                "lesson_panel_prompt_update" => "downwind_level_configure",
                "takeoff_roll_to_vr" => "takeoff_roll",
                "rotation_climb_to_altitude" => "rotate_vr",
                "vy_climb_stabilization" => "upwind_climb",
                "pattern_leg_heading_change" => "crosswind_turn",
                _ => "not lesson-gated"
            };
        }
    }
}
