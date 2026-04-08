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
        private static string _mainUxmlGuid = "276a4def4ec44c640b707bc26454c4c5";

        private VisualElement _defaultFaceField;
        private VisualElement _afkExitWaitModeField;
        private VisualElement _waitAFKExitDurationTimeField;
        private HelpBox _transitionTimeWarning;

        public override VisualElement CreateInspectorGUI()
        {
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"Cannot load UXML file: {_mainUxmlGuid}");
                return null;
            }

            var root = mainUxmlAsset.CloneTree();

            LanguagePrefs.ApplyFontPreferences(root);

            // 要素参照の取得
            _defaultFaceField = root.Q("default-face");
            _afkExitWaitModeField = root.Q("afk-exit-wait-mode");
            _waitAFKExitDurationTimeField = root.Q("wait-afk-exit-duration-time");
            _transitionTimeWarning = root.Q<HelpBox>("transition-time-warning");

            // SerializedProperty参照の取得
            var generateDefaultProp = serializedObject.FindProperty("generateDefaultFacialAnimation");
            var useAFKModeProp = serializedObject.FindProperty("useAFKMode");
            var afkExitWaitModeProp = serializedObject.FindProperty("afkExitWaitMode");
            var transitionTimeProp = serializedObject.FindProperty("transitionTime");

            // プロパティ変更の監視
            root.TrackPropertyValue(generateDefaultProp, _ => UpdateDefaultFaceVisibility(generateDefaultProp));
            root.TrackPropertyValue(useAFKModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp));
            root.TrackPropertyValue(afkExitWaitModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp));
            root.TrackPropertyValue(transitionTimeProp, _ => UpdateTransitionTimeValidation(transitionTimeProp));

            // 初期表示状態の設定
            UpdateDefaultFaceVisibility(generateDefaultProp);
            UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp);
            UpdateTransitionTimeValidation(transitionTimeProp);

            return root;
        }

        private void UpdateDefaultFaceVisibility(SerializedProperty generateDefaultProp)
        {
            _defaultFaceField?.EnableInClassList("hidden", generateDefaultProp.boolValue);
        }

        private void UpdateAFKFieldsVisibility(SerializedProperty useAFKModeProp, SerializedProperty afkExitWaitModeProp)
        {
            var useAFK = useAFKModeProp.boolValue;
            _afkExitWaitModeField?.EnableInClassList("hidden", !useAFK);

            var showDuration = useAFK &&
                (AFKExitWaitMode)afkExitWaitModeProp.enumValueIndex == AFKExitWaitMode.Duration;
            _waitAFKExitDurationTimeField?.EnableInClassList("hidden", !showDuration);
        }

        private void UpdateTransitionTimeValidation(SerializedProperty transitionTimeProp)
        {
            _transitionTimeWarning?.EnableInClassList("hidden", transitionTimeProp.floatValue >= 0);
        }
    }
}
