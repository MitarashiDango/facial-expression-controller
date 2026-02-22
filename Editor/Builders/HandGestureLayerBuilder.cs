using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class HandGestureLayerBuilder : LayerBuilderBase
    {
        public enum HandType
        {
            Left,
            Right
        }

        private readonly HandType _handType;

        public HandGestureLayerBuilder(AnimationClip blankClip, HandType handType) : base(blankClip)
        {
            _handType = handType;
        }

        public override AnimatorControllerLayer Build()
        {
            string layerName;
            string currentGestureParameterName;
            string gestureParameterName;
            int lastGestureChangedHandValue;

            if (_handType == HandType.Left)
            {
                layerName = "FEC_LEFT_HAND_GESTURE";
                currentGestureParameterName = InternalParameters.State_CurrentGestureLeft.name;
                gestureParameterName = VRCParameters.GESTURE_LEFT;
                lastGestureChangedHandValue = 1;
            }
            else
            {
                layerName = "FEC_RIGHT_HAND_GESTURE";
                currentGestureParameterName = InternalParameters.State_CurrentGestureRight.name;
                gestureParameterName = VRCParameters.GESTURE_RIGHT;
                lastGestureChangedHandValue = 2;
            }

            var layer = CreateAnimatorControllerLayer(layerName);

            layer.stateMachine.entryPosition = new Vector3(0, 0, 0);
            layer.stateMachine.anyStatePosition = new Vector3(0, -40, 0);
            layer.stateMachine.exitPosition = new Vector3(800, 0, 0);

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(200, 0, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            int currentY = 0;
            int spacingY = 60;

            var neutralState = layer.stateMachine.AddState("Neutral", new Vector3(500, currentY, 0));
            neutralState.writeDefaultValues = false;
            neutralState.motion = blankAnimationClip;
            neutralState.behaviours = new StateMachineBehaviour[]
            {
                CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, 0),
                CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand, lastGestureChangedHandValue),
            };

            AnimatorTransitionUtil.AddTransition(initialState, neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .Equals(gestureParameterName, 0)
                .If(VRCParameters.IS_LOCAL)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddExitTransition(neutralState)
                .IfNot(InternalParameters.FacialExpressionLocked)
                .NotEqual(gestureParameterName, 0)
                .SetImmediateTransitionSettings();

            // 1〜7のジェスチャーステートを一括生成
            foreach (var gesture in GestureConstants.Gestures)
            {
                currentY += spacingY;

                var state = layer.stateMachine.AddState(gesture.Name, new Vector3(500, currentY, 0));
                state.writeDefaultValues = false;
                state.motion = blankAnimationClip;
                state.behaviours = new StateMachineBehaviour[]
                {
                    CreateVRCAvatarParameterLocalSetDriver(currentGestureParameterName, gesture.Value),
                    CreateVRCAvatarParameterLocalSetDriver(InternalParameters.State_LastGestureChangedHand, lastGestureChangedHandValue),
                };

                // [Initial State] -> [Gesture]
                AnimatorTransitionUtil.AddTransition(initialState, state)
                    .IfNot(InternalParameters.FacialExpressionLocked)
                    .Equals(gestureParameterName, gesture.Value)
                    .If(VRCParameters.IS_LOCAL)
                    .SetImmediateTransitionSettings();

                // [Gesture] -> [Exit]
                AnimatorTransitionUtil.AddExitTransition(state)
                    .IfNot(InternalParameters.FacialExpressionLocked)
                    .NotEqual(gestureParameterName, gesture.Value)
                    .SetImmediateTransitionSettings();
            }

            return layer;
        }
    }
}