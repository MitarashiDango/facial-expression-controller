using UnityEditor.Animations;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class AnimatorTransitionBuilder : TransitionBuilderBase<AnimatorTransitionBuilder, AnimatorTransition>
    {
        public AnimatorTransitionBuilder(AnimatorTransition transition) : base(transition)
        {
        }
    }
}
