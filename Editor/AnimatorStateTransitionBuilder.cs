using UnityEditor.Animations;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class AnimatorStateTransitionBuilder : TransitionBuilderBase<AnimatorStateTransitionBuilder, AnimatorStateTransition>
    {
        public AnimatorStateTransitionBuilder(AnimatorStateTransition transition) : base(transition)
        {
        }

        public void SetImmediateTransitionSettings()
        {
            Transition.hasExitTime = false;
            Transition.exitTime = 0;
            Transition.hasFixedDuration = true;
            Transition.duration = 0;
            Transition.offset = 0;
            Transition.interruptionSource = TransitionInterruptionSource.None;
            Transition.orderedInterruption = true;
        }
    }
}
