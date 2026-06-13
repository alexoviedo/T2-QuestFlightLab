using System;
using System.Collections.Generic;
using QuestFlightLab.UI;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class InstrumentVerificationSnapshot
    {
        public bool panelPresent;
        public bool allRequiredPresent;
        public int requiredCount;
        public int presentCount;
        public List<string> missing = new List<string>();
        public string summary = "";
    }

    public static class InstrumentVerification
    {
        public static InstrumentVerificationSnapshot Capture()
        {
            CockpitInstrumentPanel panel = CockpitInstrumentPanel.CreateOrFindPanel();
            bool allPresent = CockpitInstrumentPanel.HasRequiredInstrumentObjects(out List<string> missing);
            int required = CockpitInstrumentPanel.RequiredInstrumentNames.Length;
            return new InstrumentVerificationSnapshot
            {
                panelPresent = panel != null,
                allRequiredPresent = allPresent,
                requiredCount = required,
                presentCount = required - missing.Count,
                missing = missing,
                summary = allPresent ? $"PASS {required}/{required} instruments present" : $"MISSING {string.Join(";", missing)}"
            };
        }
    }
}
