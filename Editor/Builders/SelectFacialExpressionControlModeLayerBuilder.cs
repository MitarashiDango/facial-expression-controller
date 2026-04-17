using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class SelectFacialExpressionControlModeLayerBuilder : LayerBuilderBase
    {
        /// <summary>
        /// 左右ハンドジェスチャー用ステートの生成パラメーター
        /// </summary>
        private class HandGestureStateSpec
        {
            public string StateName;
            public int ModeType;
            public int HandValue;                // State_CurrentGestureHand の値 (1 = 左, 2 = 右)
            public Parameter CurrentGestureParameter;  // State_CurrentGestureLeft / Right
            public bool IsFixed;                 // FacialExpressionLocked=true で入る Fixed 版かどうか
            public Vector3 Position;
        }

        public SelectFacialExpressionControlModeLayerBuilder(AnimationClip blankClip) : base(blankClip)
        {
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_SELECT_FACIAL_EXPRESSION_CONTROL_MODE");

            layer.stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            layer.stateMachine.exitPosition = new Vector3(600, 440, 0);
            layer.stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 440, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            AddInactiveState(layer.stateMachine, initialState);
            AddNeutralState(layer.stateMachine, initialState);

            var handGestureSpecs = new[]
            {
                new HandGestureStateSpec
                {
                    StateName = "Left Hand Gesture",
                    ModeType = FacialExpressionControlModeType.LeftHandGesture,
                    HandValue = 1,
                    CurrentGestureParameter = InternalParameters.State_CurrentGestureLeft,
                    IsFixed = false,
                    Position = new Vector3(300, 240, 0),
                },
                new HandGestureStateSpec
                {
                    StateName = "Left Hand Gesture (Fixed)",
                    ModeType = FacialExpressionControlModeType.LeftHandGestureFixed,
                    HandValue = 1,
                    CurrentGestureParameter = InternalParameters.State_CurrentGestureLeft,
                    IsFixed = true,
                    Position = new Vector3(300, 320, 0),
                },
                new HandGestureStateSpec
                {
                    StateName = "Right Hand Gesture",
                    ModeType = FacialExpressionControlModeType.RightHandGesture,
                    HandValue = 2,
                    CurrentGestureParameter = InternalParameters.State_CurrentGestureRight,
                    IsFixed = false,
                    Position = new Vector3(300, 400, 0),
                },
                new HandGestureStateSpec
                {
                    StateName = "Right Hand Gesture (Fixed)",
                    ModeType = FacialExpressionControlModeType.RightHandGestureFixed,
                    HandValue = 2,
                    CurrentGestureParameter = InternalParameters.State_CurrentGestureRight,
                    IsFixed = true,
                    Position = new Vector3(300, 480, 0),
                },
            };

            foreach (var spec in handGestureSpecs)
            {
                AddHandGestureState(layer.stateMachine, initialState, spec);
            }

            AddSelectedFacialExpressionInMenuState(layer.stateMachine, initialState);
            AddDanceModeState(layer.stateMachine, initialState);
            AddAfkState(layer.stateMachine, initialState);

            return layer;
        }

        private AnimatorState CreateModeState(AnimatorStateMachine stateMachine, string stateName, int modeType, Vector3 position)
        {
            var state = stateMachine.AddState(stateName, position);
            state.writeDefaultValues = false;
            state.motion = blankAnimationClip;
            state.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode, modeType),
            };
            return state;
        }

        private void AddInactiveState(AnimatorStateMachine stateMachine, AnimatorState initialState)
        {
            var inactiveState = CreateModeState(stateMachine, "Inactive", FacialExpressionControlModeType.FacialExpressionControlInactive, new Vector3(300, 80, 0));

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(inactiveState)
                .If(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();
        }

        private void AddNeutralState(AnimatorStateMachine stateMachine, AnimatorState initialState)
        {
            var neutralState = CreateModeState(stateMachine, "Neutral", FacialExpressionControlModeType.Neutral, new Vector3(300, 160, 0));

            // 左右どちらの手のジェスチャーも適用されていないパターン (Neutral)
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 0)
                .SetImmediateTransitionSettings();

            // 片手のジェスチャーが選ばれているが値が 0 (Neutral)
            for (int hand = 1; hand <= 2; hand++)
            {
                var currentGestureParam = hand == 1 ? InternalParameters.State_CurrentGestureLeft : InternalParameters.State_CurrentGestureRight;

                AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                    .If(VRCParameters.IS_LOCAL)
                    .If(InternalParameters.FacialExpressionControlON)
                    .IfNot(InternalParameters.State_AFKModeActive)
                    .IfNot(InternalParameters.State_DanceModeActive)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand, hand)
                    .Equals(currentGestureParam, 0)
                    .SetImmediateTransitionSettings();
            }

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionControlON)
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

            for (int hand = 1; hand <= 2; hand++)
            {
                var currentGestureParam = hand == 1 ? InternalParameters.State_CurrentGestureLeft : InternalParameters.State_CurrentGestureRight;

                AnimatorTransitionUtil.AddExitTransition(neutralState)
                    .IfNot(InternalParameters.FacialExpressionLocked)
                    .Equals(InternalParameters.State_CurrentGestureHand, hand)
                    .NotEqual(currentGestureParam, 0)
                    .SetImmediateTransitionSettings();
            }
        }

        /// <summary>
        /// 左右ハンドジェスチャー用のステートを <paramref name="spec"/> に基づいて生成する。
        /// Normal (未ロック時) と Fixed (ロック時) で Entry/Exit のロック条件のみ反転する。
        /// </summary>
        private void AddHandGestureState(AnimatorStateMachine stateMachine, AnimatorState initialState, HandGestureStateSpec spec)
        {
            var state = CreateModeState(stateMachine, spec.StateName, spec.ModeType, spec.Position);

            // Entry
            var entryTransition = AnimatorTransitionUtil.AddTransition(initialState, state)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, spec.HandValue)
                .NotEqual(spec.CurrentGestureParameter, 0);

            if (spec.IsFixed)
            {
                entryTransition.If(InternalParameters.FacialExpressionLocked);
            }
            else
            {
                entryTransition.IfNot(InternalParameters.FacialExpressionLocked);
            }
            entryTransition.SetImmediateTransitionSettings();

            // Exit - 共通 (Normal/Fixed 双方)
            AnimatorTransitionUtil.AddExitTransition(state)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            // Exit - ジェスチャー状態の変化 (Fixed のときだけ FacialExpressionLocked 解除が条件に追加される)
            var handMismatchExit = AnimatorTransitionUtil.AddExitTransition(state);
            if (spec.IsFixed)
            {
                handMismatchExit.IfNot(InternalParameters.FacialExpressionLocked);
            }
            handMismatchExit.NotEqual(InternalParameters.State_CurrentGestureHand, spec.HandValue)
                .SetImmediateTransitionSettings();

            var gestureResetExit = AnimatorTransitionUtil.AddExitTransition(state);
            if (spec.IsFixed)
            {
                gestureResetExit.IfNot(InternalParameters.FacialExpressionLocked);
            }
            gestureResetExit.Equals(InternalParameters.State_CurrentGestureHand, spec.HandValue)
                .Equals(spec.CurrentGestureParameter, 0)
                .SetImmediateTransitionSettings();

            // Exit - ロック状態の変化
            var lockExit = AnimatorTransitionUtil.AddExitTransition(state);
            if (spec.IsFixed)
            {
                lockExit.IfNot(InternalParameters.FacialExpressionLocked);
            }
            else
            {
                lockExit.If(InternalParameters.FacialExpressionLocked);
            }
            lockExit.SetImmediateTransitionSettings();
        }

        private void AddSelectedFacialExpressionInMenuState(AnimatorStateMachine stateMachine, AnimatorState initialState)
        {
            var state = CreateModeState(stateMachine, "Selected Facial Expression (In Menu)", FacialExpressionControlModeType.SelectedFacialExpressionInMenu, new Vector3(300, 560, 0));

            AnimatorTransitionUtil.AddTransition(initialState, state)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .IfNot(InternalParameters.State_AFKModeActive)
                .IfNot(InternalParameters.State_DanceModeActive)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();
        }

        private void AddDanceModeState(AnimatorStateMachine stateMachine, AnimatorState initialState)
        {
            var state = CreateModeState(stateMachine, "Dance Mode", FacialExpressionControlModeType.DanceMode, new Vector3(300, 640, 0));

            AnimatorTransitionUtil.AddTransition(initialState, state)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .IfNot(InternalParameters.State_AFKModeActive)
                .If(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .IfNot(InternalParameters.State_DanceModeActive)
                .SetImmediateTransitionSettings();
        }

        private void AddAfkState(AnimatorStateMachine stateMachine, AnimatorState initialState)
        {
            var state = CreateModeState(stateMachine, "AFK", FacialExpressionControlModeType.AFKMode, new Vector3(300, 880, 0));

            AnimatorTransitionUtil.AddTransition(initialState, state)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON)
                .If(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .IfNot(InternalParameters.FacialExpressionControlON)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .IfNot(InternalParameters.State_AFKModeActive)
                .SetImmediateTransitionSettings();
        }
    }
}
