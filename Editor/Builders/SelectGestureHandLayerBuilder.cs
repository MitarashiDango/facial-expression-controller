using System;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class SelectGestureHandLayerBuilder : LayerBuilderBase
    {
        /// <summary>
        /// ジェスチャー優先順位別のサブステート生成ルール<br />
        /// Neutral / Left Hand / Right Hand の 3 ステートで構成される単純構造の優先度モード
        /// (FirstWin / LeftHandPriority / RightHandPriority) 用
        /// </summary>
        private class SimplePriorityRuleSet
        {
            public int PriorityValue;
            public Action<AnimatorTransitionBuilder> LeftEntryCondition;
            public Action<AnimatorTransitionBuilder> RightEntryCondition;
            public Action<AnimatorStateTransitionBuilder> NeutralToLeftCondition;
            public Action<AnimatorStateTransitionBuilder> NeutralToRightCondition;
            public Action<AnimatorStateTransitionBuilder> LeftToRightCondition;
            public Action<AnimatorStateTransitionBuilder> RightToLeftCondition;
        }

        public SelectGestureHandLayerBuilder(AnimationClip blankClip) : base(blankClip)
        {
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_SELECT_GESTURE_HAND");

            layer.stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            layer.stateMachine.exitPosition = new Vector3(800, 120, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -40, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 120, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            // 優先度などをもとに、ジェスチャーを適用する手を選択する
            AddPriorityStateMachine(layer.stateMachine, initialState, "First Win", new Vector3(400, 0, 0), GesturePriorityType.FirstWin,
                sm => CreateSimpleGesturePriorityState(sm, BuildFirstWinRuleSet()));

            AddPriorityStateMachine(layer.stateMachine, initialState, "Last Win", new Vector3(400, 80, 0), GesturePriorityType.LastWin,
                CreateGesturePriorityLastWinState);

            AddPriorityStateMachine(layer.stateMachine, initialState, "Left Hand Priority", new Vector3(400, 160, 0), GesturePriorityType.LeftHandPriority,
                sm => CreateSimpleGesturePriorityState(sm, BuildLeftHandPriorityRuleSet()));

            AddPriorityStateMachine(layer.stateMachine, initialState, "Right Hand Priority", new Vector3(400, 240, 0), GesturePriorityType.RightHandPriority,
                sm => CreateSimpleGesturePriorityState(sm, BuildRightHandPriorityRuleSet()));

            return layer;
        }

        /// <summary>
        /// ルート StateMachine 上に 1 優先度分のサブ StateMachine を追加し、初期状態からの分岐・復帰遷移を張る
        /// </summary>
        private void AddPriorityStateMachine(
            AnimatorStateMachine rootStateMachine,
            AnimatorState initialState,
            string name,
            Vector3 position,
            int priorityValue,
            Action<AnimatorStateMachine> populate)
        {
            var stateMachine = rootStateMachine.AddStateMachine(name, position);
            AnimatorTransitionUtil.AddTransition(initialState, stateMachine)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority, priorityValue)
                .SetImmediateTransitionSettings();
            AnimatorTransitionUtil.AddExitTransition(stateMachine, rootStateMachine);
            populate(stateMachine);
        }

        /// <summary>
        /// Neutral / Left / Right の 3 ステートで表現できる優先度モード (FirstWin / LeftHandPriority / RightHandPriority) の共通生成処理
        /// </summary>
        private void CreateSimpleGesturePriorityState(AnimatorStateMachine stateMachine, SimplePriorityRuleSet rules)
        {
            SetupPriorityStateMachineLayout(stateMachine);

            var neutralState = CreateHandSelectionState(stateMachine, "Neutral", 0, new Vector3(300, 160, 0));
            var leftHandState = CreateHandSelectionState(stateMachine, "Left Hand", 1, new Vector3(500, 0, 0));
            var rightHandState = CreateHandSelectionState(stateMachine, "Right Hand", 2, new Vector3(500, 320, 0));

            // Entry Transitions (Neutral は常に両手ともジェスチャーが 0 の場合)
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, neutralState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            rules.LeftEntryCondition(AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandState));
            rules.RightEntryCondition(AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandState));

            // Inter-State Transitions
            AddSimplePriorityTransition(neutralState, leftHandState, rules.PriorityValue, rules.NeutralToLeftCondition);
            AddSimplePriorityTransition(neutralState, rightHandState, rules.PriorityValue, rules.NeutralToRightCondition);
            AddSimplePriorityTransition(leftHandState, neutralState, rules.PriorityValue, BothGesturesZero);
            AddSimplePriorityTransition(leftHandState, rightHandState, rules.PriorityValue, rules.LeftToRightCondition);
            AddSimplePriorityTransition(rightHandState, neutralState, rules.PriorityValue, BothGesturesZero);
            AddSimplePriorityTransition(rightHandState, leftHandState, rules.PriorityValue, rules.RightToLeftCondition);

            // Exit Transitions (3 ステートとも同じ)
            foreach (var state in new[] { neutralState, leftHandState, rightHandState })
            {
                AnimatorTransitionUtil.AddExitTransition(state)
                    .NotEqual(InternalParameters.GesturePriority, rules.PriorityValue)
                    .IfNot(InternalParameters.FacialExpressionLocked)
                    .SetImmediateTransitionSettings();
            }
        }

        private static SimplePriorityRuleSet BuildFirstWinRuleSet()
        {
            return new SimplePriorityRuleSet
            {
                PriorityValue = GesturePriorityType.FirstWin,
                LeftEntryCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0),
                RightEntryCondition = t => t.NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                NeutralToLeftCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0),
                NeutralToRightCondition = t => t.NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                LeftToRightCondition = t => t.Equals(VRCParameters.GESTURE_LEFT, 0).NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                RightToLeftCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0).Equals(VRCParameters.GESTURE_RIGHT, 0),
            };
        }

        private static SimplePriorityRuleSet BuildLeftHandPriorityRuleSet()
        {
            return new SimplePriorityRuleSet
            {
                PriorityValue = GesturePriorityType.LeftHandPriority,
                LeftEntryCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0),
                RightEntryCondition = t => t.Equals(VRCParameters.GESTURE_LEFT, 0).NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                NeutralToLeftCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0),
                NeutralToRightCondition = t => t.Equals(VRCParameters.GESTURE_LEFT, 0).NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                LeftToRightCondition = t => t.Equals(VRCParameters.GESTURE_LEFT, 0).NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                RightToLeftCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0),
            };
        }

        private static SimplePriorityRuleSet BuildRightHandPriorityRuleSet()
        {
            return new SimplePriorityRuleSet
            {
                PriorityValue = GesturePriorityType.RightHandPriority,
                LeftEntryCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0).Equals(VRCParameters.GESTURE_RIGHT, 0),
                RightEntryCondition = t => t.NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                NeutralToLeftCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0).Equals(VRCParameters.GESTURE_RIGHT, 0),
                NeutralToRightCondition = t => t.NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                LeftToRightCondition = t => t.NotEqual(VRCParameters.GESTURE_RIGHT, 0),
                RightToLeftCondition = t => t.NotEqual(VRCParameters.GESTURE_LEFT, 0).Equals(VRCParameters.GESTURE_RIGHT, 0),
            };
        }

        private static void BothGesturesZero(AnimatorStateTransitionBuilder builder)
        {
            builder
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);
        }

        private static void SetupPriorityStateMachineLayout(AnimatorStateMachine stateMachine)
        {
            stateMachine.entryPosition = new Vector3(0, 160, 0);
            stateMachine.exitPosition = new Vector3(800, 160, 0);
            stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
            stateMachine.parentStateMachinePosition = new Vector3(0, -200, 0);
        }

        private AnimatorState CreateHandSelectionState(AnimatorStateMachine stateMachine, string name, int currentGestureHandValue, Vector3 position)
        {
            var state = stateMachine.AddState(name, position);
            state.writeDefaultValues = false;
            state.motion = blankAnimationClip;
            state.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, currentGestureHandValue),
            };
            return state;
        }

        private static void AddSimplePriorityTransition(
            AnimatorState from,
            AnimatorState to,
            int priorityValue,
            Action<AnimatorStateTransitionBuilder> condition)
        {
            var builder = AnimatorTransitionUtil.AddTransition(from, to)
                .Equals(InternalParameters.GesturePriority, priorityValue);
            condition(builder);
            builder
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();
        }

        /// <summary>
        /// ジェスチャー優先順位(後勝ち)制御用サブステートの生成
        /// </summary>
        private void CreateGesturePriorityLastWinState(AnimatorStateMachine stateMachine)
        {
            SetupPriorityStateMachineLayout(stateMachine);

            var initialState = stateMachine.AddState("Initial State (Neutral)", new Vector3(300, 160, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;
            initialState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 0),
            };

            var leftHandStateMachine = stateMachine.AddStateMachine("Left Hand", new Vector3(500, 0, 0));
            leftHandStateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            leftHandStateMachine.exitPosition = new Vector3(800, 0, 0);
            leftHandStateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
            leftHandStateMachine.parentStateMachinePosition = new Vector3(800, 560, 0);

            var rightHandStateMachine = stateMachine.AddStateMachine("Right Hand", new Vector3(500, 320, 0));
            rightHandStateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            rightHandStateMachine.exitPosition = new Vector3(800, 0, 0);
            rightHandStateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
            rightHandStateMachine.parentStateMachinePosition = new Vector3(800, 560, 0);

            // Entry Transitions
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, initialState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0);

            // Inter-State Transitions
            AnimatorTransitionUtil.AddTransition(initialState, leftHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, rightHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            // Exit Transitions
            AnimatorTransitionUtil.AddExitTransition(initialState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LastWin)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandStateMachine, stateMachine)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LastWin);

            AnimatorTransitionUtil.AddExitTransition(rightHandStateMachine, stateMachine)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LastWin);

            float currentY = 0;
            float spacingY = AnimatorLayout.RowSpacing;

            // 0: Neutral用のステート生成
            var rightNeutralState = CreateLastWinOpposingHandState(leftHandStateMachine, "Neutral (Right Hand)", 1, new Vector3(400, currentY, 0));
            AddGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightNeutralState, 0);

            var leftNeutralState = CreateLastWinOpposingHandState(rightHandStateMachine, "Neutral (Left Hand)", 2, new Vector3(400, currentY, 0));
            AddGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftNeutralState, 0);

            // 1〜7: ハンドジェスチャー用のステートを生成
            foreach (var gesture in GestureConstants.Gestures)
            {
                currentY += spacingY;

                // 左手が選択されている際の右手の状態
                var rightState = CreateLastWinOpposingHandState(leftHandStateMachine, $"{gesture.Name} (Right Hand)", 1, new Vector3(400, currentY, 0));
                AddGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightState, gesture.Value);

                // 右手が選択されている際の左手の状態
                var leftState = CreateLastWinOpposingHandState(rightHandStateMachine, $"{gesture.Name} (Left Hand)", 2, new Vector3(400, currentY, 0));
                AddGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftState, gesture.Value);
            }
        }

        private AnimatorState CreateLastWinOpposingHandState(AnimatorStateMachine stateMachine, string name, int currentGestureHandValue, Vector3 position)
        {
            var state = stateMachine.AddState(name, position);
            state.writeDefaultValues = false;
            state.motion = blankAnimationClip;
            state.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, currentGestureHandValue)
            };
            return state;
        }

        private void AddGestureTransition(AnimatorStateMachine sm1, AnimatorStateMachine sm2, string gestureHandParamName, AnimatorState handState, int gestureNumber)
        {
            AnimatorTransitionUtil.AddEntryTransition(sm1, handState).Equals(gestureHandParamName, gestureNumber);

            AnimatorTransitionUtil.AddTransition(handState, sm2)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LastWin)
                .NotEqual(gestureHandParamName, gestureNumber)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(handState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LastWin)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();
        }
    }
}
