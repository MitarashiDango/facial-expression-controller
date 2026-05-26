using MitarashiDango.FacialExpressionController;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    [CustomEditor(typeof(FacialExpressionController))]
    public class FacialExpressionControllerEditor : UnityEditor.Editor
    {
        private const string MainUxmlPath = "Packages/com.matcha-soft.facial-expression-controller/Editor/UI/Inspectors/FacialExpressionControllerEditor.uxml";

        private VisualElement _defaultFacialExpressionMotionField;
        private VisualElement _afkExitWaitModeField;
        private VisualElement _afkExitWaitDurationField;
        private VisualElement _afkExitWaitConditions;
        private VisualElement _layerRemovalTargetsField;
        private HelpBox _transitionDurationWarning;

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
            _defaultFacialExpressionMotionField = root.Q("default-facial-expression-motion");
            _afkExitWaitModeField = root.Q("afk-exit-wait-mode");
            _afkExitWaitDurationField = root.Q("afk-exit-wait-duration");
            _afkExitWaitConditions = root.Q("afk-exit-wait-conditions");
            _layerRemovalTargetsField = root.Q("layer-removal-targets");
            _transitionDurationWarning = root.Q<HelpBox>("transition-duration-warning");

            // SerializedProperty参照の取得
            var generateDefaultProp = serializedObject.FindProperty("generateDefaultFacialExpressionAnimation");
            var useAFKModeProp = serializedObject.FindProperty("useAFKMode");
            var afkExitWaitModeProp = serializedObject.FindProperty("afkExitWaitMode");
            var transitionDurationProp = serializedObject.FindProperty("transitionDuration");
            var removeExistingFacialExpressionLayersProp = serializedObject.FindProperty("removeExistingFacialExpressionLayers");

            // プロパティ変更の監視
            root.TrackPropertyValue(generateDefaultProp, _ => UpdateDefaultFaceVisibility(generateDefaultProp));
            root.TrackPropertyValue(useAFKModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp));
            root.TrackPropertyValue(afkExitWaitModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp));
            root.TrackPropertyValue(transitionDurationProp, _ => UpdateTransitionDurationValidation(transitionDurationProp));
            root.TrackPropertyValue(removeExistingFacialExpressionLayersProp, _ => UpdateLayersToRemoveVisibility(removeExistingFacialExpressionLayersProp));

            // 初期表示状態の設定
            UpdateDefaultFaceVisibility(generateDefaultProp);
            UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp);
            UpdateTransitionDurationValidation(transitionDurationProp);
            UpdateLayersToRemoveVisibility(removeExistingFacialExpressionLayersProp);

            return root;
        }

        private void UpdateDefaultFaceVisibility(SerializedProperty generateDefaultProp)
        {
            _defaultFacialExpressionMotionField?.EnableInClassList("hidden", generateDefaultProp.boolValue);
        }

        private void UpdateAFKFieldsVisibility(SerializedProperty useAFKModeProp, SerializedProperty afkExitWaitModeProp)
        {
            var useAFK = useAFKModeProp.boolValue;
            _afkExitWaitModeField?.EnableInClassList("hidden", !useAFK);

            var mode = (AFKExitWaitMode)afkExitWaitModeProp.enumValueIndex;
            _afkExitWaitDurationField?.EnableInClassList("hidden", !(useAFK && mode == AFKExitWaitMode.Duration));
            _afkExitWaitConditions?.EnableInClassList("hidden", !(useAFK && mode == AFKExitWaitMode.Parameter));
        }

        private void UpdateTransitionDurationValidation(SerializedProperty transitionDurationProp)
        {
            _transitionDurationWarning?.EnableInClassList("hidden", transitionDurationProp.floatValue >= 0);
        }

        private void UpdateLayersToRemoveVisibility(SerializedProperty removeExistingFacialExpressionLayersProp)
        {
            _layerRemovalTargetsField?.EnableInClassList("hidden", !removeExistingFacialExpressionLayersProp.boolValue);
        }
    }
}
