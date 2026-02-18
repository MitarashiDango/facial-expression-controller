using System;
using System.Collections.Generic;
using System.Linq;
using MitarashiDango.FacialExpressionController.Runtime;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class FacialExpressionControlAnimatorControllerBuilder
    {
        private AnimationClip blankAnimationClip = new AnimationClip
        {
            name = "blank"
        };

        private enum HandType
        {
            Left,
            Right
        }

        public AnimatorController CreateMainAnimatorController(BuildContext ctx, FacialExpressionControl fec)
        {
            var parameters = new Parameters();

            var ac = new AnimatorController
            {
                name = "FEC_MAIN",
                parameters = parameters.CreateAnimatorControllerParameters()
            };

            if (ac.layers.Length == 0)
            {
                ac.AddLayer("DUMMY_LAYER");
            }

            // 内部処理用レイヤー
            ac.AddLayer(CreateHandGestureLayer(HandType.Left));
            ac.AddLayer(CreateHandGestureLayer(HandType.Right));
            ac.AddLayer(CreateSelectGestureHandLayer());
            if (fec.useAFKMode)
            {
                ac.AddLayer(CreateAFKModeControlLayer(fec));
            }
            ac.AddLayer(CreateDanceModeControlLayer());
            ac.AddLayer(CreateVehicleModeControlLayer());
            ac.AddLayer(CreateFacialExpressionGestureLockLayer());
            ac.AddLayer(CreateCopyGestureWeightLayer());
            ac.AddLayer(CreateSelectFacialExpressionControlModeLayer());
            ac.AddLayer(CreateSelectFacialExpressionNumberLayer(fec));

            // 表情適用レイヤー
            ac.AddLayer(CreateFacialExpressionControlLayer(fec, ctx.AvatarRootObject));

            return ac;
        }

        public AnimatorController CreateDefaultFacialExpressionAnimatorController(BuildContext ctx, FacialExpressionControl fec)
        {
            var parameters = new Parameters();

            var ac = new AnimatorController
            {
                name = "FEC_DEFAULT_FACIAL_EXPRESION",
                parameters = parameters.CreateAnimatorControllerParameters()
            };

            // 表情適用レイヤー
            if (fec.defaultFace != null || fec.generateDefaultFacialAnimation)
            {
                ac.AddLayer(CreateDefaultFacialExpressionLayer(fec, ctx.AvatarRootObject));
            }

            return ac;
        }

        /// <summary>
        /// ハンドジェスチャー判定レイヤーの生成
        /// </summary>
        /// <param name="handType">対象の手</param>
        /// <returns>ハンドジェスチャー判定レイヤー</returns>
        private AnimatorControllerLayer CreateHandGestureLayer(HandType handType)
        {
            AnimatorControllerLayer layer;
            string currentGestureParameterName;
            string gestureParameterName;
            int lastGestureChangedHandValue;

            if (handType == HandType.Left)
            {
                layer = CreateAnimatorControllerLayer("FEC_LEFT_HAND_GESTURE");
                currentGestureParameterName = InternalParameters.State_CurrentGestureLeft.name;
                gestureParameterName = VRCParameters.GESTURE_LEFT;
                lastGestureChangedHandValue = 1;
            }
            else
            {
                layer = CreateAnimatorControllerLayer("FEC_RIGHT_HAND_GESTURE");
                currentGestureParameterName = InternalParameters.State_CurrentGestureRight.name;
                gestureParameterName = VRCParameters.GESTURE_RIGHT;
                lastGestureChangedHandValue = 2;
            }

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -40, 0);
            layer.stateMachine.exitPosition = new Vector3(800, 0, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(500, 0, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 0),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var fistState = layer.stateMachine.AddState("Fist", new Vector3(500, 60, 0));
            fistState.writeDefaultValues = false;
            fistState.motion = blankAnimationClip;
            fistState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 1),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var handOpenState = layer.stateMachine.AddState("HandOpen", new Vector3(500, 120, 0));
            handOpenState.writeDefaultValues = false;
            handOpenState.motion = blankAnimationClip;
            handOpenState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 2),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var fingerPointState = layer.stateMachine.AddState("FingerPoint", new Vector3(500, 180, 0));
            fingerPointState.writeDefaultValues = false;
            fingerPointState.motion = blankAnimationClip;
            fingerPointState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 3),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var victoryState = layer.stateMachine.AddState("Victory", new Vector3(500, 240, 0));
            victoryState.writeDefaultValues = false;
            victoryState.motion = blankAnimationClip;
            victoryState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 4),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var rockNRollState = layer.stateMachine.AddState("RockNRoll", new Vector3(500, 300, 0));
            rockNRollState.writeDefaultValues = false;
            rockNRollState.motion = blankAnimationClip;
            rockNRollState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 5),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var handGunState = layer.stateMachine.AddState("HandGun", new Vector3(500, 360, 0));
            handGunState.writeDefaultValues = false;
            handGunState.motion = blankAnimationClip;
            handGunState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 6),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            var thumbsUpState = layer.stateMachine.AddState("ThumbsUp", new Vector3(500, 420, 0));
            thumbsUpState.writeDefaultValues = false;
            thumbsUpState.motion = blankAnimationClip;
            thumbsUpState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 7),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand.name, lastGestureChangedHandValue),
            };

            // [Initial State] -> [Neutral]
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 0)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Fist]
            AnimatorTransitionUtil.AddTransition(initialState, fistState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 1)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [HandOpen]
            AnimatorTransitionUtil.AddTransition(initialState, handOpenState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 2)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [FingerPoint]
            AnimatorTransitionUtil.AddTransition(initialState, fingerPointState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 3)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Victory]
            AnimatorTransitionUtil.AddTransition(initialState, victoryState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 4)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [RockNRoll]
            AnimatorTransitionUtil.AddTransition(initialState, rockNRollState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 5)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [HandGun]
            AnimatorTransitionUtil.AddTransition(initialState, handGunState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 6)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [ThumbsUp]
            AnimatorTransitionUtil.AddTransition(initialState, thumbsUpState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(gestureParameterName, 7)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            // [Neutral] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 0)
                .SetImmediateTransitionSettings();

            // [Fist] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(fistState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 1)
                .SetImmediateTransitionSettings();

            // [HandOpen] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(handOpenState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 2)
                .SetImmediateTransitionSettings();

            // [FingerPoint] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(fingerPointState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 3)
                .SetImmediateTransitionSettings();

            // [Victory] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(victoryState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 4)
                .SetImmediateTransitionSettings();

            // [RockNRoll] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(rockNRollState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 5)
                .SetImmediateTransitionSettings();

            // [HandGun] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(handGunState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 6)
                .SetImmediateTransitionSettings();

            // [ThumbsUp] -> [Exit]
            AnimatorTransitionUtil.AddExitTransition(thumbsUpState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(gestureParameterName, 7)
                .SetImmediateTransitionSettings();

            return layer;
        }

        /// <summary>
        /// ハンドジェスチャー適用対象判定レイヤーの生成
        /// </summary>
        /// <returns>ハンドジェスチャー適用対象判定レイヤー</returns>
        private AnimatorControllerLayer CreateSelectGestureHandLayer()
        {
            var layer = CreateAnimatorControllerLayer("FEC_SELECT_GESTURE_HAND");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(800, 120, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -40, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 120, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            // 優先度などをもとに、ジェスチャーを適用する手を選択する
            var gesturePriorityFirstWinState = layer.stateMachine.AddStateMachine("First Win", new Vector3(400, 0, 0));
            AnimatorTransitionUtil.AddTransition(initialState, gesturePriorityFirstWinState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(gesturePriorityFirstWinState, layer.stateMachine);

            CreateGesturePriorityFirstWinState(gesturePriorityFirstWinState);

            var gesturePriorityLastWinState = layer.stateMachine.AddStateMachine("Last Win", new Vector3(400, 80, 0));
            AnimatorTransitionUtil.AddTransition(initialState, gesturePriorityLastWinState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LastWin)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(gesturePriorityLastWinState, layer.stateMachine);

            CreateGesturePriorityLastWinState(gesturePriorityLastWinState);

            var gesturePriorityLeftHandPriorityState = layer.stateMachine.AddStateMachine("Left Hand Priority", new Vector3(400, 160, 0));
            AnimatorTransitionUtil.AddTransition(initialState, gesturePriorityLeftHandPriorityState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(gesturePriorityLeftHandPriorityState, layer.stateMachine);

            CreateGesturePriorityLeftHandPriorityState(gesturePriorityLeftHandPriorityState);

            var gesturePriorityRightHandPriorityState = layer.stateMachine.AddStateMachine("Right Hand Priority", new Vector3(400, 240, 0));
            AnimatorTransitionUtil.AddTransition(initialState, gesturePriorityRightHandPriorityState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(gesturePriorityRightHandPriorityState, layer.stateMachine);

            CreateGesturePriorityRightHandPriorityState(gesturePriorityRightHandPriorityState);

            return layer;
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
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 0),
            };

            var leftHandState = stateMachine.AddState("Left Hand", new Vector3(500, 0, 0));
            leftHandState.writeDefaultValues = false;
            leftHandState.motion = blankAnimationClip;
            leftHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            var rightHandState = stateMachine.AddState("Right Hand", new Vector3(500, 320, 0));
            rightHandState.writeDefaultValues = false;
            rightHandState.motion = blankAnimationClip;
            rightHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, neutralState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandState)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandState)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddTransition(neutralState, leftHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(neutralState, rightHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, neutralState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, rightHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, neutralState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, leftHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.FirstWin)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();
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
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 0),
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

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, initialState)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0);

            AnimatorTransitionUtil.AddTransition(initialState, leftHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, rightHandStateMachine)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(initialState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LastWin)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandStateMachine, stateMachine)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LastWin);

            AnimatorTransitionUtil.AddExitTransition(rightHandStateMachine, stateMachine)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LastWin);

            Action<AnimatorStateMachine, AnimatorStateMachine, string, AnimatorState, int> addGestureTransition = (sm1, sm2, gestureHandParamName, handState, gestureNumber) =>
            {
                AnimatorTransitionUtil.AddEntryTransition(sm1, handState)
                    .Equals(gestureHandParamName, gestureNumber);

                AnimatorTransitionUtil.AddTransition(handState, sm2)
                    .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LastWin)
                    .NotEqual(gestureHandParamName, gestureNumber)
                    .IfNot(InternalParameters.FacialExpressionLocked.name)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(handState)
                    .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LastWin)
                    .IfNot(InternalParameters.FacialExpressionLocked.name)
                    .SetImmediateTransitionSettings();
            };

            //---------------------------------------------------------
            // 左手のジェスチャーが選択されている際の右手の状態
            //---------------------------------------------------------
            var rightNeutralState = leftHandStateMachine.AddState("Neutral (Right Hand)", new Vector3(400, 0, 0));
            rightNeutralState.writeDefaultValues = false;
            rightNeutralState.motion = blankAnimationClip;
            rightNeutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightNeutralState, 0);

            var rightFistState = leftHandStateMachine.AddState("Fist (Right Hand)", new Vector3(400, 80, 0));
            rightFistState.writeDefaultValues = false;
            rightFistState.motion = blankAnimationClip;
            rightFistState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightFistState, 1);

            var rightHandOpenState = leftHandStateMachine.AddState("HandOpen (Right Hand)", new Vector3(400, 160, 0));
            rightHandOpenState.writeDefaultValues = false;
            rightHandOpenState.motion = blankAnimationClip;
            rightHandOpenState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightHandOpenState, 2);

            var rightFingerPointState = leftHandStateMachine.AddState("FingerPoint (Right Hand)", new Vector3(400, 240, 0));
            rightFingerPointState.writeDefaultValues = false;
            rightFingerPointState.motion = blankAnimationClip;
            rightFingerPointState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightFingerPointState, 3);

            var rightVictoryState = leftHandStateMachine.AddState("Victory (Right Hand)", new Vector3(400, 320, 0));
            rightVictoryState.writeDefaultValues = false;
            rightVictoryState.motion = blankAnimationClip;
            rightVictoryState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightVictoryState, 4);

            var rightRockNRollState = leftHandStateMachine.AddState("RockNRoll (Right Hand)", new Vector3(400, 400, 0));
            rightRockNRollState.writeDefaultValues = false;
            rightRockNRollState.motion = blankAnimationClip;
            rightRockNRollState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightRockNRollState, 5);

            var rightHandGunState = leftHandStateMachine.AddState("HandGun (Right Hand)", new Vector3(400, 480, 0));
            rightHandGunState.writeDefaultValues = false;
            rightHandGunState.motion = blankAnimationClip;
            rightHandGunState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightHandGunState, 6);

            var rightThumbsUpState = leftHandStateMachine.AddState("ThumbsUp (Right Hand)", new Vector3(400, 560, 0));
            rightThumbsUpState.writeDefaultValues = false;
            rightThumbsUpState.motion = blankAnimationClip;
            rightThumbsUpState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            addGestureTransition(leftHandStateMachine, rightHandStateMachine, VRCParameters.GESTURE_RIGHT, rightThumbsUpState, 7);

            //---------------------------------------------------------
            // 右手のジェスチャーが選択されている際の左手の状態
            //---------------------------------------------------------
            var leftNeutralState = rightHandStateMachine.AddState("Neutral (Left Hand)", new Vector3(400, 0, 0));
            leftNeutralState.writeDefaultValues = false;
            leftNeutralState.motion = blankAnimationClip;
            leftNeutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftNeutralState, 0);

            var leftFistState = rightHandStateMachine.AddState("Fist (Left Hand)", new Vector3(400, 80, 0));
            leftFistState.writeDefaultValues = false;
            leftFistState.motion = blankAnimationClip;
            leftFistState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftFistState, 1);

            var leftHandOpenState = rightHandStateMachine.AddState("HandOpen (Left Hand)", new Vector3(400, 160, 0));
            leftHandOpenState.writeDefaultValues = false;
            leftHandOpenState.motion = blankAnimationClip;
            leftHandOpenState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftHandOpenState, 2);

            var leftFingerPointState = rightHandStateMachine.AddState("FingerPoint (Left Hand)", new Vector3(400, 240, 0));
            leftFingerPointState.writeDefaultValues = false;
            leftFingerPointState.motion = blankAnimationClip;
            leftFingerPointState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftFingerPointState, 3);

            var leftVictoryState = rightHandStateMachine.AddState("Victory (Left Hand)", new Vector3(400, 320, 0));
            leftVictoryState.writeDefaultValues = false;
            leftVictoryState.motion = blankAnimationClip;
            leftVictoryState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftVictoryState, 4);

            var leftRockNRollState = rightHandStateMachine.AddState("RockNRoll (Left Hand)", new Vector3(400, 400, 0));
            leftRockNRollState.writeDefaultValues = false;
            leftRockNRollState.motion = blankAnimationClip;
            leftRockNRollState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftRockNRollState, 5);

            var leftHandGunState = rightHandStateMachine.AddState("HandGun (Left Hand)", new Vector3(400, 480, 0));
            leftHandGunState.writeDefaultValues = false;
            leftHandGunState.motion = blankAnimationClip;
            leftHandGunState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftHandGunState, 6);

            var leftThumbsUpState = rightHandStateMachine.AddState("ThumbsUp (Left Hand)", new Vector3(400, 560, 0));
            leftThumbsUpState.writeDefaultValues = false;
            leftThumbsUpState.motion = blankAnimationClip;
            leftThumbsUpState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
            };

            addGestureTransition(rightHandStateMachine, leftHandStateMachine, VRCParameters.GESTURE_LEFT, leftThumbsUpState, 7);
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
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 0),
            };

            var leftHandState = stateMachine.AddState("Left Hand", new Vector3(500, 0, 0));
            leftHandState.writeDefaultValues = false;
            leftHandState.motion = blankAnimationClip;
            leftHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            var rightHandState = stateMachine.AddState("Right Hand", new Vector3(500, 320, 0));
            rightHandState.writeDefaultValues = false;
            rightHandState.motion = blankAnimationClip;
            rightHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
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
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(neutralState, rightHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, neutralState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, rightHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, neutralState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, leftHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.LeftHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
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
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 0),
            };

            var leftHandState = stateMachine.AddState("Left Hand", new Vector3(500, 0, 0));
            leftHandState.writeDefaultValues = false;
            leftHandState.motion = blankAnimationClip;
            leftHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 1),
            };

            var rightHandState = stateMachine.AddState("Right Hand", new Vector3(500, 320, 0));
            rightHandState.writeDefaultValues = false;
            rightHandState.motion = blankAnimationClip;
            rightHandState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_CurrentGestureHand.name, 2),
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
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(neutralState, rightHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, neutralState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftHandState, rightHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, neutralState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .Equals(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightHandState, leftHandState)
                .Equals(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .NotEqual(VRCParameters.GESTURE_LEFT, 0)
                .Equals(VRCParameters.GESTURE_RIGHT, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandState)
                .NotEqual(InternalParameters.GesturePriority.name, GesturePriorityType.RightHandPriority)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();
        }

        private AnimatorControllerLayer CreateSelectFacialExpressionNumberLayer(FacialExpressionControl fec)
        {
            var layer = CreateAnimatorControllerLayer("FEC_SELECT_FACIAL_EXPRESSION_NUMBER");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(600, 80, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 80, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(300, -80, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, 0),
            };

            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .Equals(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            Action<AnimatorStateMachine, AnimatorState, int, int> addGestureTransition = (stateMachine, state, gestureNumber, presetNumber) =>
            {
                AnimatorTransitionUtil.AddEntryTransition(stateMachine, state)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                    .Equals(InternalParameters.State_CurrentGestureLeft.name, gestureNumber);

                AnimatorTransitionUtil.AddEntryTransition(stateMachine, state)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                    .Equals(InternalParameters.State_CurrentGestureRight.name, gestureNumber);

                AnimatorTransitionUtil.AddExitTransition(state)
                    .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 0)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                    .NotEqual(InternalParameters.State_CurrentGestureLeft.name, gestureNumber)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                    .NotEqual(InternalParameters.State_CurrentGestureRight.name, gestureNumber)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                    .NotEqual(InternalParameters.SelectedLeftGesturePreset.name, presetNumber)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                    .NotEqual(InternalParameters.SelectedRightGesturePreset.name, presetNumber)
                    .SetImmediateTransitionSettings();
            };

            // ハンドジェスチャーによる表情番号選択ステートを生成
            for (var i = 0; i < fec.facialExpressionGesturePresets.Count; i++)
            {
                var gesturePreset = fec.facialExpressionGesturePresets[i];
                var presetName = string.IsNullOrEmpty(gesturePreset.presetName) ? i.ToString() : gesturePreset.presetName;

                var gesturePresetStateMachine = layer.stateMachine.AddStateMachine($"Gesture Preset ({presetName})", new Vector3(300, i * 80, 0));
                gesturePresetStateMachine.entryPosition = new Vector3(0, 0, 0);
                gesturePresetStateMachine.exitPosition = new Vector3(600, 0, 0);
                gesturePresetStateMachine.anyStatePosition = new Vector3(0, -80, 0);
                gesturePresetStateMachine.parentStateMachinePosition = new Vector3(0, -160, 0);

                AnimatorTransitionUtil.AddTransition(initialState, gesturePresetStateMachine)
                    .If(VRCParameters.IS_LOCAL)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                    .NotEqual(InternalParameters.State_CurrentGestureLeft.name, 0)
                    .Equals(InternalParameters.SelectedLeftGesturePreset.name, i)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddTransition(initialState, gesturePresetStateMachine)
                    .If(VRCParameters.IS_LOCAL)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                    .NotEqual(InternalParameters.State_CurrentGestureRight.name, 0)
                    .Equals(InternalParameters.SelectedLeftGesturePreset.name, i)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(gesturePresetStateMachine, layer.stateMachine);

                var fistState = gesturePresetStateMachine.AddState("Fist", new Vector3(300, 0, 0));
                fistState.writeDefaultValues = false;
                fistState.motion = blankAnimationClip;
                fistState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 1),
                };

                addGestureTransition(gesturePresetStateMachine, fistState, 1, i);

                var handOpenState = gesturePresetStateMachine.AddState("HandOpen", new Vector3(300, 80, 0));
                handOpenState.writeDefaultValues = false;
                handOpenState.motion = blankAnimationClip;
                handOpenState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 2),
                };

                addGestureTransition(gesturePresetStateMachine, handOpenState, 2, i);

                var fingerPointState = gesturePresetStateMachine.AddState("FingerPoint", new Vector3(300, 160, 0));
                fingerPointState.writeDefaultValues = false;
                fingerPointState.motion = blankAnimationClip;
                fingerPointState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 3),
                };

                addGestureTransition(gesturePresetStateMachine, fingerPointState, 3, i);

                var victoryState = gesturePresetStateMachine.AddState("Victory", new Vector3(300, 240, 0));
                victoryState.writeDefaultValues = false;
                victoryState.motion = blankAnimationClip;
                victoryState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 4),
                };

                addGestureTransition(gesturePresetStateMachine, victoryState, 4, i);

                var rockNRollState = gesturePresetStateMachine.AddState("RockNRoll", new Vector3(300, 320, 0));
                rockNRollState.writeDefaultValues = false;
                rockNRollState.motion = blankAnimationClip;
                rockNRollState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 5),
                };

                addGestureTransition(gesturePresetStateMachine, rockNRollState, 5, i);

                var handGunState = gesturePresetStateMachine.AddState("HandGun", new Vector3(300, 400, 0));
                handGunState.writeDefaultValues = false;
                handGunState.motion = blankAnimationClip;
                handGunState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 6),
                };

                addGestureTransition(gesturePresetStateMachine, handGunState, 6, i);

                var thumbsUpState = gesturePresetStateMachine.AddState("ThumbsUp", new Vector3(300, 480, 0));
                thumbsUpState.writeDefaultValues = false;
                thumbsUpState.motion = blankAnimationClip;
                thumbsUpState.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, i * 7 + 7),
                };

                addGestureTransition(gesturePresetStateMachine, thumbsUpState, 7, i);
            }

            var selectedFacialExpressions = fec.facialExpressionGroups
                .SelectMany(x => x.facialExpressions)
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / 10)
                .Select(g => g.Select(x => x.v).ToList())
                .ToList();


            for (var i = 0; i < selectedFacialExpressions.Count; i++)
            {
                var selectedFacialExpressionGroupStateMachine = layer.stateMachine.AddStateMachine($"Selected Facial Expression Group ({i * 10 + 1} ~ {i * 10 + selectedFacialExpressions[i].Count})", new Vector3(300, 80 * i + fec.facialExpressionGesturePresets.Count * 80, 0));
                selectedFacialExpressionGroupStateMachine.entryPosition = new Vector3(0, 0, 0);
                selectedFacialExpressionGroupStateMachine.exitPosition = new Vector3(600, 0, 0);
                selectedFacialExpressionGroupStateMachine.anyStatePosition = new Vector3(0, -80, 0);
                selectedFacialExpressionGroupStateMachine.parentStateMachinePosition = new Vector3(600, 320, 0);

                AnimatorTransitionUtil.AddTransition(initialState, selectedFacialExpressionGroupStateMachine)
                    .If(VRCParameters.IS_LOCAL)
                    .Greater(InternalParameters.SelectedFacialExpressionInMenu.name, i * 10)
                    .Less(InternalParameters.SelectedFacialExpressionInMenu.name, i * 10 + selectedFacialExpressions[i].Count + 1)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionGroupStateMachine, layer.stateMachine);

                for (var j = 0; j < selectedFacialExpressions[i].Count; j++)
                {
                    var facialExpressionNumber = i * 10 + j + 1;
                    var selectedFacialExpressionState = selectedFacialExpressionGroupStateMachine.AddState($"Selected Facial Expression ({facialExpressionNumber})", new Vector3(300, 80 * j, 0));
                    selectedFacialExpressionState.behaviours = new StateMachineBehaviour[]
                    {
                        CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber),
                    };

                    AnimatorTransitionUtil.AddEntryTransition(selectedFacialExpressionGroupStateMachine, selectedFacialExpressionState)
                        .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, facialExpressionNumber);

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, facialExpressionNumber);
                }
            }

            return layer;
        }

        private AnimatorControllerLayer CreateSelectFacialExpressionControlModeLayer()
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
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive),
            };

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(inactiveState)
                .If(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(300, 160, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.Neutral),
            };

            // 左右どちらの手のジェスチャーも適用されていないパターン (Neutral)
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 0)
                .SetImmediateTransitionSettings();

            // 左手のジェスチャーが適用されているが、Neutralな値である場合
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            // 右手のジェスチャーが適用されているが、Neutralな値である場合
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .Equals(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            var leftHandGestureState = layer.stateMachine.AddState("Left Hand Gesture", new Vector3(300, 240, 0));
            leftHandGestureState.writeDefaultValues = false;
            leftHandGestureState.motion = blankAnimationClip;
            leftHandGestureState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture),
            };

            AnimatorTransitionUtil.AddTransition(initialState, leftHandGestureState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft.name, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .NotEqual(InternalParameters.State_CurrentGestureHand.name, 1)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            var leftHandGestureFixedState = layer.stateMachine.AddState("Left Hand Gesture (Fixed)", new Vector3(300, 320, 0));
            leftHandGestureFixedState.writeDefaultValues = false;
            leftHandGestureFixedState.motion = blankAnimationClip;
            leftHandGestureFixedState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGestureFixed),
            };

            AnimatorTransitionUtil.AddTransition(initialState, leftHandGestureFixedState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft.name, 0)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(InternalParameters.State_CurrentGestureHand.name, 1)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(leftHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            var rightHandGestureState = layer.stateMachine.AddState("Right Hand Gesture", new Vector3(300, 400, 0));
            rightHandGestureState.writeDefaultValues = false;
            rightHandGestureState.motion = blankAnimationClip;
            rightHandGestureState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGesture),
            };

            AnimatorTransitionUtil.AddTransition(initialState, rightHandGestureState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight.name, 0)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .NotEqual(InternalParameters.State_CurrentGestureHand.name, 2)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .Equals(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            var rightHandGestureFixedState = layer.stateMachine.AddState("Right Hand Gesture (Fixed)", new Vector3(300, 480, 0));
            rightHandGestureFixedState.writeDefaultValues = false;
            rightHandGestureFixedState.motion = blankAnimationClip;
            rightHandGestureFixedState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed),
            };

            AnimatorTransitionUtil.AddTransition(initialState, rightHandGestureFixedState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight.name, 0)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(InternalParameters.State_CurrentGestureHand.name, 2)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .Equals(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(rightHandGestureFixedState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            var selectedFacialExpressionInMenuState = layer.stateMachine.AddState("Selected Facial Expression (In Menu)", new Vector3(300, 560, 0));
            selectedFacialExpressionInMenuState.writeDefaultValues = false;
            selectedFacialExpressionInMenuState.motion = blankAnimationClip;
            selectedFacialExpressionInMenuState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.SelectedFacialExpressionInMenu),
            };

            AnimatorTransitionUtil.AddTransition(initialState, selectedFacialExpressionInMenuState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.Inactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionInMenuState)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu.name, 0)
                .SetImmediateTransitionSettings();

            var builtInFacialTrackingState = layer.stateMachine.AddState("Built-in Facial Tracking", new Vector3(300, 640, 0));
            builtInFacialTrackingState.writeDefaultValues = false;
            builtInFacialTrackingState.motion = blankAnimationClip;
            builtInFacialTrackingState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.BuiltInFacialTracking),
            };

            AnimatorTransitionUtil.AddTransition(initialState, builtInFacialTrackingState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.BuiltInFacialTracking)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.BuiltInFacialTracking)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            var animatorBasedFacialTrackingState = layer.stateMachine.AddState("Animator Based Facial Tracking", new Vector3(300, 720, 0));
            animatorBasedFacialTrackingState.writeDefaultValues = false;
            animatorBasedFacialTrackingState.motion = blankAnimationClip;
            animatorBasedFacialTrackingState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AnimatorBasedFacialTracking),
            };

            AnimatorTransitionUtil.AddTransition(initialState, animatorBasedFacialTrackingState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .Equals(InternalParameters.FacialTrackingMode.name, FacialTrackingType.AnimatorBasedFacialTracking)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .NotEqual(InternalParameters.FacialTrackingMode.name, FacialTrackingType.AnimatorBasedFacialTracking)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            var danceModeState = layer.stateMachine.AddState("Dance Mode", new Vector3(300, 800, 0));
            danceModeState.writeDefaultValues = false;
            danceModeState.motion = blankAnimationClip;
            danceModeState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.DanceMode),
            };

            AnimatorTransitionUtil.AddTransition(initialState, danceModeState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .If(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .SetImmediateTransitionSettings();

            var afkState = layer.stateMachine.AddState("AFK", new Vector3(300, 880, 0));
            afkState.writeDefaultValues = false;
            afkState.motion = blankAnimationClip;
            afkState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AFKMode),
            };

            AnimatorTransitionUtil.AddTransition(initialState, afkState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionControlON.name)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(afkState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(afkState)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateDefaultFacialExpressionLayer(FacialExpressionControl fec, GameObject avatarRootObject)
        {
            var layer = CreateAnimatorControllerLayer("FEC_DEFAULT_FACIAL_EXPRESSION", 1.0f);

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 80, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var defaultFaceInactiveState = layer.stateMachine.AddState("Default Face Inactive", new Vector3(200, 80, 0));
            defaultFaceInactiveState.writeDefaultValues = false;
            defaultFaceInactiveState.motion = blankAnimationClip;

            var defaultFaceActiveState = layer.stateMachine.AddState("Default Face Active", new Vector3(200, 0, 0));
            defaultFaceActiveState.writeDefaultValues = false;
            defaultFaceActiveState.motion = GetDefaultFaceAnimation(fec, avatarRootObject);

            AnimatorTransitionUtil.AddTransition(initialState, defaultFaceInactiveState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, defaultFaceActiveState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(defaultFaceInactiveState, defaultFaceActiveState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(defaultFaceActiveState, defaultFaceInactiveState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateFacialExpressionControlLayer(FacialExpressionControl fec, GameObject avatarRootObject)
        {
            var layer = CreateAnimatorControllerLayer("FEC_FACIAL_EXPRESSION_CONTROL", 1.0f);

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(1000, 300, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var inactiveState = layer.stateMachine.AddState("Inactive", new Vector3(500, 0, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;

            var builtInFacialTrackingState = layer.stateMachine.AddState("Built-in Facial Tracking", new Vector3(500, 80, 0));
            builtInFacialTrackingState.writeDefaultValues = false;
            builtInFacialTrackingState.motion = blankAnimationClip;
            builtInFacialTrackingState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType.Tracking, VRC_AnimatorTrackingControl.TrackingType.Tracking)
            };

            var animatorBasedFacialTrackingState = layer.stateMachine.AddState("Animator Based Facial Tracking", new Vector3(500, 160, 0));
            animatorBasedFacialTrackingState.writeDefaultValues = false;
            animatorBasedFacialTrackingState.motion = blankAnimationClip;
            animatorBasedFacialTrackingState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType.Animation, VRC_AnimatorTrackingControl.TrackingType.Animation)
            };

            var danceModeState = layer.stateMachine.AddState("Dance Mode", new Vector3(500, 240, 0));
            danceModeState.writeDefaultValues = false;
            danceModeState.motion = blankAnimationClip;
            danceModeState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType.Animation, VRC_AnimatorTrackingControl.TrackingType.Animation)
            };

            var afkState = layer.stateMachine.AddState("AFK", new Vector3(500, 320, 0));
            afkState.writeDefaultValues = false;
            afkState.motion = blankAnimationClip;

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(500, 400, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;   // MEMO まばたき等をアニメーションで表現する場合、ここで設定する (設定する場合、目と口のトラッキングタイプを Animation にする)
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType.Tracking, VRC_AnimatorTrackingControl.TrackingType.Tracking)
            };

            var gestureFacialExpressionsStateMachine = layer.stateMachine.AddStateMachine("Facial Expressions (Gesture)", new Vector3(500, 480, 0));
            gestureFacialExpressionsStateMachine.entryPosition = new Vector3(0, 0, 0);
            gestureFacialExpressionsStateMachine.exitPosition = new Vector3(1000, 0, 0);
            gestureFacialExpressionsStateMachine.anyStatePosition = new Vector3(0, -80, 0);
            gestureFacialExpressionsStateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

            var selectedFacialExpressionsStateMachine = layer.stateMachine.AddStateMachine("Facial Expressions (Selected)", new Vector3(500, 560, 0));
            selectedFacialExpressionsStateMachine.entryPosition = new Vector3(0, 0, 0);
            selectedFacialExpressionsStateMachine.exitPosition = new Vector3(1000, 0, 0);
            selectedFacialExpressionsStateMachine.anyStatePosition = new Vector3(0, -80, 0);
            selectedFacialExpressionsStateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

            //---------------------------------------------
            // トランジション設定 (Inactive)
            //---------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, inactiveState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive);

            AnimatorTransitionUtil.AddExitTransition(inactiveState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            //---------------------------------------------
            // トランジション設定 (Neutral)
            //---------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.Neutral);

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            // 表情が変化した場合に適用されるトランジション設定
            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Greater(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture - 1)
                .Less(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.SelectedFacialExpressionInMenu + 1)
                .Exec((builder) =>
                {
                    var transition = builder.Transition;
                    transition.hasExitTime = false;
                    transition.exitTime = 0;
                    transition.hasFixedDuration = true;
                    transition.duration = fec.transitionTime;
                    transition.offset = 0;
                    transition.interruptionSource = TransitionInterruptionSource.None;
                    transition.orderedInterruption = true;
                });

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.BuiltInFacialTracking)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AnimatorBasedFacialTracking)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.DanceMode)
                .SetImmediateTransitionSettings();

            //---------------------------------------------
            // トランジション設定 (表情設定用サブステート)
            //---------------------------------------------

            // Gesture Facial Expressions
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, gestureFacialExpressionsStateMachine)
                .Greater(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture - 1)
                .Less(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed + 1);

            AnimatorTransitionUtil.AddExitTransition(gestureFacialExpressionsStateMachine, layer.stateMachine);

            // Selected Facial Expressions
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, selectedFacialExpressionsStateMachine)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.SelectedFacialExpressionInMenu);

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionsStateMachine, layer.stateMachine);

            //---------------------------------------------
            // トランジション設定 (BuiltInFacialTracking)
            //---------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, builtInFacialTrackingState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.BuiltInFacialTracking);

            AnimatorTransitionUtil.AddExitTransition(builtInFacialTrackingState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.BuiltInFacialTracking)
                .SetImmediateTransitionSettings();

            //---------------------------------------------
            // トランジション設定 (AnimatorBasedFacialTracking)
            //---------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, animatorBasedFacialTrackingState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AnimatorBasedFacialTracking);

            AnimatorTransitionUtil.AddExitTransition(animatorBasedFacialTrackingState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AnimatorBasedFacialTracking)
                .SetImmediateTransitionSettings();

            //---------------------------------------------
            // トランジション設定 (DanceMode)
            //---------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, danceModeState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.DanceMode);

            AnimatorTransitionUtil.AddExitTransition(danceModeState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.DanceMode)
                .SetImmediateTransitionSettings();

            //---------------------------------------------
            // トランジション設定 (AFKMode)
            //---------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, afkState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AFKMode);

            AnimatorTransitionUtil.AddExitTransition(afkState)
                .NotEqual(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AFKMode)
                .SetImmediateTransitionSettings();

            var gestureFacialExpressions = fec.facialExpressionGesturePresets
                .SelectMany(x => new List<FacialExpression>
                {
                    x.fist,
                    x.handOpen,
                    x.fingerPoint,
                    x.victory,
                    x.rockNRoll,
                    x.handGun,
                    x.thumbsUp
                })
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / 10)
                .Select(g => g.Select(x => x.v).ToList())
                .ToList();

            for (var i = 0; i < gestureFacialExpressions.Count; i++)
            {
                var facialExpressionGesturePresetStateMachine = gestureFacialExpressionsStateMachine.AddStateMachine($"Facial Expression Gesture Preset ({i * 10 + 1} ~ {i * 10 + gestureFacialExpressions[i].Count})", new Vector3(500, 80 * i, 0));
                facialExpressionGesturePresetStateMachine.entryPosition = new Vector3(0, 0, 0);
                facialExpressionGesturePresetStateMachine.exitPosition = new Vector3(1000, 0, 0);
                facialExpressionGesturePresetStateMachine.anyStatePosition = new Vector3(0, -80, 0);
                facialExpressionGesturePresetStateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

                AnimatorTransitionUtil.AddEntryTransition(gestureFacialExpressionsStateMachine, facialExpressionGesturePresetStateMachine)
                    .Greater(SyncParameters.CurrentFacialExpressionNumber.name, i * 10)
                    .Less(SyncParameters.CurrentFacialExpressionNumber.name, i * 10 + gestureFacialExpressions[i].Count + 1);

                AnimatorTransitionUtil.AddExitTransition(facialExpressionGesturePresetStateMachine, gestureFacialExpressionsStateMachine);

                for (var j = 0; j < gestureFacialExpressions[i].Count; j++)
                {
                    var facialExpressionNumber = i * 10 + j + 1;
                    var gestureFacialExpressionStateMachine = CreateGestureFacialExpressionSubStateMachine(facialExpressionGesturePresetStateMachine, fec, gestureFacialExpressions[i][j], facialExpressionNumber, new Vector3(500, 80 * j, 0));

                    AnimatorTransitionUtil.AddEntryTransition(facialExpressionGesturePresetStateMachine, gestureFacialExpressionStateMachine)
                        .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber);

                    AnimatorTransitionUtil.AddExitTransition(gestureFacialExpressionStateMachine, facialExpressionGesturePresetStateMachine);
                }
            }

            var selectedFacialExpressions = fec.facialExpressionGroups
                .SelectMany(x => x.facialExpressions)
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / 10)
                .Select(g => g.Select(x => x.v).ToList())
                .ToList();

            for (var i = 0; i < selectedFacialExpressions.Count; i++)
            {
                var selectedFacialExpressionGroupStateMachine = selectedFacialExpressionsStateMachine.AddStateMachine($"Selected Facial Expression Group ({i * 10 + 1} ~ {i * 10 + selectedFacialExpressions[i].Count})", new Vector3(500, 80 * i, 0));
                selectedFacialExpressionGroupStateMachine.entryPosition = new Vector3(0, 0, 0);
                selectedFacialExpressionGroupStateMachine.exitPosition = new Vector3(1000, 0, 0);
                selectedFacialExpressionGroupStateMachine.anyStatePosition = new Vector3(0, -80, 0);
                selectedFacialExpressionGroupStateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

                AnimatorTransitionUtil.AddEntryTransition(selectedFacialExpressionsStateMachine, selectedFacialExpressionGroupStateMachine)
                    .Greater(SyncParameters.CurrentFacialExpressionNumber.name, i * 10)
                    .Less(SyncParameters.CurrentFacialExpressionNumber.name, i * 10 + selectedFacialExpressions[i].Count + 1);

                AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionGroupStateMachine, selectedFacialExpressionsStateMachine);

                for (var j = 0; j < selectedFacialExpressions[i].Count; j++)
                {
                    var facialExpressionNumber = i * 10 + j + 1;
                    var selectedFacialExpressionState = CreateFacialExpressionState(selectedFacialExpressionGroupStateMachine, $"Selected Facial Expression ({facialExpressionNumber})", fec, selectedFacialExpressions[i][j], SyncParameters.FixedWeight.name, new Vector3(500, 80 * j, 0));

                    AnimatorTransitionUtil.AddEntryTransition(selectedFacialExpressionGroupStateMachine, selectedFacialExpressionState)
                        .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber);

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Greater(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.Neutral - 1)
                        .Less(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed + 1)
                        .Exec((builder) =>
                        {
                            var transition = builder.Transition;
                            transition.hasExitTime = false;
                            transition.exitTime = 0;
                            transition.hasFixedDuration = true;
                            transition.duration = fec.transitionTime;
                            transition.offset = 0;
                            transition.interruptionSource = TransitionInterruptionSource.None;
                            transition.orderedInterruption = true;
                        });

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.SelectedFacialExpressionInMenu)
                        .NotEqual(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                        .Exec((builder) =>
                        {
                            var transition = builder.Transition;
                            transition.hasExitTime = false;
                            transition.exitTime = 0;
                            transition.hasFixedDuration = true;
                            transition.duration = fec.transitionTime;
                            transition.offset = 0;
                            transition.interruptionSource = TransitionInterruptionSource.None;
                            transition.orderedInterruption = true;
                        });

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                        .SetImmediateTransitionSettings();

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.BuiltInFacialTracking)
                        .SetImmediateTransitionSettings();

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AnimatorBasedFacialTracking)
                        .SetImmediateTransitionSettings();

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.DanceMode)
                        .SetImmediateTransitionSettings();

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AFKMode)
                        .SetImmediateTransitionSettings();
                }
            }


            return layer;
        }

        private AnimatorStateMachine CreateGestureFacialExpressionSubStateMachine(
            AnimatorStateMachine parentStateMachine,
            FacialExpressionControl fec,
            FacialExpression facialExpression,
            int facialExpressionNumber,
            Vector3 position)
        {
            var facialExpressionName = $"Gesture Facial Expression ({(string.IsNullOrEmpty(facialExpression?.FacialExpressionName) ? facialExpressionNumber : facialExpression?.FacialExpressionName)})";

            var stateMachine = parentStateMachine.AddStateMachine(facialExpressionName, position);
            stateMachine.entryPosition = new Vector3(0, 160, 0);
            stateMachine.exitPosition = new Vector3(1000, 160, 0);
            stateMachine.anyStatePosition = new Vector3(0, -80, 0);
            stateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

            var leftGestureState = CreateFacialExpressionState(stateMachine, "Left Gesture", fec, facialExpression, VRCParameters.GESTURE_LEFT_WEIGHT, new Vector3(300, 0, 0));
            var leftGestureFixedState = CreateFacialExpressionState(stateMachine, "Left Gesture (Fixed)", fec, facialExpression, SyncParameters.FixedWeight.name, new Vector3(700, 0, 0));
            var rightGestureState = CreateFacialExpressionState(stateMachine, "Right Gesture", fec, facialExpression, VRCParameters.GESTURE_RIGHT_WEIGHT, new Vector3(300, 320, 0));
            var rightGestureFixedState = CreateFacialExpressionState(stateMachine, "Right Gesture (Fixed)", fec, facialExpression, SyncParameters.FixedWeight.name, new Vector3(700, 320, 0));

            Action<AnimatorState> addExitTransition = (state) =>
            {
                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.FacialExpressionControlInactive)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Greater(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.Neutral - 1)
                    .Less(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed + 1)
                    .NotEqual(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                    .Exec((builder) =>
                    {
                        var transition = builder.Transition;
                        transition.hasExitTime = false;
                        transition.exitTime = 0;
                        transition.hasFixedDuration = true;
                        transition.duration = fec.transitionTime;
                        transition.offset = 0;
                        transition.interruptionSource = TransitionInterruptionSource.None;
                        transition.orderedInterruption = true;
                    });

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.SelectedFacialExpressionInMenu)
                    .Exec((builder) =>
                    {
                        var transition = builder.Transition;
                        transition.hasExitTime = false;
                        transition.exitTime = 0;
                        transition.hasFixedDuration = true;
                        transition.duration = fec.transitionTime;
                        transition.offset = 0;
                        transition.interruptionSource = TransitionInterruptionSource.None;
                        transition.orderedInterruption = true;
                    });

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.BuiltInFacialTracking)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AnimatorBasedFacialTracking)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.DanceMode)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(state)
                    .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.AFKMode)
                    .SetImmediateTransitionSettings();
            };

            //-----------------------------------------------------------------------
            // Left Gesture
            //-----------------------------------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture);

            AnimatorTransitionUtil.AddTransition(leftGestureState, leftGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGestureFixed)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftGestureState, rightGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGesture)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftGestureState, rightGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            addExitTransition(leftGestureState);

            //-----------------------------------------------------------------------
            // Left Gesture (Fixed)
            //-----------------------------------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, leftGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGestureFixed);

            AnimatorTransitionUtil.AddTransition(leftGestureFixedState, leftGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftGestureFixedState, rightGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGesture)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(leftGestureFixedState, rightGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            addExitTransition(leftGestureFixedState);

            //-----------------------------------------------------------------------
            // Right Gesture
            //-----------------------------------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGesture);

            AnimatorTransitionUtil.AddTransition(rightGestureState, leftGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightGestureState, leftGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGestureFixed)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightGestureState, rightGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            addExitTransition(rightGestureState);

            //-----------------------------------------------------------------------
            // Right Gesture (Fixed)
            //-----------------------------------------------------------------------
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, rightGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGestureFixed);

            AnimatorTransitionUtil.AddTransition(rightGestureFixedState, leftGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGesture)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightGestureFixedState, leftGestureFixedState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.LeftHandGestureFixed)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(rightGestureFixedState, rightGestureState)
                .Equals(SyncParameters.FacialExpressionControlMode.name, FacialExpressionControlModeType.RightHandGesture)
                .Equals(SyncParameters.CurrentFacialExpressionNumber.name, facialExpressionNumber)
                .SetImmediateTransitionSettings();

            addExitTransition(rightGestureFixedState);

            return stateMachine;
        }

        private AnimatorState CreateFacialExpressionState(
            AnimatorStateMachine stateMachine,
            string stateName,
            FacialExpressionControl fec,
            FacialExpression facialExpression,
            string motionTimeParameterName,
            Vector3 position)
        {
            var state = stateMachine.AddState(stateName, position);
            state.speed = 1 / fec.transitionTime;
            state.writeDefaultValues = false;
            state.motion = facialExpression?.motion != null ? facialExpression?.motion : blankAnimationClip;
            if (motionTimeParameterName != "")
            {
                state.timeParameterActive = true;
                state.timeParameter = motionTimeParameterName;
            }

            state.behaviours = new StateMachineBehaviour[]
            {
                new VRCAnimatorTrackingControl()
                {
                    trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                    trackingEyes = GetTrackingType(facialExpression?.eyeControlType ?? TrackingControlType.Tracking),
                    trackingMouth = GetTrackingType(facialExpression?.mouthControlType ?? TrackingControlType.Tracking),
                }
            };

            return state;
        }

        private Motion GetDefaultFaceAnimation(FacialExpressionControl fec, GameObject avatarRootObject)
        {
            if (fec.generateDefaultFacialAnimation)
            {
                var feag = new FacialExpressionAnimationGenerator();
                return feag.FromAvatar("Default Facial Expression (Auto Generated)", avatarRootObject, null);
            }

            if (fec.defaultFace != null)
            {
                return fec.defaultFace;
            }

            return blankAnimationClip;
        }

        /// <summary>
        /// AFK制御用レイヤーの生成
        /// </summary>
        /// <param name="fec"></param>
        /// <returns></returns>
        private AnimatorControllerLayer CreateAFKModeControlLayer(FacialExpressionControl fec)
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
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_AFKModeActive.name, 1)
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
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_AFKModeActive.name, 0)
            };

            AnimatorTransitionUtil.AddTransition(initialState, afkInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            var switchToAFKInactiveTransition = AnimatorTransitionUtil.AddTransition(switchToAFKInactiveState, afkInactiveState)
                .IfNot(VRCParameters.AFK);

            switch (fec.afkExitWaitMode)
            {
                case AFKExitWaitMode.Duration:
                    if (fec.waitAFKExitDurationTime > 0)
                    {
                        switchToAFKInactiveTransition.Exec((builder) =>
                        {
                            var transition = builder.Transition;
                            transition.hasExitTime = true;
                            transition.exitTime = fec.waitAFKExitDurationTime;
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
                    break;

                default:
                    switchToAFKInactiveTransition.SetImmediateTransitionSettings();
                    break;
            }

            AnimatorTransitionUtil.AddTransition(afkInactiveState, afkActiveState)
                .If(VRCParameters.AFK)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateDanceModeControlLayer()
        {
            var layer = CreateAnimatorControllerLayer("FEC_DANCE_MODE_CONTROL");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));

            var danceModeInactiveState = layer.stateMachine.AddState("Dance Mode Inactive", new Vector3(500, 0, 0));
            danceModeInactiveState.writeDefaultValues = false;
            danceModeInactiveState.motion = blankAnimationClip;
            danceModeInactiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_DanceModeActive.name, 0)
            };

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, danceModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.SwitchToDanceModeON.name)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            // 表情コントロールがOFFになっている場合
            AnimatorTransitionUtil.AddTransition(initialState, danceModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, danceModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, danceModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, danceModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            var danceModeActiveState = layer.stateMachine.AddState("Dance Mode Active", new Vector3(500, 80, 0));
            danceModeActiveState.writeDefaultValues = false;
            danceModeActiveState.motion = blankAnimationClip;
            danceModeActiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_DanceModeActive.name, 1)
            };

            // [Initial State] -> [Dance Mode Active]
            AnimatorTransitionUtil.AddTransition(initialState, danceModeActiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.SwitchToDanceModeON.name)
                .If(InternalParameters.FacialExpressionControlON.name)
                .If(VRCParameters.IN_STATION)
                .IfNot(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            // [Dance Mode Inactive] -> [Dance Mode Active]
            AnimatorTransitionUtil.AddTransition(danceModeInactiveState, danceModeActiveState)
                .If(InternalParameters.SwitchToDanceModeON.name)
                .If(InternalParameters.FacialExpressionControlON.name)
                .If(VRCParameters.IN_STATION)
                .IfNot(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(danceModeActiveState, danceModeInactiveState)
                .IfNot(InternalParameters.SwitchToDanceModeON.name)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            // 表情コントロールがOFFになっている場合
            AnimatorTransitionUtil.AddTransition(danceModeActiveState, danceModeInactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(danceModeActiveState, danceModeInactiveState)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(danceModeActiveState, danceModeInactiveState)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Dance Mode Active] -> [Dance Mode Inactive]
            AnimatorTransitionUtil.AddTransition(danceModeActiveState, danceModeInactiveState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateVehicleModeControlLayer()
        {
            var layer = CreateAnimatorControllerLayer("FEC_VEHICLE_MODE_CONTROL");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));

            var vehicleModeInactiveState = layer.stateMachine.AddState("Vehicle Mode Inactive", new Vector3(500, 0, 0));
            vehicleModeInactiveState.writeDefaultValues = false;
            vehicleModeInactiveState.motion = blankAnimationClip;
            vehicleModeInactiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_VehicleModeActive.name, 0)
            };

            // [Initial State] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, vehicleModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.SwitchToVehicleModeON.name)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Vehicle Mode Inactive]
            // 表情コントロールがOFFになっている場合
            AnimatorTransitionUtil.AddTransition(initialState, vehicleModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, vehicleModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, vehicleModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(initialState, vehicleModeInactiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            var vehicleModeActiveState = layer.stateMachine.AddState("Vehicle Mode Active", new Vector3(500, 80, 0));
            vehicleModeActiveState.writeDefaultValues = false;
            vehicleModeActiveState.motion = blankAnimationClip;
            vehicleModeActiveState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_VehicleModeActive.name, 1)
            };

            // [Initial State] -> [Vehicle Mode Active]
            AnimatorTransitionUtil.AddTransition(initialState, vehicleModeActiveState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.SwitchToVehicleModeON.name)
                .If(InternalParameters.FacialExpressionControlON.name)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            // [Vehicle Mode Inactive] -> [Vehicle Mode Active]
            AnimatorTransitionUtil.AddTransition(vehicleModeInactiveState, vehicleModeActiveState)
                .If(InternalParameters.SwitchToVehicleModeON.name)
                .If(InternalParameters.FacialExpressionControlON.name)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            // [Vehicle Mode Active] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(vehicleModeActiveState, vehicleModeInactiveState)
                .IfNot(InternalParameters.SwitchToVehicleModeON.name)
                .SetImmediateTransitionSettings();

            // [Vehicle Mode Active] -> [Vehicle Mode Inactive]
            // 表情コントロールがOFFになっている場合
            AnimatorTransitionUtil.AddTransition(vehicleModeActiveState, vehicleModeInactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Vehicle Mode Active] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(vehicleModeActiveState, vehicleModeInactiveState)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Vehicle Mode Active] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(vehicleModeActiveState, vehicleModeInactiveState)
                .IfNot(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Vehicle Mode Active] -> [Vehicle Mode Inactive]
            AnimatorTransitionUtil.AddTransition(vehicleModeActiveState, vehicleModeInactiveState)
                .If(InternalParameters.State_AFKModeActive.name)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateCopyGestureWeightLayer()
        {
            var layer = CreateAnimatorControllerLayer("FEC_COPY_GESUTRE_WEIGHT");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
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

            var copyLeftGestureWeightState = layer.stateMachine.AddState("Copy Gesture Weight (Left)", new Vector3(500, 80, 0));
            copyLeftGestureWeightState.writeDefaultValues = false;
            copyLeftGestureWeightState.motion = blankAnimationClip;
            copyLeftGestureWeightState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalCopyDriver(VRCParameters.GESTURE_LEFT_WEIGHT, SyncParameters.FixedWeight.name)
            };

            var copyRightGestureWeightState = layer.stateMachine.AddState("Copy Gesture Weight (Right)", new Vector3(500, 160, 0));
            copyRightGestureWeightState.writeDefaultValues = false;
            copyRightGestureWeightState.motion = blankAnimationClip;
            copyRightGestureWeightState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalCopyDriver(VRCParameters.GESTURE_RIGHT_WEIGHT, SyncParameters.FixedWeight.name)
            };

            AnimatorTransitionUtil.AddTransition(initialState, unlockState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, lockingState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, copyLeftGestureWeightState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, copyRightGestureWeightState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, lockingState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, lockingState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(InternalParameters.State_CurrentGestureHand.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(unlockState, lockingState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .NotEqual(InternalParameters.State_CurrentGestureHand.name, 0)
                .Equals(InternalParameters.State_CurrentGestureHand.name, 2)
                .Equals(InternalParameters.State_CurrentGestureRight.name, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyLeftGestureWeightState, lockingState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyRightGestureWeightState, lockingState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyLeftGestureWeightState, unlockState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(copyRightGestureWeightState, unlockState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(lockingState, unlockState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateFacialExpressionGestureLockLayer()
        {
            var layer = CreateAnimatorControllerLayer("FEC_FACIAL_EXPRESSION_GESTURE_LOCK");

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.exitPosition = new Vector3(0, -40, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -80, 0);

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
            setDisableState.motion = blankAnimationClip;
            setDisableState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.FacialExpressionLocked.name, 0)
            };

            var setEnableState = layer.stateMachine.AddState("Set Enable", new Vector3(600, 160, 0));
            setEnableState.writeDefaultValues = false;
            setEnableState.motion = blankAnimationClip;
            setEnableState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.FacialExpressionLocked.name, 1)
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
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Set Disable] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(setDisableState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Set Enable] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(setEnableState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Interval (Lock to Unlock)] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(lockToUnlockIntervalState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Interval (Unlock to Lock)] -> [Inactive]
            AnimatorTransitionUtil.AddTransition(unlockToLockIntervalState, inactiveState)
                .IfNot(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Inactive] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(inactiveState, gestureLockDisabledState)
                .If(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Inactive] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(inactiveState, gestureLockEnabledState)
                .If(InternalParameters.FacialExpressionControlON.name)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(initialState, gestureLockDisabledState)
                .If(VRCParameters.IS_LOCAL)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            // [Initial State] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(initialState, gestureLockEnabledState)
                .If(VRCParameters.IS_LOCAL)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, gestureLockEnabledState)
                .If(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, gestureLockDisabledState)
                .IfNot(InternalParameters.FacialExpressionLocked.name)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Set Enable]
            // - 以下のすべての条件を満たす場合、ロック有効化を行う
            //   - AFK状態ではない場合
            //   - ダンスモードではない場合
            //   - Sit状態ではない場合
            //     - InStation = falseな状態が保証されているため、Sit判定時のロック機能自動無効化の状態についてはチェックを行わない
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, setEnableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact.name)
                .If(InternalParameters.ContactLockON.name)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Disabled] -> [Set Enable]
            // - Sit判定時にロック機能自動無効化がOFFの場合、Sit判定かつSeatedな状態でもロック有効化を行う
            //   - InStation = true かつ Seated = falseの時はロック機能自動無効化がOFFの場合でもロック状態の切り替えを行わないようにする(いわゆるダンスワールドでのアニメーション適用時など)
            AnimatorTransitionUtil.AddTransition(gestureLockDisabledState, setEnableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact.name)
                .If(InternalParameters.ContactLockON.name)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .IfNot(InternalParameters.SwitchToVehicleModeON.name)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Set Disable]
            // - 以下のすべての条件を満たす場合、ロック無効化を行う
            //   - AFK状態ではない場合
            //   - ダンスモードではない場合
            //   - Sit状態ではない場合
            //     - InStation = falseな状態が保証されているため、Sit判定時のロック機能自動無効化の状態についてはチェックを行わない
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, setDisableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact.name)
                .If(InternalParameters.ContactLockON.name)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .IfNot(VRCParameters.IN_STATION)
                .SetImmediateTransitionSettings();

            // [Gesture Lock Enabled] -> [Set Disable]
            // - Sit判定時にロック機能自動無効化がOFFの場合、Sit判定かつSeatedな状態でもロック無効化を行う
            //   - InStation = true かつ Seated = falseの時はロック機能自動無効化がOFFの場合でもロック状態の切り替えを行わないようにする(いわゆるダンスワールドでのアニメーション適用時など)
            AnimatorTransitionUtil.AddTransition(gestureLockEnabledState, setDisableState)
                .If(InternalParameters.FacialExpressionLockReceiverInContact.name)
                .If(InternalParameters.ContactLockON.name)
                .IfNot(InternalParameters.State_AFKModeActive.name)
                .IfNot(InternalParameters.State_DanceModeActive.name)
                .IfNot(InternalParameters.SwitchToVehicleModeON.name)
                .If(VRCParameters.IN_STATION)
                .If(VRCParameters.SEATED)
                .SetImmediateTransitionSettings();

            // [Set Disable] -> [Interval (Lock to Unlock)]
            AnimatorTransitionUtil.AddTransition(setDisableState, lockToUnlockIntervalState)
                .Exec((builder) =>
                {
                    var transition = builder.Transition;
                    transition.hasExitTime = true;
                    transition.exitTime = 0.5f;
                    transition.hasFixedDuration = true;
                    transition.duration = 0;
                    transition.offset = 0;
                    transition.interruptionSource = TransitionInterruptionSource.None;
                    transition.orderedInterruption = true;
                });

            // [Set Enable] -> [Interval (Unlock to Lock)]
            AnimatorTransitionUtil.AddTransition(setEnableState, unlockToLockIntervalState)
                .Exec((builder) =>
                {
                    var transition = builder.Transition;
                    transition.hasExitTime = true;
                    transition.exitTime = 0.5f;
                    transition.hasFixedDuration = true;
                    transition.duration = 0;
                    transition.offset = 0;
                    transition.interruptionSource = TransitionInterruptionSource.None;
                    transition.orderedInterruption = true;
                });

            // [Interval (Lock to Unlock)] -> [Gesture Lock Disabled]
            AnimatorTransitionUtil.AddTransition(lockToUnlockIntervalState, gestureLockDisabledState)
                .IfNot(InternalParameters.FacialExpressionLockReceiverInContact.name)
                .SetImmediateTransitionSettings();

            // [Interval (Unlock to Lock)] -> [Gesture Lock Enabled]
            AnimatorTransitionUtil.AddTransition(unlockToLockIntervalState, gestureLockEnabledState)
                .IfNot(InternalParameters.FacialExpressionLockReceiverInContact.name)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private AnimatorControllerLayer CreateAnimatorControllerLayer(string name, float defaultWeight = 0)
        {
            return new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = defaultWeight,
                stateMachine = new AnimatorStateMachine(),
            };
        }

        private VRCAvatarParameterDriver CreateVRCAvatarParameterLocalSetDriver(string parameterName, float value)
        {
            return new VRCAvatarParameterDriver
            {
                localOnly = true,
                parameters = new List<VRCAvatarParameterDriver.Parameter>
                {
                    new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRCAvatarParameterDriver.ChangeType.Set,
                        name = parameterName,
                        value = value,
                    }
                }
            };
        }

        private VRCAvatarParameterDriver CreateVRCAvatarParameterLocalCopyDriver(string sourcParameterName, string destinationParameterName)
        {
            return new VRCAvatarParameterDriver
            {
                localOnly = true,
                parameters = new List<VRCAvatarParameterDriver.Parameter>
                {
                    new VRCAvatarParameterDriver.Parameter
                    {
                        type = VRCAvatarParameterDriver.ChangeType.Copy,
                        name = destinationParameterName,
                        source = sourcParameterName,
                    }
                }
            };
        }

        private VRCAnimatorTrackingControl CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType trackingEyes, VRC_AnimatorTrackingControl.TrackingType trackingMouth)
        {
            return new VRCAnimatorTrackingControl()
            {
                trackingHead = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingHip = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.NoChange,
                trackingEyes = trackingEyes,
                trackingMouth = trackingMouth,
            };
        }

        private VRC_AnimatorTrackingControl.TrackingType GetTrackingType(TrackingControlType trackingControlType)
        {
            switch (trackingControlType)
            {
                case TrackingControlType.Animation:
                    return VRC_AnimatorTrackingControl.TrackingType.Animation;
                case TrackingControlType.Tracking:
                    return VRC_AnimatorTrackingControl.TrackingType.Tracking;
                default:
                    return VRC_AnimatorTrackingControl.TrackingType.NoChange;
            }
        }
    }
}
