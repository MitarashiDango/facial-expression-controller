using MitarashiDango.FacialExpressionController.Runtime;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    [CustomEditor(typeof(FacialExpressionControl))]
    public class FacialExpressionControlEditor : UnityEditor.Editor
    {
        private const string MainUxmlPath = "Packages/com.matcha-soft.facial-expression-controller/Editor/UI/Inspectors/FacialExpressionControlEditor.uxml";

        private VisualElement _defaultFaceField;
        private VisualElement _afkExitMotionWaitModeField;
        private VisualElement _afkExitMotionWaitDurationField;
        private VisualElement _afkExitMotionWaitParameterConditions;
        private HelpBox _transitionTimeWarning;

        public override VisualElement CreateInspectorGUI()
        {
            var mainUxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainUxmlPath);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"[FacialExpressionController] Cannot load UXML file: {MainUxmlPath}");
                return null;
            }

            var root = mainUxmlAsset.CloneTree();

            LanguagePrefs.ApplyFontPreferences(root);

            // 要素参照の取得
            _defaultFaceField = root.Q("default-face");
            _afkExitMotionWaitModeField = root.Q("afk-exit-motion-wait-mode");
            _afkExitMotionWaitDurationField = root.Q("afk-exit-motion-wait-duration");
            _afkExitMotionWaitParameterConditions = root.Q("afk-exit-motion-wait-parameter-conditions");
            _transitionTimeWarning = root.Q<HelpBox>("transition-time-warning");

            // SerializedProperty参照の取得
            var generateDefaultProp = serializedObject.FindProperty("generateDefaultFacialAnimation");
            var useAFKModeProp = serializedObject.FindProperty("useAFKMode");
            var afkExitMotionWaitModeProp = serializedObject.FindProperty("afkExitMotionWaitMode");
            var transitionTimeProp = serializedObject.FindProperty("transitionTime");

            // プロパティ変更の監視
            root.TrackPropertyValue(generateDefaultProp, _ => UpdateDefaultFaceVisibility(generateDefaultProp));
            root.TrackPropertyValue(useAFKModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitMotionWaitModeProp));
            root.TrackPropertyValue(afkExitMotionWaitModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitMotionWaitModeProp));
            root.TrackPropertyValue(transitionTimeProp, _ => UpdateTransitionTimeValidation(transitionTimeProp));

            // 初期表示状態の設定
            UpdateDefaultFaceVisibility(generateDefaultProp);
            UpdateAFKFieldsVisibility(useAFKModeProp, afkExitMotionWaitModeProp);
            UpdateTransitionTimeValidation(transitionTimeProp);

            return root;
        }

        private void UpdateDefaultFaceVisibility(SerializedProperty generateDefaultProp)
        {
            _defaultFaceField?.EnableInClassList("hidden", generateDefaultProp.boolValue);
        }

        private void UpdateAFKFieldsVisibility(SerializedProperty useAFKModeProp, SerializedProperty afkExitMotionWaitModeProp)
        {
            var useAFK = useAFKModeProp.boolValue;
            _afkExitMotionWaitModeField?.EnableInClassList("hidden", !useAFK);

            var mode = (AFKExitMotionWaitMode)afkExitMotionWaitModeProp.enumValueIndex;
            _afkExitMotionWaitDurationField?.EnableInClassList("hidden", !(useAFK && mode == AFKExitMotionWaitMode.Duration));
            _afkExitMotionWaitParameterConditions?.EnableInClassList("hidden", !(useAFK && mode == AFKExitMotionWaitMode.Parameter));
        }

        private void UpdateTransitionTimeValidation(SerializedProperty transitionTimeProp)
        {
            _transitionTimeWarning?.EnableInClassList("hidden", transitionTimeProp.floatValue >= 0);
        }
    }
}
