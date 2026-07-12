using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace QuestFlightLab.Runtime
{
    /// <summary>
    /// Creates the standard left/right controller transforms beneath the one
    /// active XR Origin. OpenXR owns both poses; calibration never writes them.
    /// </summary>
    public static class TrackedXrControllerPoseDrivers
    {
        public const string LeftControllerName = "Left Touch Controller";
        public const string RightControllerName = "Right Touch Controller";
        public const string LegacyLeftControllerName = "LeftHand Controller";
        public const string LegacyRightControllerName = "RightHand Controller";

        public static Transform EnsureLeft(Transform xrOrigin) => Ensure(xrOrigin, true);
        public static Transform EnsureRight(Transform xrOrigin) => Ensure(xrOrigin, false);

        public static bool HasRequiredHierarchy(Transform xrOrigin)
        {
            if (xrOrigin == null) return false;
            Transform left = FindController(xrOrigin, true);
            Transform right = FindController(xrOrigin, false);
            return HasRequiredBindings(left, true) && HasRequiredBindings(right, false);
        }

        public static int ResolvedControlCount(Transform controller)
        {
            TrackedPoseDriver driver = controller != null ? controller.GetComponent<TrackedPoseDriver>() : null;
            if (driver == null) return 0;
            return ControlCount(driver.positionInput) +
                   ControlCount(driver.rotationInput) +
                   ControlCount(driver.trackingStateInput);
        }

        private static Transform Ensure(Transform xrOrigin, bool left)
        {
            if (xrOrigin == null) return null;
            string name = left ? LeftControllerName : RightControllerName;
            Transform controller = FindController(xrOrigin, left);
            if (controller == null)
            {
                GameObject go = new GameObject(name);
                controller = go.transform;
                controller.SetParent(xrOrigin, false);
            }

            controller.localScale = Vector3.one;
            TrackedPoseDriver driver = controller.GetComponent<TrackedPoseDriver>();
            if (driver == null) driver = controller.gameObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = false;

            string hand = left ? "LeftHand" : "RightHand";
            EnsureAction(
                driver.positionInput,
                $"{hand} Controller Position",
                $"<XRController>{{{hand}}}/devicePosition",
                "Vector3",
                property => driver.positionInput = property);
            EnsureAction(
                driver.rotationInput,
                $"{hand} Controller Rotation",
                $"<XRController>{{{hand}}}/deviceRotation",
                "Quaternion",
                property => driver.rotationInput = property);
            EnsureAction(
                driver.trackingStateInput,
                $"{hand} Controller Tracking State",
                $"<XRController>{{{hand}}}/trackingState",
                "Integer",
                property => driver.trackingStateInput = property);
            return controller;
        }

        private static void EnsureAction(
            InputActionProperty current,
            string actionName,
            string binding,
            string controlType,
            Action<InputActionProperty> assign)
        {
            InputAction action = current.action;
            if (action != null && action.bindings.Any(candidate =>
                    string.Equals(candidate.path, binding, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.effectivePath, binding, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            assign(new InputActionProperty(new InputAction(
                actionName,
                InputActionType.Value,
                binding,
                expectedControlType: controlType)));
        }

        public static bool HasRequiredBindings(Transform controller, bool left)
        {
            if (controller == null) return false;
            TrackedPoseDriver driver = controller.GetComponent<TrackedPoseDriver>();
            if (driver == null || driver.trackingType != TrackedPoseDriver.TrackingType.RotationAndPosition) return false;
            string hand = left ? "LeftHand" : "RightHand";
            return HasBinding(driver.positionInput, $"<XRController>{{{hand}}}/devicePosition") &&
                   HasBinding(driver.rotationInput, $"<XRController>{{{hand}}}/deviceRotation") &&
                   HasBinding(driver.trackingStateInput, $"<XRController>{{{hand}}}/trackingState");
        }

        private static bool HasBinding(InputActionProperty property, string binding)
        {
            InputAction action = property.action;
            return action != null && action.bindings.Any(candidate =>
                string.Equals(candidate.path, binding, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.effectivePath, binding, StringComparison.OrdinalIgnoreCase));
        }

        private static int ControlCount(InputActionProperty property)
        {
            InputAction action = property.action;
            return action != null ? action.controls.Count : 0;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal)) return child;
            }

            return null;
        }

        private static Transform FindController(Transform xrOrigin, bool left)
        {
            string currentName = left ? LeftControllerName : RightControllerName;
            string legacyName = left ? LegacyLeftControllerName : LegacyRightControllerName;
            return FindDirectChild(xrOrigin, currentName) ?? FindDirectChild(xrOrigin, legacyName);
        }
    }
}
