using MitarashiDango.FacialExpressionController.Runtime;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class AFKModeControlLayerBuilder : LayerBuilderBase
    {
        private readonly FacialExpressionControl _fec;

        public AFKModeControlLayerBuilder(AnimationClip blankClip, FacialExpressionControl fec) : base(blankClip)
        {
            _fec = fec;
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_AFK_MODE_CONTROL");
            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var afkActiveState = layer.stateMachine.AddState("AFK Active", new Vector3(500, 0, 0));
            afkActiveState.writeDefaultValues = false;
            afkActiveState.motion = blankAnimationClip;
            afkActiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_AFKModeActive, 1)
            };

            AnimatorTransitionUtil.AddTransition(initialState, afkActiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            var switchToAFKInactiveState = layer.stateMachine.AddState("Waiting (Switch To AFK Inactive)", new Vector3(500, 80, 0));
            switchToAFKInactiveState.writeDefaultValues = false;
            switchToAFKInactiveState.motion = blankAnimationClip;

            AnimatorTransitionUtil.AddTransition(afkActiveState, switchToAFKInactiveState)
                .IfNot(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(switchToAFKInactiveState, afkActiveState)
                .If(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            var afkInactiveState = layer.stateMachine.AddState("AFK Inactive", new Vector3(200, 80, 0));
            afkInactiveState.writeDefaultValues = false;
            afkInactiveState.motion = blankAnimationClip;
            afkInactiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_AFKModeActive, 0)
            };

            AnimatorTransitionUtil.AddTransition(initialState, afkInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            var switchToAFKInactiveTransition = AnimatorTransitionUtil.AddTransition(switchToAFKInactiveState, afkInactiveState)
                .IfNot(VRCParameters.AFK);

            if (_fec.afkExitWaitMode == AFKExitWaitMode.Duration && _fec.waitAFKExitDurationTime > 0)
            {
                switchToAFKInactiveTransition.Exec((builder) =>
                {
                    var transition = builder.Transition;
                    transition.hasExitTime = true;
                    transition.exitTime = _fec.waitAFKExitDurationTime;
                    transition.hasFixedDuration = true;
                    transition.duration = 0;
                    transition.offset = 0;
                    transition.interruptionSource = TransitionInterruptionSource.None;
                    transition.orderedInterruption = true;
                });
            }
            else
            {
                switchToAFKInactiveTransition.SetImmediateTransitionSettings();
            }

            AnimatorTransitionUtil.AddTransition(afkInactiveState, afkActiveState)
                .If(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            return layer;
        }
    }
}