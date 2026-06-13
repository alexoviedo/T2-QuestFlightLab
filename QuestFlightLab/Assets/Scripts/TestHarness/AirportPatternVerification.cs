using System;
using System.Collections.Generic;
using QuestFlightLab.Environment;
using UnityEngine;

namespace QuestFlightLab.TestHarness
{
    [Serializable]
    public class AirportPatternVerificationSnapshot
    {
        public bool airportRootPresent;
        public bool allRequiredReferencesPresent;
        public int requiredCount;
        public int presentCount;
        public List<string> missing = new List<string>();
        public string summary = "";
    }

    public static class AirportPatternVerification
    {
        public static readonly string[] RequiredReferenceNames =
        {
            "Runway_08_26_Approx_4100x75ft",
            "RunwayEdgeLineLeft",
            "RunwayEdgeLineRight",
            "TouchdownZoneMarker08",
            "ApproachPathPlaceholder08",
            "PatternGate_Upwind_08",
            "PatternGate_Crosswind_08",
            "PatternGate_Downwind_Midfield",
            "PatternGate_Abeam_Touchdown_08",
            "PatternGate_Base_08",
            "PatternGate_Final_08",
            "PatternAltitudeBandPlaceholder",
            "PatternBoxBoundaryNorth",
            "PatternBoxBoundarySouth",
            "Windsock"
        };

        public static AirportPatternVerificationSnapshot Capture()
        {
            GameObject root = GameObject.Find("KBDU_Approx_Airport_NotForNavigation");
            bool createdForVerification = false;
            if (root == null)
            {
                root = KbduApproxAirport.Build(null);
                createdForVerification = true;
            }
            else
            {
                AirportRuntimeEnhancer.EnhanceExistingScene();
            }

            AirportPatternVerificationSnapshot snapshot = new AirportPatternVerificationSnapshot
            {
                airportRootPresent = root != null,
                requiredCount = RequiredReferenceNames.Length
            };

            if (root != null)
            {
                foreach (string reference in RequiredReferenceNames)
                {
                    if (FindChildRecursive(root.transform, reference) != null)
                    {
                        snapshot.presentCount++;
                    }
                    else
                    {
                        snapshot.missing.Add(reference);
                    }
                }
            }

            snapshot.allRequiredReferencesPresent = snapshot.missing.Count == 0;
            snapshot.summary = snapshot.allRequiredReferencesPresent
                ? $"PASS {snapshot.presentCount}/{snapshot.requiredCount} airport/pattern references present"
                : $"MISSING {string.Join(";", snapshot.missing)}";

            if (createdForVerification && root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return snapshot;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
