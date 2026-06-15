using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public sealed class FacialExpressionEditorWindow : EditorWindow
    {
        private const string MainUxmlPath = "Packages/com.matcha-soft.facial-expression-controller/Editor/UI/Windows/FacialExpressionEditorWindow.uxml";
        private const string MainUssPath = "Packages/com.matcha-soft.facial-expression-controller/Editor/UI/Windows/FacialExpressionEditorWindow.uss";
        private const double PreviewDebounceSeconds = 0.016d;
        private const float PartialImportPreviewOffscreenOffset = 10000f;
        private const string SingleFrameModeLabel = "1 フレーム";
        private const string WeightBlendModeLabel = "ウェイト連動（2 フレーム）";
        private static readonly List<string> FrameModeChoices = new List<string> { SingleFrameModeLabel, WeightBlendModeLabel };

        private ObjectField _avatarField;
        private ObjectField _rendererField;
        private ObjectField _clipField;
        private PopupField<string> _frameModeField;
        private ToolbarSearchField _searchField;
        private ToolbarMenu _filterMenu;
        private Toggle _previewToggle;
        private Button _newButton;
        private Button _loadButton;
        private Button _saveButton;
        private Button _saveAsButton;
        private Button _resetButton;
        private Button _startFrameButton;
        private Button _endFrameButton;
        private Button _loadStartFromRendererButton;
        private Slider _previewWeightSlider;
        private HelpBox _messageBox;
        private VisualElement _weightModeContainer;
        private VisualElement _listContainer;
        private Label _emptyLabel;

        private ExpressionEditModel _model;
        private BlendShapeListView _blendShapeListView;
        private ThumbnailCaptureView _thumbnailCaptureView;
        private PartialImportView _partialImportView;
        private ExpressionPreviewService _previewService;
        private AnimationClip _currentClip;
        private string _currentAssetPath;
        private ExpressionFrameMode _loadedSourceFrameMode = ExpressionFrameMode.SingleFrame;
        private ExpressionEditFrame _editingFrame = ExpressionEditFrame.Start;
        private float _previewWeight;
        private bool _previewDirty;
        private bool _showChangedOnly;
        private bool _showEditableOnly;
        private bool _guiBuildRetryScheduled;
        private double _lastPreviewRequestTime;
        private int _partialImportPreviewRequestVersion;
        private double _partialImportPreviewCaptureReadyTime;
        private PartialImportPreviewCapture _pendingPartialImportPreviewCapture;
        private double _thumbnailCaptureReadyTime;
        private PendingThumbnailCapture _pendingThumbnailCapture;

        [MenuItem("Tools/Facial Expression Controller/表情アニメーションエディター")]
        public static void Open()
        {
            GetWindow<FacialExpressionEditorWindow>("表情アニメーションエディター");
        }

        [MenuItem("GameObject/Facial Expression Controller/表情アニメーションエディター", validate = true)]
        private static bool OpenFromGameObjectValidate()
        {
            return Selection.activeGameObject != null
                && Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>() != null;
        }

        [MenuItem("GameObject/Facial Expression Controller/表情アニメーションエディター")]
        private static void OpenFromGameObject()
        {
            var window = GetWindow<FacialExpressionEditorWindow>("表情アニメーションエディター");
            var descriptor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>()
                : null;
            if (descriptor != null)
            {
                window.SetAvatar(descriptor);
            }
        }

        public override void SaveChanges()
        {
            Save();
        }

        public override void DiscardChanges()
        {
            hasUnsavedChanges = false;
            StopPreview();
        }

        private void OnEnable()
        {
            minSize = new Vector2(560f, 420f);
            saveChangesMessage = "未保存の表情編集があります。保存しますか？";
            _previewService = new ExpressionPreviewService();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.update += OnEditorUpdate;
            if (BuildGui())
            {
                TryInitializeFromSelection();
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.delayCall -= RetryBuildGui;
            _guiBuildRetryScheduled = false;
            CancelPendingPartialImportPreviewCapture();
            CancelPendingThumbnailCapture();
            StopPreview();
            _previewService?.Dispose();
            _previewService = null;
            _thumbnailCaptureView?.Dispose();
            _thumbnailCaptureView = null;
            _partialImportView?.Dispose();
            _partialImportView = null;

            if (_model != null)
            {
                DestroyImmediate(_model);
                _model = null;
            }
        }

        private bool BuildGui()
        {
            rootVisualElement.Clear();

            var mainUxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainUxmlPath);
            if (mainUxmlAsset == null)
            {
                ScheduleBuildGuiRetry();
                Debug.LogWarning($"[FacialExpressionController] UXML file is not ready yet: {MainUxmlPath}");
                return false;
            }

            var root = mainUxmlAsset.CloneTree();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(MainUssPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            LanguagePrefs.ApplyFontPreferences(root);
            rootVisualElement.Add(root);

            var targetContainer = root.Q<VisualElement>("target-container");
            var toolbarContainer = root.Q<VisualElement>("toolbar-container");
            var frameModeContainer = root.Q<VisualElement>("frame-mode-container");
            var frameTabContainer = root.Q<VisualElement>("frame-tab-container");
            var weightPreviewContainer = root.Q<VisualElement>("weight-preview-container");
            var thumbnailContainer = root.Q<VisualElement>("thumbnail-container");
            var partialImportContainer = root.Q<VisualElement>("partial-import-container");
            _weightModeContainer = root.Q<VisualElement>("weight-mode-container");
            _listContainer = root.Q<VisualElement>("blendshape-list-container");
            _emptyLabel = root.Q<Label>("empty-label");
            _messageBox = root.Q<HelpBox>("message-box");

            _avatarField = new ObjectField("アバター")
            {
                objectType = typeof(VRCAvatarDescriptor),
                allowSceneObjects = true,
            };
            ConfigureTargetField(_avatarField);
            _avatarField.RegisterValueChangedCallback(evt => SetAvatar(evt.newValue as VRCAvatarDescriptor));
            targetContainer.Add(_avatarField);

            _rendererField = new ObjectField("対象 Skinned Mesh Renderer")
            {
                objectType = typeof(SkinnedMeshRenderer),
                allowSceneObjects = true,
            };
            ConfigureTargetField(_rendererField);
            _rendererField.RegisterValueChangedCallback(evt => SetRenderer(evt.newValue as SkinnedMeshRenderer));
            targetContainer.Add(_rendererField);

            _clipField = new ObjectField("編集対象アニメーションクリップ")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            ConfigureTargetField(_clipField);
            targetContainer.Add(_clipField);

            _newButton = new Button(CreateNewSession) { text = "新規" };
            _loadButton = new Button(LoadSelectedClip) { text = "読込" };
            _saveButton = new Button(Save) { text = "保存" };
            _saveAsButton = new Button(SaveAs) { text = "別名保存" };
            _resetButton = new Button(ResetValues) { text = "リセット" };
            toolbarContainer.Add(_newButton);
            toolbarContainer.Add(_loadButton);
            toolbarContainer.Add(_saveButton);
            toolbarContainer.Add(_saveAsButton);
            toolbarContainer.Add(_resetButton);

            _previewToggle = new Toggle("プレビュー")
            {
                value = true,
            };
            _previewToggle.RegisterValueChangedCallback(evt =>
            {
                if (_previewService != null)
                {
                    _previewService.PreviewEnabled = evt.newValue;
                }

                RequestPreview();
            });
            toolbarContainer.Add(_previewToggle);

            _frameModeField = new PopupField<string>("フレームモード", FrameModeChoices, 0);
            _frameModeField.AddToClassList("frame-mode-field");
            _frameModeField.labelElement.style.minWidth = 160f;
            _frameModeField.labelElement.style.width = 180f;
            _frameModeField.labelElement.style.flexShrink = 0f;
            _frameModeField.RegisterValueChangedCallback(evt => SetFrameMode(GetFrameModeFromLabel(evt.newValue)));
            frameModeContainer.Add(_frameModeField);

            _startFrameButton = new Button(() => SetEditingFrame(ExpressionEditFrame.Start, true)) { text = "始点" };
            _startFrameButton.AddToClassList("frame-tab-button");
            frameTabContainer.Add(_startFrameButton);

            _endFrameButton = new Button(() => SetEditingFrame(ExpressionEditFrame.End, true)) { text = "終点" };
            _endFrameButton.AddToClassList("frame-tab-button");
            frameTabContainer.Add(_endFrameButton);

            _previewWeightSlider = new Slider("ウェイトプレビュー", 0f, 1f)
            {
                value = _previewWeight,
            };
            _previewWeightSlider.AddToClassList("weight-preview-slider");
            _previewWeightSlider.RegisterValueChangedCallback(evt =>
            {
                _previewWeight = Mathf.Clamp01(evt.newValue);
                RequestPreview();
            });
            weightPreviewContainer.Add(_previewWeightSlider);

            _loadStartFromRendererButton = new Button(LoadCurrentRendererValuesIntoStart) { text = "始点へ Skinned Mesh Renderer 現在値を読み込み" };
            _loadStartFromRendererButton.AddToClassList("load-start-button");
            weightPreviewContainer.Add(_loadStartFromRendererButton);

            var filterContainer = root.Q<VisualElement>("filter-container");
            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("search-field");
            _searchField.RegisterValueChangedCallback(evt => _blendShapeListView?.SetSearchText(evt.newValue));
            filterContainer.Add(_searchField);

            _filterMenu = new ToolbarMenu();
            _filterMenu.AddToClassList("filter-menu");
            _filterMenu.menu.AppendAction("変更された項目のみ表示", _ => ToggleChangedOnlyFilter(), GetChangedOnlyFilterStatus);
            _filterMenu.menu.AppendAction("編集・出力対象のみ表示", _ => ToggleEditableOnlyFilter(), GetEditableOnlyFilterStatus);
            UpdateFilterMenuText();
            filterContainer.Add(_filterMenu);

            _thumbnailCaptureView = new ThumbnailCaptureView(thumbnailContainer);
            _thumbnailCaptureView.AutoFrameRequested += AutoFrameThumbnail;
            _thumbnailCaptureView.PreviewRequested += PreviewThumbnail;
            _thumbnailCaptureView.CaptureRequested += CaptureThumbnail;
            _thumbnailCaptureView.MessageRequested += ShowMessage;

            _partialImportView = new PartialImportView(partialImportContainer);
            _partialImportView.ApplyRequested += ApplyPartialImportValues;
            _partialImportView.PreviewRequested += RequestPartialImportPreview;

            UpdateButtonStates();
            UpdateFrameModeControls();
            ShowMessage("アバターを指定してください。", HelpBoxMessageType.Info);
            return true;
        }

        private void ScheduleBuildGuiRetry()
        {
            if (_guiBuildRetryScheduled)
            {
                return;
            }

            _guiBuildRetryScheduled = true;
            EditorApplication.delayCall += RetryBuildGui;
        }

        private void RetryBuildGui()
        {
            _guiBuildRetryScheduled = false;
            if (this == null)
            {
                return;
            }

            if (BuildGui())
            {
                TryInitializeFromSelection();
            }
        }

        private static void ConfigureTargetField(ObjectField field)
        {
            field.AddToClassList("target-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private void TryInitializeFromSelection()
        {
            if (_model != null)
            {
                return;
            }

            var descriptor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>()
                : null;
            if (descriptor != null)
            {
                SetAvatar(descriptor);
            }
        }

        private void SetAvatar(VRCAvatarDescriptor descriptor)
        {
            var currentDescriptor = _model != null && _model.avatarRootObject != null
                ? _model.avatarRootObject.GetComponent<VRCAvatarDescriptor>()
                : null;
            if (currentDescriptor != descriptor && !TryDiscardUnsavedChanges())
            {
                _avatarField.SetValueWithoutNotify(currentDescriptor);
                return;
            }

            var avatarRootObject = descriptor != null ? descriptor.gameObject : null;
            if (_avatarField != null && _avatarField.value != descriptor)
            {
                _avatarField.SetValueWithoutNotify(descriptor);
            }

            var resolvedRenderer = FaceRendererResolver.Resolve(descriptor, avatarRootObject);
            if (resolvedRenderer == null && descriptor != null)
            {
                resolvedRenderer = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(smr => smr.sharedMesh != null);
            }

            SetModel(avatarRootObject, resolvedRenderer, null, false);

            if (descriptor == null)
            {
                ShowMessage("アバターを指定してください。", HelpBoxMessageType.Info);
            }
            else if (resolvedRenderer == null)
            {
                ShowMessage("対象 Skinned Mesh Renderer を検出できませんでした。手動で指定してください。", HelpBoxMessageType.Warning);
            }
            else
            {
                ShowMessage("", HelpBoxMessageType.Info);
            }
        }

        private void SetRenderer(SkinnedMeshRenderer renderer)
        {
            var avatarDescriptor = _avatarField != null ? _avatarField.value as VRCAvatarDescriptor : null;
            var rendererAvatarRootObject = renderer != null ? MiscUtil.GetAvatarRoot(renderer.transform) : null;
            var avatarRootObject = avatarDescriptor != null ? avatarDescriptor.gameObject : rendererAvatarRootObject;
            if (renderer != null && !ValidateRendererForAvatar(renderer, avatarRootObject))
            {
                _rendererField.SetValueWithoutNotify(_model != null ? _model.targetRenderer : null);
                return;
            }

            if (_model != null && _model.targetRenderer != renderer && !TryDiscardUnsavedChanges())
            {
                _rendererField.SetValueWithoutNotify(_model.targetRenderer);
                return;
            }

            if (avatarDescriptor == null && rendererAvatarRootObject != null)
            {
                avatarDescriptor = rendererAvatarRootObject.GetComponent<VRCAvatarDescriptor>();
                _avatarField.SetValueWithoutNotify(avatarDescriptor);
                avatarRootObject = rendererAvatarRootObject;
            }

            SetModel(avatarRootObject, renderer, null, false);
            if (avatarRootObject != null && renderer != null)
            {
                ShowMessage("", HelpBoxMessageType.Info);
            }
        }

        private void CreateNewSession()
        {
            if (!TryDiscardUnsavedChanges())
            {
                return;
            }

            var avatarDescriptor = _avatarField.value as VRCAvatarDescriptor;
            var renderer = _rendererField.value as SkinnedMeshRenderer;
            var avatarRootObject = avatarDescriptor != null ? avatarDescriptor.gameObject : null;

            SetModel(avatarRootObject, renderer, null, false);
            _currentClip = null;
            _currentAssetPath = null;
            hasUnsavedChanges = false;
            ShowMessage("新規セッションを開始しました。", HelpBoxMessageType.Info);
        }

        private void LoadSelectedClip()
        {
            var clip = _clipField.value as AnimationClip;
            if (clip == null)
            {
                ShowMessage("読み込む AnimationClip を指定してください。", HelpBoxMessageType.Warning);
                return;
            }

            var avatarDescriptor = _avatarField.value as VRCAvatarDescriptor;
            var renderer = _rendererField.value as SkinnedMeshRenderer;
            var avatarRootObject = avatarDescriptor != null ? avatarDescriptor.gameObject : null;
            if (avatarRootObject == null || renderer == null)
            {
                ShowMessage("アバターと対象 Skinned Mesh Renderer を指定してください。", HelpBoxMessageType.Warning);
                return;
            }

            if (!TryDiscardUnsavedChanges())
            {
                return;
            }

            SetModel(avatarRootObject, renderer, clip, false);
            _currentClip = clip;
            _currentAssetPath = AssetDatabase.GetAssetPath(clip);
            hasUnsavedChanges = false;

            if (_model != null && _model.hasIntermediateKeys)
            {
                ShowMessage("3 つ以上のキーを持つクリップです。始端と終端のみを読み込みました。保存すると中間キーは出力されません。", HelpBoxMessageType.Warning);
            }
            else if (_loadedSourceFrameMode == ExpressionFrameMode.WeightBlend)
            {
                ShowMessage("2 フレームのクリップをウェイト連動モードで読み込みました。", HelpBoxMessageType.Info);
            }
            else
            {
                ShowMessage("クリップを読み込みました。", HelpBoxMessageType.Info);
            }
        }

        private void SetModel(GameObject avatarRootObject, SkinnedMeshRenderer renderer, AnimationClip sourceClip, bool markDirty)
        {
            CancelPendingThumbnailCapture();
            StopPreview();

            if (_model != null)
            {
                DestroyImmediate(_model);
                _model = null;
            }

            if (avatarRootObject != null && renderer != null)
            {
                _model = sourceClip != null
                    ? ExpressionClipIO.Load(sourceClip, avatarRootObject, renderer)
                    : ExpressionClipIO.CreateModel(avatarRootObject, renderer);
                _loadedSourceFrameMode = _model.frameMode;
                _editingFrame = ExpressionEditFrame.Start;
                _previewWeight = 0f;
            }
            else
            {
                _loadedSourceFrameMode = ExpressionFrameMode.SingleFrame;
                _editingFrame = ExpressionEditFrame.Start;
                _previewWeight = 0f;
            }

            if (_rendererField != null && _rendererField.value != renderer)
            {
                _rendererField.SetValueWithoutNotify(renderer);
            }

            RebuildBlendShapeList();
            _thumbnailCaptureView?.SetModel(_model);
            _partialImportView?.SetModel(_model);
            UpdateFrameModeControls();
            if (markDirty)
            {
                MarkUnsaved();
            }

            UpdateButtonStates();
            RequestPreview();
            if (_model != null)
            {
                UpdateThumbnailPreview(false);
            }
        }

        private void RebuildBlendShapeList()
        {
            _listContainer.Clear();
            _blendShapeListView = null;

            if (_model == null)
            {
                _emptyLabel?.EnableInClassList("hidden", false);
                return;
            }

            _blendShapeListView = new BlendShapeListView(_model, _listContainer, _emptyLabel);
            _blendShapeListView.Changed += OnModelChanged;
            _blendShapeListView.PreviewValueChanged += OnBlendShapePreviewValueChanged;
            _blendShapeListView.PreviewResetRequested += OnBlendShapePreviewResetRequested;
            _blendShapeListView.SetEditingFrame(_editingFrame);
            _blendShapeListView.SetSearchText(_searchField != null ? _searchField.value : "");
            _blendShapeListView.SetShowChangedOnly(_showChangedOnly);
            _blendShapeListView.SetShowEditableOnly(_showEditableOnly);
        }

        private void OnModelChanged()
        {
            _partialImportView?.Refresh();
            MarkUnsaved();
            RequestPreview();
        }

        private void OnBlendShapePreviewValueChanged(BlendShapeEntry entry)
        {
            _previewService?.SampleEntry(_model, entry, _previewWeight);
        }

        private void OnBlendShapePreviewResetRequested()
        {
            StopPreview();
            _partialImportView?.Refresh();
        }

        private void ResetValues()
        {
            _blendShapeListView?.ResetEditableValues();
        }

        private void SetFrameMode(ExpressionFrameMode frameMode)
        {
            if (_model == null)
            {
                UpdateFrameModeControls();
                return;
            }

            if (_model.frameMode == frameMode)
            {
                UpdateFrameModeControls();
                return;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("表情フレームモードを変更");
            Undo.RecordObject(_model, "表情フレームモードを変更");

            if (frameMode == ExpressionFrameMode.SingleFrame)
            {
                foreach (var entry in _model.entries)
                {
                    entry.endValue = entry.value;
                }

                _editingFrame = ExpressionEditFrame.Start;
                _previewWeight = 0f;
            }
            else
            {
                _editingFrame = ExpressionEditFrame.End;
                _previewWeight = 1f;
            }

            _model.frameMode = frameMode;
            EditorUtility.SetDirty(_model);
            Undo.CollapseUndoOperations(group);

            _blendShapeListView?.SetEditingFrame(_editingFrame);
            _partialImportView?.SetEditingFrame(_editingFrame);
            MarkUnsaved();
            UpdateFrameModeControls();
            RequestPreview();
        }

        private void SetEditingFrame(ExpressionEditFrame editingFrame, bool syncPreviewWeight)
        {
            _editingFrame = editingFrame;
            _blendShapeListView?.SetEditingFrame(_editingFrame);
            _partialImportView?.SetEditingFrame(_editingFrame);

            if (syncPreviewWeight && _model != null && _model.frameMode == ExpressionFrameMode.WeightBlend)
            {
                SetPreviewWeightWithoutNotify(editingFrame == ExpressionEditFrame.Start ? 0f : 1f);
            }

            UpdateFrameModeControls();
            RequestPreview();
        }

        private void LoadCurrentRendererValuesIntoStart()
        {
            if (_model == null || _model.targetRenderer == null || _model.targetRenderer.sharedMesh == null)
            {
                ShowMessage("対象 Skinned Mesh Renderer を指定してください。", HelpBoxMessageType.Warning);
                return;
            }

            StopPreview();

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("始点へ Skinned Mesh Renderer 現在値を読み込み");
            Undo.RecordObject(_model, "始点へ Skinned Mesh Renderer 現在値を読み込み");

            foreach (var entry in _model.entries)
            {
                if (!CanEditEntryValue(entry) || !IsValidBlendShapeIndex(entry, _model.targetRenderer))
                {
                    continue;
                }

                entry.value = Mathf.Clamp(_model.targetRenderer.GetBlendShapeWeight(entry.index), 0f, 100f);
            }

            EditorUtility.SetDirty(_model);
            Undo.CollapseUndoOperations(group);
            SetEditingFrame(ExpressionEditFrame.Start, false);
            SetPreviewWeightWithoutNotify(0f);
            _blendShapeListView?.Refresh();
            MarkUnsaved();
            RequestPreview();
            ShowMessage("始点へ現在の Skinned Mesh Renderer 値を読み込みました。", HelpBoxMessageType.Info);
        }

        private void ApplyPartialImportValues(IReadOnlyList<PartialImportValue> values)
        {
            if (_model == null)
            {
                ShowMessage("取り込み先の表情がありません。", HelpBoxMessageType.Warning);
                return;
            }

            if (values == null || values.Count == 0)
            {
                ShowMessage("取り込むブレンドシェイプを選択してください。", HelpBoxMessageType.Warning);
                return;
            }

            var applicableCount = CountApplicablePartialImportValues(_model, values);
            if (applicableCount == 0)
            {
                ShowMessage("取り込み可能なブレンドシェイプがありません。", HelpBoxMessageType.Warning);
                return;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("表情ブレンドシェイプを部分取り込み");
            Undo.RecordObject(_model, "表情ブレンドシェイプを部分取り込み");

            var appliedCount = ApplyPartialImportValuesToModel(_model, values);

            EditorUtility.SetDirty(_model);
            Undo.CollapseUndoOperations(group);
            _blendShapeListView?.Refresh();
            _partialImportView?.Refresh();
            MarkUnsaved();
            RequestPreview();
            ShowMessage($"{appliedCount} 件のブレンドシェイプを取り込みました。", HelpBoxMessageType.Info);
        }

        private int ApplyPartialImportValuesToModel(ExpressionEditModel targetModel, IReadOnlyList<PartialImportValue> values)
        {
            if (targetModel == null || values == null)
            {
                return 0;
            }

            var appliedCount = 0;
            foreach (var importValue in values)
            {
                if (!TryGetPartialImportTargetEntry(targetModel, importValue, out var entry)
                    || !CanEditEntryValue(entry))
                {
                    continue;
                }

                var clampedValue = Mathf.Clamp(importValue.Value, 0f, 100f);
                if (targetModel.frameMode == ExpressionFrameMode.WeightBlend
                    && _editingFrame == ExpressionEditFrame.End)
                {
                    entry.endValue = clampedValue;
                }
                else
                {
                    entry.value = clampedValue;
                    if (targetModel.frameMode == ExpressionFrameMode.SingleFrame)
                    {
                        entry.endValue = clampedValue;
                    }
                }

                appliedCount++;
            }

            return appliedCount;
        }

        private static int CountApplicablePartialImportValues(ExpressionEditModel targetModel, IReadOnlyList<PartialImportValue> values)
        {
            if (targetModel == null || values == null)
            {
                return 0;
            }

            return values.Count(importValue => TryGetPartialImportTargetEntry(targetModel, importValue, out var entry) && CanEditEntryValue(entry));
        }

        private static bool TryGetPartialImportTargetEntry(
            ExpressionEditModel targetModel,
            PartialImportValue importValue,
            out BlendShapeEntry entry)
        {
            entry = null;
            if (targetModel == null || importValue == null || targetModel.entries == null)
            {
                return false;
            }

            if (importValue.BlendShapeIndex >= 0)
            {
                entry = targetModel.entries.FirstOrDefault(candidate => candidate != null && candidate.index == importValue.BlendShapeIndex);
                if (entry != null)
                {
                    return true;
                }
            }

            entry = targetModel.entries.FirstOrDefault(candidate => candidate != null && candidate.name == importValue.Name);
            return entry != null;
        }

        private void RequestPartialImportPreview(IReadOnlyList<PartialImportValue> values)
        {
            _partialImportPreviewRequestVersion++;
            var previewValues = values != null
                ? values.ToList()
                : new List<PartialImportValue>();
            if (previewValues.Count == 0)
            {
                CancelPendingPartialImportPreviewCapture();
                _partialImportView?.ClearPreviewTexture();
                return;
            }

            UpdatePartialImportPreview(previewValues);
        }

        private void UpdatePartialImportPreview(IReadOnlyList<PartialImportValue> values)
        {
            if (_model == null || values == null || values.Count == 0)
            {
                _partialImportView?.ClearPreviewTexture();
                return;
            }

            try
            {
                BeginPartialImportPreviewCapture(values, _partialImportPreviewRequestVersion);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialExpressionController] Partial import preview failed: {ex}");
                _partialImportView?.ClearPreviewTexture();
                ShowMessage("部分取り込みプレビューに失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
            }
        }

        private void BeginPartialImportPreviewCapture(IReadOnlyList<PartialImportValue> values, int requestVersion)
        {
            if (_model == null
                || values == null
                || values.Count == 0
                || _thumbnailCaptureView == null
                || _model.targetRenderer == null
                || _model.targetRenderer.sharedMesh == null)
            {
                _partialImportView?.ClearPreviewTexture();
                return;
            }

            CancelPendingPartialImportPreviewCapture();

            ExpressionEditModel previewModel = null;
            GameObject previewAvatarRootObject = null;
            try
            {
                previewModel = CreatePartialImportPreviewModel(values, out previewAvatarRootObject, out var appliedCount);
                if (previewModel == null
                    || previewAvatarRootObject == null
                    || previewModel.targetRenderer == null
                    || appliedCount == 0)
                {
                    _partialImportView?.ClearPreviewTexture();
                    return;
                }

                ApplyPartialImportPreviewModelWeights(
                    previewModel.targetRenderer,
                    previewModel,
                    GetPartialImportPreviewWeight(previewModel),
                    values);

                _pendingPartialImportPreviewCapture = new PartialImportPreviewCapture(
                    requestVersion,
                    previewModel,
                    previewAvatarRootObject);
                previewModel = null;
                previewAvatarRootObject = null;
                _partialImportPreviewCaptureReadyTime = EditorApplication.timeSinceStartup + PreviewDebounceSeconds;
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialExpressionController] Partial import preview capture failed: {ex}");
                _partialImportView?.ClearPreviewTexture();
                ShowMessage("部分取り込みプレビューに失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
            }
            finally
            {
                if (previewModel != null)
                {
                    DestroyImmediate(previewModel);
                }

                if (previewAvatarRootObject != null)
                {
                    DestroyImmediate(previewAvatarRootObject);
                }
            }
        }

        private void CompletePendingPartialImportPreviewCapture()
        {
            var capture = _pendingPartialImportPreviewCapture;
            if (capture == null)
            {
                return;
            }

            _pendingPartialImportPreviewCapture = null;

            Texture2D previewTexture = null;
            try
            {
                if (capture.RequestVersion == _partialImportPreviewRequestVersion
                    && capture.PreviewModel != null
                    && capture.PreviewModel.targetRenderer != null
                    && _thumbnailCaptureView != null)
                {
                    previewTexture = ThumbnailCaptureService.Capture(capture.PreviewModel, _thumbnailCaptureView.Settings);
                    _partialImportView?.SetPreviewTexture(previewTexture);
                    previewTexture = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialExpressionController] Partial import preview capture failed: {ex}");
                _partialImportView?.ClearPreviewTexture();
                ShowMessage("部分取り込みプレビューに失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
            }
            finally
            {
                if (previewTexture != null)
                {
                    DestroyImmediate(previewTexture);
                }

                if (capture.PreviewModel != null)
                {
                    DestroyImmediate(capture.PreviewModel);
                }

                if (capture.PreviewAvatarRootObject != null)
                {
                    DestroyImmediate(capture.PreviewAvatarRootObject);
                }
            }
        }

        private void CancelPendingPartialImportPreviewCapture()
        {
            var capture = _pendingPartialImportPreviewCapture;
            if (capture == null)
            {
                return;
            }

            _pendingPartialImportPreviewCapture = null;
            if (capture.PreviewModel != null)
            {
                DestroyImmediate(capture.PreviewModel);
            }

            if (capture.PreviewAvatarRootObject != null)
            {
                DestroyImmediate(capture.PreviewAvatarRootObject);
            }
        }

        private ExpressionEditModel CreatePartialImportPreviewModel(
            IReadOnlyList<PartialImportValue> values,
            out GameObject previewAvatarRootObject,
            out int appliedCount)
        {
            previewAvatarRootObject = null;
            appliedCount = 0;
            if (_model == null || values == null)
            {
                return null;
            }

            var previewModel = CloneModelForPartialImportPreview(_model);
            if (!TryCreateOffscreenPreviewAvatar(previewModel, out previewAvatarRootObject))
            {
                DestroyImmediate(previewModel);
                return null;
            }

            appliedCount = ApplyPartialImportValuesToModel(previewModel, values);
            return previewModel;
        }

        private bool TryCreateOffscreenPreviewAvatar(ExpressionEditModel previewModel, out GameObject previewAvatarRootObject)
        {
            previewAvatarRootObject = null;
            if (_model == null
                || _model.avatarRootObject == null
                || _model.targetRenderer == null
                || previewModel == null)
            {
                return false;
            }

            var rendererPath = MiscUtil.GetPathInHierarchy(_model.targetRenderer.gameObject, _model.avatarRootObject);
            previewAvatarRootObject = Instantiate(_model.avatarRootObject);
            previewAvatarRootObject.name = $"{_model.avatarRootObject.name} Partial Import Preview";
            previewAvatarRootObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var transform in previewAvatarRootObject.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            previewAvatarRootObject.transform.position += Vector3.one * PartialImportPreviewOffscreenOffset;

            var targetTransform = string.IsNullOrEmpty(rendererPath)
                ? previewAvatarRootObject.transform
                : previewAvatarRootObject.transform.Find(rendererPath);
            var previewRenderer = targetTransform != null
                ? targetTransform.GetComponent<SkinnedMeshRenderer>()
                : null;
            if (previewRenderer == null || previewRenderer.sharedMesh == null)
            {
                DestroyImmediate(previewAvatarRootObject);
                previewAvatarRootObject = null;
                return false;
            }

            previewModel.avatarRootObject = previewAvatarRootObject;
            previewModel.targetRenderer = previewRenderer;
            return true;
        }

        private void ApplyPartialImportPreviewModelWeights(
            SkinnedMeshRenderer renderer,
            ExpressionEditModel previewModel,
            float previewWeight,
            IReadOnlyList<PartialImportValue> importValues)
        {
            if (renderer == null || renderer.sharedMesh == null || previewModel == null)
            {
                return;
            }

            foreach (var entry in previewModel.entries)
            {
                if (!entry.ShouldOutput || !IsValidBlendShapeIndex(entry, renderer))
                {
                    continue;
                }

                renderer.SetBlendShapeWeight(entry.index, GetPreviewModelBlendShapeValue(previewModel, entry, previewWeight));
            }

            ApplyPartialImportValuesDirectlyToRenderer(renderer, previewModel, importValues);
        }

        private void ApplyPartialImportValuesDirectlyToRenderer(
            SkinnedMeshRenderer renderer,
            ExpressionEditModel previewModel,
            IReadOnlyList<PartialImportValue> importValues)
        {
            if (renderer == null || renderer.sharedMesh == null || previewModel == null || importValues == null)
            {
                return;
            }

            foreach (var importValue in importValues)
            {
                if (!TryGetPartialImportTargetEntry(previewModel, importValue, out var entry)
                    || !CanEditEntryValue(entry)
                    || !IsValidBlendShapeIndex(entry, renderer))
                {
                    continue;
                }

                renderer.SetBlendShapeWeight(entry.index, Mathf.Clamp(importValue.Value, 0f, 100f));
            }
        }

        private static float GetPreviewModelBlendShapeValue(
            ExpressionEditModel previewModel,
            BlendShapeEntry entry,
            float previewWeight)
        {
            if (previewModel != null && previewModel.frameMode == ExpressionFrameMode.WeightBlend)
            {
                return Mathf.Lerp(entry.value, entry.endValue, Mathf.Clamp01(previewWeight));
            }

            return entry.value;
        }

        private static ExpressionEditModel CloneModelForPartialImportPreview(ExpressionEditModel source)
        {
            if (source == null)
            {
                return null;
            }

            var previewModel = ExpressionEditModel.Create();
            previewModel.avatarRootObject = source.avatarRootObject;
            previewModel.targetRenderer = source.targetRenderer;
            previewModel.frameMode = source.frameMode;
            previewModel.hasSourceClip = source.hasSourceClip;
            previewModel.sourceClipName = source.sourceClipName;
            previewModel.sourceFrameRate = source.sourceFrameRate;
            previewModel.hasIntermediateKeys = source.hasIntermediateKeys;
            previewModel.entries = source.entries
                .Select(CloneEntryForPartialImportPreview)
                .ToList();
            return previewModel;
        }

        private static BlendShapeEntry CloneEntryForPartialImportPreview(BlendShapeEntry source)
        {
            if (source == null)
            {
                return null;
            }

            return new BlendShapeEntry
            {
                index = source.index,
                name = source.name,
                value = source.value,
                endValue = source.endValue,
                initialValue = source.initialValue,
                hasSourceCurve = source.hasSourceCurve,
                sourceFrameMode = source.sourceFrameMode,
                sourceValue = source.sourceValue,
                sourceEndValue = source.sourceEndValue,
                sourceCurve = ExpressionClipIO.CopyCurve(source.sourceCurve),
                systemExclusion = source.systemExclusion,
                systemExclusionUnlocked = source.systemExclusionUnlocked,
                userExcluded = source.userExcluded,
            };
        }

        private float GetPartialImportPreviewWeight(ExpressionEditModel previewModel)
        {
            if (previewModel != null && previewModel.frameMode == ExpressionFrameMode.WeightBlend)
            {
                return _editingFrame == ExpressionEditFrame.End ? 1f : 0f;
            }

            return 1f;
        }

        private sealed class PartialImportPreviewCapture
        {
            public PartialImportPreviewCapture(
                int requestVersion,
                ExpressionEditModel previewModel,
                GameObject previewAvatarRootObject)
            {
                RequestVersion = requestVersion;
                PreviewModel = previewModel;
                PreviewAvatarRootObject = previewAvatarRootObject;
            }

            public int RequestVersion { get; }
            public ExpressionEditModel PreviewModel { get; }
            public GameObject PreviewAvatarRootObject { get; }
        }

        private enum PendingThumbnailCaptureKind
        {
            Preview,
            Save,
        }

        private sealed class PendingThumbnailCapture
        {
            public PendingThumbnailCapture(
                PendingThumbnailCaptureKind kind,
                ThumbnailCaptureSettings settings,
                string filePath,
                bool showMessage,
                bool previousPreviewEnabled)
            {
                Kind = kind;
                Settings = settings;
                FilePath = filePath;
                ShowMessage = showMessage;
                PreviousPreviewEnabled = previousPreviewEnabled;
            }

            public PendingThumbnailCaptureKind Kind { get; }
            public ThumbnailCaptureSettings Settings { get; }
            public string FilePath { get; }
            public bool ShowMessage { get; }
            public bool PreviousPreviewEnabled { get; }
        }

        private void ToggleChangedOnlyFilter()
        {
            _showChangedOnly = !_showChangedOnly;
            _blendShapeListView?.SetShowChangedOnly(_showChangedOnly);
            UpdateFilterMenuText();
        }

        private void ToggleEditableOnlyFilter()
        {
            _showEditableOnly = !_showEditableOnly;
            _blendShapeListView?.SetShowEditableOnly(_showEditableOnly);
            UpdateFilterMenuText();
        }

        private DropdownMenuAction.Status GetChangedOnlyFilterStatus(DropdownMenuAction action)
        {
            return _showChangedOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        private DropdownMenuAction.Status GetEditableOnlyFilterStatus(DropdownMenuAction action)
        {
            return _showEditableOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        private void UpdateFilterMenuText()
        {
            if (_filterMenu == null)
            {
                return;
            }

            var activeFilterCount = (_showChangedOnly ? 1 : 0) + (_showEditableOnly ? 1 : 0);
            _filterMenu.text = activeFilterCount > 0 ? $"絞り込み ({activeFilterCount})" : "絞り込み";
        }

        private void Save()
        {
            if (_model == null)
            {
                ShowMessage("保存する表情がありません。", HelpBoxMessageType.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_currentAssetPath))
            {
                SaveAs();
                return;
            }

            var clip = ExpressionClipIO.ToClip(_model, _currentClip != null ? _currentClip.name : _model.sourceClipName);
            ExpressionClipIO.SaveClipToAsset(clip, _currentAssetPath);
            DestroyImmediate(clip);
            _currentClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_currentAssetPath);
            hasUnsavedChanges = false;
            ShowMessage("保存しました。", HelpBoxMessageType.Info);
        }

        private void SaveAs()
        {
            if (_model == null)
            {
                ShowMessage("保存する表情がありません。", HelpBoxMessageType.Warning);
                return;
            }

            var defaultName = _currentClip != null ? _currentClip.name : $"FacialExpression_{(_model.avatarRootObject != null ? _model.avatarRootObject.name : "Avatar")}";
            var defaultDirectory = !string.IsNullOrEmpty(_currentAssetPath) ? System.IO.Path.GetDirectoryName(_currentAssetPath) : "Assets";
            var clip = ExpressionClipIO.ToClip(_model, defaultName);
            var filePath = EditorUtility.SaveFilePanelInProject("名前を付けて保存", defaultName, "anim", "アニメーションクリップの保存先を選択してください", defaultDirectory);
            if (string.IsNullOrEmpty(filePath))
            {
                DestroyImmediate(clip);
                return;
            }

            ExpressionClipIO.SaveClipToAsset(clip, filePath);
            DestroyImmediate(clip);
            _currentAssetPath = filePath;
            _currentClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(filePath);
            _clipField.SetValueWithoutNotify(_currentClip);
            hasUnsavedChanges = false;
            ShowMessage("保存しました。", HelpBoxMessageType.Info);
        }

        private void MarkUnsaved()
        {
            hasUnsavedChanges = true;
            if (_showChangedOnly || _showEditableOnly)
            {
                _blendShapeListView?.Refresh();
            }

            UpdateButtonStates();
        }

        private bool TryDiscardUnsavedChanges()
        {
            if (!hasUnsavedChanges)
            {
                return true;
            }

            var discard = EditorUtility.DisplayDialog(
                "未保存の変更",
                "未保存の表情編集があります。変更を破棄して続行しますか？",
                "破棄して続行",
                "キャンセル");
            if (discard)
            {
                hasUnsavedChanges = false;
            }

            return discard;
        }

        private bool ValidateRendererForAvatar(SkinnedMeshRenderer renderer, GameObject avatarRootObject)
        {
            if (renderer == null)
            {
                return true;
            }

            if (avatarRootObject == null)
            {
                ShowMessage("対象 Skinned Mesh Renderer の属するアバターを検出できませんでした。", HelpBoxMessageType.Warning);
                return false;
            }

            if (!renderer.transform.IsChildOf(avatarRootObject.transform))
            {
                ShowMessage("対象 Skinned Mesh Renderer は選択中アバター配下のものを指定してください。", HelpBoxMessageType.Warning);
                return false;
            }

            return true;
        }

        private static bool CanEditEntryValue(BlendShapeEntry entry)
        {
            return entry != null && !entry.IsSystemLocked && !entry.userExcluded;
        }

        private static bool IsValidBlendShapeIndex(BlendShapeEntry entry, SkinnedMeshRenderer renderer)
        {
            return entry != null
                && renderer != null
                && renderer.sharedMesh != null
                && entry.index >= 0
                && entry.index < renderer.sharedMesh.blendShapeCount;
        }

        private void SetPreviewWeightWithoutNotify(float previewWeight)
        {
            _previewWeight = Mathf.Clamp01(previewWeight);
            _previewWeightSlider?.SetValueWithoutNotify(_previewWeight);
        }

        private static string GetFrameModeLabel(ExpressionFrameMode frameMode)
        {
            return frameMode == ExpressionFrameMode.WeightBlend ? WeightBlendModeLabel : SingleFrameModeLabel;
        }

        private static ExpressionFrameMode GetFrameModeFromLabel(string label)
        {
            return label == WeightBlendModeLabel ? ExpressionFrameMode.WeightBlend : ExpressionFrameMode.SingleFrame;
        }

        private void UpdateFrameModeControls()
        {
            var hasModel = _model != null;
            var frameMode = hasModel ? _model.frameMode : ExpressionFrameMode.SingleFrame;

            _frameModeField?.SetValueWithoutNotify(GetFrameModeLabel(frameMode));
            _frameModeField?.SetEnabled(hasModel);

            var isWeightBlend = hasModel && frameMode == ExpressionFrameMode.WeightBlend;
            _weightModeContainer?.EnableInClassList("hidden", !isWeightBlend);
            if (!isWeightBlend)
            {
                _editingFrame = ExpressionEditFrame.Start;
                SetPreviewWeightWithoutNotify(0f);
                _blendShapeListView?.SetEditingFrame(_editingFrame);
                _partialImportView?.SetEditingFrame(_editingFrame);
            }

            _startFrameButton?.SetEnabled(isWeightBlend);
            _endFrameButton?.SetEnabled(isWeightBlend);
            _loadStartFromRendererButton?.SetEnabled(isWeightBlend);
            _previewWeightSlider?.SetEnabled(isWeightBlend);
            _startFrameButton?.EnableInClassList("selected", _editingFrame == ExpressionEditFrame.Start);
            _endFrameButton?.EnableInClassList("selected", _editingFrame == ExpressionEditFrame.End);
            _previewWeightSlider?.SetValueWithoutNotify(_previewWeight);
            _partialImportView?.SetEditingFrame(_editingFrame);
        }

        private void UpdateButtonStates()
        {
            var hasModel = _model != null;
            _loadButton?.SetEnabled(_clipField != null && _clipField.value != null && hasModel);
            _saveButton?.SetEnabled(hasModel);
            _saveAsButton?.SetEnabled(hasModel);
            _resetButton?.SetEnabled(hasModel);
            _newButton?.SetEnabled(_avatarField != null && _avatarField.value != null && _rendererField != null && _rendererField.value != null);
            _previewToggle?.SetEnabled(_pendingThumbnailCapture == null);
            _frameModeField?.SetEnabled(hasModel);
            _startFrameButton?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
            _endFrameButton?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
            _loadStartFromRendererButton?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
            _previewWeightSlider?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
        }

        private void ShowMessage(string message, HelpBoxMessageType messageType)
        {
            if (_messageBox == null)
            {
                return;
            }

            _messageBox.text = message;
            _messageBox.messageType = messageType;
            _messageBox.EnableInClassList("hidden", string.IsNullOrEmpty(message));
        }

        private void RequestPreview()
        {
            _previewDirty = true;
            _lastPreviewRequestTime = EditorApplication.timeSinceStartup;
        }

        private void OnEditorUpdate()
        {
            UpdateButtonStates();

            if (_pendingPartialImportPreviewCapture != null
                && EditorApplication.timeSinceStartup >= _partialImportPreviewCaptureReadyTime)
            {
                CompletePendingPartialImportPreviewCapture();
            }

            if (_pendingThumbnailCapture != null
                && EditorApplication.timeSinceStartup >= _thumbnailCaptureReadyTime)
            {
                CompletePendingThumbnailCapture();
            }

            if (_previewDirty
                && EditorApplication.timeSinceStartup - _lastPreviewRequestTime >= PreviewDebounceSeconds)
            {
                _previewDirty = false;
                _previewService?.Sample(_model, _previewWeight);
            }

        }

        private void StopPreview()
        {
            _previewDirty = false;
            _previewService?.Stop();
        }

        private void AutoFrameThumbnail()
        {
            if (_model == null)
            {
                ShowMessage("距離・角度・オフセットを自動調整する表情がありません。", HelpBoxMessageType.Warning);
                return;
            }

            var previousPreviewEnabled = SampleThumbnailPose();
            var shouldUpdatePreview = false;
            var resultMessage = "";
            var resultMessageType = HelpBoxMessageType.Info;
            try
            {
                if (ThumbnailCaptureService.TryCalculateAutoSettings(
                    _model,
                    _thumbnailCaptureView.Settings,
                    out var calculatedSettings,
                    out var detailMessage))
                {
                    _thumbnailCaptureView.ApplySettings(calculatedSettings, true);
                    shouldUpdatePreview = true;
                    resultMessage = string.IsNullOrEmpty(detailMessage)
                        ? "距離・角度・オフセットを自動調整しました。"
                        : $"距離・角度・オフセットを自動調整しました。{detailMessage}";
                }
                else
                {
                    resultMessage = detailMessage;
                    resultMessageType = HelpBoxMessageType.Warning;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialExpressionController] Thumbnail auto framing failed: {ex}");
                resultMessage = "距離・角度・オフセットの自動調整に失敗しました。コンソールを確認してください。";
                resultMessageType = HelpBoxMessageType.Error;
            }
            finally
            {
                RestoreThumbnailPose(previousPreviewEnabled);
            }

            if (!string.IsNullOrEmpty(resultMessage))
            {
                ShowMessage(resultMessage, resultMessageType);
            }

            if (shouldUpdatePreview)
            {
                UpdateThumbnailPreview(false);
            }
        }

        private void PreviewThumbnail()
        {
            UpdateThumbnailPreview(true);
        }

        private void UpdateThumbnailPreview(bool showMessage)
        {
            if (_model == null)
            {
                if (showMessage)
                {
                    ShowMessage("プレビューする表情がありません。", HelpBoxMessageType.Warning);
                }

                return;
            }

            BeginPendingThumbnailCapture(PendingThumbnailCaptureKind.Preview, _thumbnailCaptureView.Settings, "", showMessage);
        }

        private void CaptureThumbnail()
        {
            if (_model == null)
            {
                ShowMessage("撮影する表情がありません。", HelpBoxMessageType.Warning);
                return;
            }

            var settings = _thumbnailCaptureView.Settings;
            var filePath = EditorUtility.SaveFilePanelInProject(
                "サムネイルを保存",
                GetDefaultThumbnailFileName(),
                "png",
                "サムネイル PNG の保存先とファイル名を選択してください。",
                "Assets");
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            ThumbnailCaptureService.SaveSettings(_model.avatarRootObject, settings);

            BeginPendingThumbnailCapture(PendingThumbnailCaptureKind.Save, settings, filePath, true);
        }

        private void BeginPendingThumbnailCapture(
            PendingThumbnailCaptureKind kind,
            ThumbnailCaptureSettings settings,
            string filePath,
            bool showMessage)
        {
            if (_model == null)
            {
                return;
            }

            CancelPendingThumbnailCapture();

            var previousPreviewEnabled = SampleThumbnailPose();
            _pendingThumbnailCapture = new PendingThumbnailCapture(
                kind,
                settings,
                filePath,
                showMessage,
                previousPreviewEnabled);
            _thumbnailCaptureReadyTime = EditorApplication.timeSinceStartup + PreviewDebounceSeconds;

            var busyMessage = kind == PendingThumbnailCaptureKind.Save
                ? "サムネイル撮影中..."
                : "サムネイルプレビュー更新中...";
            _thumbnailCaptureView?.SetBusy(true, busyMessage);
            EditorUtility.DisplayProgressBar("サムネイル撮影", "表情プレビューを一時適用しています...", 0.35f);
            UpdateButtonStates();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void CompletePendingThumbnailCapture()
        {
            var capture = _pendingThumbnailCapture;
            if (capture == null)
            {
                return;
            }

            _pendingThumbnailCapture = null;
            Texture2D capturedTexture = null;
            try
            {
                EditorUtility.DisplayProgressBar("サムネイル撮影", "レンダリングしています...", 0.75f);
                capturedTexture = ThumbnailCaptureService.Capture(_model, capture.Settings);

                if (capture.Kind == PendingThumbnailCaptureKind.Preview)
                {
                    _thumbnailCaptureView.SetPreviewTexture(capturedTexture);
                    capturedTexture = null;
                    if (capture.ShowMessage)
                    {
                        ShowMessage("サムネイルプレビューを更新しました。", HelpBoxMessageType.Info);
                    }
                }
                else
                {
                    EditorUtility.DisplayProgressBar("サムネイル撮影", "PNG を保存しています...", 0.95f);
                    var savedTexture = ThumbnailCaptureService.SavePngAsset(
                        capturedTexture,
                        capture.FilePath,
                        capture.Settings.backgroundMode == ThumbnailBackgroundMode.Transparent);
                    _thumbnailCaptureView.SetCapturedTexture(savedTexture, capture.FilePath);
                    ShowMessage("サムネイルを保存しました。", HelpBoxMessageType.Info);
                }
            }
            catch (Exception ex)
            {
                if (capture.Kind == PendingThumbnailCaptureKind.Preview)
                {
                    _thumbnailCaptureView.SetPreviewTexture(null);
                    if (capture.ShowMessage)
                    {
                        Debug.LogError($"[FacialExpressionController] Thumbnail preview failed: {ex}");
                        ShowMessage("サムネイルプレビューに失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
                    }
                }
                else
                {
                    Debug.LogError($"[FacialExpressionController] Thumbnail capture failed: {ex}");
                    ShowMessage("サムネイル撮影に失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
                }
            }
            finally
            {
                if (capturedTexture != null)
                {
                    DestroyImmediate(capturedTexture);
                }

                RestoreThumbnailPose(capture.PreviousPreviewEnabled);
                EndPendingThumbnailCaptureUi();
            }
        }

        private void CancelPendingThumbnailCapture()
        {
            var capture = _pendingThumbnailCapture;
            if (capture == null)
            {
                return;
            }

            _pendingThumbnailCapture = null;
            RestoreThumbnailPose(capture.PreviousPreviewEnabled);
            EndPendingThumbnailCaptureUi();
        }

        private void EndPendingThumbnailCaptureUi()
        {
            _thumbnailCaptureView?.SetBusy(false, "");
            EditorUtility.ClearProgressBar();
            UpdateButtonStates();
        }

        private string GetDefaultThumbnailFileName()
        {
            var avatarName = _model != null && _model.avatarRootObject != null
                ? _model.avatarRootObject.name
                : "Avatar";
            var fileName = $"Icon_{avatarName}";
            var clip = GetSavedEditingClip();

            if (clip != null)
            {
                fileName += $"_{clip.name}";
            }

            return SanitizeFileName(fileName);
        }

        private AnimationClip GetSavedEditingClip()
        {
            if (IsSavedAsset(_currentClip))
            {
                return _currentClip;
            }

            var selectedClip = _clipField?.value as AnimationClip;
            return IsSavedAsset(selectedClip) ? selectedClip : null;
        }

        private static bool IsSavedAsset(UnityEngine.Object asset)
        {
            return asset != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset));
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }

        private bool SampleThumbnailPose()
        {
            var previousPreviewEnabled = _previewService != null && _previewService.PreviewEnabled;
            if (_previewService != null)
            {
                _previewService.PreviewEnabled = true;
                _previewService.Sample(_model, _previewWeight);
            }

            return previousPreviewEnabled;
        }

        private void RestoreThumbnailPose(bool previousPreviewEnabled)
        {
            if (_previewService == null)
            {
                return;
            }

            var shouldEnablePreview = _previewToggle != null
                ? _previewToggle.value
                : previousPreviewEnabled;
            if (shouldEnablePreview)
            {
                _previewService.PreviewEnabled = true;
                _previewService.Sample(_model, _previewWeight);
            }
            else
            {
                _previewService.PreviewEnabled = false;
            }
        }

        private void OnUndoRedoPerformed()
        {
            _blendShapeListView?.Refresh();
            _partialImportView?.Refresh();
            UpdateFrameModeControls();
            MarkUnsaved();
            RequestPreview();
        }
    }
}
