using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using QuestFlightLab.Training;
using UnityEngine;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class FlightTimelineSample
    {
        public float timestamp;
        public float positionX;
        public float positionY;
        public float positionZ;
        public float altitudeFt;
        public float airspeedKts;
        public float verticalSpeedFpm;
        public float headingDeg;
        public float pitchDeg;
        public float bankDeg;
        public float throttle;
        public float flapsDeg;
        public float trim;
        public float aileron;
        public float elevator;
        public float rudder;
        public string approachPhase = "";
        public bool stableApproach;
        public bool goAroundRequired;
        public bool goAroundInitiated;
        public string gateId = "";
        public float scoreDelta;
        public string warnings = "";
    }

    [Serializable]
    public class ReplayMarker
    {
        public float timestamp;
        public string markerType = "";
        public string id = "";
        public string note = "";
    }

    [Serializable]
    public class DebriefTimeline
    {
        public string scenarioId = "";
        public string generatedUtc = "";
        public int sampleCount;
        public int markerCount;
        public List<FlightTimelineSample> samples = new List<FlightTimelineSample>();
        public List<ReplayMarker> markers = new List<ReplayMarker>();
    }

    public static class FlightTimelineRecorder
    {
        public static DebriefTimeline BuildTimeline(FlightScenarioResult result)
        {
            DebriefTimeline timeline = new DebriefTimeline
            {
                scenarioId = result != null ? result.id : "",
                generatedUtc = DateTime.UtcNow.ToString("o")
            };

            if (result == null) return timeline;

            bool stableGateMarked = false;
            bool goAroundRequiredMarked = false;
            bool goAroundInitiatedMarked = false;
            bool touchdownMarked = false;

            foreach (FlightScenarioSample sample in result.samples)
            {
                FlightTimelineSample item = new FlightTimelineSample
                {
                    timestamp = sample.timestamp,
                    positionX = sample.positionX,
                    positionY = sample.positionY,
                    positionZ = sample.positionZ,
                    altitudeFt = sample.flight != null ? sample.flight.altitudeFt : 0f,
                    airspeedKts = sample.flight != null ? sample.flight.airspeedKts : 0f,
                    verticalSpeedFpm = sample.flight != null ? sample.flight.verticalSpeedFpm : 0f,
                    headingDeg = sample.flight != null ? sample.flight.headingDeg : 0f,
                    pitchDeg = sample.flight != null ? sample.flight.pitchDeg : 0f,
                    bankDeg = sample.flight != null ? sample.flight.bankDeg : 0f,
                    throttle = sample.controls != null ? sample.controls.throttle : 0f,
                    flapsDeg = sample.flight != null ? sample.flight.flapDegrees : 0f,
                    trim = sample.controls != null ? sample.controls.trim : 0f,
                    aileron = sample.controls != null ? sample.controls.aileron : 0f,
                    elevator = sample.controls != null ? sample.controls.elevator : 0f,
                    rudder = sample.controls != null ? sample.controls.rudder : 0f,
                    approachPhase = sample.approachPhase,
                    stableApproach = sample.stableApproach,
                    goAroundRequired = sample.goAroundRequired,
                    goAroundInitiated = sample.goAroundInitiated,
                    gateId = sample.gateId,
                    scoreDelta = sample.scoreDelta,
                    warnings = sample.approachWarningSummary
                };
                timeline.samples.Add(item);

                if (!stableGateMarked && sample.stableApproach && sample.gateId == StabilizedApproachLesson.StableGate300Agl.id)
                {
                    timeline.markers.Add(new ReplayMarker
                    {
                        timestamp = sample.timestamp,
                        markerType = "approach_gate",
                        id = sample.gateId,
                        note = "Stable approach gate met"
                    });
                    stableGateMarked = true;
                }

                if (!goAroundRequiredMarked && sample.goAroundRequired)
                {
                    timeline.markers.Add(new ReplayMarker
                    {
                        timestamp = sample.timestamp,
                        markerType = "decision",
                        id = "go_around_required",
                        note = sample.approachWarningSummary
                    });
                    goAroundRequiredMarked = true;
                }

                if (!goAroundInitiatedMarked && sample.goAroundInitiated)
                {
                    timeline.markers.Add(new ReplayMarker
                    {
                        timestamp = sample.timestamp,
                        markerType = "maneuver",
                        id = "go_around_initiated",
                        note = "High power and climb response observed"
                    });
                    goAroundInitiatedMarked = true;
                }

                if (!touchdownMarked && sample.flight != null && sample.flight.onGround && sample.timestamp > result.durationSeconds * 0.65f)
                {
                    timeline.markers.Add(new ReplayMarker
                    {
                        timestamp = sample.timestamp,
                        markerType = "endpoint",
                        id = "touchdown_or_reset_placeholder",
                        note = "Ground contact/reset endpoint observed"
                    });
                    touchdownMarked = true;
                }
            }

            timeline.sampleCount = timeline.samples.Count;
            timeline.markerCount = timeline.markers.Count;
            return timeline;
        }

        public static void ExportTimeline(DebriefTimeline timeline, string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "timeline.json"), JsonUtility.ToJson(timeline, true));
            File.WriteAllText(Path.Combine(directory, "timeline.csv"), BuildCsv(timeline));
        }

        private static string BuildCsv(DebriefTimeline timeline)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("timestamp,position_x,position_y,position_z,airspeed_kts,altitude_ft,vsi_fpm,heading_deg,pitch_deg,bank_deg,throttle,flaps_deg,trim,aileron,elevator,rudder,approach_phase,stable,goaround_required,goaround_initiated,gate_id,score_delta,warnings");
            if (timeline == null) return sb.ToString();

            foreach (FlightTimelineSample s in timeline.samples)
            {
                sb.Append(F(s.timestamp)).Append(',')
                    .Append(F(s.positionX)).Append(',')
                    .Append(F(s.positionY)).Append(',')
                    .Append(F(s.positionZ)).Append(',')
                    .Append(F(s.airspeedKts)).Append(',')
                    .Append(F(s.altitudeFt)).Append(',')
                    .Append(F(s.verticalSpeedFpm)).Append(',')
                    .Append(F(s.headingDeg)).Append(',')
                    .Append(F(s.pitchDeg)).Append(',')
                    .Append(F(s.bankDeg)).Append(',')
                    .Append(F(s.throttle)).Append(',')
                    .Append(F(s.flapsDeg)).Append(',')
                    .Append(F(s.trim)).Append(',')
                    .Append(F(s.aileron)).Append(',')
                    .Append(F(s.elevator)).Append(',')
                    .Append(F(s.rudder)).Append(',')
                    .Append(Escape(s.approachPhase)).Append(',')
                    .Append(s.stableApproach).Append(',')
                    .Append(s.goAroundRequired).Append(',')
                    .Append(s.goAroundInitiated).Append(',')
                    .Append(Escape(s.gateId)).Append(',')
                    .Append(F(s.scoreDelta)).Append(',')
                    .Append(Escape(s.warnings)).AppendLine();
            }

            return sb.ToString();
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Escape(string value)
        {
            value ??= "";
            return value.Contains(",") || value.Contains("\"")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
