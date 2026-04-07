using System;
using UnityEditor.Animations;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public abstract class TransitionBuilderBase<TBuilder, TTransition>
        where TBuilder : TransitionBuilderBase<TBuilder, TTransition>
        where TTransition : AnimatorTransitionBase
    {
        public TTransition Transition { get; private set; }

        protected TransitionBuilderBase(TTransition transition)
        {
            Transition = transition;
        }

        public TBuilder If(string parameter)
        {
            Transition.AddCondition(AnimatorConditionMode.If, 0, parameter);
            return (TBuilder)this;
        }

        public TBuilder If(Parameter parameter)
        {
            return If(parameter.name);
        }

        public TBuilder IfNot(string parameter)
        {
            Transition.AddCondition(AnimatorConditionMode.IfNot, 0, parameter);
            return (TBuilder)this;
        }

        public TBuilder IfNot(Parameter parameter)
        {
            return IfNot(parameter.name);
        }

        public TBuilder Greater(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.Greater, value, parameter);
            return (TBuilder)this;
        }

        public TBuilder Greater(Parameter parameter, float value)
        {
            return Greater(parameter.name, value);
        }

        public TBuilder Less(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.Less, value, parameter);
            return (TBuilder)this;
        }

        public TBuilder Less(Parameter parameter, float value)
        {
            return Less(parameter.name, value);
        }

        public TBuilder Equals(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.Equals, value, parameter);
            return (TBuilder)this;
        }

        public TBuilder Equals(Parameter parameter, float value)
        {
            return Equals(parameter.name, value);
        }

        public TBuilder NotEqual(string parameter, float value)
        {
            Transition.AddCondition(AnimatorConditionMode.NotEqual, value, parameter);
            return (TBuilder)this;
        }

        public TBuilder NotEqual(Parameter parameter, float value)
        {
            return NotEqual(parameter.name, value);
        }

        public TBuilder Exec(Action<TBuilder> func)
        {
            func((TBuilder)this);
            return (TBuilder)this;
        }
    }
}
