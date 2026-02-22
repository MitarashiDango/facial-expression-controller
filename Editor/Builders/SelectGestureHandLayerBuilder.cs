using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class SelectGestureHandLayerBuilder : LayerBuilderBase
    {
        public SelectGestureHandLayerBuilder(AnimationClip blankClip) : base(blankClip)
        {
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_SELECT_GESTURE_HAND");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(800, 120, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -40, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 120, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            // 優先度などをもとに、ジェスチャーを適用する手を選択する
            var firstWinState = layer.stateMachine.AddStateMachine("First Win", new Vector3(400, 0, 0));
            AnimatorTransitionUtil.AddTransition(initialState, firstWinState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .SetImmediateTransitionSettings();
            AnimatorTransitionUtil.AddExitTransition(firstWinState, layer.stateMachine);
            CreateGesturePriorityFirstWinState(firstWinState);

            var lastWinState = layer.stateMachine.AddStateMachine("Last Win", new Vector3(400, 80, 0));
            AnimatorTransitionUtil.AddTransition(initialState, lastWinState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LastWin)
                .SetImmediateTransitionSettings();
            AnimatorTransitionUtil.AddExitTransition(lastWinState, layer.stateMachine);
            CreateGesturePriorityLastWinState(lastWinState);

            var leftHandPriorityState = layer.stateMachine.AddStateMachine("Left Hand Priority", new Vector3(400, 160, 0));
            AnimatorTransitionUtil.AddTransition(initialState, leftHandPriorityState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .SetImmediateTransitionSettings();
            AnimatorTransitionUtil.AddExitTransition(leftHandPriorityState, layer.stateMachine);
            CreateGesturePriorityLeftHandPriorityState(leftHandPriorityState);

            var rightHandPriorityState = layer.stateMachine.AddStateMachine("Right Hand Priority", new Vector3(400, 240, 0));
            AnimatorTransitionUtil.AddTransition(initialState, rightHandPriorityState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .SetImmediateTransitionSettings();
            AnimatorTransitionUtil.AddExitTransition(rightHandPriorityState, layer.stateMachine);
            CreateGesturePriorityRightHandPriorityState(rightHandPriorityState);

            return layer;
        }

        /// <summary>
        /// ジェスチャー優先順位(後勝ち)制御用サブステートの生成
        /// </summary>
        /// <param name="stateMachine"></param>
        private void CreateGesturePriorityLastWinState(AnimatorStateMachine stateMachine)
        {
            stateMachine.entryPosition = new Vector3(0, 160, 0);
            stateMachine.exitPosition = new Vector3(800, 160, 0);
            stateMachine.anyStatePosition = new Vector3(0, -80, 0);
            stateMachine.parentStateMachinePosition = new Vector3(0, -200, 0);

            var initialState = stateMachine.AddState("Initial State (Neutral)", new Vector3(300, 160, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;
            initialState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 0),
            };

            var leftHandStateMachine = stateMachine.AddStateMachine("Left Hand", new Vector3(500, 0, 0));
            leftHandStateMachine.entryPosition = new Vector3(0, 0, 0);
            leftHandStateMachine.exitPosition = new Vector3(800, 0, 0);
            leftHandStateMachine.anyStatePosition = new Vector3(0, -80, 0);
            leftHandStateMachine.parentStateMachinePosition = new Vector3(800, 560, 0);

            var rightHandStateMachine = stateMachine.AddStateMachine("Right Hand", new Vector3(500, 320, 0));
            rightHandStateMachine.entryPosition = new Vector3(0, 0, 0);
            rightHandStateMachine.exitPosition = new Vector3(800, 0, 0);
            rightHandStateMachine.anyStatePosition = new Vector3(0, -80, 0);
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

            int currentY = 0;
            int spacingY = 80;

            // 0: Neutral用のステート生成
            var rightNeutralState = leftHandStateMachine.AddState("Neutral (Right Hand)", new Vector3(400, currentY, 0));
            rightNeutralState.writeDefaultValues = false; rightNeutralState.motion = blankAnimationClip;
            rightNeutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 1)
            };
            AddGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightNeutralState, 0);

            var leftNeutralState = rightHandStateMachine.AddState("Neutral (Left Hand)", new Vector3(400, currentY, 0));
            leftNeutralState.writeDefaultValues = false; leftNeutralState.motion = blankAnimationClip;
            leftNeutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 2)
            };
            AddGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftNeutralState, 0);

            // 1〜7: ハンドジェスチャー用のステートを生成
            foreach (var gesture in GestureConstants.Gestures)
            {
                currentY += spacingY;

                // 左手が選択されている際の右手の状態
                var rightState = leftHandStateMachine.AddState($"{gesture.Name} (Right Hand)", new Vector3(400, currentY, 0));
                rightState.writeDefaultValues = false; rightState.motion = blankAnimationClip;
                rightState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 1)
                };
                AddGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightState, gesture.Value);

                // 右手が選択されている際の左手の状態
                var leftState = rightHandStateMachine.AddState($"{gesture.Name} (Left Hand)", new Vector3(400, currentY, 0));
                leftState.writeDefaultValues = false; leftState.motion = blankAnimationClip;
                leftState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 2)
                };
                AddGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftState, gesture.Value);
            }
        }

        /// <summary>
        /// ジェスチャー優先順位(先勝ち)制御用サブステートの生成
        /// </summary>
        /// <param name="stateMachine"></param>
        private void CreateGesturePriorityFirstWinState(AnimatorStateMachine stateMachine)
        {
            stateMachine.entryPosition = new Vector3(0, 160, 0);
            stateMachine.exitPosition = new Vector3(800, 160, 0);
            stateMachine.anyStatePosition = new Vector3(0, -80, 0);
            stateMachine.parentStateMachinePosition = new Vector3(0, -200, 0);

            var neutralState = stateMachine.AddState("Neutral", new Vector3(300, 160, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 0),
            };

            var leftHandState = stateMachine.AddState("Left Hand", new Vector3(500, 0, 0));
            leftHandState.writeDefaultValues = false;
            leftHandState.motion = blankAnimationClip;
            leftHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 1),
            };

            var rightHandState = stateMachine.AddState("Right Hand", new Vector3(500, 320, 0));
            rightHandState.writeDefaultValues = false;
            rightHandState.motion = blankAnimationClip;
            rightHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 2),
            };

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, neutralState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandState)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandState)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddTransition(neutralState, leftHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(neutralState, rightHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, neutralState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, rightHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, neutralState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, leftHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.FirstWin)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();
        }

        /// <summary>
        /// ジェスチャー優先順位(左手優先)制御用サブステートの生成
        /// </summary>
        /// <param name="stateMachine"></param>
        private void CreateGesturePriorityLeftHandPriorityState(AnimatorStateMachine stateMachine)
        {
            stateMachine.entryPosition = new Vector3(0, 160, 0);
            stateMachine.exitPosition = new Vector3(800, 160, 0);
            stateMachine.anyStatePosition = new Vector3(0, -80, 0);
            stateMachine.parentStateMachinePosition = new Vector3(0, -200, 0);

            var neutralState = stateMachine.AddState("Neutral", new Vector3(300, 160, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 0),
            };

            var leftHandState = stateMachine.AddState("Left Hand", new Vector3(500, 0, 0));
            leftHandState.writeDefaultValues = false;
            leftHandState.motion = blankAnimationClip;
            leftHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 1),
            };

            var rightHandState = stateMachine.AddState("Right Hand", new Vector3(500, 320, 0));
            rightHandState.writeDefaultValues = false;
            rightHandState.motion = blankAnimationClip;
            rightHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 2),
            };

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, neutralState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandState)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddTransition(neutralState, leftHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(neutralState, rightHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, neutralState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, rightHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, neutralState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, leftHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.LeftHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();
        }

        /// <summary>
        /// ジェスチャー優先順位(右手優先)制御用サブステートの生成
        /// </summary>
        /// <param name="stateMachine"></param>
        private void CreateGesturePriorityRightHandPriorityState(AnimatorStateMachine stateMachine)
        {
            stateMachine.entryPosition = new Vector3(0, 160, 0);
            stateMachine.exitPosition = new Vector3(800, 160, 0);
            stateMachine.anyStatePosition = new Vector3(0, -80, 0);
            stateMachine.parentStateMachinePosition = new Vector3(0, -200, 0);

            var neutralState = stateMachine.AddState("Neutral", new Vector3(300, 160, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 0),
            };

            var leftHandState = stateMachine.AddState("Left Hand", new Vector3(500, 0, 0));
            leftHandState.writeDefaultValues = false;
            leftHandState.motion = blankAnimationClip;
            leftHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 1),
            };

            var rightHandState = stateMachine.AddState("Right Hand", new Vector3(500, 320, 0));
            rightHandState.writeDefaultValues = false;
            rightHandState.motion = blankAnimationClip;
            rightHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand, 2),
            };

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, neutralState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandState)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandState)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddTransition(neutralState, leftHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(neutralState, rightHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, neutralState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, rightHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, neutralState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, leftHandState)
                .Equals(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandState)
                .NotEqual(InternalParameters.GesturePriority, GesturePriorityType.RightHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .SetImmediateTransitionSettings();
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