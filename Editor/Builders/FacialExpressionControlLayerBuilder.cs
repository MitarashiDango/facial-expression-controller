using System.Collections.Generic;
using System.Linq;
using MitarashiDango.FacialExpressionController.Runtime;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class FacialExpressionControlLayerBuilder : LayerBuilderBase
    {
        private readonly FacialExpressionControl _fec;

        public FacialExpressionControlLayerBuilder(AnimationClip blankClip, FacialExpressionControl fec)
            : base(blankClip)
        {
            _fec = fec;
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_FACIAL_EXPRESSION_CONTROL", 1.0f);

            layer.stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            layer.stateMachine.exitPosition = new Vector3(1000, 300, 0);
            layer.stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;

            var inactiveState = layer.stateMachine.AddState("Inactive", new Vector3(500, 0, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;

            var danceModeState = layer.stateMachine.AddState("Dance Mode", new Vector3(500, 80, 0));
            danceModeState.writeDefaultValues = false;
            danceModeState.motion = blankAnimationClip;
            danceModeState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType.Animation, VRC_AnimatorTrackingControl.TrackingType.Animation)
            };

            var afkState = layer.stateMachine.AddState("AFK", new Vector3(500, 160, 0));
            afkState.writeDefaultValues = false;
            afkState.motion = blankAnimationClip;

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(500, 240, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip; // MEMO まばたき等をアニメーションで表現する場合、ここで設定する (設定する場合、目と口のトラッキングタイプを Animation にする)
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType.Tracking, VRC_AnimatorTrackingControl.TrackingType.Tracking)
            };

            var gestureFacialExpressionsStateMachine = layer.stateMachine.AddStateMachine("Facial Expressions (Gesture)", new Vector3(500, 320, 0));
            gestureFacialExpressionsStateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            gestureFacialExpressionsStateMachine.exitPosition = new Vector3(1000, 0, 0);
            gestureFacialExpressionsStateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
            gestureFacialExpressionsStateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

            var selectedFacialExpressionsStateMachine = layer.stateMachine.AddStateMachine("Facial Expressions (Selected)", new Vector3(500, 400, 0));
            selectedFacialExpressionsStateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            selectedFacialExpressionsStateMachine.exitPosition = new Vector3(1000, 0, 0);
            selectedFacialExpressionsStateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
            selectedFacialExpressionsStateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

            // =========================================================
            // 基本ステートのトランジション設定
            // =========================================================

            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, inactiveState)
                .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive);

            AnimatorTransitionUtil.AddExitTransition(inactiveState)
                .NotEqual(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.Neutral);

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            // 表情が変化した場合に適用されるトランジション設定
            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Greater(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.LeftHandGesture - 1)
                .Less(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.SelectedFacialExpressionInMenu + 1)
                .Exec(SetCrossFadeStateTransitionSettings);

            var immediateModes = new[]
            {
                FacialExpressionControlModeType.DanceMode,
                FacialExpressionControlModeType.AFKMode
            };

            foreach (var mode in immediateModes)
            {
                AnimatorTransitionUtil.AddExitTransition(neutralState)
                    .Equals(SyncParameters.FacialExpressionControlMode, mode)
                    .SetImmediateTransitionSettings();
            }

            // 表情設定用サブステートマシンへのトランジション
            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, gestureFacialExpressionsStateMachine)
                .Greater(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.LeftHandGesture - 1)
                .Less(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.RightHandGestureFixed + 1);

            AnimatorTransitionUtil.AddExitTransition(gestureFacialExpressionsStateMachine, layer.stateMachine);

            AnimatorTransitionUtil.AddEntryTransition(layer.stateMachine, selectedFacialExpressionsStateMachine)
                .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.SelectedFacialExpressionInMenu);

            AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionsStateMachine, layer.stateMachine);

            // 個別ステート（ダンスモード等）へのトランジション
            AddSimpleModeTransition(layer.stateMachine, danceModeState, FacialExpressionControlModeType.DanceMode);
            AddSimpleModeTransition(layer.stateMachine, afkState, FacialExpressionControlModeType.AFKMode);

            var gestureFacialExpressions = _fec.facialExpressionGesturePresets
                .SelectMany(x => x != null
                    ? new List<FacialExpression> { x.fist, x.handOpen, x.fingerPoint, x.victory, x.rockNRoll, x.handGun, x.thumbsUp }
                    : new List<FacialExpression> { null, null, null, null, null, null, null })
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / FacialExpressionNumbering.StateGroupSize)
                .Select(g => g.Select(x => x.v).ToList())
                .ToList();

            for (var i = 0; i < gestureFacialExpressions.Count; i++)
            {
                var groupBaseNumber = i * FacialExpressionNumbering.StateGroupSize;
                var stateMachine = gestureFacialExpressionsStateMachine.AddStateMachine($"Facial Expression Gesture Preset ({groupBaseNumber + 1} ~ {groupBaseNumber + gestureFacialExpressions[i].Count})", new Vector3(500, AnimatorLayout.RowSpacing * i, 0));
                stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
                stateMachine.exitPosition = new Vector3(1000, 0, 0);
                stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
                stateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

                AnimatorTransitionUtil.AddEntryTransition(gestureFacialExpressionsStateMachine, stateMachine)
                    .Greater(SyncParameters.CurrentFacialExpressionNumber, groupBaseNumber)
                    .Less(SyncParameters.CurrentFacialExpressionNumber, groupBaseNumber + gestureFacialExpressions[i].Count + 1);

                AnimatorTransitionUtil.AddExitTransition(stateMachine, gestureFacialExpressionsStateMachine);

                for (var j = 0; j < gestureFacialExpressions[i].Count; j++)
                {
                    var facialExpressionNumber = groupBaseNumber + j + 1;
                    var childStateMachine = CreateGestureFacialExpressionSubStateMachine(stateMachine, gestureFacialExpressions[i][j], facialExpressionNumber, new Vector3(500, AnimatorLayout.RowSpacing * j, 0));

                    AnimatorTransitionUtil.AddEntryTransition(stateMachine, childStateMachine)
                        .Equals(SyncParameters.CurrentFacialExpressionNumber, facialExpressionNumber);

                    AnimatorTransitionUtil.AddExitTransition(childStateMachine, stateMachine);
                }
            }

            var selectedFacialExpressions = _fec.facialExpressionGroups
                .SelectMany(x => x.facialExpressions)
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / FacialExpressionNumbering.StateGroupSize)
                .Select(g => g.Select(x => x.v).ToList())
                .ToList();

            for (var i = 0; i < selectedFacialExpressions.Count; i++)
            {
                var groupBaseNumber = i * FacialExpressionNumbering.StateGroupSize;
                var stateMachine = selectedFacialExpressionsStateMachine.AddStateMachine($"Selected Facial Expression Group ({groupBaseNumber + 1} ~ {groupBaseNumber + selectedFacialExpressions[i].Count})", new Vector3(500, AnimatorLayout.RowSpacing * i, 0));
                stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
                stateMachine.exitPosition = new Vector3(1000, 0, 0);
                stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
                stateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

                AnimatorTransitionUtil.AddEntryTransition(selectedFacialExpressionsStateMachine, stateMachine)
                    .Greater(SyncParameters.CurrentFacialExpressionNumber, groupBaseNumber)
                    .Less(SyncParameters.CurrentFacialExpressionNumber, groupBaseNumber + selectedFacialExpressions[i].Count + 1);

                AnimatorTransitionUtil.AddExitTransition(stateMachine, selectedFacialExpressionsStateMachine);

                for (var j = 0; j < selectedFacialExpressions[i].Count; j++)
                {
                    var facialExpressionNumber = groupBaseNumber + j + 1;
                    var state = CreateFacialExpressionState(stateMachine, $"Selected Facial Expression ({facialExpressionNumber})", selectedFacialExpressions[i][j], SyncParameters.FixedWeight, new Vector3(500, AnimatorLayout.RowSpacing * j, 0));

                    AnimatorTransitionUtil.AddEntryTransition(stateMachine, state)
                        .Equals(SyncParameters.CurrentFacialExpressionNumber, facialExpressionNumber);

                    AnimatorTransitionUtil.AddExitTransition(state)
                        .Greater(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.Neutral - 1)
                        .Less(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.RightHandGestureFixed + 1)
                        .Exec(SetCrossFadeStateTransitionSettings);

                    AnimatorTransitionUtil.AddExitTransition(state)
                        .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.SelectedFacialExpressionInMenu)
                        .NotEqual(SyncParameters.CurrentFacialExpressionNumber, facialExpressionNumber)
                        .Exec(SetCrossFadeStateTransitionSettings);

                    AnimatorTransitionUtil.AddExitTransition(state)
                        .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                        .SetImmediateTransitionSettings();

                    foreach (var mode in immediateModes)
                    {
                        AnimatorTransitionUtil.AddExitTransition(state)
                            .Equals(SyncParameters.FacialExpressionControlMode, mode)
                            .SetImmediateTransitionSettings();
                    }
                }
            }

            return layer;
        }

        private void AddSimpleModeTransition(AnimatorStateMachine stateMachine, AnimatorState state, int modeValue)
        {
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, state)
                .Equals(SyncParameters.FacialExpressionControlMode, modeValue);

            AnimatorTransitionUtil.AddExitTransition(state)
                .NotEqual(SyncParameters.FacialExpressionControlMode, modeValue)
                .SetImmediateTransitionSettings();
        }

        private AnimatorStateMachine CreateGestureFacialExpressionSubStateMachine(
            AnimatorStateMachine parentStateMachine,
            FacialExpression facialExpression,
            int facialExpressionNumber,
            Vector3 position)
        {
            var facialExpressionName = $"Gesture Facial Expression ({(string.IsNullOrEmpty(facialExpression?.FacialExpressionName) ? facialExpressionNumber.ToString() : facialExpression?.FacialExpressionName)})";

            var stateMachine = parentStateMachine.AddStateMachine(facialExpressionName, position);
            stateMachine.entryPosition = new Vector3(0, 2 * AnimatorLayout.RowSpacing, 0);
            stateMachine.exitPosition = new Vector3(1000, 2 * AnimatorLayout.RowSpacing, 0);
            stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;
            stateMachine.parentStateMachinePosition = new Vector3(1000, 320, 0);

            var leftGestureState = CreateFacialExpressionState(stateMachine, "Left Gesture", facialExpression, VRCParameters.GESTURE_LEFT_WEIGHT, new Vector3(300, 0, 0));
            var leftGestureFixedState = CreateFacialExpressionState(stateMachine, "Left Gesture (Fixed)", facialExpression, SyncParameters.FixedWeight, new Vector3(700, 0, 0));
            var rightGestureState = CreateFacialExpressionState(stateMachine, "Right Gesture", facialExpression, VRCParameters.GESTURE_RIGHT_WEIGHT, new Vector3(300, 320, 0));
            var rightGestureFixedState = CreateFacialExpressionState(stateMachine, "Right Gesture (Fixed)", facialExpression, SyncParameters.FixedWeight, new Vector3(700, 320, 0));

            // ハンドジェスチャー用ステート
            var stateDefinitions = new[]
            {
                new { State = leftGestureState, Mode = FacialExpressionControlModeType.LeftHandGesture },
                new { State = leftGestureFixedState, Mode = FacialExpressionControlModeType.LeftHandGestureFixed },
                new { State = rightGestureState, Mode = FacialExpressionControlModeType.RightHandGesture },
                new { State = rightGestureFixedState, Mode = FacialExpressionControlModeType.RightHandGestureFixed }
            };

            foreach (var src in stateDefinitions)
            {
                // Entry遷移
                AnimatorTransitionUtil.AddEntryTransition(stateMachine, src.State)
                    .Equals(SyncParameters.FacialExpressionControlMode, src.Mode);

                // 相互遷移
                foreach (var dest in stateDefinitions)
                {
                    if (src.Mode == dest.Mode)
                    {
                        continue;
                    }

                    AnimatorTransitionUtil.AddTransition(src.State, dest.State)
                        .Equals(SyncParameters.FacialExpressionControlMode, dest.Mode)
                        .Equals(SyncParameters.CurrentFacialExpressionNumber, facialExpressionNumber)
                        .SetImmediateTransitionSettings();
                }

                // Exit遷移の追加
                AnimatorTransitionUtil.AddExitTransition(src.State)
                    .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(src.State)
                    .Greater(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.Neutral - 1)
                    .Less(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.RightHandGestureFixed + 1)
                    .NotEqual(SyncParameters.CurrentFacialExpressionNumber, facialExpressionNumber)
                    .Exec(SetCrossFadeStateTransitionSettings);

                AnimatorTransitionUtil.AddExitTransition(src.State)
                    .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.SelectedFacialExpressionInMenu)
                    .Exec(SetCrossFadeStateTransitionSettings);

                // その他（Dance, AFK）
                var immediateModes = new[]
                {
                    FacialExpressionControlModeType.DanceMode,
                    FacialExpressionControlModeType.AFKMode
                };

                foreach (var mode in immediateModes)
                {
                    AnimatorTransitionUtil.AddExitTransition(src.State)
                        .Equals(SyncParameters.FacialExpressionControlMode, mode)
                        .SetImmediateTransitionSettings();
                }
            }

            return stateMachine;
        }

        private AnimatorState CreateFacialExpressionState(AnimatorStateMachine stateMachine, string stateName, FacialExpression facialExpression, Parameter motionTimeParameter, Vector3 position)
        {
            return CreateFacialExpressionState(stateMachine, stateName, facialExpression, motionTimeParameter.name, position);
        }

        private AnimatorState CreateFacialExpressionState(AnimatorStateMachine stateMachine, string stateName, FacialExpression facialExpression, string motionTimeParameterName, Vector3 position)
        {
            var state = stateMachine.AddState(stateName, position);
            state.speed = _fec.transitionTime > 0 ? 1f / _fec.transitionTime : 0f;
            state.writeDefaultValues = false;
            state.motion = facialExpression?.motion != null ? facialExpression.motion : blankAnimationClip;

            if (!string.IsNullOrEmpty(motionTimeParameterName))
            {
                state.timeParameterActive = true;
                state.timeParameter = motionTimeParameterName;
            }

            state.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAnimatorTrackingControl(
                    GetTrackingType(facialExpression?.eyeControlType ?? TrackingControlType.Tracking),
                    GetTrackingType(facialExpression?.mouthControlType ?? TrackingControlType.Tracking)
                )
            };

            return state;
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

        private void SetCrossFadeStateTransitionSettings(AnimatorStateTransitionBuilder builder)
        {
            var transition = builder.Transition;
            transition.hasExitTime = false;
            transition.exitTime = 0;
            transition.hasFixedDuration = true;
            transition.duration = _fec.transitionTime;
            transition.offset = 0;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
        }
    }
}