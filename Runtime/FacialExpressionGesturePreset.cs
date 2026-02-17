
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    [CreateAssetMenu(fileName = "New Facial Expression Gesture Preset", menuName = "Facial Expression Controller/Facial Expression Gesture Preset", order = 1)]
    public class FacialExpressionGesturePreset : ScriptableObject
    {
        public string presetName;
        public FacialExpression fist;
        public FacialExpression handOpen;
        public FacialExpression fingerPoint;
        public FacialExpression victory;
        public FacialExpression rockNRoll;
        public FacialExpression handGun;
        public FacialExpression thumbsUp;
    }
}