using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    public abstract class LayerBuilderBase
    {
        protected readonly AnimationClip blankAnimationClip;

        protected LayerBuilderBase(AnimationClip blankClip)
        {
            blankAnimationClip = blankClip;
        }

        /// <summary>
        /// レイヤーの構築を行います。サブクラスで具体的なステートやトランジションを実装します。
        /// </summary>
        public abstract AnimatorControllerLayer Build();

        protected AnimatorControllerLayer CreateAnimatorControllerLayer(string name, float defaultWeight = 0)
        {
            return new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = defaultWeight,
                stateMachine = new AnimatorStateMachine(),
            };
        }

        protected VRCAvatarParameterDriver CreateVRCAvatarParameterLocalSetDriver(Parameter parameter, float value)
        {
            return CreateVRCAvatarParameterLocalSetDriver(parameter.name, value);
        }

        protected VRCAvatarParameterDriver CreateVRCAvatarParameterLocalSetDriver(string parameterName, float value)
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

        protected VRCAvatarParameterDriver CreateVRCAvatarParameterLocalCopyDriver(Parameter sourceParameter, Parameter destinationParameter)
        {
            return CreateVRCAvatarParameterLocalCopyDriver(sourceParameter.name, destinationParameter.name);
        }

        protected VRCAvatarParameterDriver CreateVRCAvatarParameterLocalCopyDriver(string sourceParameterName, Parameter destinationParameter)
        {
            return CreateVRCAvatarParameterLocalCopyDriver(sourceParameterName, destinationParameter.name);
        }

        protected VRCAvatarParameterDriver CreateVRCAvatarParameterLocalCopyDriver(Parameter sourceParameter, string destinationParameterName)
        {
            return CreateVRCAvatarParameterLocalCopyDriver(sourceParameter.name, destinationParameterName);
        }

        protected VRCAvatarParameterDriver CreateVRCAvatarParameterLocalCopyDriver(string sourceParameterName, string destinationParameterName)
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
                        source = sourceParameterName,
                    }
                }
            };
        }

        protected VRCAnimatorTrackingControl CreateVRCAnimatorTrackingControl(VRC_AnimatorTrackingControl.TrackingType trackingEyes, VRC_AnimatorTrackingControl.TrackingType trackingMouth)
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
    }
}