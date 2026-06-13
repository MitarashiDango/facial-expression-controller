using System;
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

        private ObjectField _avatarField;
        private ObjectField _rendererField;
        private ObjectField _clipField;
        private ToolbarSearchField _searchField;
        private ToolbarMenu _filterMenu;
        private Toggle _previewToggle;
        private Button _newButton;
        private Button _loadButton;
        private Button _saveButton;
        private Button _saveAsButton;
        private Button _resetButton;
        private HelpBox _messageBox;
        private VisualElement _listContainer;
        private Label _emptyLabel;

        private ExpressionEditModel _model;
        private BlendShapeListView _blendShapeListView;
        private ExpressionPreviewService _previewService;
        private AnimationClip _currentClip;
        private string _currentAssetPath;
        private ExpressionFrameMode _loadedSourceFrameMode = ExpressionFrameMode.SingleFrame;
        private bool _previewDirty;
        private bool _showChangedOnly;
        private bool _showEditableOnly;
        private double _lastPreviewRequestTime;

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
            BuildGui();
            TryInitializeFromSelection();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= OnEditorUpdate;
            StopPreview();
            _previewService?.Dispose();
            _previewService = null;

            if (_model != null)
            {
                DestroyImmediate(_model);
                _model = null;
            }
        }

        private void BuildGui()
        {
            rootVisualElement.Clear();

            var mainUxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainUxmlPath);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"[FacialExpressionController] Cannot load UXML file: {MainUxmlPath}");
                return;
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

            UpdateButtonStates();
            ShowMessage("アバターを指定してください。", HelpBoxMessageType.Info);
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
                ShowMessage("3 つ以上のキーを持つクリップです。Phase 1 では始端のみを読み込み、保存時に中間キーは出力されません。", HelpBoxMessageType.Warning);
            }
            else if (_loadedSourceFrameMode != ExpressionFrameMode.SingleFrame)
            {
                ShowMessage("2 フレームのクリップです。Phase 1 では始端のみを読み込み、1 フレームで保存します。", HelpBoxMessageType.Warning);
            }
            else
            {
                ShowMessage("クリップを読み込みました。", HelpBoxMessageType.Info);
            }
        }

        private void SetModel(GameObject avatarRootObject, SkinnedMeshRenderer renderer, AnimationClip sourceClip, bool markDirty)
        {
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
                NormalizeModelToSingleFrame(_model);
            }
            else
            {
                _loadedSourceFrameMode = ExpressionFrameMode.SingleFrame;
            }

            if (_rendererField != null && _rendererField.value != renderer)
            {
                _rendererField.SetValueWithoutNotify(renderer);
            }

            RebuildBlendShapeList();
            if (markDirty)
            {
                MarkUnsaved();
            }

            UpdateButtonStates();
            RequestPreview();
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
            _blendShapeListView.PreviewResetRequested += StopPreview;
            _blendShapeListView.SetSearchText(_searchField != null ? _searchField.value : "");
            _blendShapeListView.SetShowChangedOnly(_showChangedOnly);
            _blendShapeListView.SetShowEditableOnly(_showEditableOnly);
        }

        private void OnModelChanged()
        {
            MarkUnsaved();
            RequestPreview();
        }

        private void OnBlendShapePreviewValueChanged(BlendShapeEntry entry)
        {
            _previewService?.SampleEntry(_model, entry);
        }

        private void ResetValues()
        {
            _blendShapeListView?.ResetEditableValues();
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

        private static void NormalizeModelToSingleFrame(ExpressionEditModel model)
        {
            if (model == null || model.frameMode == ExpressionFrameMode.SingleFrame)
            {
                return;
            }

            foreach (var entry in model.entries)
            {
                entry.endValue = entry.value;
            }

            model.frameMode = ExpressionFrameMode.SingleFrame;
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

        private void UpdateButtonStates()
        {
            var hasModel = _model != null;
            _loadButton?.SetEnabled(_clipField != null && _clipField.value != null && hasModel);
            _saveButton?.SetEnabled(hasModel);
            _saveAsButton?.SetEnabled(hasModel);
            _resetButton?.SetEnabled(hasModel);
            _newButton?.SetEnabled(_avatarField != null && _avatarField.value != null && _rendererField != null && _rendererField.value != null);
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

            if (!_previewDirty)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastPreviewRequestTime < PreviewDebounceSeconds)
            {
                return;
            }

            _previewDirty = false;
            _previewService?.Sample(_model);
        }

        private void StopPreview()
        {
            _previewDirty = false;
            _previewService?.Stop();
        }

        private void OnUndoRedoPerformed()
        {
            _blendShapeListView?.Refresh();
            MarkUnsaved();
            RequestPreview();
        }
    }
}
