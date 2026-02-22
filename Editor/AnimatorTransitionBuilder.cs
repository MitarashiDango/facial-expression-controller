using System;
using UnityEditor.Animations;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class AnimatorTransitionBuilder
    {
        public AnimatorTransition Transition { get; private set; }

        public AnimatorTransitionBuilder(AnimatorTransition transition)
        {
            Transition = transition;
        }

        public AnimatorTransitionBuilder If(string parameter)
        {
            Transition.AddCondition(AnimatorConditionMode.If, 0, parameter);
            return this;
        }

        public AnimatorTransitionBuilder If(Parameter parameter)
        {
            return If(parameter.name);
        }

        public AnimatorTransitionBuilder IfNot(string parameter)
        {
            Transition.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);
            return this;
        }

        public AnimatorTransitionBuilder IfNot(Parameter parameter)
        {
            return IfNot(parameter.name);
        }

        public AnimatorTransitionBuilder Greater(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.Greater, value, parameter);
            return this;
        }

        public AnimatorTransitionBuilder Greater(Parameter parameter, float value)
        {
            return Greater(parameter.name, value);
        }

        public AnimatorTransitionBuilder Less(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.Less, value, parameter);
            return this;
        }

        public AnimatorTransitionBuilder Less(Parameter parameter, float value)
        {
            return Less(parameter.name, value);
        }

        public AnimatorTransitionBuilder Equals(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.Equals, value, parameter);
            return this;
        }

        public AnimatorTransitionBuilder Equals(Parameter parameter, float value)
        {
            return Equals(parameter.name, value);
        }

        public AnimatorTransitionBuilder Exec(Action<AnimatorTransitionBuilder> func)
        {
            func(this);
            return this;
        }

        public AnimatorTransitionBuilder NotEqual(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.NotEqual, value, parameter);
            return this;
        }

        public AnimatorTransitionBuilder NotEqual(Parameter parameter, float value)
        {
            return NotEqual(parameter.name, value);
        }
    }
}
