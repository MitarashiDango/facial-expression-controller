using System.Collections.Generic;
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
        private const double DefaultGesturePresetRefreshIntervalSeconds = 0.25d;

        private VisualElement _defaultFacialExpressionMotionField;
        private VisualElement _afkExitWaitModeField;
        private VisualElement _afkExitWaitDurationField;
        private VisualElement _afkExitWaitConditions;
        private VisualElement _layerRemovalTargetsField;
        private VisualElement _defaultLeftGesturePresetField;
        private VisualElement _defaultRightGesturePresetField;
        private HelpBox _transitionDurationWarning;
        private HelpBox _defaultGesturePresetWarning;
        private SerializedProperty _defaultLeftGesturePresetNumberProp;
        private SerializedProperty _defaultRightGesturePresetNumberProp;
        private string _defaultGesturePresetSignature;
        private double _nextDefaultGesturePresetRefreshTime;

        private void OnEnable()
        {
            EditorApplication.update += UpdateDefaultGesturePresetFieldsIfNeeded;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateDefaultGesturePresetFieldsIfNeeded;
        }

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
            _defaultLeftGesturePresetField = root.Q("default-left-gesture-preset");
            _defaultRightGesturePresetField = root.Q("default-right-gesture-preset");
            _transitionDurationWarning = root.Q<HelpBox>("transition-duration-warning");
            _defaultGesturePresetWarning = root.Q<HelpBox>("default-gesture-preset-warning");

            // SerializedProperty参照の取得
            var generateDefaultProp = serializedObject.FindProperty("generateDefaultFacialExpressionAnimation");
            var facialExpressionGesturePresetsProp = serializedObject.FindProperty("facialExpressionGesturePresets");
            _defaultLeftGesturePresetNumberProp = serializedObject.FindProperty("defaultLeftGesturePresetNumber");
            _defaultRightGesturePresetNumberProp = serializedObject.FindProperty("defaultRightGesturePresetNumber");
            var useAFKModeProp = serializedObject.FindProperty("useAFKMode");
            var afkExitWaitModeProp = serializedObject.FindProperty("afkExitWaitMode");
            var transitionDurationProp = serializedObject.FindProperty("transitionDuration");
            var removeExistingFacialExpressionLayersProp = serializedObject.FindProperty("removeExistingFacialExpressionLayers");

            // プロパティ変更の監視
            root.TrackPropertyValue(generateDefaultProp, _ => UpdateDefaultFaceVisibility(generateDefaultProp));
            root.TrackPropertyValue(facialExpressionGesturePresetsProp, _ => RefreshDefaultGesturePresetFieldsAndValidation(_defaultLeftGesturePresetNumberProp, _defaultRightGesturePresetNumberProp));
            root.TrackPropertyValue(_defaultLeftGesturePresetNumberProp, _ => RefreshDefaultGesturePresetFieldsAndValidation(_defaultLeftGesturePresetNumberProp, _defaultRightGesturePresetNumberProp));
            root.TrackPropertyValue(_defaultRightGesturePresetNumberProp, _ => RefreshDefaultGesturePresetFieldsAndValidation(_defaultLeftGesturePresetNumberProp, _defaultRightGesturePresetNumberProp));
            root.TrackPropertyValue(useAFKModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp));
            root.TrackPropertyValue(afkExitWaitModeProp, _ => UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp));
            root.TrackPropertyValue(transitionDurationProp, _ => UpdateTransitionDurationValidation(transitionDurationProp));
            root.TrackPropertyValue(removeExistingFacialExpressionLayersProp, _ => UpdateLayersToRemoveVisibility(removeExistingFacialExpressionLayersProp));

            // 初期表示状態の設定
            UpdateDefaultFaceVisibility(generateDefaultProp);
            RefreshDefaultGesturePresetFieldsAndValidation(_defaultLeftGesturePresetNumberProp, _defaultRightGesturePresetNumberProp);
            UpdateAFKFieldsVisibility(useAFKModeProp, afkExitWaitModeProp);
            UpdateTransitionDurationValidation(transitionDurationProp);
            UpdateLayersToRemoveVisibility(removeExistingFacialExpressionLayersProp);

            return root;
        }

        private void UpdateDefaultFaceVisibility(SerializedProperty generateDefaultProp)
        {
            _defaultFacialExpressionMotionField?.EnableInClassList("hidden", generateDefaultProp.boolValue);
        }

        private void RefreshDefaultGesturePresetFieldsAndValidation(SerializedProperty leftProp, SerializedProperty rightProp)
        {
            _defaultGesturePresetSignature = CreateDefaultGesturePresetSignature(leftProp.intValue, rightProp.intValue);
            RefreshDefaultGesturePresetFields(leftProp, rightProp);
            UpdateDefaultGesturePresetValidation(leftProp, rightProp);
        }

        private void UpdateDefaultGesturePresetFieldsIfNeeded()
        {
            if (_defaultLeftGesturePresetField == null
                || _defaultRightGesturePresetField == null
                || _defaultLeftGesturePresetNumberProp == null
                || _defaultRightGesturePresetNumberProp == null)
            {
                return;
            }

            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime < _nextDefaultGesturePresetRefreshTime)
            {
                return;
            }

            _nextDefaultGesturePresetRefreshTime = currentTime + DefaultGesturePresetRefreshIntervalSeconds;
            serializedObject.UpdateIfRequiredOrScript();
            var signature = CreateDefaultGesturePresetSignature(
                _defaultLeftGesturePresetNumberProp.intValue,
                _defaultRightGesturePresetNumberProp.intValue);
            if (signature == _defaultGesturePresetSignature)
            {
                return;
            }

            RefreshDefaultGesturePresetFieldsAndValidation(
                _defaultLeftGesturePresetNumberProp,
                _defaultRightGesturePresetNumberProp);
        }

        private void RefreshDefaultGesturePresetFields(SerializedProperty leftProp, SerializedProperty rightProp)
        {
            RefreshDefaultGesturePresetField(
                _defaultLeftGesturePresetField,
                "左手の初期プリセット",
                "ビルド時に左手で最初に選択する表情ジェスチャープリセットです。",
                leftProp,
                leftProp,
                rightProp);
            RefreshDefaultGesturePresetField(
                _defaultRightGesturePresetField,
                "右手の初期プリセット",
                "ビルド時に右手で最初に選択する表情ジェスチャープリセットです。",
                rightProp,
                leftProp,
                rightProp);
        }

        private void RefreshDefaultGesturePresetField(
            VisualElement container,
            string label,
            string tooltip,
            SerializedProperty presetNumberProp,
            SerializedProperty leftProp,
            SerializedProperty rightProp)
        {
            if (container == null)
            {
                return;
            }

            var choices = CreateDefaultGesturePresetChoices(presetNumberProp.intValue);
            var labels = new List<string>(choices.Count);
            var selectedIndex = 0;
            var hasSelectableChoice = false;

            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                labels.Add(choice.Label);

                if (choice.IsSelectable)
                {
                    hasSelectableChoice = true;
                }

                if (choice.PresetNumber == presetNumberProp.intValue)
                {
                    selectedIndex = i;
                }
            }

            var popup = new PopupField<string>(label, labels, selectedIndex)
            {
                tooltip = tooltip,
            };
            LanguagePrefs.ApplyFontPreferences(popup);
            popup.SetEnabled(hasSelectableChoice);
            popup.RegisterValueChangedCallback(evt =>
            {
                var choice = FindDefaultGesturePresetChoice(choices, evt.newValue);
                if (choice == null || !choice.IsSelectable)
                {
                    popup.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                serializedObject.Update();
                presetNumberProp.intValue = choice.PresetNumber;
                serializedObject.ApplyModifiedProperties();
                RefreshDefaultGesturePresetFieldsAndValidation(leftProp, rightProp);
            });

            container.Clear();
            container.Add(popup);
        }

        private List<DefaultGesturePresetChoice> CreateDefaultGesturePresetChoices(int currentPresetNumber)
        {
            var choices = new List<DefaultGesturePresetChoice>();
            var fec = target as FacialExpressionController;

            if (fec != null && fec.facialExpressionGesturePresets != null)
            {
                for (var i = 0; i < fec.facialExpressionGesturePresets.Count; i++)
                {
                    var preset = fec.facialExpressionGesturePresets[i];
                    if (preset == null)
                    {
                        continue;
                    }

                    var presetNumber = i + GesturePresetDefaultValueResolver.FirstPresetNumber;
                    choices.Add(DefaultGesturePresetChoice.Selectable(
                        presetNumber,
                        FormatDefaultGesturePresetLabel(presetNumber, preset)));
                }
            }

            if (choices.Count == 0)
            {
                choices.Add(DefaultGesturePresetChoice.Disabled(0, "有効なプリセットがありません"));
                return choices;
            }

            if (!GesturePresetDefaultValueResolver.IsValidPresetNumber(fec, currentPresetNumber))
            {
                choices.Insert(0, DefaultGesturePresetChoice.Disabled(
                    currentPresetNumber,
                    $"現在の設定: プリセット {currentPresetNumber} (無効)"));
            }

            return choices;
        }

        private string CreateDefaultGesturePresetSignature(int leftPresetNumber, int rightPresetNumber)
        {
            var fec = target as FacialExpressionController;
            var signatureParts = new List<string>
            {
                leftPresetNumber.ToString(),
                rightPresetNumber.ToString(),
            };

            if (fec == null || fec.facialExpressionGesturePresets == null)
            {
                signatureParts.Add("null");
                return string.Join("\n", signatureParts);
            }

            signatureParts.Add(fec.facialExpressionGesturePresets.Count.ToString());
            for (var i = 0; i < fec.facialExpressionGesturePresets.Count; i++)
            {
                var preset = fec.facialExpressionGesturePresets[i];
                signatureParts.Add(preset == null
                    ? "null"
                    : $"{preset.GetInstanceID()}:{preset.presetName}");
            }

            return string.Join("\n", signatureParts);
        }

        private static DefaultGesturePresetChoice FindDefaultGesturePresetChoice(List<DefaultGesturePresetChoice> choices, string label)
        {
            foreach (var choice in choices)
            {
                if (choice.Label == label)
                {
                    return choice;
                }
            }

            return null;
        }

        private static string FormatDefaultGesturePresetLabel(int presetNumber, FacialExpressionGesturePreset preset)
        {
            var presetName = string.IsNullOrWhiteSpace(preset.presetName)
                ? $"プリセット {presetNumber}"
                : preset.presetName;
            return $"{presetNumber}: {presetName}";
        }

        private static string FormatDefaultGesturePresetReference(FacialExpressionController fec, int presetNumber)
        {
            var preset = GetGesturePreset(fec, presetNumber);
            return preset == null
                ? $"プリセット {presetNumber}"
                : FormatDefaultGesturePresetLabel(presetNumber, preset);
        }

        private static FacialExpressionGesturePreset GetGesturePreset(FacialExpressionController fec, int presetNumber)
        {
            if (fec == null || fec.facialExpressionGesturePresets == null)
            {
                return null;
            }

            var index = presetNumber - GesturePresetDefaultValueResolver.FirstPresetNumber;
            if (index < 0 || index >= fec.facialExpressionGesturePresets.Count)
            {
                return null;
            }

            return fec.facialExpressionGesturePresets[index];
        }

        private void UpdateDefaultGesturePresetValidation(SerializedProperty leftProp, SerializedProperty rightProp)
        {
            if (_defaultGesturePresetWarning == null)
            {
                return;
            }

            var fec = target as FacialExpressionController;
            var warnings = new List<string>();

            if (!GesturePresetDefaultValueResolver.HasValidPreset(fec))
            {
                warnings.Add("有効な表情ジェスチャープリセットがありません。ビルド時は左右とも内部値 0 を使用します。");
            }
            else
            {
                AddDefaultGesturePresetWarning(warnings, fec, leftProp.intValue, "左手");
                AddDefaultGesturePresetWarning(warnings, fec, rightProp.intValue, "右手");
            }

            _defaultGesturePresetWarning.text = string.Join("\n", warnings);
            _defaultGesturePresetWarning.EnableInClassList("hidden", warnings.Count == 0);
        }

        private static void AddDefaultGesturePresetWarning(List<string> warnings, FacialExpressionController fec, int presetNumber, string handLabel)
        {
            if (GesturePresetDefaultValueResolver.IsValidPresetNumber(fec, presetNumber))
            {
                return;
            }

            var resolvedPresetNumber = GesturePresetDefaultValueResolver.ResolveIndex(fec, presetNumber)
                + GesturePresetDefaultValueResolver.FirstPresetNumber;
            warnings.Add($"{handLabel}の初期プリセットが有効なプリセットを指していません。ビルド時は {FormatDefaultGesturePresetReference(fec, resolvedPresetNumber)} を使用します。");
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

        private sealed class DefaultGesturePresetChoice
        {
            private DefaultGesturePresetChoice(int presetNumber, string label, bool isSelectable)
            {
                PresetNumber = presetNumber;
                Label = label;
                IsSelectable = isSelectable;
            }

            public int PresetNumber { get; }
            public string Label { get; }
            public bool IsSelectable { get; }

            public static DefaultGesturePresetChoice Selectable(int presetNumber, string label)
            {
                return new DefaultGesturePresetChoice(presetNumber, label, true);
            }

            public static DefaultGesturePresetChoice Disabled(int presetNumber, string label)
            {
                return new DefaultGesturePresetChoice(presetNumber, label, false);
            }
        }
    }
}
