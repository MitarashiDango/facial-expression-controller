using MitarashiDango.FacialExpressionController.Runtime;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public class DefaultFacialExpressionLayerBuilder : LayerBuilderBase
    {
        private readonly FacialExpressionControl _fec;
        private readonly GameObject _avatarRootObject;

        public DefaultFacialExpressionLayerBuilder(AnimationClip blankClip, FacialExpressionControl fec, GameObject avatarRootObject) : base(blankClip)
        {
            _fec = fec;
            _avatarRootObject = avatarRootObject;
        }

        public override AnimatorControllerLayer Build()
        {
            var layer = CreateAnimatorControllerLayer("FEC_DEFAULT_FACIAL_EXPRESSION", 1.0f);
            layer.stateMachine.entryPosition = AnimatorLayout.DefaultEntryPosition;
            layer.stateMachine.anyStatePosition = AnimatorLayout.DefaultAnyStatePosition;

            var initialState = layer.stateMachine.AddState("Initial State", new Vector3(-20, 80, 0));
            initialState.writeDefaultValues = false;
            initialState.motion = blankAnimationClip;

            var inactiveState = layer.stateMachine.AddState("Default Face Inactive", new Vector3(200, 80, 0));
            inactiveState.writeDefaultValues = false;
            inactiveState.motion = blankAnimationClip;

            var activeState = layer.stateMachine.AddState("Default Face Active", new Vector3(200, 0, 0));
            activeState.writeDefaultValues = false;
            activeState.motion = GetDefaultFaceAnimation();

            AnimatorTransitionUtil.AddTransition(initialState, inactiveState)
                .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(initialState, activeState)
                .NotEqual(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(inactiveState, activeState)
                .NotEqual(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            AnimatorTransitionUtil.AddTransition(activeState, inactiveState)
                .Equals(SyncParameters.FacialExpressionControlMode, FacialExpressionControlModeType.FacialExpressionControlInactive)
                .SetImmediateTransitionSettings();

            return layer;
        }

        private Motion GetDefaultFaceAnimation()
        {
            if (_fec.generateDefaultFacialAnimation)
            {
                var feag = new FacialExpressionAnimationGenerator();
                var generated = feag.FromAvatar("Default Facial Expression (Auto Generated)", _avatarRootObject, null);
                if (generated != null)
                {
                    return generated;
                }

                Debug.LogWarning("[FacialExpressionController] Failed to auto-generate the default facial expression animation. Falling back to a blank animation clip.");
                return blankAnimationClip;
            }
            return _fec.defaultFace != null ? _fec.defaultFace : blankAnimationClip;
        }
    }
}