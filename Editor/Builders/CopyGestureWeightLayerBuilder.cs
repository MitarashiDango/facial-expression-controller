using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class CopyGestureWeightLayerBuilder : LayerBuilderBase
    {
        public CopyGestureWeightLayerBuilder(AnimationClip blankClip) : base(blankClip) { }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_COPY_GESUTRE_WEIGHT");
            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 80, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var unlockState = layer.stateMachine.AddState("Unlock", new Vector3(200, 80, 0));
            unlockState.writeDefaultValues = false;
            unlockState.motion = blankAnimationClip;

            var lockingState = layer.stateMachine.AddState("Locking", new Vector3(200, 160, 0));
            lockingState.writeDefaultValues = false;
            lockingState.motion = blankAnimationClip;

            var copyLeftState = layer.stateMachine.AddState("Copy Gesture Weight (Left)", new Vector3(500, 80, 0));
            copyLeftState.writeDefaultValues = false; copyLeftState.motion = blankAnimationClip;
            copyLeftState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalCopyDriver(VRCParameters.GESTURE_LEFT_WEIGHT, SyncParameters.FixedWeight)
            };

            var copyRightState = layer.stateMachine.AddState("Copy Gesture Weight (Right)", new Vector3(500, 160, 0));
            copyRightState.writeDefaultValues = false; copyRightState.motion = blankAnimationClip;
            copyRightState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalCopyDriver(VRCParameters.GESTURE_RIGHT_WEIGHT, SyncParameters.FixedWeight)
            };

            AnimatorTransitionUtil.AddTransition(initialState, unlockState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, lockingState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, copyLeftState)
                .If(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, copyRightState)
                .If(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, lockingState)
                .If(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, lockingState)
                .If(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, lockingState)
                .If(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .Equals(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyLeftState, lockingState)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyRightState, lockingState)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyLeftState, unlockState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyRightState, unlockState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(lockingState, unlockState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            return layer;
        }
    }
}