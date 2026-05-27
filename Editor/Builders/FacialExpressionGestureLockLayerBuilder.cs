using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class FacialExpressionGestureLockLayerBuilder : LayerBuilderBase
    {
        private const float GestureLockIntervalDuration = 0.5f;

        public FacialExpressionGestureLockLayerBuilder(AnimationClip blankClip, string waitClipTargetPath) : base(blankClip, waitClipTargetPath)
        {
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_FACIAL_EXPRESSION_GESTURE_LOCK");

            layer.stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            layer.stateMachine.exitPosition = AnimatorLayout.DefaultExitPosition;
            layer.stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var inactiveState = layer.stateMachine.AddState("Inactive", new Vector3(600, 0, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;

            var gestureLockDisabledState = layer.stateMachine.AddState("Gesture Lock Disabled", new Vector3(400, -80, 0));
            gestureLockDisabledState.writeDefaultValues = false;
            gestureLockDisabledState.motion = blankAnimationClip;

            var gestureLockEnabledState = layer.stateMachine.AddState("Gesture Lock Enabled", new Vector3(400, 80, 0));
            gestureLockEnabledState.writeDefaultValues = false;
            gestureLockEnabledState.motion = blankAnimationClip;

            var setDisableState = layer.stateMachine.AddState("Set Disable", new Vector3(600, -160, 0));
            setDisableState.writeDefaultValues = false;
            setDisableState.motion = CreateWaitClip("Gesture Lock Interval (Lock to Unlock)", GestureLockIntervalDuration);
            setDisableState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.FacialExpressionLocked, 0)
            };

            var setEnableState = layer.stateMachine.AddState("Set Enable", new Vector3(600, 160, 0));
            setEnableState.writeDefaultValues = false;
            setEnableState.motion = CreateWaitClip("Gesture Lock Interval (Unlock to Lock)", GestureLockIntervalDuration);
            setEnableState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.FacialExpressionLocked, 1)
            };

            var lockToUnlockIntervalState = layer.stateMachine.AddState("Interval (Lock to Unlock)", new Vector3(800, -80, 0));
            lockToUnlockIntervalState.writeDefaultValues = false;
            lockToUnlockIntervalState.motion = blankAnimationClip;

            var unlockToLockIntervalState = layer.stateMachine.AddState("Interval (Unlock to Lock)", new Vector3(800, 80, 0));
            unlockToLockIntervalState.writeDefaultValues = false;
            unlockToLockIntervalState.motion = blankAnimationClip;

            // [Initial State] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Set Disable] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(setDisableState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Set Enable] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(setEnableState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Interval (Lock to Unlock)] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(lockToUnlockIntervalState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Interval (Unlock to Lock)] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(unlockToLockIntervalState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControllerEnabled)
                .SetImmediateTransitionSettings();

            // [Inactive] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(inactiveState, gestureLockDisabledState)
                .If(InternalParameters.FacialExpressionControllerEnabled)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // [Inactive] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(inactiveState, gestureLockEnabledState)
                .If(InternalParameters.FacialExpressionControllerEnabled)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(initialState, gestureLockDisabledState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(initialState, gestureLockEnabledState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, gestureLockEnabledState)
                .If(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, gestureLockDisabledState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Set Enable]
            // - 以下のすべての条件を満たす場合、ロック有効化を行う
            //   - AFK状態ではない場合
            //   - ダンスモードではない場合
            //   - InStation = false の場合
            //     - InStation = false が保証されているため、乗り物着座時のロック切り替え一時停止フラグはチェック不要
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, setEnableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact)
                .If(InternalParameters.ContactLockEnabled)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Set Enable]
            // - SuspendFacialExpressionLockSwitchInVehicleEnabled が OFF の場合、InStation かつ Seated でもロック有効化を行う
            //   - InStation = true かつ Seated = false の時は、本フラグが OFF でもロック切り替えを行わない (いわゆるダンスワールドでのアニメーション適用時など)
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, setEnableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact)
                .If(InternalParameters.ContactLockEnabled)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .IfNot(InternalParameters.SuspendFacialExpressionLockSwitchInVehicleEnabled)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Set Disable]
            // - 以下のすべての条件を満たす場合、ロック無効化を行う
            //   - AFK状態ではない場合
            //   - ダンスモードではない場合
            //   - InStation = false の場合
            //     - InStation = false が保証されているため、乗り物着座時のロック切り替え一時停止フラグはチェック不要
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, setDisableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact)
                .If(InternalParameters.ContactLockEnabled)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Set Disable]
            // - SuspendFacialExpressionLockSwitchInVehicleEnabled が OFF の場合、InStation かつ Seated でもロック無効化を行う
            //   - InStation = true かつ Seated = false の時は、本フラグが OFF でもロック切り替えを行わない (いわゆるダンスワールドでのアニメーション適用時など)
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, setDisableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact)
                .If(InternalParameters.ContactLockEnabled)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .IfNot(InternalParameters.SuspendFacialExpressionLockSwitchInVehicleEnabled)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Set Disable] -> [Interval (Lock to Unlock)]
            AnimatorTransitionUtil.AddTransition(setDisableState, lockToUnlockIntervalState)
                .Exec(builder => builder.SetExitAfterMotionSettings());

            // [Set Enable] -> [Interval (Unlock to Lock)]
            AnimatorTransitionUtil.AddTransition(setEnableState, unlockToLockIntervalState)
                .Exec(builder => builder.SetExitAfterMotionSettings());

            // [Interval (Lock to Unlock)] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(lockToUnlockIntervalState, gestureLockDisabledState)
                .IfNot(InternalParameters.FacialExpressionLockReceiverInContact)
                .SetImmediateTransitionSettings();

            // [Interval (Unlock to Lock)] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(unlockToLockIntervalState, gestureLockEnabledState)
                .IfNot(InternalParameters.FacialExpressionLockReceiverInContact)
                .SetImmediateTransitionSettings();

            return layer;
        }
    }
}
