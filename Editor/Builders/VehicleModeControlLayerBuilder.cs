using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class VehicleModeControlLayerBuilder : LayerBuilderBase
    {
        public VehicleModeControlLayerBuilder(AnimationClip blankClip) : base(blankClip) { }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_VEHICLE_MODE_CONTROL");
            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var inactiveState = layer.stateMachine.AddState("Vehicle Mode Inactive", new Vector3(500, 0, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;
            inactiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_VehicleModeActive, 0)
            };

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.SwitchToVehicleModeON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            var activeState = layer.stateMachine.AddState("Vehicle Mode Active", new Vector3(500, 80, 0));
            activeState.writeDefaultValues = false;
            activeState.motion = blankAnimationClip;
            activeState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_VehicleModeActive, 1)
            };

            AnimatorTransitionUtil.AddTransition(initialState, activeState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.SwitchToVehicleModeON)
                .If(InternalParameters.FacialExpressionControlON)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(inactiveState, activeState)
                .If(InternalParameters.SwitchToVehicleModeON)
                .If(InternalParameters.FacialExpressionControlON)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(InternalParameters.SwitchToVehicleModeON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            return layer;
        }
    }
}