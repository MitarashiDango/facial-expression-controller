using System.Collections.Generic;
using System.Linq;
using MitarashiDango.FacialExpressionController;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class Parameters
    {
        private readonly int _selectedLeftGesturePresetDefaultIndex;
        private readonly int _selectedRightGesturePresetDefaultIndex;

        public Parameters(FacialExpressionController fec = null)
        {
            _selectedLeftGesturePresetDefaultIndex = GesturePresetDefaultValueResolver.ResolveLeftIndex(fec);
            _selectedRightGesturePresetDefaultIndex = GesturePresetDefaultValueResolver.ResolveRightIndex(fec);
        }

        public AnimatorControllerParameter[] CreateAnimatorControllerParameters()
        {
            var parameters = CreateFECParameters().Select(p => p.ToAnimatorControllerParameter()).ToList();

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.AFK,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.IN_STATION,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.IS_LOCAL,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.GESTURE_LEFT,
                type = AnimatorControllerParameterType.Int,
                defaultInt = 0,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.GESTURE_RIGHT,
                type = AnimatorControllerParameterType.Int,
                defaultInt = 0,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.GESTURE_LEFT_WEIGHT,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.GESTURE_RIGHT_WEIGHT,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0,
            });

            parameters.Add(new AnimatorControllerParameter
            {
                name = VRCParameters.SEATED,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false,
            });

            return parameters.ToArray();
        }

        public List<ParameterConfig> CreateNDMFParameterConfigs()
        {
            return CreateFECParameters().Select(p => p.ToParameterConfig()).ToList();
        }

        private Parameter[] CreateFECParameters()
        {
            var selectedLeftGesturePreset = InternalParameters.SelectedLeftGesturePreset
                .OverrideDefaultInt(_selectedLeftGesturePresetDefaultIndex);
            var selectedRightGesturePreset = InternalParameters.SelectedRightGesturePreset
                .OverrideDefaultInt(_selectedRightGesturePresetDefaultIndex);

            return new Parameter[]
            {
                InternalParameters.FacialExpressionControllerEnabled,
                InternalParameters.DanceModeAutoSwitchEnabled,
                InternalParameters.SuspendFacialExpressionLockSwitchInVehicleEnabled,
                InternalParameters.FacialExpressionLocked,
                selectedLeftGesturePreset,
                selectedRightGesturePreset,
                InternalParameters.SelectedFacialExpressionInMenu,
                InternalParameters.ContactLockEnabled,
                InternalParameters.FacialExpressionLockReceiverInContact,
                InternalParameters.GesturePriority,
                InternalParameters.State_CurrentGestureLeft,
                InternalParameters.State_CurrentGestureRight,
                InternalParameters.State_CurrentGestureHand,
                InternalParameters.State_LastGestureChangedHand,
                InternalParameters.State_AFKModeActive,
                InternalParameters.State_DanceModeActive,
                SyncParameters.FacialExpressionMode,
                SyncParameters.CurrentFacialExpressionNumber,
                SyncParameters.LockedFacialExpressionWeight,
            };
        }
    }

    internal static class GesturePresetDefaultValueResolver
    {
        public const int FirstPresetNumber = 1;

        public static int ResolveLeftIndex(FacialExpressionController fec)
        {
            return ResolveIndex(fec, fec != null ? fec.defaultLeftGesturePresetNumber : FirstPresetNumber);
        }

        public static int ResolveRightIndex(FacialExpressionController fec)
        {
            return ResolveIndex(fec, fec != null ? fec.defaultRightGesturePresetNumber : FirstPresetNumber);
        }

        public static int ResolveIndex(FacialExpressionController fec, int presetNumber)
        {
            if (IsExistingPresetNumber(fec, presetNumber))
            {
                return presetNumber - FirstPresetNumber;
            }

            return GetFirstValidPresetIndex(fec) ?? 0;
        }

        public static bool IsValidPresetNumber(FacialExpressionController fec, int presetNumber)
        {
            if (presetNumber < FirstPresetNumber)
            {
                return false;
            }

            return IsExistingPresetNumber(fec, presetNumber);
        }

        public static bool HasValidPreset(FacialExpressionController fec)
        {
            return GetFirstValidPresetIndex(fec).HasValue;
        }

        private static bool IsExistingPresetNumber(FacialExpressionController fec, int presetNumber)
        {
            if (fec == null)
            {
                return presetNumber == FirstPresetNumber;
            }

            if (fec.facialExpressionGesturePresets == null)
            {
                return false;
            }

            var index = presetNumber - FirstPresetNumber;
            return index >= 0
                && index < fec.facialExpressionGesturePresets.Count
                && fec.facialExpressionGesturePresets[index] != null;
        }

        private static int? GetFirstValidPresetIndex(FacialExpressionController fec)
        {
            if (fec == null || fec.facialExpressionGesturePresets == null)
            {
                return null;
            }

            for (var i = 0; i < fec.facialExpressionGesturePresets.Count; i++)
            {
                if (fec.facialExpressionGesturePresets[i] != null)
                {
                    return i;
                }
            }

            return null;
        }
    }
}
