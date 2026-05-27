using System.Linq;
using MitarashiDango.FacialExpressionController;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class AFKModeControlLayerBuilder : LayerBuilderBase
    {
        private readonly FacialExpressionController _fec;

        public AFKModeControlLayerBuilder(AnimationClip blankClip, FacialExpressionController fec, string waitClipTargetPath) : base(blankClip, waitClipTargetPath)
        {
            _fec = fec;
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_AFK_MODE_CONTROL");
            layer.stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            layer.stateMachine.exitPosition = AnimatorLayout.DefaultExitPosition;
            layer.stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;

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

            if (_fec.afkExitWaitMode == AFKExitWaitMode.Duration && _fec.afkExitWaitDuration > 0)
            {
                switchToAFKInactiveState.motion = CreateWaitClip($"AFK Exit Wait ({_fec.afkExitWaitDuration:0.###}s)", _fec.afkExitWaitDuration);

                switchToAFKInactiveTransition.Exec(builder => builder.SetExitAfterMotionSettings());
            }
            else if (_fec.afkExitWaitMode == AFKExitWaitMode.Parameter
                && _fec.afkExitWaitConditions != null
                && HasValidParameterConditions())
            {
                foreach (var condition in _fec.afkExitWaitConditions)
                {
                    if (condition == null || string.IsNullOrEmpty(condition.parameterName))
                    {
                        continue;
                    }

                    switch (condition.compareMode)
                    {
                        case AnimatorConditionCompareMode.If:
                            switchToAFKInactiveTransition.If(condition.parameterName);
                            break;
                        case AnimatorConditionCompareMode.IfNot:
                            switchToAFKInactiveTransition.IfNot(condition.parameterName);
                            break;
                        case AnimatorConditionCompareMode.Greater:
                            switchToAFKInactiveTransition.Greater(condition.parameterName, condition.threshold);
                            break;
                        case AnimatorConditionCompareMode.Less:
                            switchToAFKInactiveTransition.Less(condition.parameterName, condition.threshold);
                            break;
                        case AnimatorConditionCompareMode.Equals:
                            switchToAFKInactiveTransition.Equals(condition.parameterName, condition.threshold);
                            break;
                        case AnimatorConditionCompareMode.NotEqual:
                            switchToAFKInactiveTransition.NotEqual(condition.parameterName, condition.threshold);
                            break;
                    }
                }

                switchToAFKInactiveTransition.SetImmediateTransitionSettings();
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

        private bool HasValidParameterConditions()
        {
            return _fec.afkExitWaitConditions
                .Any(c => c != null && !string.IsNullOrEmpty(c.parameterName));
        }
    }
}
