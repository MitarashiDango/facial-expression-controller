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
        private const float SceneInfluenceFilterRadiusMeters = BlendShapeInfluenceProbeOptions.DefaultWorldRadius;
        private const float SceneInfluenceFilterMinimumDeltaMeters = BlendShapeInfluenceProbeOptions.DefaultMinimumDelta;
        private const int SceneInfluenceFilterMaxResults = BlendShapeInfluenceProbeOptions.DefaultMaxResults;
        private const string SingleFrameModeLabel = "1 フレーム";
        private const string WeightBlendModeLabel = "ウェイト連動（2 フレーム）";
        private const string OutputModeAllTargetsLabel = "全ての編集・出力対象";
        private const string OutputModeSessionBaselineDiffLabel = "セッション開始時の値との差分";
        private const string OutputModeReferenceClipDiffLabel = "参照クリップとの差分";
        private const string BulkAllEditableLabel = "すべてのブレンドシェイプを編集・出力対象にする";
        private const string BulkAllExcludedLabel = "すべてのブレンドシェイプを編集・出力対象外にする";
        private const string BulkRestoreEditingStartLabel = "編集開始時点の状態に戻す";
        private static readonly List<string> FrameModeChoices = new List<string> { SingleFrameModeLabel, WeightBlendModeLabel };
        private static readonly List<string> OutputModeChoices = new List<string>
        {
            OutputModeAllTargetsLabel,
            OutputModeSessionBaselineDiffLabel,
            OutputModeReferenceClipDiffLabel,
        };

        private ObjectField _avatarField;
        private ObjectField _rendererField;
        private ObjectField _clipField;
        private PopupField<string> _frameModeField;
        private PopupField<string> _outputModeField;
        private ObjectField _referenceClipField;
        private ToolbarSearchField _searchField;
        private ToolbarMenu _filterMenu;
        private ToolbarMenu _bulkTargetMenu;
        private ToolbarToggle _sceneInfluenceFilterToggle;
        private Button _clearSceneInfluenceFilterButton;
        private Label _sceneInfluenceFilterStatusLabel;
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
        private HelpBox _outputSettingsMessageBox;
        private VisualElement _weightModeContainer;
        private VisualElement _listContainer;
        private Label _emptyLabel;
        private Label _outputSummaryLabel;
        private Label _preservedCurveSummaryLabel;

        private BlendShapeListView _blendShapeListView;
        private ThumbnailCaptureView _thumbnailCaptureView;
        private PartialImportView _partialImportView;
        private ExpressionPreviewService _previewService;
        private BlendShapeInfluenceProbeContext _sceneInfluenceProbeContext;
        [SerializeField] private ExpressionEditModel _model;
        [SerializeField] private AnimationClip _currentClip;
        [SerializeField] private AnimationClip _referenceClip;
        [SerializeField] private string _currentAssetPath;
        [SerializeField] private ExpressionFrameMode _loadedSourceFrameMode = ExpressionFrameMode.SingleFrame;
        [SerializeField] private ExpressionEditFrame _editingFrame = ExpressionEditFrame.Start;
        [SerializeField] private BlendShapeOutputMode _outputMode = BlendShapeOutputMode.AllTargets;
        [SerializeField] private float _previewWeight;
        [SerializeField] private bool _previewEnabled = true;
        [SerializeField] private bool _hasSerializedUnsavedChanges;
        private bool _previewDirty;
        private bool _outputDecisionsDirty;
        private bool _partialImportRefreshDirty;
        private bool _showChangedOnly;
        private bool _showEditableOnly;
        private bool _showOutputOnly;
        private bool _sceneInfluenceFilterEnabled;
        private bool _sceneInfluenceMouseDownConsumed;
        private IReadOnlyDictionary<int, BlendShapeInfluenceResult> _sceneInfluenceResults;
        private BlendShapeInfluenceHit _sceneInfluenceHit;
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
            if (!TrySave())
            {
                throw new OperationCanceledException("表情編集の保存がキャンセルされました。");
            }

            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            SetUnsavedChanges(false);
            StopPreview();
        }

        private void OnEnable()
        {
            minSize = new Vector2(560f, 420f);
            saveChangesMessage = "未保存の表情編集があります。保存しますか？";
            _previewService = new ExpressionPreviewService();
            _previewService.PreviewEnabled = _previewEnabled;
            hasUnsavedChanges = _hasSerializedUnsavedChanges;
            if (_model == null)
            {
                SetUnsavedChanges(false);
            }

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            if (BuildGui())
            {
                TryInitializeFromSelection();
            }
        }

        private void OnDisable()
        {
            _previewEnabled = _previewToggle != null ? _previewToggle.value : _previewEnabled;
            _hasSerializedUnsavedChanges = hasUnsavedChanges;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.delayCall -= RetryBuildGui;
            _guiBuildRetryScheduled = false;
            SetSceneInfluenceFilterEnabled(false);
            ClearSceneInfluenceFilter(false);
            DisposeSceneInfluenceProbeContext();
            _outputDecisionsDirty = false;
            _partialImportRefreshDirty = false;
            CancelPendingPartialImportPreviewCapture();
            CancelPendingThumbnailCapture();
            StopPreview();
            _previewService?.Dispose();
            _previewService = null;
            _thumbnailCaptureView?.Dispose();
            _thumbnailCaptureView = null;
            _partialImportView?.Dispose();
            _partialImportView = null;
        }

        private void OnDestroy()
        {
            DestroyModel();
        }

        private bool BuildGui()
        {
            if (_model == null)
            {
                SetUnsavedChanges(false);
            }

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
            var outputSettingsContainer = root.Q<VisualElement>("output-settings-container");
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
            _clipField.RegisterValueChangedCallback(_ => UpdateButtonStates());
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
                value = _previewEnabled,
            };
            _previewToggle.RegisterValueChangedCallback(evt =>
            {
                _previewEnabled = evt.newValue;
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

            BuildOutputSettingsControls(outputSettingsContainer);

            var filterContainer = root.Q<VisualElement>("filter-container");
            var filterPrimaryRow = new VisualElement();
            filterPrimaryRow.AddToClassList("filter-primary-row");
            filterContainer.Add(filterPrimaryRow);

            var filterSceneRow = new VisualElement();
            filterSceneRow.AddToClassList("filter-scene-row");
            filterContainer.Add(filterSceneRow);

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("search-field");
            _searchField.RegisterValueChangedCallback(evt => _blendShapeListView?.SetSearchText(evt.newValue));
            filterPrimaryRow.Add(_searchField);

            _filterMenu = new ToolbarMenu();
            _filterMenu.AddToClassList("filter-menu");
            _filterMenu.menu.AppendAction("変更された項目のみ表示", _ => ToggleChangedOnlyFilter(), GetChangedOnlyFilterStatus);
            _filterMenu.menu.AppendAction("編集・出力対象のみ表示", _ => ToggleEditableOnlyFilter(), GetEditableOnlyFilterStatus);
            _filterMenu.menu.AppendAction("出力予定のみ表示", _ => ToggleOutputOnlyFilter(), GetOutputOnlyFilterStatus);
            UpdateFilterMenuText();
            filterPrimaryRow.Add(_filterMenu);

            _bulkTargetMenu = new ToolbarMenu
            {
                text = "一括変更",
                tooltip = "検索や表示フィルターにかかわらず、すべてのブレンドシェイプへ適用します。",
            };
            _bulkTargetMenu.AddToClassList("bulk-target-menu");
            _bulkTargetMenu.menu.AppendAction(
                BulkAllEditableLabel,
                _ => ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllEditable),
                GetBulkEditableTargetActionStatus);
            _bulkTargetMenu.menu.AppendAction(
                BulkAllExcludedLabel,
                _ => ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded),
                GetBulkEditableTargetActionStatus);
            _bulkTargetMenu.menu.AppendAction(
                BulkRestoreEditingStartLabel,
                _ => ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.RestoreEditingStart),
                GetBulkEditableTargetActionStatus);
            filterPrimaryRow.Add(_bulkTargetMenu);

            _sceneInfluenceFilterToggle = new ToolbarToggle
            {
                text = "シーンから絞り込み",
                tooltip = "Scene View でクリックした位置に影響するブレンドシェイプだけを表示します。",
            };
            _sceneInfluenceFilterToggle.AddToClassList("scene-influence-toggle");
            _sceneInfluenceFilterToggle.RegisterValueChangedCallback(evt => SetSceneInfluenceFilterEnabled(evt.newValue));
            filterSceneRow.Add(_sceneInfluenceFilterToggle);

            _clearSceneInfluenceFilterButton = new Button(() => ClearSceneInfluenceFilter(true))
            {
                text = "位置フィルタ解除",
                tooltip = "クリック位置による絞り込みを解除します。",
            };
            _clearSceneInfluenceFilterButton.AddToClassList("scene-influence-clear-button");
            filterSceneRow.Add(_clearSceneInfluenceFilterButton);

            _sceneInfluenceFilterStatusLabel = new Label();
            _sceneInfluenceFilterStatusLabel.AddToClassList("scene-influence-status");
            filterSceneRow.Add(_sceneInfluenceFilterStatusLabel);
            UpdateSceneInfluenceFilterStatus();

            _thumbnailCaptureView = new ThumbnailCaptureView(thumbnailContainer);
            _thumbnailCaptureView.AutoFrameRequested += AutoFrameThumbnail;
            _thumbnailCaptureView.PreviewRequested += PreviewThumbnail;
            _thumbnailCaptureView.CaptureRequested += CaptureThumbnail;
            _thumbnailCaptureView.MessageRequested += ShowMessage;

            _partialImportView = new PartialImportView(partialImportContainer);
            _partialImportView.ApplyRequested += ApplyPartialImportValues;
            _partialImportView.PreviewRequested += RequestPartialImportPreview;

            if (_model != null)
            {
                RestoreSerializedSessionUi();
            }
            else
            {
                UpdateButtonStates();
                UpdateFrameModeControls();
                RefreshOutputDecisionUi();
                ShowMessage("アバターを指定してください。", HelpBoxMessageType.Info);
            }

            return true;
        }

        private void BuildOutputSettingsControls(VisualElement outputSettingsContainer)
        {
            if (outputSettingsContainer == null)
            {
                return;
            }

            outputSettingsContainer.Clear();

            var outputSettingsRow = new VisualElement();
            outputSettingsRow.AddToClassList("output-settings-row");
            outputSettingsContainer.Add(outputSettingsRow);

            _outputModeField = new PopupField<string>("出力範囲", OutputModeChoices, GetOutputModeLabel(_outputMode));
            _outputModeField.AddToClassList("output-mode-field");
            _outputModeField.labelElement.style.minWidth = 160f;
            _outputModeField.labelElement.style.width = 180f;
            _outputModeField.labelElement.style.flexShrink = 0f;
            _outputModeField.RegisterValueChangedCallback(evt =>
            {
                _outputMode = GetOutputModeFromLabel(evt.newValue);
                OnOutputSettingsChanged();
            });
            outputSettingsRow.Add(_outputModeField);

            _referenceClipField = new ObjectField("参照 AnimationClip")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            _referenceClipField.AddToClassList("output-reference-field");
            _referenceClipField.labelElement.style.minWidth = 160f;
            _referenceClipField.labelElement.style.width = 180f;
            _referenceClipField.labelElement.style.flexShrink = 0f;
            _referenceClipField.RegisterValueChangedCallback(evt =>
            {
                _referenceClip = evt.newValue as AnimationClip;
                OnOutputSettingsChanged();
            });
            outputSettingsRow.Add(_referenceClipField);

            _outputSummaryLabel = new Label();
            _outputSummaryLabel.AddToClassList("output-summary");
            outputSettingsContainer.Add(_outputSummaryLabel);

            _preservedCurveSummaryLabel = new Label();
            _preservedCurveSummaryLabel.AddToClassList("preserved-summary");
            outputSettingsContainer.Add(_preservedCurveSummaryLabel);

            _outputSettingsMessageBox = new HelpBox("", HelpBoxMessageType.Info);
            _outputSettingsMessageBox.AddToClassList("output-settings-message");
            _outputSettingsMessageBox.AddToClassList("hidden");
            outputSettingsContainer.Add(_outputSettingsMessageBox);
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

        private void RestoreSerializedSessionUi()
        {
            var avatarDescriptor = _model.avatarRootObject != null
                ? _model.avatarRootObject.GetComponent<VRCAvatarDescriptor>()
                : null;
            _previewWeight = Mathf.Clamp01(_previewWeight);

            _avatarField?.SetValueWithoutNotify(avatarDescriptor);
            _rendererField?.SetValueWithoutNotify(_model.targetRenderer);
            _clipField?.SetValueWithoutNotify(_currentClip);
            _previewToggle?.SetValueWithoutNotify(_previewEnabled);
            _outputModeField?.SetValueWithoutNotify(GetOutputModeLabel(_outputMode));
            _referenceClipField?.SetValueWithoutNotify(_referenceClip);
            if (_previewService != null)
            {
                _previewService.PreviewEnabled = _previewEnabled;
            }

            RebuildBlendShapeList();
            _thumbnailCaptureView?.SetModel(_model);
            _partialImportView?.SetModel(_model);
            UpdateFrameModeControls();
            RefreshOutputDecisionUi();
            UpdateButtonStates();
            RequestPreview();
            ShowMessage(hasUnsavedChanges ? "未保存の編集を復元しました。" : "", HelpBoxMessageType.Info);
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

            SetModel(avatarRootObject, resolvedRenderer, null);

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

            SetModel(avatarRootObject, renderer, null);
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

            SetModel(avatarRootObject, renderer, null);
            _currentClip = null;
            _currentAssetPath = null;
            SetUnsavedChanges(false);
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

            SetModel(avatarRootObject, renderer, clip);
            _currentClip = clip;
            _currentAssetPath = AssetDatabase.GetAssetPath(clip);
            SetUnsavedChanges(false);

            if (_model != null && _model.hasIntermediateKeys)
            {
                ShowMessage("3 つ以上のキーを持つクリップです。未編集の項目は元カーブを保持し、編集した項目は始端と終端の直線カーブとして保存されます。", HelpBoxMessageType.Warning);
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

        private void SetModel(GameObject avatarRootObject, SkinnedMeshRenderer renderer, AnimationClip sourceClip)
        {
            CancelPendingThumbnailCapture();
            InvalidatePartialImportPreview();
            _partialImportRefreshDirty = false;
            StopPreview();
            SetSceneInfluenceFilterEnabled(false);
            ClearSceneInfluenceFilter(false);
            DisposeSceneInfluenceProbeContext();

            DestroyModel();

            if (avatarRootObject != null && renderer != null)
            {
                _model = sourceClip != null
                    ? ExpressionClipIO.Load(sourceClip, avatarRootObject, renderer)
                    : ExpressionClipIO.CreateModel(avatarRootObject, renderer);
                _loadedSourceFrameMode = _model.frameMode;
                _editingFrame = ExpressionEditFrame.Start;
                _previewWeight = 0f;
                ResetOutputSettings();
            }
            else
            {
                _loadedSourceFrameMode = ExpressionFrameMode.SingleFrame;
                _editingFrame = ExpressionEditFrame.Start;
                _previewWeight = 0f;
                ResetOutputSettings();
            }

            if (_rendererField != null && _rendererField.value != renderer)
            {
                _rendererField.SetValueWithoutNotify(renderer);
            }

            RebuildBlendShapeList();
            _thumbnailCaptureView?.SetModel(_model);
            _partialImportView?.SetModel(_model);
            UpdateFrameModeControls();
            RefreshOutputDecisionUi();

            UpdateButtonStates();
            RequestPreview();
            if (_model != null)
            {
                UpdateThumbnailPreview(false);
            }
        }

        private void DestroyModel()
        {
            if (_model == null)
            {
                return;
            }

            DestroyImmediate(_model);
            _model = null;
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
            _blendShapeListView.SetShowOutputOnly(_showOutputOnly);
            if (_sceneInfluenceResults != null)
            {
                _blendShapeListView.SetSpatialInfluenceFilter(_sceneInfluenceResults);
            }

            RefreshOutputDecisionUi();
        }

        private void OnModelChanged()
        {
            RequestPartialImportViewRefresh();
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

        private void InvalidatePartialImportPreview()
        {
            _partialImportPreviewRequestVersion++;
            CancelPendingPartialImportPreviewCapture();
            _partialImportView?.ClearPreviewTexture();
        }

        private void UpdatePartialImportPreview(IReadOnlyList<PartialImportValue> values)
        {
            if (_model == null || values == null || values.Count == 0)
            {
                CancelPendingPartialImportPreviewCapture();
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
            CancelPendingPartialImportPreviewCapture();

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

            try
            {
                _pendingPartialImportPreviewCapture = new PartialImportPreviewCapture(
                    requestVersion,
                    values);
                _partialImportPreviewCaptureReadyTime = EditorApplication.timeSinceStartup + PreviewDebounceSeconds;
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialExpressionController] Partial import preview capture failed: {ex}");
                _partialImportView?.ClearPreviewTexture();
                ShowMessage("部分取り込みプレビューに失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
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
            if (capture.RequestVersion != _partialImportPreviewRequestVersion
                || _model == null
                || _thumbnailCaptureView == null)
            {
                capture.DisposePreparedObjects();
                return;
            }

            if (!capture.IsPrepared)
            {
                PreparePendingPartialImportPreviewCapture(capture);
                return;
            }

            CapturePreparedPartialImportPreview(capture);
        }

        private void PreparePendingPartialImportPreviewCapture(PartialImportPreviewCapture capture)
        {
            ExpressionEditModel previewModel = null;
            GameObject previewCleanupRootObject = null;
            try
            {
                previewModel = CreatePartialImportPreviewModel(
                    capture.Values,
                    out previewCleanupRootObject,
                    out var appliedCount);
                if (previewModel == null
                    || previewCleanupRootObject == null
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
                    capture.Values);

                capture.SetPreparedObjects(previewModel, previewCleanupRootObject);
                previewModel = null;
                previewCleanupRootObject = null;
                _pendingPartialImportPreviewCapture = capture;
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

                if (previewCleanupRootObject != null)
                {
                    DestroyImmediate(previewCleanupRootObject);
                }
            }
        }

        private void CapturePreparedPartialImportPreview(PartialImportPreviewCapture capture)
        {
            Texture2D previewTexture = null;
            try
            {
                previewTexture = ThumbnailCaptureService.Capture(capture.PreviewModel, _thumbnailCaptureView.Settings);
                _partialImportView?.SetPreviewTexture(previewTexture);
                previewTexture = null;
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

                capture.DisposePreparedObjects();
            }
        }

        private void CancelPendingPartialImportPreviewCapture()
        {
            _pendingPartialImportPreviewCapture?.DisposePreparedObjects();
            _pendingPartialImportPreviewCapture = null;
        }

        private ExpressionEditModel CreatePartialImportPreviewModel(
            IReadOnlyList<PartialImportValue> values,
            out GameObject previewCleanupRootObject,
            out int appliedCount)
        {
            previewCleanupRootObject = null;
            appliedCount = 0;
            if (_model == null || values == null)
            {
                return null;
            }

            var previewModel = CloneModelForPartialImportPreview(_model);
            if (!TryCreateOffscreenPreviewAvatar(previewModel, out previewCleanupRootObject))
            {
                DestroyImmediate(previewModel);
                return null;
            }

            appliedCount = ApplyPartialImportValuesToModel(previewModel, values);
            return previewModel;
        }

        private bool TryCreateOffscreenPreviewAvatar(
            ExpressionEditModel previewModel,
            out GameObject previewCleanupRootObject)
        {
            previewCleanupRootObject = null;
            if (_model == null
                || _model.avatarRootObject == null
                || _model.targetRenderer == null
                || previewModel == null)
            {
                return false;
            }

            var rendererPath = MiscUtil.GetPathInHierarchy(_model.targetRenderer.gameObject, _model.avatarRootObject);
            var previewAvatarRootObject = Instantiate(_model.avatarRootObject);
            previewAvatarRootObject.name = $"{_model.avatarRootObject.name} Partial Import Preview";
            previewAvatarRootObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var transform in previewAvatarRootObject.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            try
            {
                previewCleanupRootObject = MatchPreviewAvatarTransform(
                    previewAvatarRootObject.transform,
                    _model.avatarRootObject.transform,
                    Vector3.one * PartialImportPreviewOffscreenOffset);
            }
            catch
            {
                if (previewAvatarRootObject != null)
                {
                    DestroyImmediate(previewAvatarRootObject);
                }

                throw;
            }

            var targetTransform = string.IsNullOrEmpty(rendererPath)
                ? previewAvatarRootObject.transform
                : previewAvatarRootObject.transform.Find(rendererPath);
            var previewRenderer = targetTransform != null
                ? targetTransform.GetComponent<SkinnedMeshRenderer>()
                : null;
            if (previewRenderer == null || previewRenderer.sharedMesh == null)
            {
                DestroyImmediate(previewCleanupRootObject);
                previewCleanupRootObject = null;
                return false;
            }

            previewModel.avatarRootObject = previewAvatarRootObject;
            previewModel.targetRenderer = previewRenderer;
            return true;
        }

        /// <summary>
        /// 複製元の親階層を Transform だけの一時階層として再現し、
        /// プレビュー用のワールドオフセットだけを加える。
        /// </summary>
        /// <returns>プレビュー階層をまとめて破棄するためのルート。</returns>
        internal static GameObject MatchPreviewAvatarTransform(
            Transform previewTransform,
            Transform sourceTransform,
            Vector3 worldOffset)
        {
            if (previewTransform == null)
            {
                throw new ArgumentNullException(nameof(previewTransform));
            }

            if (sourceTransform == null)
            {
                throw new ArgumentNullException(nameof(sourceTransform));
            }

            var previewParent = CreatePreviewParentChain(sourceTransform.parent, out var previewHierarchyRootObject);
            try
            {
                previewTransform.SetParent(previewParent, false);
                previewTransform.localPosition = sourceTransform.localPosition;
                previewTransform.localRotation = sourceTransform.localRotation;
                previewTransform.localScale = sourceTransform.localScale;
                previewTransform.position += worldOffset;
                return previewHierarchyRootObject != null
                    ? previewHierarchyRootObject
                    : previewTransform.gameObject;
            }
            catch
            {
                if (previewHierarchyRootObject != null)
                {
                    DestroyImmediate(previewHierarchyRootObject);
                }

                throw;
            }
        }

        private static Transform CreatePreviewParentChain(
            Transform sourceParent,
            out GameObject previewHierarchyRootObject)
        {
            previewHierarchyRootObject = null;
            if (sourceParent == null)
            {
                return null;
            }

            var sourceAncestors = new Stack<Transform>();
            for (var sourceAncestor = sourceParent; sourceAncestor != null; sourceAncestor = sourceAncestor.parent)
            {
                sourceAncestors.Push(sourceAncestor);
            }

            Transform previewParent = null;
            try
            {
                while (sourceAncestors.Count > 0)
                {
                    var sourceAncestor = sourceAncestors.Pop();
                    var previewParentObject = new GameObject($"{sourceAncestor.name} Partial Import Preview Transform")
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    previewParentObject.transform.SetParent(previewParent, false);
                    previewParentObject.transform.localPosition = sourceAncestor.localPosition;
                    previewParentObject.transform.localRotation = sourceAncestor.localRotation;
                    previewParentObject.transform.localScale = sourceAncestor.localScale;
                    previewHierarchyRootObject = previewHierarchyRootObject != null
                        ? previewHierarchyRootObject
                        : previewParentObject;
                    previewParent = previewParentObject.transform;
                }

                return previewParent;
            }
            catch
            {
                if (previewHierarchyRootObject != null)
                {
                    DestroyImmediate(previewHierarchyRootObject);
                    previewHierarchyRootObject = null;
                }

                throw;
            }
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
            previewModel.sourceStartTime = source.sourceStartTime;
            previewModel.sourceEndTime = source.sourceEndTime;
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
                IReadOnlyList<PartialImportValue> values)
            {
                RequestVersion = requestVersion;
                Values = values != null
                    ? values.ToList()
                    : new List<PartialImportValue>();
            }

            public int RequestVersion { get; }
            public IReadOnlyList<PartialImportValue> Values { get; }
            public ExpressionEditModel PreviewModel { get; private set; }
            public GameObject PreviewCleanupRootObject { get; private set; }
            public bool IsPrepared => PreviewModel != null && PreviewCleanupRootObject != null;

            public void SetPreparedObjects(ExpressionEditModel previewModel, GameObject previewCleanupRootObject)
            {
                DisposePreparedObjects();
                PreviewModel = previewModel;
                PreviewCleanupRootObject = previewCleanupRootObject;
            }

            public void DisposePreparedObjects()
            {
                if (PreviewModel != null)
                {
                    UnityEngine.Object.DestroyImmediate(PreviewModel);
                    PreviewModel = null;
                }

                if (PreviewCleanupRootObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(PreviewCleanupRootObject);
                    PreviewCleanupRootObject = null;
                }
            }
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

        private void ToggleOutputOnlyFilter()
        {
            _showOutputOnly = !_showOutputOnly;
            _blendShapeListView?.SetShowOutputOnly(_showOutputOnly);
            UpdateFilterMenuText();
        }

        private void SetSceneInfluenceFilterEnabled(bool enabled)
        {
            var canEnable = enabled && CanUseSceneInfluenceFilter();
            if (_sceneInfluenceFilterEnabled == canEnable)
            {
                _sceneInfluenceFilterToggle?.SetValueWithoutNotify(canEnable);
                return;
            }

            _sceneInfluenceFilterEnabled = canEnable;
            if (!_sceneInfluenceFilterEnabled)
            {
                _sceneInfluenceMouseDownConsumed = false;
            }

            _sceneInfluenceFilterToggle?.SetValueWithoutNotify(_sceneInfluenceFilterEnabled);
            if (_sceneInfluenceFilterEnabled)
            {
                SceneView.duringSceneGui -= OnSceneViewDuringSceneGui;
                SceneView.duringSceneGui += OnSceneViewDuringSceneGui;
            }
            else
            {
                SceneView.duringSceneGui -= OnSceneViewDuringSceneGui;
            }

            if (enabled && !canEnable)
            {
                ShowMessage("アバターと対象 Skinned Mesh Renderer を指定してください。", HelpBoxMessageType.Warning);
            }

            UpdateButtonStates();
            SceneView.RepaintAll();
        }

        private bool CanUseSceneInfluenceFilter()
        {
            return _model != null
                && _model.targetRenderer != null
                && _model.targetRenderer.sharedMesh != null
                && _model.targetRenderer.sharedMesh.blendShapeCount > 0;
        }

        private void ClearSceneInfluenceFilter(bool showMessage)
        {
            var hadFilter = _sceneInfluenceResults != null;
            _sceneInfluenceResults = null;
            _sceneInfluenceHit = null;
            _blendShapeListView?.ClearSpatialInfluenceFilter();
            UpdateSceneInfluenceFilterStatus();
            UpdateFilterMenuText();
            SceneView.RepaintAll();

            if (showMessage && hadFilter)
            {
                ShowMessage("位置フィルタを解除しました。", HelpBoxMessageType.Info);
            }
        }

        private void ApplySceneInfluenceFilter(
            BlendShapeInfluenceHit hit,
            IReadOnlyList<BlendShapeInfluenceResult> results)
        {
            _sceneInfluenceHit = hit;
            _sceneInfluenceResults = results != null
                ? results.ToDictionary(result => result.BlendShapeIndex)
                : new Dictionary<int, BlendShapeInfluenceResult>();
            _blendShapeListView?.SetSpatialInfluenceFilter(_sceneInfluenceResults);
            UpdateSceneInfluenceFilterStatus();
            UpdateFilterMenuText();
            SceneView.RepaintAll();

            var count = _sceneInfluenceResults.Count;
            if (count == 0)
            {
                ShowMessage("クリック位置に影響するブレンドシェイプは見つかりませんでした。", HelpBoxMessageType.Warning);
                return;
            }

            ShowMessage($"{count} 件のブレンドシェイプをクリック位置で絞り込みました。", HelpBoxMessageType.Info);
        }

        private void UpdateSceneInfluenceFilterStatus()
        {
            if (_sceneInfluenceFilterStatusLabel != null)
            {
                var hasFilter = _sceneInfluenceResults != null;
                var count = hasFilter ? _sceneInfluenceResults.Count : 0;
                var statusText = hasFilter ? $"位置フィルタ: {count} 件" : "";
                _sceneInfluenceFilterStatusLabel.text = statusText;
                _sceneInfluenceFilterStatusLabel.tooltip = statusText;
                _sceneInfluenceFilterStatusLabel.EnableInClassList("hidden", !hasFilter);
            }

            _clearSceneInfluenceFilterButton?.SetEnabled(_sceneInfluenceResults != null);
        }

        private void DisposeSceneInfluenceProbeContext()
        {
            _sceneInfluenceProbeContext?.Dispose();
            _sceneInfluenceProbeContext = null;
        }

        private DropdownMenuAction.Status GetChangedOnlyFilterStatus(DropdownMenuAction action)
        {
            return _showChangedOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        private DropdownMenuAction.Status GetEditableOnlyFilterStatus(DropdownMenuAction action)
        {
            return _showEditableOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        private DropdownMenuAction.Status GetOutputOnlyFilterStatus(DropdownMenuAction action)
        {
            return _showOutputOnly ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        private DropdownMenuAction.Status GetBulkEditableTargetActionStatus(DropdownMenuAction action)
        {
            return _model != null && _blendShapeListView != null
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;
        }

        private void ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction action)
        {
            if (_model == null || _blendShapeListView == null)
            {
                return;
            }

            var result = _blendShapeListView.ApplyBulkEditableTargetAction(action);
            var message = GetBulkEditableTargetResultMessage(action, result);
            ShowMessage(message, HelpBoxMessageType.Info);
        }

        private static string GetBulkEditableTargetResultMessage(
            BlendShapeBulkEditableTargetAction action,
            BlendShapeBulkEditableTargetResult result)
        {
            string message;
            if (result.ChangedCount == 0)
            {
                message = "変更対象はありませんでした。";
            }
            else
            {
                switch (action)
                {
                    case BlendShapeBulkEditableTargetAction.AllEditable:
                        message = $"{result.ChangedCount} 件を編集・出力対象にしました。";
                        break;
                    case BlendShapeBulkEditableTargetAction.AllExcluded:
                        message = $"{result.ChangedCount} 件を編集・出力対象外にしました。";
                        break;
                    case BlendShapeBulkEditableTargetAction.RestoreEditingStart:
                        message = $"{result.ChangedCount} 件を編集開始時点の状態に戻しました。";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, null);
                }
            }

            if (result.UnavailableCount > 0)
            {
                message += $" 同名のため変更できない項目: {result.UnavailableCount} 件";
            }

            return message;
        }

        private void UpdateFilterMenuText()
        {
            if (_filterMenu == null)
            {
                return;
            }

            var activeFilterCount = (_showChangedOnly ? 1 : 0)
                + (_showEditableOnly ? 1 : 0)
                + (_showOutputOnly ? 1 : 0)
                + (_sceneInfluenceResults != null ? 1 : 0);
            _filterMenu.text = activeFilterCount > 0 ? $"絞り込み ({activeFilterCount})" : "絞り込み";
        }

        private void Save()
        {
            TrySave();
        }

        private bool TrySave()
        {
            if (!TryValidateModelReferencesForSave())
            {
                return false;
            }

            if (string.IsNullOrEmpty(_currentAssetPath))
            {
                return TrySaveAs();
            }

            if (!TryCreateOutputSettingsForSave(out var outputSettings))
            {
                return false;
            }

            AnimationClip clip = null;
            AnimationClip savedClip;
            try
            {
                clip = ExpressionClipIO.ToClip(
                    _model,
                    _currentClip != null ? _currentClip.name : _model.sourceClipName,
                    outputSettings: outputSettings);
                savedClip = ExpressionClipIO.SaveClipToAsset(clip, _currentAssetPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowMessage("保存に失敗しました。Console を確認してください。", HelpBoxMessageType.Error);
                return false;
            }
            finally
            {
                DestroyTemporaryClip(clip);
            }

            _currentClip = savedClip;
            SetUnsavedChanges(false);
            ShowMessage("保存しました。", HelpBoxMessageType.Info);
            return true;
        }

        private void SaveAs()
        {
            TrySaveAs();
        }

        private bool TrySaveAs()
        {
            if (!TryValidateModelReferencesForSave())
            {
                return false;
            }

            var defaultName = _currentClip != null ? _currentClip.name : $"FacialExpression_{(_model.avatarRootObject != null ? _model.avatarRootObject.name : "Avatar")}";
            var defaultDirectory = !string.IsNullOrEmpty(_currentAssetPath) ? System.IO.Path.GetDirectoryName(_currentAssetPath) : "Assets";
            if (!TryValidateOutputSettingsForSave(out var outputSettings))
            {
                return false;
            }

            var filePath = EditorUtility.SaveFilePanelInProject("名前を付けて保存", defaultName, "anim", "アニメーションクリップの保存先を選択してください", defaultDirectory);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            if (!ConfirmDiffOutputSave(outputSettings))
            {
                return false;
            }

            AnimationClip clip = null;
            AnimationClip savedClip;
            try
            {
                clip = ExpressionClipIO.ToClip(_model, defaultName, outputSettings: outputSettings);
                savedClip = ExpressionClipIO.SaveClipToAsset(clip, filePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowMessage("保存に失敗しました。Console を確認してください。", HelpBoxMessageType.Error);
                return false;
            }
            finally
            {
                DestroyTemporaryClip(clip);
            }

            _currentAssetPath = filePath;
            _currentClip = savedClip;
            _clipField.SetValueWithoutNotify(_currentClip);
            SetUnsavedChanges(false);
            ShowMessage("保存しました。", HelpBoxMessageType.Info);
            return true;
        }

        private static void DestroyTemporaryClip(AnimationClip clip)
        {
            if (clip != null && !EditorUtility.IsPersistent(clip))
            {
                DestroyImmediate(clip);
            }
        }

        private void ResetOutputSettings()
        {
            _outputMode = BlendShapeOutputMode.AllTargets;
            _referenceClip = null;
            _outputModeField?.SetValueWithoutNotify(GetOutputModeLabel(_outputMode));
            _referenceClipField?.SetValueWithoutNotify(null);
        }

        private void OnOutputSettingsChanged()
        {
            if (_model != null)
            {
                MarkUnsaved();
                return;
            }

            RefreshOutputDecisionUi();
        }

        private ExpressionOutputSettings CreateCurrentOutputSettings()
        {
            return new ExpressionOutputSettings
            {
                mode = _outputMode,
                referenceClip = _referenceClip,
                missingReferencePolicy = MissingReferenceBlendShapePolicy.UseSessionBaseline,
                tolerance = ExpressionOutputDiffService.DefaultTolerance,
            };
        }

        private bool TryCreateOutputSettingsForSave(out ExpressionOutputSettings outputSettings)
        {
            if (!TryValidateOutputSettingsForSave(out outputSettings))
            {
                return false;
            }

            return ConfirmDiffOutputSave(outputSettings);
        }

        private bool TryValidateOutputSettingsForSave(out ExpressionOutputSettings outputSettings)
        {
            outputSettings = CreateCurrentOutputSettings();
            if (!ExpressionOutputDiffService.TryValidateSettings(outputSettings, out var message))
            {
                ShowMessage(message, HelpBoxMessageType.Warning);
                RefreshOutputDecisionUi();
                return false;
            }

            return true;
        }

        private bool ConfirmDiffOutputSave(ExpressionOutputSettings outputSettings)
        {
            if (outputSettings == null || outputSettings.mode == BlendShapeOutputMode.AllTargets)
            {
                return true;
            }

            var message = "差分なしのブレンドシェイプカーブは .anim から省略されます。\n"
                + "既存クリップへ上書き保存すると、省略対象の既存カーブも削除されます。\n"
                + "値残り防止を優先する場合は「全ての編集・出力対象」を使ってください。";
            if (outputSettings.mode == BlendShapeOutputMode.ReferenceClipDiff
                && _model != null
                && ExpressionOutputDiffService.Summarize(ExpressionOutputDiffService.Evaluate(_model, outputSettings)).intermediateKeyCount > 0)
            {
                message += "\n\n参照クリップに中間キーがあります。差分判定には始端と終端のみを使用します。";
            }

            return EditorUtility.DisplayDialog("差分出力で保存", message, "保存", "キャンセル");
        }

        private void RequestOutputDecisionUiRefresh()
        {
            _outputDecisionsDirty = true;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void RequestPartialImportViewRefresh()
        {
            if (_blendShapeListView != null && _blendShapeListView.IsGroupedUndoActive)
            {
                _partialImportRefreshDirty = true;
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            _partialImportRefreshDirty = false;
            _partialImportView?.Refresh();
        }

        private void FlushPartialImportViewRefreshIfReady()
        {
            if (!_partialImportRefreshDirty)
            {
                return;
            }

            if (_blendShapeListView != null && _blendShapeListView.IsGroupedUndoActive)
            {
                return;
            }

            _partialImportRefreshDirty = false;
            _partialImportView?.Refresh();
        }

        private void FlushOutputDecisionUiRefreshIfReady()
        {
            if (!_outputDecisionsDirty)
            {
                return;
            }

            if (_blendShapeListView != null && _blendShapeListView.IsGroupedUndoActive)
            {
                return;
            }

            RefreshOutputDecisionUi();
        }

        private void RefreshOutputDecisionUi()
        {
            _outputDecisionsDirty = false;
            var outputSettings = CreateCurrentOutputSettings();
            IReadOnlyList<BlendShapeOutputDecision> decisions = null;
            var validationMessage = "";
            var canEvaluate = _model != null && ExpressionOutputDiffService.TryValidateSettings(outputSettings, out validationMessage);
            if (canEvaluate)
            {
                decisions = ExpressionOutputDiffService.Evaluate(_model, outputSettings);
            }
            else if (_model == null)
            {
                ExpressionOutputDiffService.TryValidateSettings(outputSettings, out validationMessage);
            }

            var decisionMap = ExpressionOutputDiffService.ToDecisionMap(decisions);
            _blendShapeListView?.SetOutputDecisions(_outputMode, decisionMap);
            UpdateOutputSettingsControls(decisions, validationMessage);
            UpdateButtonStates();
        }

        private void UpdateOutputSettingsControls(
            IReadOnlyList<BlendShapeOutputDecision> decisions,
            string validationMessage)
        {
            _outputModeField?.SetValueWithoutNotify(GetOutputModeLabel(_outputMode));
            _referenceClipField?.SetValueWithoutNotify(_referenceClip);

            var hasModel = _model != null;
            _outputModeField?.SetEnabled(hasModel);
            _referenceClipField?.SetEnabled(hasModel && _outputMode == BlendShapeOutputMode.ReferenceClipDiff);

            if (_outputSummaryLabel != null)
            {
                _outputSummaryLabel.text = GetOutputSummaryText(decisions, validationMessage);
                _outputSummaryLabel.EnableInClassList("hidden", !hasModel);
            }

            if (_preservedCurveSummaryLabel != null)
            {
                _preservedCurveSummaryLabel.text = GetPreservedCurveSummaryText();
                _preservedCurveSummaryLabel.EnableInClassList("hidden", string.IsNullOrEmpty(_preservedCurveSummaryLabel.text));
            }

            UpdateOutputSettingsMessage(decisions, validationMessage);
        }

        private string GetOutputSummaryText(
            IReadOnlyList<BlendShapeOutputDecision> decisions,
            string validationMessage)
        {
            if (_model == null)
            {
                return "";
            }

            if (!string.IsNullOrEmpty(validationMessage))
            {
                return "出力予定: - / 差分なし: - / 対象外: -";
            }

            var summary = ExpressionOutputDiffService.Summarize(decisions);
            if (_outputMode == BlendShapeOutputMode.AllTargets)
            {
                return $"出力予定: {summary.outputCount} / 対象外: {summary.excludedCount}";
            }

            var text = $"出力予定: {summary.outputCount} / 差分なし: {summary.unchangedCount} / 対象外: {summary.excludedCount}";
            if (_outputMode == BlendShapeOutputMode.ReferenceClipDiff)
            {
                var referenceNotes = new List<string>();
                if (summary.missingReferenceCount > 0)
                {
                    referenceNotes.Add($"参照なし: {summary.missingReferenceCount}");
                }

                if (summary.ambiguousReferenceCount > 0)
                {
                    referenceNotes.Add($"参照曖昧: {summary.ambiguousReferenceCount}");
                }

                if (summary.intermediateKeyCount > 0)
                {
                    referenceNotes.Add($"中間キー: {summary.intermediateKeyCount}");
                }

                if (referenceNotes.Count > 0)
                {
                    text += $" / {string.Join(" / ", referenceNotes)}";
                }
            }

            return text;
        }

        private string GetPreservedCurveSummaryText()
        {
            if (_model == null)
            {
                return "";
            }

            var preservedCurveCount = _model.preservedCurves != null ? _model.preservedCurves.Count : 0;
            var preservedObjectReferenceCurveCount = _model.preservedObjectReferenceCurves != null ? _model.preservedObjectReferenceCurves.Count : 0;
            if (preservedCurveCount == 0 && preservedObjectReferenceCurveCount == 0)
            {
                return "";
            }

            return $"保全カーブ: {preservedCurveCount} / ObjectReference: {preservedObjectReferenceCurveCount}";
        }

        private void UpdateOutputSettingsMessage(
            IReadOnlyList<BlendShapeOutputDecision> decisions,
            string validationMessage)
        {
            if (_outputSettingsMessageBox == null)
            {
                return;
            }

            if (_model == null)
            {
                _outputSettingsMessageBox.text = "";
                _outputSettingsMessageBox.EnableInClassList("hidden", true);
                return;
            }

            var message = "";
            var messageType = HelpBoxMessageType.Info;
            if (!string.IsNullOrEmpty(validationMessage))
            {
                message = validationMessage;
                messageType = HelpBoxMessageType.Warning;
            }
            else if (_outputMode == BlendShapeOutputMode.ReferenceClipDiff
                && ExpressionOutputDiffService.Summarize(decisions).intermediateKeyCount > 0)
            {
                message = "参照クリップに中間キーがあります。差分判定には始端と終端のみを使用します。";
                messageType = HelpBoxMessageType.Warning;
            }
            else if (_outputMode != BlendShapeOutputMode.AllTargets)
            {
                message = "差分なしのブレンドシェイプカーブは保存時に省略されます。";
                messageType = HelpBoxMessageType.Info;
            }

            _outputSettingsMessageBox.text = message;
            _outputSettingsMessageBox.messageType = messageType;
            _outputSettingsMessageBox.EnableInClassList("hidden", string.IsNullOrEmpty(message));
        }

        private void MarkUnsaved()
        {
            SetUnsavedChanges(true);
            RequestOutputDecisionUiRefresh();
        }

        private bool TryValidateModelReferencesForSave()
        {
            if (ExpressionClipIO.TryValidateModelReferences(_model, out var message))
            {
                return true;
            }

            ShowMessage(message, HelpBoxMessageType.Warning);
            return false;
        }

        private void SetUnsavedChanges(bool value)
        {
            hasUnsavedChanges = value;
            _hasSerializedUnsavedChanges = value;
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
                SetUnsavedChanges(false);
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

        private static string GetOutputModeLabel(BlendShapeOutputMode outputMode)
        {
            switch (outputMode)
            {
                case BlendShapeOutputMode.SessionBaselineDiff:
                    return OutputModeSessionBaselineDiffLabel;
                case BlendShapeOutputMode.ReferenceClipDiff:
                    return OutputModeReferenceClipDiffLabel;
                default:
                    return OutputModeAllTargetsLabel;
            }
        }

        private static BlendShapeOutputMode GetOutputModeFromLabel(string label)
        {
            if (label == OutputModeSessionBaselineDiffLabel)
            {
                return BlendShapeOutputMode.SessionBaselineDiff;
            }

            if (label == OutputModeReferenceClipDiffLabel)
            {
                return BlendShapeOutputMode.ReferenceClipDiff;
            }

            return BlendShapeOutputMode.AllTargets;
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
            var canSaveOutput = hasModel
                && (_outputMode != BlendShapeOutputMode.ReferenceClipDiff || _referenceClip != null);
            _loadButton?.SetEnabled(_clipField != null && _clipField.value != null && hasModel);
            _saveButton?.SetEnabled(canSaveOutput);
            _saveAsButton?.SetEnabled(canSaveOutput);
            _resetButton?.SetEnabled(hasModel);
            _newButton?.SetEnabled(_avatarField != null && _avatarField.value != null && _rendererField != null && _rendererField.value != null);
            _previewToggle?.SetEnabled(_pendingThumbnailCapture == null);
            _frameModeField?.SetEnabled(hasModel);
            _outputModeField?.SetEnabled(hasModel);
            _referenceClipField?.SetEnabled(hasModel && _outputMode == BlendShapeOutputMode.ReferenceClipDiff);
            _bulkTargetMenu?.SetEnabled(hasModel && _blendShapeListView != null);
            _sceneInfluenceFilterToggle?.SetEnabled(CanUseSceneInfluenceFilter());
            _clearSceneInfluenceFilterButton?.SetEnabled(_sceneInfluenceResults != null);
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
            FlushPartialImportViewRefreshIfReady();
            FlushOutputDecisionUiRefreshIfReady();

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

        private void OnBeforeAssemblyReload()
        {
            SetSceneInfluenceFilterEnabled(false);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                SetSceneInfluenceFilterEnabled(false);
            }
        }

        private void OnSceneViewDuringSceneGui(SceneView sceneView)
        {
            if (!_sceneInfluenceFilterEnabled)
            {
                return;
            }

            var currentEvent = Event.current;
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                _sceneInfluenceMouseDownConsumed = false;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && _sceneInfluenceMouseDownConsumed)
            {
                _sceneInfluenceMouseDownConsumed = false;
                currentEvent.Use();
                return;
            }

            DrawSceneInfluenceFilterMarker(sceneView);

            if (currentEvent.type != EventType.MouseDown
                || currentEvent.button != 0
                || currentEvent.alt)
            {
                return;
            }

            if (!CanUseSceneInfluenceFilter())
            {
                SetSceneInfluenceFilterEnabled(false);
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            try
            {
                _sceneInfluenceProbeContext ??= new BlendShapeInfluenceProbeContext();
                var options = new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = SceneInfluenceFilterRadiusMeters,
                    minimumDelta = SceneInfluenceFilterMinimumDeltaMeters,
                    maxResults = SceneInfluenceFilterMaxResults,
                };
                if (!BlendShapeInfluenceProbe.TryProbe(
                    _model.targetRenderer,
                    ray,
                    options,
                    _sceneInfluenceProbeContext,
                    out var hit,
                    out var results))
                {
                    return;
                }

                ApplySceneInfluenceFilter(hit, results);
                _sceneInfluenceMouseDownConsumed = true;
                currentEvent.Use();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FacialExpressionController] Blend shape influence filter failed: {ex}");
                ShowMessage("位置フィルタの計算に失敗しました。コンソールを確認してください。", HelpBoxMessageType.Error);
                currentEvent.Use();
            }
        }

        private void DrawSceneInfluenceFilterMarker(SceneView sceneView)
        {
            Handles.BeginGUI();
            EditorGUI.HelpBox(new Rect(8f, 8f, 180f, 22f), "顔メッシュをクリック", MessageType.Info);
            Handles.EndGUI();

            if (_sceneInfluenceHit == null)
            {
                return;
            }

            var normal = _sceneInfluenceHit.WorldNormal.sqrMagnitude > 0.000001f
                ? _sceneInfluenceHit.WorldNormal.normalized
                : Vector3.up;
            if (sceneView != null && sceneView.camera != null && Vector3.Dot(normal, sceneView.camera.transform.forward) > 0f)
            {
                normal = -normal;
            }

            var markerSize = HandleUtility.GetHandleSize(_sceneInfluenceHit.WorldPosition) * 0.04f;
            var previousColor = Handles.color;
            Handles.color = new Color(0.25f, 0.75f, 1f, 0.9f);
            Handles.SphereHandleCap(0, _sceneInfluenceHit.WorldPosition, Quaternion.identity, markerSize, EventType.Repaint);
            Handles.DrawWireDisc(_sceneInfluenceHit.WorldPosition, normal, SceneInfluenceFilterRadiusMeters);
            Handles.color = previousColor;
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

            ThumbnailCaptureService.SaveSettings(_model, settings);

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
