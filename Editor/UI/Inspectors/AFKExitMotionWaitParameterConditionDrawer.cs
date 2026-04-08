using MitarashiDango.FacialExpressionController.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    [CustomPropertyDrawer(typeof(AFKExitMotionWaitParameterCondition))]
    public class AFKExitMotionWaitParameterConditionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            var parameterNameField = new PropertyField(property.FindPropertyRelative("parameterName"), "パラメーター名");
            var compareModeField = new PropertyField(property.FindPropertyRelative("compareMode"), "条件");
            var thresholdField = new PropertyField(property.FindPropertyRelative("threshold"), "閾値")
            {
                name = "threshold-field"
            };

            root.Add(parameterNameField);
            root.Add(compareModeField);
            root.Add(thresholdField);

            var compareModeProp = property.FindPropertyRelative("compareMode");
            root.TrackPropertyValue(compareModeProp, _ => UpdateThresholdVisibility(thresholdField, compareModeProp));
            UpdateThresholdVisibility(thresholdField, compareModeProp);

            return root;
        }

        private void UpdateThresholdVisibility(VisualElement thresholdField, SerializedProperty compareModeProp)
        {
            var mode = (AnimatorConditionCompareMode)compareModeProp.enumValueIndex;
            var hideThreshold = mode == AnimatorConditionCompareMode.If || mode == AnimatorConditionCompareMode.IfNot;
            thresholdField?.EnableInClassList("hidden", hideThreshold);
        }
    }
}
