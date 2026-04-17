using System.Linq;
using MitarashiDango.FacialExpressionController.Runtime;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class SelectFacialExpressionNumberLayerBuilder : LayerBuilderBase
    {
        private readonly FacialExpressionControl _fec;

        public SelectFacialExpressionNumberLayerBuilder(AnimationClip blankClip, FacialExpressionControl fec) : base(blankClip)
        {
            _fec = fec;
        }

        public override AnimatorControllerLayer Build()
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
                CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber, 0),
            };

            // Neutral への遷移
            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .If(VRCParameters.IS_LOCAL)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .Equals(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight, 0)
                .SetImmediateTransitionSettings();

            // ハンドジェスチャーによる表情番号選択ステートを生成
            for (var i = 0; i < _fec.facialExpressionGesturePresets.Count; i++)
            {
                var gesturePreset = _fec.facialExpressionGesturePresets[i];
                if (gesturePreset == null)
                {
                    continue;
                }

                var presetName = string.IsNullOrEmpty(gesturePreset.presetName) ? i.ToString() : gesturePreset.presetName;

                var gesturePresetStateMachine = layer.stateMachine.AddStateMachine($"Gesture Preset ({presetName})", new Vector3(300, i * 80, 0));
                gesturePresetStateMachine.entryPosition = new Vector3(0, 0, 0);
                gesturePresetStateMachine.exitPosition = new Vector3(600, 0, 0);
                gesturePresetStateMachine.anyStatePosition = new Vector3(0, -80, 0);
                gesturePresetStateMachine.parentStateMachinePosition = new Vector3(0, -160, 0);

                AnimatorTransitionUtil.AddTransition(initialState, gesturePresetStateMachine)
                    .If(VRCParameters.IS_LOCAL)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand, 1)
                    .NotEqual(InternalParameters.State_CurrentGestureLeft, 0)
                    .Equals(InternalParameters.SelectedLeftGesturePreset, i)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddTransition(initialState, gesturePresetStateMachine)
                    .If(VRCParameters.IS_LOCAL)
                    .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                    .Equals(InternalParameters.State_CurrentGestureHand, 2)
                    .NotEqual(InternalParameters.State_CurrentGestureRight, 0)
                    .Equals(InternalParameters.SelectedRightGesturePreset, i)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(gesturePresetStateMachine, layer.stateMachine);

                int currentY = 0;
                int spacingY = 80;

                foreach (var gesture in GestureConstants.Gestures)
                {
                    var state = gesturePresetStateMachine.AddState(gesture.Name, new Vector3(300, currentY, 0));
                    state.writeDefaultValues = false;
                    state.motion = blankAnimationClip;
                    state.behaviours = new StateMachineBehaviour[]
                    {
                        CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber, i * FacialExpressionNumbering.GestureCountPerPreset + gesture.Value),
                    };

                    AddGestureTransition(gesturePresetStateMachine, state, gesture.Value, i);

                    currentY += spacingY;
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
                var selectedFacialExpressionGroupStateMachine = layer.stateMachine.AddStateMachine($"Selected Facial Expression Group ({groupBaseNumber + 1} ~ {groupBaseNumber + selectedFacialExpressions[i].Count})", new Vector3(300, 80 * i + _fec.facialExpressionGesturePresets.Count * 80, 0));
                selectedFacialExpressionGroupStateMachine.entryPosition = new Vector3(0, 0, 0);
                selectedFacialExpressionGroupStateMachine.exitPosition = new Vector3(600, 0, 0);
                selectedFacialExpressionGroupStateMachine.anyStatePosition = new Vector3(0, -80, 0);
                selectedFacialExpressionGroupStateMachine.parentStateMachinePosition = new Vector3(600, 320, 0);

                AnimatorTransitionUtil.AddTransition(initialState, selectedFacialExpressionGroupStateMachine)
                    .If(VRCParameters.IS_LOCAL)
                    .Greater(InternalParameters.SelectedFacialExpressionInMenu, groupBaseNumber)
                    .Less(InternalParameters.SelectedFacialExpressionInMenu, groupBaseNumber + selectedFacialExpressions[i].Count + 1)
                    .SetImmediateTransitionSettings();

                AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionGroupStateMachine, layer.stateMachine);

                for (var j = 0; j < selectedFacialExpressions[i].Count; j++)
                {
                    var facialExpressionNumber = groupBaseNumber + j + 1;
                    var selectedFacialExpressionState = selectedFacialExpressionGroupStateMachine.AddState($"Selected Facial Expression ({facialExpressionNumber})", new Vector3(300, 80 * j, 0));
                    selectedFacialExpressionState.writeDefaultValues = false;
                    selectedFacialExpressionState.motion = blankAnimationClip;
                    selectedFacialExpressionState.behaviours = new StateMachineBehaviour[]
                    {
                        CreateVRCAvatarParameterLocalSetDriver(SyncParameters.CurrentFacialExpressionNumber, facialExpressionNumber),
                    };

                    AnimatorTransitionUtil.AddEntryTransition(selectedFacialExpressionGroupStateMachine, selectedFacialExpressionState)
                        .Equals(InternalParameters.SelectedFacialExpressionInMenu, facialExpressionNumber);

                    AnimatorTransitionUtil.AddExitTransition(selectedFacialExpressionState)
                        .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, facialExpressionNumber);
                }
            }

            return layer;
        }

        private void AddGestureTransition(AnimatorStateMachine stateMachine, AnimatorState state, int gestureNumber, int presetNumber)
        {
            AnimatorTransitionUtil.AddEntryTransition(stateMachine, state)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .Equals(InternalParameters.State_CurrentGestureLeft, gestureNumber);

            AnimatorTransitionUtil.AddEntryTransition(stateMachine, state)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .Equals(InternalParameters.State_CurrentGestureRight, gestureNumber);

            AnimatorTransitionUtil.AddExitTransition(state)
                .NotEqual(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 0)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.State_CurrentGestureLeft, gestureNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.State_CurrentGestureRight, gestureNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 1)
                .NotEqual(InternalParameters.SelectedLeftGesturePreset, presetNumber)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(state)
                .Equals(InternalParameters.SelectedFacialExpressionInMenu, 0)
                .Equals(InternalParameters.State_CurrentGestureHand, 2)
                .NotEqual(InternalParameters.SelectedRightGesturePreset, presetNumber)
                .SetImmediateTransitionSettings();
        }
    }
}