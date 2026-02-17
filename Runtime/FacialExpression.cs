using System;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    [Serializable]
    public class FacialExpression
    {
        public string facialExpressionName = "";
        public Motion motion;
        public Texture2D menuIcon;
        public TrackingControlType eyeControlType;
        public TrackingControlType mouthControlType;

        public string FacialExpressionName => !string.IsNullOrEmpty(facialExpressionName) ? facialExpressionName : motion != null ? motion.name : "";
    }
}