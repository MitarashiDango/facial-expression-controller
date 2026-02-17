using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class Parameters
    {
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
            return new Parameter[]
            {
                InternalParameters.FacialExpressionControlON,
                InternalParameters.SwitchToDanceModeON,
                InternalParameters.SwitchToVehicleModeON,
                InternalParameters.FacialTrackingMode,
                InternalParameters.FacialExpressionLocked,
                InternalParameters.SelectedLeftGesturePreset,
                InternalParameters.SelectedRightGesturePreset,
                InternalParameters.SelectedFacialExpressionInMenu,
                InternalParameters.ContactLockON,
                InternalParameters.FacialExpressionLockReceiverInContact,
                InternalParameters.GesturePriority,
                InternalParameters.State_CurrentGestureLeft,
                InternalParameters.State_CurrentGestureRight,
                InternalParameters.State_CurrentGestureHand,
                InternalParameters.State_LastGestureChangedHand,
                InternalParameters.State_VehicleModeActive,
                InternalParameters.State_AFKModeActive,
                InternalParameters.State_DanceModeActive,
                SyncParameters.FacialExpressionControlMode,
                SyncParameters.CurrentFacialExpressionNumber,
                SyncParameters.FixedWeight,
            };
        }
    }
}