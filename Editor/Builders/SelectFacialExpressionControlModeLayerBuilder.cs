using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class SelectFacialExpressionControlModeLayerBuilder : LayerBuilderBase
    {
        public SelectFacialExpressionControlModeLayerBuilder(AnimationClip blankClip) : base(blankClip)
        {
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_SELECT_FACIAL_EXPRESSION_CONTROL_MODE");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(600, 440, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 440, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var inactiveState = layer.stateMachine.AddState("Inactive", new Vector3(300, 80, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;
            inactiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive),
            };

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(inactiveState)
                .If(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(300, 160, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.Neutral),
            };

            // 左右どちらの手のジェスチャーも適用されていないパターン (Neutral)
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 0)
                .SetImmediateTransitionSettings();

            // 左手のジェスチャーが適用されているが、Neutralな値である場合
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            // 右手のジェスチャーが適用されているが、Neutralな値である場合
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .Equals(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            var leftHandGestureState = layer.stateMachine.AddState("Left Hand Gesture", new Vector3(300, 240, 0));
            leftHandGestureState.writeDefaultValues = false;
            leftHandGestureState.motion = blankAnimationClip;
            leftHandGestureState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.LeftHandGesture),
            };

            AnimatorTransitionUtil.AddTransition(initialState, leftHandGestureState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .NotEqual(InternalParameters.State_CurrentGestureHand, 1)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            var leftHandGestureFixedState = layer.stateMachine.AddState("Left Hand Gesture (Fixed)", new Vector3(300, 320, 0));
            leftHandGestureFixedState.writeDefaultValues = false;
            leftHandGestureFixedState.motion = blankAnimationClip;
            leftHandGestureFixedState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.LeftHandGestureFixed),
            };

            AnimatorTransitionUtil.AddTransition(initialState, leftHandGestureFixedState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft, 0)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .NotEqual(InternalParameters.State_CurrentGestureHand, 1)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            var rightHandGestureState = layer.stateMachine.AddState("Right Hand Gesture", new Vector3(300, 400, 0));
            rightHandGestureState.writeDefaultValues = false;
            rightHandGestureState.motion = blankAnimationClip;
            rightHandGestureState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.RightHandGesture),
            };

            AnimatorTransitionUtil.AddTransition(initialState, rightHandGestureState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .NotEqual(InternalParameters.State_CurrentGestureHand, 2)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .Equals(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            var rightHandGestureFixedState = layer.stateMachine.AddState("Right Hand Gesture (Fixed)", new Vector3(300, 480, 0));
            rightHandGestureFixedState.writeDefaultValues = false;
            rightHandGestureFixedState.motion = blankAnimationClip;
            rightHandGestureFixedState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.RightHandGestureFixed),
            };

            AnimatorTransitionUtil.AddTransition(initialState, rightHandGestureFixedState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight, 0)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .NotEqual(InternalParameters.State_CurrentGestureHand, 2)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .Equals(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            var selectedFacialExpressionInMenuState = layer.stateMachine.AddState("Selected Facial Expression (In Menu)", new Vector3(300, 560, 0));
            selectedFacialExpressionInMenuState.writeDefaultValues = false;
            selectedFacialExpressionInMenuState.motion = blankAnimationClip;
            selectedFacialExpressionInMenuState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.SelectedFacialExpressionInMenu),
            };

            AnimatorTransitionUtil.AddTransition(initialState, selectedFacialExpressionInMenuState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            var builtInFacialTrackingState = layer.stateMachine.AddState("Built-in Facial Tracking", new Vector3(300, 640, 0));
            builtInFacialTrackingState.writeDefaultValues = false;
            builtInFacialTrackingState.motion = blankAnimationClip;
            builtInFacialTrackingState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.BuiltInFacialTracking),
            };

            AnimatorTransitionUtil.AddTransition(initialState, builtInFacialTrackingState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.BuiltInFacialTracking)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.BuiltInFacialTracking)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            var animatorBasedFacialTrackingState = layer.stateMachine.AddState("Animator Based Facial Tracking", new Vector3(300, 720, 0));
            animatorBasedFacialTrackingState.writeDefaultValues = false;
            animatorBasedFacialTrackingState.motion = blankAnimationClip;
            animatorBasedFacialTrackingState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.AnimatorBasedFacialTracking),
            };

            AnimatorTransitionUtil.AddTransition(initialState, animatorBasedFacialTrackingState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .Equals(InternalParameters.FacialTrackingMode, FacialTrackingType.AnimatorBasedFacialTracking)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .NotEqual(InternalParameters.FacialTrackingMode, FacialTrackingType.AnimatorBasedFacialTracking)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            var danceModeState = layer.stateMachine.AddState("Dance Mode", new Vector3(300, 800, 0));
            danceModeState.writeDefaultValues = false;
            danceModeState.motion = blankAnimationClip;
            danceModeState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.DanceMode),
            };

            AnimatorTransitionUtil.AddTransition(initialState, danceModeState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .IfNot(InternalParameters.State_AFKModeActive)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .IfNot(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            var afkState = layer.stateMachine.AddState("AFK", new Vector3(300, 880, 0));
            afkState.writeDefaultValues = false;
            afkState.motion = blankAnimationClip;
            afkState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.AFKMode),
            };

            AnimatorTransitionUtil.AddTransition(initialState, afkState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(afkState)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(afkState)
                .IfNot(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            return layer;
        }
    }
}