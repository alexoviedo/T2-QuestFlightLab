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
            "after_landing_reset",
            "downwind_stabilized_setup",
            "stabilized_approach_gate",
            "go_around_decision",
            "go_around_power_pitch_config",
            "landing_touchdown_placeholder",
            "after_landing_or_reset"
        };

        public static TrainingVerificationSnapshot Capture(string scenarioId)
        {
            LessonSequence trafficLesson = LessonSequence.BasicTrafficPatternFamiliarization();
            LessonSequence approachLesson = LessonSequence.StabilizedApproachGoAroundFamiliarization();
            HashSet<string> available = new HashSet<string>(trafficLesson.steps.Select(step => step.id));
            foreach (LessonStep step in approachLesson.steps)
            {
                available.Add(step.id);
            }
            List<string> missing = RequiredStepIds.Where(id => !available.Contains(id)).ToList();
            string matched = MapScenarioToStep(scenarioId);

            return new TrainingVerificationSnapshot
            {
                lessonTitle = $"{trafficLesson.title}; {approachLesson.title}",
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
                "stabilized_final_approach" => "stabilized_approach_gate",
                "high_unstable_approach_goaround" => "go_around_decision",
                "low_unstable_approach_goaround" => "go_around_decision",
                "excessive_sink_rate_goaround" => "go_around_decision",
                "go_around_sequence" => "go_around_power_pitch_config",
                "timeline_export_replay_markers" => "go_around_power_pitch_config",
                "instrument_approach_status_verification" => "stabilized_approach_gate",
                "pattern_to_final_transition" => "final_intercept",
                "stable_touchdown_placeholder" => "landing_touchdown_placeholder",
                "reset_after_goaround" => "after_landing_or_reset",
                _ => "not lesson-gated"
            };
        }
    }
}
