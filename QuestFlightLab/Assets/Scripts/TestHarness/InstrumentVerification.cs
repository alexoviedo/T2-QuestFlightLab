using System;
using System.Collections.Generic;
using QuestFlightLab.UI;
using UnityEngine;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class InstrumentVerificationSnapshot
    {
        public bool panelPresent;
        public bool allRequiredPresent;
        public bool valuesUpdated;
        public bool approachFieldsPresent;
        public int requiredCount;
        public int presentCount;
        public int approachFieldCount;
        public List<string> missing = new List<string>();
        public string summary = "";
    }

    public static class InstrumentVerification
    {
        public static InstrumentVerificationSnapshot Capture()
        {
            CockpitInstrumentPanel panel = CockpitInstrumentPanel.CreateOrFindPanel();
            if (panel != null)
            {
                panel.RefreshDisplay();
            }
            bool allPresent = CockpitInstrumentPanel.HasRequiredInstrumentObjects(out List<string> missing);
            int required = CockpitInstrumentPanel.RequiredInstrumentNames.Length;
            int approachRequired = 0;
            foreach (string name in CockpitInstrumentPanel.RequiredInstrumentNames)
            {
                if (name.Contains("Approach") || name.Contains("GlidePath") || name.Contains("Centerline") || name.Contains("GoAround"))
                {
                    approachRequired++;
                }
            }
            bool valuesUpdated = allPresent && RequiredTextValuesLookUpdated();
            return new InstrumentVerificationSnapshot
            {
                panelPresent = panel != null,
                allRequiredPresent = allPresent,
                valuesUpdated = valuesUpdated,
                approachFieldsPresent = allPresent && approachRequired >= 7,
                requiredCount = required,
                presentCount = required - missing.Count,
                approachFieldCount = approachRequired,
                missing = missing,
                summary = allPresent && valuesUpdated
                    ? $"PASS {required}/{required} instruments present and updated; approach fields {approachRequired}"
                    : allPresent
                        ? $"PRESENT {required}/{required} instruments but values not refreshed"
                        : $"MISSING {string.Join(";", missing)}"
            };
        }

        private static bool RequiredTextValuesLookUpdated()
        {
            foreach (string name in CockpitInstrumentPanel.RequiredInstrumentNames)
            {
                GameObject go = GameObject.Find(name);
                TextMesh text = go != null ? go.GetComponent<TextMesh>() : null;
                if (text == null || string.IsNullOrWhiteSpace(text.text))
                {
                    return false;
                }

                if (text.text == name.Replace("Instrument_", ""))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
