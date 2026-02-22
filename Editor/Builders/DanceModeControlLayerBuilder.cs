using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class DanceModeControlLayerBuilder : LayerBuilderBase
    {
        public DanceModeControlLayerBuilder(AnimationClip blankClip) : base(blankClip) { }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_DANCE_MODE_CONTROL");
            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var inactiveState = layer.stateMachine.AddState("Dance Mode Inactive", new Vector3(500, 0, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;
            inactiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_DanceModeActive, 0)
            };

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.SwitchToDanceModeON)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            // 表情コントロールがOFFになっている場合
            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            var activeState = layer.stateMachine.AddState("Dance Mode Active", new Vector3(500, 80, 0));
            activeState.writeDefaultValues = false;
            activeState.motion = blankAnimationClip;
            activeState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_DanceModeActive, 1)
            };

            // [Initial State] -> [Dance Mode Active]
            AnimatorTransitionUtil.AddTransition(initialState, activeState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.SwitchToDanceModeON)
                .If(InternalParameters.FacialExpressionControlON)
                .If(VRCParameters.IN_STATION)
                .IfNot(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            // [Dance Mode Inactive] -> [Dance Mode Active]
            AnimatorTransitionUtil.AddTransition(inactiveState, activeState)
                .If(InternalParameters.SwitchToDanceModeON)
                .If(InternalParameters.FacialExpressionControlON)
                .If(VRCParameters.IN_STATION)
                .IfNot(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(InternalParameters.SwitchToDanceModeON)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            // 表情コントロールがOFFになっている場合
            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            return layer;
        }
    }
}