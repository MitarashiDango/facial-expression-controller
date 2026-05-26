using System;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController
{
    [Serializable]
    public class AnimatorLayerRemovalTarget
    {
        public VRCAvatarDescriptor.AnimLayerType layerType = VRCAvatarDescriptor.AnimLayerType.FX;
        public string layerName;
    }
}
