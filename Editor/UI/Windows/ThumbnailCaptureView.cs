using System;
using System.Collections.Generic;
using MitarashiDango.FacialExpressionController;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public sealed class ThumbnailCaptureView : IDisposable
    {
        private const string TransparentLabel = "透過";
        private const string SolidColorLabel = "単色";
        private const string NoExpressionLabel = "表情がありません";

        private static readonly List<string> BackgroundChoices = new List<string> { TransparentLabel, SolidColorLabel };

        private readonly VisualElement _root;
        private readonly FloatField _distanceField;
        private readonly Vector3Field _angleField;
        private readonly Vector3Field _offsetField;
        private readonly PopupField<string> _backgroundModeField;
        private readonly ColorField _backgroundColorField;
        private readonly VisualElement _previewContainer;
        private readonly Button _autoFrameButton;
        private readonly Button _previewButton;
        private readonly Image _previewImage;
        private readonly HelpBox _busyBox;
        private readonly Button _captureButton;
        private readonly ObjectField _capturedIconField;
        private readonly Label _capturedPathLabel;
        private readonly ObjectField _targetGroupField;
        private readonly Button _reloadExpressionsButton;
        private readonly PopupField<string> _targetExpressionField;
        private readonly Button _assignButton;
        private readonly List<string> _expressionChoices = new List<string>();

        private GameObject _avatarRootObject;
        private Texture2D _capturedTexture;
        private Texture2D _previewTexture;
        private string _capturedAssetPath = "";
        private int _selectedExpressionIndex = -1;
        private bool _hasModel;
        private bool _isBusy;
        private bool _ignoreChange;

        public ThumbnailCaptureView(VisualElement container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            container.Clear();

            _root = new Foldout
            {
                text = "サムネイル撮影",
                value = false,
            };
            _root.AddToClassList("thumbnail-foldout");
            container.Add(_root);

            var bodyContainer = new VisualElement();
            bodyContainer.AddToClassList("thumbnail-body");
            _root.Add(bodyContainer);

            var controlContainer = new VisualElement();
            controlContainer.AddToClassList("thumbnail-controls");
            bodyContainer.Add(controlContainer);

            _previewContainer = new VisualElement();
            _previewContainer.AddToClassList("thumbnail-preview");
            bodyContainer.Add(_previewContainer);

            var settingsContainer = new VisualElement();
            settingsContainer.AddToClassList("thumbnail-settings");
            controlContainer.Add(settingsContainer);

            _autoFrameButton = new Button(() => AutoFrameRequested?.Invoke())
            {
                text = "距離・角度・オフセットを自動調整",
            };
            _autoFrameButton.AddToClassList("thumbnail-button");

            _distanceField = new FloatField("距離");
            ConfigureField(_distanceField);
            _distanceField.RegisterValueChangedCallback(_ => SaveSettings());
            settingsContainer.Add(_distanceField);

            _angleField = new Vector3Field("角度");
            ConfigureField(_angleField);
            _angleField.RegisterValueChangedCallback(_ => SaveSettings());
            settingsContainer.Add(_angleField);

            _offsetField = new Vector3Field("オフセット");
            ConfigureField(_offsetField);
            _offsetField.RegisterValueChangedCallback(_ => SaveSettings());
            settingsContainer.Add(_offsetField);

            _backgroundModeField = new PopupField<string>("背景", BackgroundChoices, 0);
            ConfigureField(_backgroundModeField);
            _backgroundModeField.RegisterValueChangedCallback(_ =>
            {
                UpdateBackgroundColorVisibility();
                SaveSettings();
            });
            settingsContainer.Add(_backgroundModeField);

            _backgroundColorField = new ColorField("背景色")
            {
                showAlpha = false,
                showEyeDropper = false,
            };
            ConfigureField(_backgroundColorField);
            _backgroundColorField.RegisterValueChangedCallback(_ => SaveSettings());
            settingsContainer.Add(_backgroundColorField);

            _captureButton = new Button(() => CaptureRequested?.Invoke())
            {
                text = "撮影して保存",
            };
            _captureButton.AddToClassList("thumbnail-button");

            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
            };
            _previewImage.AddToClassList("thumbnail-preview-image");
            _previewContainer.Add(_previewImage);

            _busyBox = new HelpBox("", HelpBoxMessageType.Info);
            _busyBox.AddToClassList("thumbnail-busy");
            _previewContainer.Add(_busyBox);

            _previewButton = new Button(() => PreviewRequested?.Invoke())
            {
                text = "プレビューを更新",
            };
            _previewButton.AddToClassList("thumbnail-button");
            _previewButton.AddToClassList("thumbnail-preview-button");
            _previewContainer.Add(_previewButton);

            var assignmentContainer = new VisualElement();
            assignmentContainer.AddToClassList("thumbnail-assignment");
            controlContainer.Add(assignmentContainer);

            _capturedIconField = new ObjectField("撮影済み PNG")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
            };
            ConfigureField(_capturedIconField);
            _capturedIconField.RegisterValueChangedCallback(evt =>
            {
                if (_ignoreChange)
                {
                    return;
                }

                _capturedTexture = evt.newValue as Texture2D;
                _capturedAssetPath = _capturedTexture != null ? AssetDatabase.GetAssetPath(_capturedTexture) : "";
                UpdateCapturedPathLabel();
                UpdateButtonStates();
            });
            assignmentContainer.Add(_capturedIconField);

            _capturedPathLabel = new Label();
            _capturedPathLabel.AddToClassList("thumbnail-path");
            assignmentContainer.Add(_capturedPathLabel);

            var groupFieldContainer = new VisualElement();
            groupFieldContainer.AddToClassList("thumbnail-group-row");
            assignmentContainer.Add(groupFieldContainer);

            _targetGroupField = new ObjectField("割り当て先グループ")
            {
                objectType = typeof(FacialExpressionGroup),
                allowSceneObjects = false,
            };
            ConfigureField(_targetGroupField);
            _targetGroupField.RegisterValueChangedCallback(_ => RefreshExpressionChoices());
            groupFieldContainer.Add(_targetGroupField);

            _reloadExpressionsButton = new Button(RefreshExpressionChoices)
            {
                text = "再読み込み",
                tooltip = "割り当て先グループの表情一覧を再読み込みします",
            };
            _reloadExpressionsButton.AddToClassList("thumbnail-reload-button");
            groupFieldContainer.Add(_reloadExpressionsButton);

            _expressionChoices.Add(NoExpressionLabel);
            _targetExpressionField = new PopupField<string>("表情", _expressionChoices, 0);
            ConfigureField(_targetExpressionField);
            _targetExpressionField.RegisterValueChangedCallback(evt =>
            {
                _selectedExpressionIndex = _expressionChoices.IndexOf(evt.newValue);
                UpdateButtonStates();
            });
            assignmentContainer.Add(_targetExpressionField);

            var assignmentActionContainer = new VisualElement();
            assignmentActionContainer.AddToClassList("thumbnail-action-row");
            assignmentContainer.Add(assignmentActionContainer);

            _assignButton = new Button(AssignCapturedIcon)
            {
                text = "メニューアイコンに設定",
            };
            _assignButton.AddToClassList("thumbnail-button");
            assignmentActionContainer.Add(_autoFrameButton);
            assignmentActionContainer.Add(_captureButton);
            assignmentActionContainer.Add(_assignButton);

            SetModel(null);
            RefreshExpressionChoices();
            UpdateCapturedPathLabel();
            UpdateBackgroundColorVisibility();
            SetBusy(false, "");
        }

        public event Action AutoFrameRequested;
        public event Action PreviewRequested;
        public event Action CaptureRequested;
        public event Action<string, HelpBoxMessageType> MessageRequested;

        public ThumbnailCaptureSettings Settings
        {
            get
            {
                return new ThumbnailCaptureSettings
                {
                    distance = Mathf.Max(0.01f, _distanceField.value),
                    eulerAngles = _angleField.value,
                    offset = _offsetField.value,
                    backgroundMode = _backgroundModeField.value == SolidColorLabel
                        ? ThumbnailBackgroundMode.SolidColor
                        : ThumbnailBackgroundMode.Transparent,
                    backgroundColor = _backgroundColorField.value,
                };
            }
        }

        public void SetModel(ExpressionEditModel model)
        {
            _avatarRootObject = model != null ? model.avatarRootObject : null;
            _hasModel = model != null;
            ClearPreviewTexture();
            SetSettingsWithoutNotify(ThumbnailCaptureService.LoadSettings(_avatarRootObject));
            UpdateButtonStates();
        }

        public void SetCapturedTexture(Texture2D texture, string assetPath)
        {
            _capturedTexture = texture;
            _capturedAssetPath = assetPath ?? "";
            _ignoreChange = true;
            _capturedIconField.SetValueWithoutNotify(texture);
            _ignoreChange = false;
            UpdateCapturedPathLabel();
            UpdateButtonStates();
        }

        public void ApplySettings(ThumbnailCaptureSettings settings, bool save)
        {
            SetSettingsWithoutNotify(settings);
            if (save)
            {
                ThumbnailCaptureService.SaveSettings(_avatarRootObject, settings);
            }
        }

        public void SetPreviewTexture(Texture2D texture)
        {
            ClearPreviewTexture();
            _previewTexture = texture;
            _previewImage.image = _previewTexture;
            UpdatePreviewVisibility();
        }

        public void SetBusy(bool busy, string message)
        {
            _isBusy = busy;
            if (_busyBox != null)
            {
                _busyBox.text = message ?? "";
                _busyBox.EnableInClassList("hidden", !busy || string.IsNullOrEmpty(message));
            }

            UpdateButtonStates();
            UpdatePreviewVisibility();
        }

        public void Dispose()
        {
            ClearPreviewTexture();
        }

        private static void ConfigureField(BaseField<float> field)
        {
            field.AddToClassList("thumbnail-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private static void ConfigureField(BaseField<Vector3> field)
        {
            field.AddToClassList("thumbnail-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private static void ConfigureField(BaseField<string> field)
        {
            field.AddToClassList("thumbnail-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private static void ConfigureField(BaseField<Color> field)
        {
            field.AddToClassList("thumbnail-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private static void ConfigureField(ObjectField field)
        {
            field.AddToClassList("thumbnail-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private void SetSettingsWithoutNotify(ThumbnailCaptureSettings settings)
        {
            _ignoreChange = true;
            _distanceField.SetValueWithoutNotify(Mathf.Max(0.01f, settings.distance));
            _angleField.SetValueWithoutNotify(settings.eulerAngles);
            _offsetField.SetValueWithoutNotify(settings.offset);
            _backgroundModeField.SetValueWithoutNotify(settings.backgroundMode == ThumbnailBackgroundMode.SolidColor
                ? SolidColorLabel
                : TransparentLabel);
            _backgroundColorField.SetValueWithoutNotify(settings.backgroundColor);
            _ignoreChange = false;
            UpdateBackgroundColorVisibility();
        }

        private void SaveSettings()
        {
            if (_ignoreChange)
            {
                return;
            }

            ThumbnailCaptureService.SaveSettings(_avatarRootObject, Settings);
        }

        private void RefreshExpressionChoices()
        {
            _expressionChoices.Clear();
            _selectedExpressionIndex = -1;

            var group = _targetGroupField.value as FacialExpressionGroup;
            if (group != null && group.facialExpressions != null)
            {
                for (var i = 0; i < group.facialExpressions.Count; i++)
                {
                    _expressionChoices.Add(GetExpressionLabel(group.facialExpressions[i], i));
                }
            }

            if (_expressionChoices.Count == 0)
            {
                _expressionChoices.Add(NoExpressionLabel);
                _targetExpressionField.choices = _expressionChoices;
                _targetExpressionField.SetValueWithoutNotify(NoExpressionLabel);
                _targetExpressionField.SetEnabled(false);
            }
            else
            {
                _selectedExpressionIndex = 0;
                _targetExpressionField.choices = _expressionChoices;
                _targetExpressionField.SetValueWithoutNotify(_expressionChoices[0]);
                _targetExpressionField.SetEnabled(true);
            }

            UpdateButtonStates();
            UpdateReloadButtonVisibility();
        }

        private static string GetExpressionLabel(FacialExpression expression, int index)
        {
            var expressionName = expression != null ? expression.FacialExpressionName : "";
            if (string.IsNullOrEmpty(expressionName))
            {
                expressionName = $"表情 {index + 1}";
            }

            return $"{index + 1}: {expressionName}";
        }

        private void AssignCapturedIcon()
        {
            var group = _targetGroupField.value as FacialExpressionGroup;
            if (_capturedTexture == null)
            {
                MessageRequested?.Invoke("割り当てる PNG を指定してください。", HelpBoxMessageType.Warning);
                return;
            }

            if (group == null || _selectedExpressionIndex < 0)
            {
                MessageRequested?.Invoke("割り当て先の表情グループと表情を指定してください。", HelpBoxMessageType.Warning);
                return;
            }

            var serializedObject = new SerializedObject(group);
            serializedObject.Update();
            var expressionsProperty = serializedObject.FindProperty("facialExpressions");
            if (expressionsProperty == null || _selectedExpressionIndex >= expressionsProperty.arraySize)
            {
                MessageRequested?.Invoke("割り当て先の表情を取得できませんでした。", HelpBoxMessageType.Warning);
                return;
            }

            var iconProperty = expressionsProperty.GetArrayElementAtIndex(_selectedExpressionIndex)
                .FindPropertyRelative("menuIcon");
            if (iconProperty == null)
            {
                MessageRequested?.Invoke("割り当て先のメニューアイコンを取得できませんでした。", HelpBoxMessageType.Warning);
                return;
            }

            Undo.RecordObject(group, "表情サムネイルを割り当て");
            iconProperty.objectReferenceValue = _capturedTexture;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssets();
            MessageRequested?.Invoke("メニューアイコンに設定しました。", HelpBoxMessageType.Info);
        }

        private void UpdateBackgroundColorVisibility()
        {
            _backgroundColorField.EnableInClassList("hidden", _backgroundModeField.value != SolidColorLabel);
        }

        private void UpdateCapturedPathLabel()
        {
            _capturedPathLabel.text = _capturedAssetPath;
            _capturedPathLabel.EnableInClassList("hidden", string.IsNullOrEmpty(_capturedAssetPath));
        }

        private void UpdateButtonStates()
        {
            var canAssign = _capturedTexture != null
                && _targetGroupField.value is FacialExpressionGroup
                && _selectedExpressionIndex >= 0;
            _autoFrameButton.SetEnabled(_hasModel && !_isBusy);
            _previewButton.SetEnabled(_hasModel && !_isBusy);
            _captureButton.SetEnabled(_hasModel && !_isBusy);
            _assignButton.SetEnabled(canAssign && !_isBusy);
            _distanceField.SetEnabled(!_isBusy);
            _angleField.SetEnabled(!_isBusy);
            _offsetField.SetEnabled(!_isBusy);
            _backgroundModeField.SetEnabled(!_isBusy);
            _backgroundColorField.SetEnabled(!_isBusy);
            _capturedIconField.SetEnabled(!_isBusy);
            _targetGroupField.SetEnabled(!_isBusy);
            _targetExpressionField.SetEnabled(!_isBusy && _expressionChoices.Count > 0 && _expressionChoices[0] != NoExpressionLabel);
            UpdateReloadButtonVisibility();
        }

        private void ClearPreviewTexture()
        {
            if (_previewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            if (_previewImage != null)
            {
                _previewImage.image = null;
            }

            UpdatePreviewVisibility();
        }

        private void UpdatePreviewVisibility()
        {
            _previewContainer.EnableInClassList("hidden", !_hasModel || (_previewTexture == null && !_isBusy));
        }

        private void UpdateReloadButtonVisibility()
        {
            _reloadExpressionsButton.EnableInClassList("hidden", _targetGroupField.value == null);
            _reloadExpressionsButton.SetEnabled(!_isBusy);
        }
    }
}
