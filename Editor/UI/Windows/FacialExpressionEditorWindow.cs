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
        internal const string ClipFieldLabel = "編集中のクリップ";
        internal const string UnsavedLabelSuffix = "*";
        internal const string UnsavedChangesTooltip = "未保存の変更があります";
        internal const string ChangedOnlyFilterLabel = "変更あり";
        internal const string EditableOnlyFilterLabel = "対象のみ";
        internal const string OutputOnlyFilterLabel = "出力予定";
        internal const string ChangedOnlyFilterTooltip = "変更された項目のみ表示";
        internal const string EditableOnlyFilterTooltip = "編集・出力対象のみ表示";
        internal const string OutputOnlyFilterTooltip = "出力予定のみ表示";
        internal const string SceneInfluenceFilterTooltip =
            "シーンから絞り込み: Scene View でクリックした位置に影響するブレンドシェイプだけを表示します。";
        internal const string ClearSceneInfluenceFilterTooltip = "クリック位置による絞り込みを解除します。";
        internal const string FilterCountTooltip = "表示中の件数 / 全件数";
        internal const string EmptyListText = "表示できるブレンドシェイプがありません。";
        internal const string FilteredEmptyListText = "絞り込みの条件に一致するブレンドシェイプがありません。";
        internal const string ClearFiltersLabel = "絞り込みを解除";
        internal const string ClearFiltersTooltip = "検索文字・表示フィルター・クリック位置による絞り込みをすべて解除します。";
        internal const string WeightModeNoteText =
            "Fist はトリガー量にリアルタイム連動します。他ジェスチャーや表情固定中・メニュー選択中は、既存メニューの「表情固定時の表情ウェイト」で調整されます。";
        internal const string DiffOutputNoteText = "差分なしのブレンドシェイプカーブは保存時に省略されます。";
        internal const string IntermediateKeysWarningText =
            "3 つ以上のキーを持つクリップです。未編集の項目は元カーブを保持し、編集した項目は始端と終端の直線カーブとして保存されます。";
        internal const string IntermediateKeysSingleFrameWarningText =
            "3 つ以上のキーを持つクリップです。1 フレームで保存すると、未編集の項目も元カーブを保持せず 1 キーのカーブとして保存されます。";
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
        private Button _changedOnlyFilterChip;
        private Button _editableOnlyFilterChip;
        private Button _outputOnlyFilterChip;
        private Button _sceneInfluenceFilterButton;
        private ExpressionEditorIconElement _sceneInfluenceFilterIcon;
        private VisualElement _sceneInfluenceFilterChip;
        private Label _sceneInfluenceFilterChipLabel;
        private Button _sceneInfluenceFilterClearButton;
        private ExpressionEditorIconElement _sceneInfluenceFilterClearIcon;
        private Label _filterCountLabel;
        private Button _clearFiltersButton;
        private ExpressionEditorIconElement _frameModeInfoIcon;
        private ExpressionEditorIconElement _outputModeInfoIcon;
        private Toggle _previewToggle;
        private Button _newButton;
        private Button _saveButton;
        private Button _saveAsButton;
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
        private bool? _appliedProSkin;
        private double _lastPreviewRequestTime;
        private int _partialImportPreviewRequestVersion;
        private double _partialImportPreviewCaptureReadyTime;
        private PartialImportPreviewCapture _pendingPartialImportPreviewCapture;
        private double _thumbnailCaptureReadyTime;
        private PendingThumbnailCapture _pendingThumbnailCapture;

        /// <summary>
        /// 未保存の変更を破棄して続行してよいかを確認する処理。既定では確認ダイアログを表示する。
        /// テストでダイアログを表示せずに確認結果を差し替えるためのもので、通常の処理からは変更しない。
        /// </summary>
        internal Func<bool> DiscardUnsavedChangesConfirmation { get; set; } = ShowDiscardUnsavedChangesDialog;

        /// <summary>
        /// 成功・完了の通知を表示する処理。null の場合は <see cref="EditorWindow.ShowNotification(GUIContent)"/> で表示する。
        /// テストで通知の内容を確認するためのもので、通常の処理からは変更しない。
        /// </summary>
        internal Action<string> NotificationPresenter { get; set; }

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
            UpdateSkinClass();

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

            _clipField = new ObjectField(ClipFieldLabel)
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            ConfigureTargetField(_clipField);
            _clipField.RegisterValueChangedCallback(evt => OnClipFieldChanged(evt.newValue as AnimationClip));
            targetContainer.Add(_clipField);
            UpdateUnsavedIndicator();

            _newButton = new Button(CreateNewSession) { text = "新規" };
            _saveButton = new Button(Save) { text = "保存" };
            _saveAsButton = new Button(SaveAs) { text = "別名保存" };
            toolbarContainer.Add(_newButton);
            toolbarContainer.Add(_saveButton);
            toolbarContainer.Add(_saveAsButton);

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

            _frameModeInfoIcon = CreateInfoIcon(WeightModeNoteText);
            _frameModeInfoIcon.AddToClassList("frame-mode-info-icon");
            frameModeContainer.Add(_frameModeInfoIcon);

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

            BuildFilterControls(root.Q<VisualElement>("filter-container"));
            BuildEmptyStateControls(root.Q<VisualElement>("empty-state"));
            RefreshIcons();
            UpdateFilterBar();

            _thumbnailCaptureView = new ThumbnailCaptureView(thumbnailContainer);
            _thumbnailCaptureView.AutoFrameRequested += AutoFrameThumbnail;
            _thumbnailCaptureView.PreviewRequested += PreviewThumbnail;
            _thumbnailCaptureView.CaptureRequested += CaptureThumbnail;
            _thumbnailCaptureView.MessageRequested += OnThumbnailCaptureMessageRequested;

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

            _outputModeInfoIcon = CreateInfoIcon(DiffOutputNoteText);
            _outputModeInfoIcon.AddToClassList("output-mode-info-icon");
            outputSettingsRow.Add(_outputModeInfoIcon);

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

        /// <summary>
        /// 補足説明を tooltip で示す ⓘ アイコンを作る。初期状態は非表示。
        /// </summary>
        private static ExpressionEditorIconElement CreateInfoIcon(string tooltip)
        {
            var icon = new ExpressionEditorIconElement
            {
                tooltip = tooltip,
            };
            icon.AddToClassList("info-icon");
            icon.AddToClassList(ExpressionEditorStyles.HiddenClassName);
            icon.SetIcon(ExpressionEditorIcons.Info);
            return icon;
        }

        /// <summary>
        /// 絞り込み行を作る。検索欄・表示フィルターのチップ・シーンから絞り込むボタン・位置フィルタのチップを
        /// 1 行に並べ（幅が足りないときは折り返す）、右端に表示数を置く。
        /// </summary>
        private void BuildFilterControls(VisualElement filterContainer)
        {
            if (filterContainer == null)
            {
                return;
            }

            filterContainer.Clear();

            var filterItems = new VisualElement();
            filterItems.AddToClassList("filter-items");
            filterContainer.Add(filterItems);

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("search-field");
            _searchField.RegisterValueChangedCallback(evt => _blendShapeListView?.SetSearchText(evt.newValue));
            filterItems.Add(_searchField);

            _changedOnlyFilterChip = CreateFilterChip(ChangedOnlyFilterLabel, ChangedOnlyFilterTooltip, ToggleChangedOnlyFilter);
            filterItems.Add(_changedOnlyFilterChip);
            _editableOnlyFilterChip = CreateFilterChip(EditableOnlyFilterLabel, EditableOnlyFilterTooltip, ToggleEditableOnlyFilter);
            filterItems.Add(_editableOnlyFilterChip);
            _outputOnlyFilterChip = CreateFilterChip(OutputOnlyFilterLabel, OutputOnlyFilterTooltip, ToggleOutputOnlyFilter);
            filterItems.Add(_outputOnlyFilterChip);

            _sceneInfluenceFilterButton = new Button(ToggleSceneInfluenceFilterEnabled)
            {
                tooltip = SceneInfluenceFilterTooltip,
            };
            _sceneInfluenceFilterButton.AddToClassList("filter-icon-button");
            _sceneInfluenceFilterIcon = new ExpressionEditorIconElement();
            _sceneInfluenceFilterIcon.SetPickingModeIgnore();
            _sceneInfluenceFilterButton.Add(_sceneInfluenceFilterIcon);
            filterItems.Add(_sceneInfluenceFilterButton);

            _sceneInfluenceFilterChip = new VisualElement();
            _sceneInfluenceFilterChip.AddToClassList("filter-chip");
            _sceneInfluenceFilterChip.AddToClassList("filter-location-chip");
            _sceneInfluenceFilterChip.AddToClassList(ExpressionEditorStyles.SelectedClassName);
            _sceneInfluenceFilterChip.AddToClassList(ExpressionEditorStyles.HiddenClassName);
            filterItems.Add(_sceneInfluenceFilterChip);

            _sceneInfluenceFilterChipLabel = new Label();
            _sceneInfluenceFilterChipLabel.AddToClassList("filter-location-chip__label");
            _sceneInfluenceFilterChip.Add(_sceneInfluenceFilterChipLabel);

            _sceneInfluenceFilterClearButton = new Button(() => ClearSceneInfluenceFilter(true))
            {
                tooltip = ClearSceneInfluenceFilterTooltip,
            };
            _sceneInfluenceFilterClearButton.AddToClassList("filter-chip-clear-button");
            _sceneInfluenceFilterClearIcon = new ExpressionEditorIconElement();
            _sceneInfluenceFilterClearIcon.SetPickingModeIgnore();
            _sceneInfluenceFilterClearButton.Add(_sceneInfluenceFilterClearIcon);
            _sceneInfluenceFilterChip.Add(_sceneInfluenceFilterClearButton);

            _filterCountLabel = new Label
            {
                tooltip = FilterCountTooltip,
            };
            _filterCountLabel.AddToClassList("filter-count");
            filterContainer.Add(_filterCountLabel);
        }

        private static Button CreateFilterChip(string text, string tooltip, Action clicked)
        {
            var chip = new Button(clicked)
            {
                text = text,
                tooltip = tooltip,
            };
            chip.AddToClassList("filter-chip");
            chip.AddToClassList("filter-toggle-chip");
            return chip;
        }

        /// <summary>
        /// 0 件表示に「絞り込みを解除」ボタンを加える。ボタンは絞り込みが原因で 0 件のときだけ表示する。
        /// </summary>
        private void BuildEmptyStateControls(VisualElement emptyState)
        {
            _clearFiltersButton = new Button(ClearAllFilters)
            {
                text = ClearFiltersLabel,
                tooltip = ClearFiltersTooltip,
            };
            _clearFiltersButton.AddToClassList("clear-filters-button");
            _clearFiltersButton.AddToClassList(ExpressionEditorStyles.HiddenClassName);
            (emptyState ?? _emptyLabel?.parent)?.Add(_clearFiltersButton);
        }

        /// <summary>
        /// ウィンドウ上の組み込みアイコンを、現在のエディタースキンのものにする。
        /// </summary>
        private void RefreshIcons()
        {
            _sceneInfluenceFilterIcon?.SetIcon(ExpressionEditorIcons.ScenePick);
            _sceneInfluenceFilterClearIcon?.SetIcon(ExpressionEditorIcons.Clear);
            _frameModeInfoIcon?.SetIcon(ExpressionEditorIcons.Info);
            _outputModeInfoIcon?.SetIcon(ExpressionEditorIcons.Info);
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

            // 未保存の編集はクリップ欄のラベルとタブ名の * で示しているため、復元したことは通知しない。
            ShowMessage("", HelpBoxMessageType.Info);
        }

        /// <summary>
        /// 編集対象のアバターを設定する。アバターが変わる場合は新規セッションとして開き直し、編集中のクリップを外す。
        /// 編集中のアバターを再指定した場合は、現在のセッションをそのまま維持する。
        /// ただし対象 Skinned Mesh Renderer が削除されている場合は、Renderer を解決し直して開き直す。
        /// </summary>
        private void SetAvatar(VRCAvatarDescriptor descriptor)
        {
            var currentDescriptor = _model != null && _model.avatarRootObject != null
                ? _model.avatarRootObject.GetComponent<VRCAvatarDescriptor>()
                : null;
            if (_model != null
                && descriptor != null
                && currentDescriptor == descriptor
                && _model.targetRenderer != null)
            {
                _avatarField?.SetValueWithoutNotify(descriptor);
                return;
            }

            if (!TryDiscardUnsavedChanges())
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

            SetModel(avatarRootObject, resolvedRenderer);

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

        /// <summary>
        /// 編集対象の Skinned Mesh Renderer を設定する。Renderer が変わる場合は新規セッションとして開き直し、編集中のクリップを外す。
        /// 編集中の Renderer を再指定した場合は、現在のセッションをそのまま維持する。
        /// </summary>
        private void SetRenderer(SkinnedMeshRenderer renderer)
        {
            var avatarDescriptor = _avatarField != null ? _avatarField.value as VRCAvatarDescriptor : null;
            var rendererAvatarRootObject = renderer != null ? MiscUtil.GetAvatarRoot(renderer.transform) : null;
            var avatarRootObject = avatarDescriptor != null ? avatarDescriptor.gameObject : rendererAvatarRootObject;
            if (_model != null
                && renderer != null
                && _model.targetRenderer == renderer
                && _model.avatarRootObject == avatarRootObject)
            {
                _rendererField?.SetValueWithoutNotify(renderer);
                return;
            }

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

            SetModel(avatarRootObject, renderer);
            if (avatarRootObject != null && renderer != null)
            {
                ShowMessage("", HelpBoxMessageType.Info);
            }
        }

        private void CreateNewSession()
        {
            TryCreateNewSession();
        }

        /// <summary>
        /// 現在のアバターと Skinned Mesh Renderer で新規セッションを開始する。
        /// </summary>
        /// <returns>開始した場合は true。未保存の変更の確認がキャンセルされた場合は false。</returns>
        private bool TryCreateNewSession()
        {
            if (!TryDiscardUnsavedChanges())
            {
                return false;
            }

            var avatarDescriptor = _avatarField.value as VRCAvatarDescriptor;
            var renderer = _rendererField.value as SkinnedMeshRenderer;
            var avatarRootObject = avatarDescriptor != null ? avatarDescriptor.gameObject : null;

            SetModel(avatarRootObject, renderer);
            SetUnsavedChanges(false);
            ShowSuccess("新規セッションを開始しました。");
            return true;
        }

        /// <summary>
        /// クリップ欄の変更を受けて、指定されたクリップを読み込む。欄を空にした場合は新規セッションを開始する。
        /// 実行しなかった場合は、欄を編集中のクリップへ戻す。
        /// </summary>
        private void OnClipFieldChanged(AnimationClip clip)
        {
            var applied = clip != null
                ? TryLoadClip(clip)
                : TryCreateNewSession();
            if (!applied)
            {
                _clipField?.SetValueWithoutNotify(_currentClip);
            }
        }

        /// <summary>
        /// 指定した AnimationClip を読み込み、編集中のクリップにする。
        /// クリップにないブレンドシェイプはプレビュー適用前の Skinned Mesh Renderer の値で読み込むため、先にプレビューを止める。
        /// 読み込みに失敗した場合や確認がキャンセルされた場合は、現在のセッションをそのまま残してプレビューを再開する。
        /// </summary>
        /// <returns>読み込んだ場合は true。対象が未設定、確認がキャンセルされた、または読み込みに失敗した場合は false。</returns>
        private bool TryLoadClip(AnimationClip clip)
        {
            if (clip == null)
            {
                ShowMessage("読み込む AnimationClip を指定してください。", HelpBoxMessageType.Warning);
                return false;
            }

            var avatarDescriptor = _avatarField.value as VRCAvatarDescriptor;
            var renderer = _rendererField.value as SkinnedMeshRenderer;
            var avatarRootObject = avatarDescriptor != null ? avatarDescriptor.gameObject : null;
            if (avatarRootObject == null || renderer == null)
            {
                ShowMessage("アバターと対象 Skinned Mesh Renderer を指定してください。", HelpBoxMessageType.Warning);
                return false;
            }

            StopPreview();

            ExpressionEditModel loadedModel;
            try
            {
                loadedModel = ExpressionClipIO.Load(clip, avatarRootObject, renderer);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                RequestPreview();
                ShowMessage("クリップの読み込みに失敗しました。Console を確認してください。", HelpBoxMessageType.Error);
                return false;
            }

            if (!TryDiscardUnsavedChanges())
            {
                DestroyImmediate(loadedModel);
                RequestPreview();
                return false;
            }

            SetModel(avatarRootObject, renderer, loadedModel, clip);
            SetUnsavedChanges(false);

            // 3 つ以上のキーを持つクリップの警告は、セッション中ずっと当てはまるため出力設定の HelpBox に出す
            // （UpdateOutputSettingsMessage）。ここでは読み込めたことだけを通知する。
            if (_loadedSourceFrameMode == ExpressionFrameMode.WeightBlend)
            {
                ShowSuccess("2 フレームのクリップをウェイト連動モードで読み込みました。");
            }
            else
            {
                ShowSuccess("クリップを読み込みました。");
            }

            return true;
        }

        /// <summary>
        /// 編集モデルを差し替える。<paramref name="loadedModel"/> を渡した場合はそれを使い、
        /// 編集中のクリップとクリップ欄を <paramref name="loadedClip"/> に合わせる。
        /// 渡さない場合は新規モデルを作成し、編集中のクリップとクリップ欄を空にする。
        /// </summary>
        private void SetModel(
            GameObject avatarRootObject,
            SkinnedMeshRenderer renderer,
            ExpressionEditModel loadedModel = null,
            AnimationClip loadedClip = null)
        {
            CancelPendingThumbnailCapture();
            InvalidatePartialImportPreview();
            _partialImportRefreshDirty = false;
            StopPreview();
            SetSceneInfluenceFilterEnabled(false);
            ClearSceneInfluenceFilter(false);
            DisposeSceneInfluenceProbeContext();

            DestroyModel();

            if (loadedModel != null || (avatarRootObject != null && renderer != null))
            {
                _model = loadedModel != null
                    ? loadedModel
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

            SetCurrentClip(loadedModel != null ? loadedClip : null);

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

        /// <summary>
        /// 編集中のクリップを設定し、クリップ欄の表示を合わせる。
        /// </summary>
        private void SetCurrentClip(AnimationClip clip)
        {
            _currentClip = clip;
            _clipField?.SetValueWithoutNotify(clip);
        }

        /// <summary>
        /// 上書き保存先のパスを、編集中のクリップの現在の場所から求める。
        /// 編集中のクリップが書き込み可能な単独の .anim アセットでない場合
        /// （サブアセット、FBX 内のクリップ、読み取り専用パッケージ内、削除済み、未保存）は空文字を返す。
        /// </summary>
        private string GetOverwriteSaveAssetPath()
        {
            if (_currentClip == null || !AssetDatabase.IsMainAsset(_currentClip))
            {
                return "";
            }

            var assetPath = AssetDatabase.GetAssetPath(_currentClip);
            return string.Equals(Path.GetExtension(assetPath), ".anim", StringComparison.OrdinalIgnoreCase)
                && IsWritableAssetPath(assetPath)
                ? assetPath
                : "";
        }

        /// <summary>
        /// 指定したアセットパスへ書き込めるかを判定する。
        /// Assets 配下と、Embedded / Local のパッケージ内は書き込み可能とし、
        /// それ以外のパッケージ（レジストリ・Git など Library/PackageCache 由来）内は書き込めないものとして扱う。
        /// </summary>
        internal static bool IsWritableAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            return packageInfo == null
                || packageInfo.source == UnityEditor.PackageManager.PackageSource.Embedded
                || packageInfo.source == UnityEditor.PackageManager.PackageSource.Local;
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
                UpdateFilterBar();
                return;
            }

            _blendShapeListView = new BlendShapeListView(_model, _listContainer, _emptyLabel);
            _blendShapeListView.Changed += OnModelChanged;
            _blendShapeListView.PreviewValueChanged += OnBlendShapePreviewValueChanged;
            _blendShapeListView.PreviewResetRequested += OnBlendShapePreviewResetRequested;
            _blendShapeListView.BulkEditableTargetActionApplied += OnBulkEditableTargetActionApplied;
            _blendShapeListView.VisibleEntriesChanged += UpdateListCountAndEmptyState;
            _blendShapeListView.SetEditingFrame(_editingFrame);
            _blendShapeListView.SetFilters(
                _searchField != null ? _searchField.value : "",
                _showChangedOnly,
                _showEditableOnly,
                _showOutputOnly,
                _sceneInfluenceResults);

            RefreshOutputDecisionUi();
            UpdateFilterBar();
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
            ShowSuccess("始点へ現在の Skinned Mesh Renderer 値を読み込みました。");
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
            ShowSuccess($"{appliedCount} 件のブレンドシェイプを取り込みました。");
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
            UpdateFilterBar();
        }

        private void ToggleEditableOnlyFilter()
        {
            _showEditableOnly = !_showEditableOnly;
            _blendShapeListView?.SetShowEditableOnly(_showEditableOnly);
            UpdateFilterBar();
        }

        private void ToggleOutputOnlyFilter()
        {
            _showOutputOnly = !_showOutputOnly;
            _blendShapeListView?.SetShowOutputOnly(_showOutputOnly);
            UpdateFilterBar();
        }

        private void ToggleSceneInfluenceFilterEnabled()
        {
            SetSceneInfluenceFilterEnabled(!_sceneInfluenceFilterEnabled);
        }

        private void SetSceneInfluenceFilterEnabled(bool enabled)
        {
            var canEnable = enabled && CanUseSceneInfluenceFilter();
            if (_sceneInfluenceFilterEnabled == canEnable)
            {
                UpdateFilterBar();
                return;
            }

            _sceneInfluenceFilterEnabled = canEnable;
            if (!_sceneInfluenceFilterEnabled)
            {
                _sceneInfluenceMouseDownConsumed = false;
            }

            UpdateFilterBar();
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
            UpdateFilterBar();
            SceneView.RepaintAll();

            if (showMessage && hadFilter)
            {
                ShowSuccess("位置フィルタを解除しました。");
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
            UpdateFilterBar();
            SceneView.RepaintAll();

            var count = _sceneInfluenceResults.Count;
            if (count == 0)
            {
                ShowMessage("クリック位置に影響するブレンドシェイプは見つかりませんでした。", HelpBoxMessageType.Warning);
                return;
            }

            ShowSuccess($"{count} 件のブレンドシェイプをクリック位置で絞り込みました。");
        }

        private void DisposeSceneInfluenceProbeContext()
        {
            _sceneInfluenceProbeContext?.Dispose();
            _sceneInfluenceProbeContext = null;
        }

        /// <summary>
        /// 検索文字・表示フィルターのチップ・位置フィルタをすべて解除する。0 件表示の「絞り込みを解除」ボタンから呼ぶ。
        /// シーンから絞り込むモード自体（クリック待ち）は切り替えない。
        /// </summary>
        private void ClearAllFilters()
        {
            _searchField?.SetValueWithoutNotify("");
            _showChangedOnly = false;
            _showEditableOnly = false;
            _showOutputOnly = false;

            // 一覧の作り直しは 1 回にする。位置フィルタは一覧側で先に外すため、下の ClearSceneInfluenceFilter では作り直さない。
            _blendShapeListView?.SetFilters("", false, false, false, null);
            ClearSceneInfluenceFilter(false);
            UpdateFilterBar();
        }

        /// <summary>
        /// 絞り込み行の表示（チップのオン／オフ、シーンから絞り込むボタン、位置フィルタのチップ）と、
        /// 表示数・0 件時の表示を現在の状態に合わせる。
        /// </summary>
        private void UpdateFilterBar()
        {
            _changedOnlyFilterChip?.EnableInClassList(ExpressionEditorStyles.SelectedClassName, _showChangedOnly);
            _editableOnlyFilterChip?.EnableInClassList(ExpressionEditorStyles.SelectedClassName, _showEditableOnly);
            _outputOnlyFilterChip?.EnableInClassList(ExpressionEditorStyles.SelectedClassName, _showOutputOnly);
            _sceneInfluenceFilterButton?.EnableInClassList(ExpressionEditorStyles.SelectedClassName, _sceneInfluenceFilterEnabled);

            if (_sceneInfluenceFilterChip != null)
            {
                var hasLocationFilter = _sceneInfluenceResults != null;
                var count = hasLocationFilter ? _sceneInfluenceResults.Count : 0;
                _sceneInfluenceFilterChipLabel.text = $"位置 {count}";
                _sceneInfluenceFilterChip.tooltip = $"クリック位置に影響する {count} 件のブレンドシェイプで絞り込んでいます。";
                _sceneInfluenceFilterChip.EnableInClassList(ExpressionEditorStyles.HiddenClassName, !hasLocationFilter);
            }

            UpdateListCountAndEmptyState();
        }

        /// <summary>
        /// 絞り込み行の右端の「表示数 / 全数」と、0 件時の表示を一覧の状態に合わせる。
        /// 絞り込みが原因で 0 件のときは、0 件表示の文言を変えて「絞り込みを解除」ボタンを出す。
        /// 編集中の表情がない場合や、ブレンドシェイプが 1 件もない場合はボタンを出さない。
        /// </summary>
        private void UpdateListCountAndEmptyState()
        {
            var listView = _blendShapeListView;
            var totalCount = listView != null ? listView.TotalEntryCount : 0;
            var visibleCount = listView != null ? listView.VisibleEntryCount : 0;
            if (_filterCountLabel != null)
            {
                _filterCountLabel.text = listView != null ? $"{visibleCount} / {totalCount}" : "";
                _filterCountLabel.EnableInClassList(ExpressionEditorStyles.HiddenClassName, listView == null);
            }

            var isEmptyByFilter = listView != null
                && visibleCount == 0
                && totalCount > 0
                && HasActiveListFilter();
            if (_emptyLabel != null)
            {
                _emptyLabel.text = isEmptyByFilter ? FilteredEmptyListText : EmptyListText;
            }

            _clearFiltersButton?.EnableInClassList(ExpressionEditorStyles.HiddenClassName, !isEmptyByFilter);
        }

        private bool HasActiveListFilter()
        {
            return (_searchField != null && !string.IsNullOrWhiteSpace(_searchField.value))
                || _showChangedOnly
                || _showEditableOnly
                || _showOutputOnly
                || _sceneInfluenceResults != null;
        }

        /// <summary>
        /// 一覧ヘッダーのメニューから実行した一括変更の結果を通知する。
        /// </summary>
        private void OnBulkEditableTargetActionApplied(
            BlendShapeBulkEditableTargetAction action,
            BlendShapeBulkEditableTargetResult result)
        {
            ShowSuccess(GetBulkEditableTargetResultMessage(action, result));
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

            var assetPath = GetOverwriteSaveAssetPath();
            if (string.IsNullOrEmpty(assetPath))
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
                savedClip = ExpressionClipIO.SaveClipToAsset(clip, assetPath);
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

            SetCurrentClip(savedClip);
            SetUnsavedChanges(false);
            ShowSuccess("保存しました。");
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
            var currentClipPath = _currentClip != null ? AssetDatabase.GetAssetPath(_currentClip) : "";
            var defaultDirectory = IsWritableAssetPath(currentClipPath) ? System.IO.Path.GetDirectoryName(currentClipPath) : "Assets";
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

            SetCurrentClip(savedClip);
            SetUnsavedChanges(false);
            ShowSuccess("保存しました。");
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

        /// <summary>
        /// 出力設定の警告（設定不備・参照クリップの中間キー・編集中のクリップの中間キー）を HelpBox に表示する。
        /// 編集中のクリップの中間キーの警告は、そのクリップを編集している間は常に表示する。
        /// 文言は、今のフレームモードで元カーブを保持できるかによって出し分ける。
        /// 差分出力の補足説明は HelpBox ではなく、出力範囲の横の ⓘ アイコンの tooltip で示す。
        /// </summary>
        private void UpdateOutputSettingsMessage(
            IReadOnlyList<BlendShapeOutputDecision> decisions,
            string validationMessage)
        {
            _outputModeInfoIcon?.EnableInClassList(
                ExpressionEditorStyles.HiddenClassName,
                _outputMode == BlendShapeOutputMode.AllTargets);

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

            var warnings = new List<string>();
            if (!string.IsNullOrEmpty(validationMessage))
            {
                warnings.Add(validationMessage);
            }
            else if (_outputMode == BlendShapeOutputMode.ReferenceClipDiff
                && ExpressionOutputDiffService.Summarize(decisions).intermediateKeyCount > 0)
            {
                warnings.Add("参照クリップに中間キーがあります。差分判定には始端と終端のみを使用します。");
            }

            if (_model.hasIntermediateKeys)
            {
                warnings.Add(CanKeepAnySourceCurve()
                    ? IntermediateKeysWarningText
                    : IntermediateKeysSingleFrameWarningText);
            }

            var message = string.Join("\n", warnings);
            _outputSettingsMessageBox.text = message;
            _outputSettingsMessageBox.messageType = HelpBoxMessageType.Warning;
            _outputSettingsMessageBox.EnableInClassList("hidden", string.IsNullOrEmpty(message));
        }

        /// <summary>
        /// 今のフレームモードで保存したときに、読み込み元のカーブを保持できる項目があるかを返す。
        /// 保存時の判定（<see cref="ExpressionClipIO.CanKeepSourceCurve"/>）と同じ条件を使う。
        /// 中間キーのあるクリップは 2 フレームとして読み込むため、1 フレームに切り替えると保持できなくなる。
        /// </summary>
        private bool CanKeepAnySourceCurve()
        {
            return _model != null
                && _model.entries != null
                && _model.entries.Any(entry => ExpressionClipIO.CanKeepSourceCurve(_model, entry));
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
            UpdateUnsavedIndicator();
        }

        /// <summary>
        /// 未保存の変更があるとき、クリップ欄のラベル末尾に「*」を付け、ラベルに tooltip を設定する。
        /// tooltip はラベル要素にだけ設定し、欄自体の tooltip には触れない。
        /// </summary>
        private void UpdateUnsavedIndicator()
        {
            if (_clipField == null)
            {
                return;
            }

            _clipField.label = hasUnsavedChanges ? ClipFieldLabel + UnsavedLabelSuffix : ClipFieldLabel;
            _clipField.labelElement.tooltip = hasUnsavedChanges ? UnsavedChangesTooltip : "";
        }

        private bool TryDiscardUnsavedChanges()
        {
            if (!hasUnsavedChanges)
            {
                return true;
            }

            var confirmation = DiscardUnsavedChangesConfirmation ?? ShowDiscardUnsavedChangesDialog;
            var discard = confirmation();
            if (discard)
            {
                SetUnsavedChanges(false);
            }

            return discard;
        }

        private static bool ShowDiscardUnsavedChangesDialog()
        {
            return EditorUtility.DisplayDialog(
                "未保存の変更",
                "未保存の表情編集があります。変更を破棄して続行しますか？",
                "破棄して続行",
                "キャンセル");
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
            _frameModeInfoIcon?.EnableInClassList(ExpressionEditorStyles.HiddenClassName, !isWeightBlend);
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
            _saveButton?.SetEnabled(canSaveOutput);
            _saveAsButton?.SetEnabled(canSaveOutput);
            _newButton?.SetEnabled(_avatarField != null && _avatarField.value != null && _rendererField != null && _rendererField.value != null);
            _previewToggle?.SetEnabled(_pendingThumbnailCapture == null);
            _frameModeField?.SetEnabled(hasModel);
            _outputModeField?.SetEnabled(hasModel);
            _referenceClipField?.SetEnabled(hasModel && _outputMode == BlendShapeOutputMode.ReferenceClipDiff);
            _sceneInfluenceFilterButton?.SetEnabled(CanUseSceneInfluenceFilter());
            _startFrameButton?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
            _endFrameButton?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
            _loadStartFromRendererButton?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
            _previewWeightSlider?.SetEnabled(hasModel && _model.frameMode == ExpressionFrameMode.WeightBlend);
        }

        /// <summary>
        /// 操作ごとの警告・エラーと、操作を続けるための案内をメッセージ欄に表示する。空文字を渡すとメッセージ欄を隠す。
        /// 成功・完了の通知には <see cref="ShowSuccess"/> を使う。セッション中ずっと当てはまる警告
        /// （編集中のクリップの中間キーなど）は、次の操作で消えないよう出力設定の HelpBox に出す。
        /// </summary>
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

        /// <summary>
        /// 成功・完了をウィンドウの通知（<see cref="EditorWindow.ShowNotification(GUIContent)"/>）で知らせる。
        /// 通知は一覧の位置を動かさない。メッセージ欄に残っている以前のメッセージは消す。
        /// </summary>
        private void ShowSuccess(string message)
        {
            ShowMessage("", HelpBoxMessageType.Info);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var presenter = NotificationPresenter ?? ShowNotificationText;
            presenter(message);
        }

        private void ShowNotificationText(string message)
        {
            ShowNotification(new GUIContent(message));
        }

        /// <summary>
        /// サムネイル撮影欄からのメッセージを表示する。Info は完了の通知（メニューアイコンの割り当て）なので通知にし、
        /// 警告はメッセージ欄に表示する。
        /// </summary>
        private void OnThumbnailCaptureMessageRequested(string message, HelpBoxMessageType messageType)
        {
            if (messageType == HelpBoxMessageType.Info)
            {
                ShowSuccess(message);
                return;
            }

            ShowMessage(message, messageType);
        }

        private void RequestPreview()
        {
            _previewDirty = true;
            _lastPreviewRequestTime = EditorApplication.timeSinceStartup;
        }

        /// <summary>
        /// エディタースキンに合わせて、ウィンドウのルートにスキンクラス（dark-skin / light-skin）を付ける。
        /// 開いている間にスキンが切り替わった場合は、絞り込み行などのアイコンを付け直し、行のアイコンもそのスキンのものにするため一覧を更新する。
        /// </summary>
        private void UpdateSkinClass()
        {
            var isProSkin = EditorGUIUtility.isProSkin;
            if (_appliedProSkin == isProSkin)
            {
                return;
            }

            var skinChanged = _appliedProSkin.HasValue;
            _appliedProSkin = isProSkin;
            ExpressionEditorStyles.ApplySkinClass(rootVisualElement, isProSkin);
            if (skinChanged)
            {
                RefreshIcons();
                _blendShapeListView?.Refresh();
            }
        }

        private void OnEditorUpdate()
        {
            UpdateSkinClass();
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
                if (resultMessageType == HelpBoxMessageType.Info)
                {
                    ShowSuccess(resultMessage);
                }
                else
                {
                    ShowMessage(resultMessage, resultMessageType);
                }
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
                        ShowSuccess("サムネイルプレビューを更新しました。");
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
                    ShowSuccess("サムネイルを保存しました。");
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
            if (_model == null)
            {
                return;
            }

            _blendShapeListView?.Refresh();
            _partialImportView?.Refresh();
            UpdateFrameModeControls();
            MarkUnsaved();
            RequestPreview();
        }
    }
}
