using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace QuestFlightLab.Environment
{
    [Serializable]
    public class SceneryBudgetEstimate
    {
        public int splatCount;
        public string sampleAssetPath;
        public long assetBytes;
        public float assetMegabytes;
        public long estimatedGpuBytes;
        public float estimatedGpuMegabytes;
        public string status;
        public string notes;
    }

    [Serializable]
    public class ScenerySpikeReport
    {
        public string generatedUtc;
        public string unityVersion;
        public string graphicsDeviceType;
        public string renderPipeline;
        public string rendererPackage;
        public string viabilityClassification;
        public List<SceneryProviderStatus> providerStatuses = new List<SceneryProviderStatus>();
        public List<SceneryBudgetEstimate> budgets = new List<SceneryBudgetEstimate>();
        public List<string> limitations = new List<string>();
    }

    public static class SceneryEvidenceLogger
    {
        public static readonly int[] DefaultBudgets = { 5000, 50000, 100000, 200000, 400000 };

        public static List<SceneryBudgetEstimate> BuildBudgetEstimates(string sampleDirectory)
        {
            List<SceneryBudgetEstimate> estimates = new List<SceneryBudgetEstimate>();
            foreach (int count in DefaultBudgets)
            {
                estimates.Add(BuildBudgetEstimate(count, sampleDirectory));
            }

            return estimates;
        }

        public static SceneryBudgetEstimate BuildBudgetEstimate(int splatCount, string sampleDirectory)
        {
            string path = string.Empty;
            long bytes = 0L;
            if (!string.IsNullOrWhiteSpace(sampleDirectory))
            {
                string candidate = Path.Combine(sampleDirectory, $"synthetic_splats_{splatCount}.ply");
                if (File.Exists(candidate))
                {
                    path = candidate;
                    bytes = new FileInfo(candidate).Length;
                }
            }

            string status = bytes > 0 ? "synthetic_sample_generated" : "estimated_only";
            string notes = bytes > 0
                ? "Synthetic PLY generated for size/budget plumbing; not a production photogrammetry capture."
                : "No sample asset generated at this budget; estimate retained for Quest 3 budget planning.";

            return new SceneryBudgetEstimate
            {
                splatCount = splatCount,
                sampleAssetPath = path,
                assetBytes = bytes,
                assetMegabytes = bytes / (1024f * 1024f),
                estimatedGpuBytes = SceneryPerformanceProbe.EstimateGpuBytes(splatCount),
                estimatedGpuMegabytes = SceneryPerformanceProbe.EstimateGpuMegabytes(splatCount),
                status = status,
                notes = notes
            };
        }

        public static void WriteReport(ScenerySpikeReport report, string artifactDir)
        {
            Directory.CreateDirectory(artifactDir);
            File.WriteAllText(Path.Combine(artifactDir, "splat_editor_results.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(artifactDir, "splat_editor_results.csv"), BuildCsv(report));
            File.WriteAllText(Path.Combine(artifactDir, "splat_spike_summary.md"), BuildMarkdown(report));
        }

        public static string Classify(ScenerySpikeReport report)
        {
            bool rendererDetected = report.providerStatuses.Exists(s => s.rendererAvailable);
            bool androidBroken = report.providerStatuses.Exists(s => s.warnings.Exists(w => w.IndexOf("Android build failed", StringComparison.OrdinalIgnoreCase) >= 0));
            if (androidBroken) return "blocked_android_build";
            if (!rendererDetected) return "defer_to_later";
            if (report.providerStatuses.Exists(s => s.activeMode == SceneryMode.ExperimentalSplatRenderer.ToString()))
            {
                return "partial_editor_only";
            }

            return "defer_to_later";
        }

        private static string BuildCsv(ScenerySpikeReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("kind,name_or_count,status,asset_mb,estimated_gpu_mb,active_mode,fallback_used,notes");
            foreach (SceneryProviderStatus status in report.providerStatuses)
            {
                builder.Append("provider,");
                builder.Append(Escape(status.providerName));
                builder.Append(',');
                builder.Append(Escape(status.rendererAvailable ? "renderer_detected" : "renderer_missing"));
                builder.Append(',');
                builder.Append((status.assetBytes / (1024f * 1024f)).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append((status.estimatedGpuBytes / (1024f * 1024f)).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(Escape(status.activeMode));
                builder.Append(',');
                builder.Append(status.fallbackUsed ? "true" : "false");
                builder.Append(',');
                builder.AppendLine(Escape(string.Join("; ", status.warnings)));
            }

            foreach (SceneryBudgetEstimate budget in report.budgets)
            {
                builder.Append("budget,");
                builder.Append(budget.splatCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(Escape(budget.status));
                builder.Append(',');
                builder.Append(budget.assetMegabytes.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(budget.estimatedGpuMegabytes.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(",,,");
                builder.AppendLine(Escape(budget.notes));
            }

            return builder.ToString();
        }

        private static string BuildMarkdown(ScenerySpikeReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Gaussian Splat Spike Summary");
            builder.AppendLine();
            builder.AppendLine($"Generated UTC: {report.generatedUtc}");
            builder.AppendLine($"Unity: {report.unityVersion}");
            builder.AppendLine($"Graphics device: {report.graphicsDeviceType}");
            builder.AppendLine($"Render pipeline: {report.renderPipeline}");
            builder.AppendLine($"Renderer/package: {report.rendererPackage}");
            builder.AppendLine($"Viability classification: `{report.viabilityClassification}`");
            builder.AppendLine();
            builder.AppendLine("## Provider Results");
            builder.AppendLine();
            foreach (SceneryProviderStatus status in report.providerStatuses)
            {
                builder.AppendLine($"- {status.providerName}: requested `{status.requestedMode}`, active `{status.activeMode}`, fallback `{status.fallbackUsed}`, renderer available `{status.rendererAvailable}`.");
                foreach (string warning in status.warnings)
                {
                    builder.AppendLine($"  - {warning}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Budget Estimates");
            builder.AppendLine();
            builder.AppendLine("| Splats | Asset MB | Estimated GPU MB | Status |");
            builder.AppendLine("| ---: | ---: | ---: | --- |");
            foreach (SceneryBudgetEstimate budget in report.budgets)
            {
                builder.AppendLine($"| {budget.splatCount} | {budget.assetMegabytes:0.###} | {budget.estimatedGpuMegabytes:0.###} | {budget.status} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Limitations");
            builder.AppendLine();
            foreach (string limitation in report.limitations)
            {
                builder.AppendLine($"- {limitation}");
            }

            return builder.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string escaped = value.Replace("\"", "\"\"");
            if (escaped.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                return $"\"{escaped}\"";
            }

            return escaped;
        }
    }
}
