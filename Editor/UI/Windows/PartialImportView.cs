using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public sealed class PartialImportView : IDisposable
    {
        private const string NoSourceClipMessage = "取り込み元 AnimationClip を指定してください。";
        private const float ValueTolerance = 0.0001f;

        private readonly Foldout _root;
        private readonly ObjectField _sourceClipField;
        private readonly ToolbarSearchField _searchField;
        private readonly Button _selectAllButton;
        private readonly Button _clearAllButton;
        private readonly Button _applyButton;
        private readonly Label _summaryLabel;
        private readonly HelpBox _messageBox;
        private readonly VisualElement _previewContainer;
        private readonly Image _previewImage;
        private readonly ListView _listView;
        private readonly List<PartialImportItem> _items = new List<PartialImportItem>();
        private readonly List<PartialImportItem> _filteredItems = new List<PartialImportItem>();

        private ExpressionEditModel _model;
        private ExpressionEditFrame _editingFrame = ExpressionEditFrame.Start;
        private Texture2D _previewTexture;
        private string _searchText = "";

        public PartialImportView(VisualElement container)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            container.Clear();

            _root = new Foldout
            {
                text = "部分取り込み",
                value = false,
            };
            _root.AddToClassList("partial-import-foldout");
            container.Add(_root);

            var sourceRow = new VisualElement();
            sourceRow.AddToClassList("partial-import-source-row");
            _root.Add(sourceRow);

            _sourceClipField = new ObjectField("取り込み元クリップ")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            ConfigureField(_sourceClipField);
            _sourceClipField.RegisterValueChangedCallback(_ => RebuildItems(false));
            sourceRow.Add(_sourceClipField);

            var actionRow = new VisualElement();
            actionRow.AddToClassList("partial-import-action-row");
            _root.Add(actionRow);

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("partial-import-search");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? "";
                RefreshFilteredItems();
            });
            actionRow.Add(_searchField);

            _selectAllButton = new Button(() => SetVisibleSelection(true))
            {
                text = "表示中を全選択",
            };
            _selectAllButton.AddToClassList("partial-import-button");
            actionRow.Add(_selectAllButton);

            _clearAllButton = new Button(() => SetVisibleSelection(false))
            {
                text = "表示中を全解除",
            };
            _clearAllButton.AddToClassList("partial-import-button");
            actionRow.Add(_clearAllButton);

            _applyButton = new Button(ApplySelected)
            {
                text = "選択項目を取り込み",
            };
            _applyButton.AddToClassList("partial-import-button");
            actionRow.Add(_applyButton);

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("partial-import-summary");
            _root.Add(_summaryLabel);

            _messageBox = new HelpBox("", HelpBoxMessageType.Info);
            _messageBox.AddToClassList("partial-import-message");
            _root.Add(_messageBox);

            var contentRow = new VisualElement();
            contentRow.AddToClassList("partial-import-content-row");
            _root.Add(contentRow);

            var listContainer = new VisualElement();
            listContainer.AddToClassList("partial-import-list-container");
            contentRow.Add(listContainer);

            _listView = CreateListView();
            listContainer.Add(_listView);

            _previewContainer = new VisualElement();
            _previewContainer.AddToClassList("partial-import-preview");
            contentRow.Add(_previewContainer);

            var previewTitle = new Label("取り込みプレビュー");
            previewTitle.AddToClassList("partial-import-preview-title");
            _previewContainer.Add(previewTitle);

            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
            };
            _previewImage.AddToClassList("partial-import-preview-image");
            _previewContainer.Add(_previewImage);

            SetModel(null);
        }

        public event Action<IReadOnlyList<PartialImportValue>> ApplyRequested;
        public event Action<IReadOnlyList<PartialImportValue>> PreviewRequested;

        public void SetModel(ExpressionEditModel model)
        {
            _model = model;
            RebuildItems(false);
            UpdateButtonStates();
        }

        public void SetEditingFrame(ExpressionEditFrame editingFrame)
        {
            if (_editingFrame == editingFrame)
            {
                return;
            }

            _editingFrame = editingFrame;
            RebuildItems(true);
        }

        public void Refresh()
        {
            foreach (var item in _items)
            {
                item.canApply = CanApplyToEntry(item.targetEntry);
                item.disabledReason = GetDisabledReason(item.targetEntry);
                if (!item.canApply)
                {
                    item.selected = false;
                }
            }

            RefreshFilteredItems();
            RequestSelectionPreview();
        }

        public void SetPreviewTexture(Texture2D texture)
        {
            ClearPreviewTexture();
            _previewTexture = texture;
            _previewImage.image = _previewTexture;
            _previewImage.MarkDirtyRepaint();
            UpdatePreviewVisibility();
        }

        public void ClearPreviewTexture()
        {
            if (_previewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            if (_previewImage != null)
            {
                _previewImage.image = null;
                _previewImage.MarkDirtyRepaint();
            }

            UpdatePreviewVisibility();
        }

        public void Dispose()
        {
            ClearPreviewTexture();
        }

        private static void ConfigureField(ObjectField field)
        {
            field.AddToClassList("partial-import-field");
            field.labelElement.style.minWidth = 160f;
            field.labelElement.style.width = 180f;
            field.labelElement.style.flexShrink = 0f;
        }

        private ListView CreateListView()
        {
            var listView = new ListView
            {
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 28,
                showBorder = true,
                makeItem = () => new PartialImportRow(this),
            };
            listView.bindItem = (element, index) =>
            {
                var row = (PartialImportRow)element;
                row.Bind((PartialImportItem)listView.itemsSource[index]);
            };
            listView.unbindItem = (element, _) =>
            {
                var row = (PartialImportRow)element;
                row.Unbind();
            };
            listView.AddToClassList("partial-import-list");
            return listView;
        }

        private void RebuildItems(bool preserveSelection)
        {
            var selectedNames = preserveSelection
                ? new HashSet<string>(_items.Where(item => item.canApply && item.selected).Select(item => item.name))
                : new HashSet<string>();
            _items.Clear();
            ClearSelectionPreview();

            if (_model == null)
            {
                RefreshFilteredItems("アバターと対象 Skinned Mesh Renderer を指定してください。", HelpBoxMessageType.Info);
                return;
            }

            var sourceClip = _sourceClipField.value as AnimationClip;
            if (sourceClip == null)
            {
                RefreshFilteredItems(NoSourceClipMessage, HelpBoxMessageType.Info);
                return;
            }

            var sourceValuesByName = GetSourceValuesByBlendShapeName(sourceClip, _model);

            foreach (var entry in _model.entries)
            {
                if (!sourceValuesByName.TryGetValue(entry.name, out var sourceValue))
                {
                    continue;
                }

                var canApply = CanApplyToEntry(entry);
                _items.Add(new PartialImportItem
                {
                    name = entry.name,
                    value = sourceValue,
                    selected = preserveSelection && selectedNames.Contains(entry.name) && canApply,
                    canApply = canApply,
                    disabledReason = GetDisabledReason(entry),
                    targetEntry = entry,
                });
            }

            var message = _items.Count > 0
                ? ""
                : "対象 Skinned Mesh Renderer に対応するブレンドシェイプカーブがありません。";
            RefreshFilteredItems(message, _items.Count > 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
            if (preserveSelection)
            {
                RequestSelectionPreview();
            }
        }

        private Dictionary<string, float> GetSourceValuesByBlendShapeName(AnimationClip sourceClip, ExpressionEditModel model)
        {
            var result = new Dictionary<string, float>();
            if (sourceClip == null || model == null || model.targetRenderer == null)
            {
                return result;
            }

            var rendererPath = MiscUtil.GetPathInHierarchy(model.targetRenderer.gameObject, model.avatarRootObject);
            var blendShapeCurves = AnimationUtility.GetCurveBindings(sourceClip)
                .Where(binding => binding.type == typeof(SkinnedMeshRenderer)
                    && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
                .Select(binding => new SourceBlendShapeCurve(binding, AnimationUtility.GetEditorCurve(sourceClip, binding)))
                .Where(item => item.curve != null && item.curve.length > 0)
                .ToList();

            var targetCurves = blendShapeCurves
                .Where(item => item.binding.path == rendererPath)
                .ToList();
            if (targetCurves.Count == 0)
            {
                targetCurves = blendShapeCurves;
            }

            var sampleTime = GetSourceSampleTime(targetCurves);
            foreach (var item in targetCurves)
            {
                var blendShapeName = item.binding.propertyName.Substring("blendShape.".Length);
                if (!result.ContainsKey(blendShapeName))
                {
                    result.Add(blendShapeName, Mathf.Clamp(item.curve.Evaluate(sampleTime), 0f, 100f));
                }
            }

            return result;
        }

        private float GetSourceSampleTime(IReadOnlyList<SourceBlendShapeCurve> curves)
        {
            if (curves == null || curves.Count == 0)
            {
                return 0f;
            }

            if (_editingFrame == ExpressionEditFrame.Start)
            {
                return curves.Min(item => item.curve.keys[0].time);
            }

            return curves.Max(item => item.curve.keys[item.curve.length - 1].time);
        }

        private void RefreshFilteredItems(string message = "", HelpBoxMessageType messageType = HelpBoxMessageType.Info)
        {
            var refreshedItems = _items
                .Where(MatchesFilter)
                .ToList();
            var visibleItemsChanged = HasFilteredItemsChanged(refreshedItems);

            _filteredItems.Clear();
            foreach (var item in refreshedItems)
            {
                _filteredItems.Add(item);
            }

            _listView.itemsSource = _filteredItems;
            if (visibleItemsChanged)
            {
                _listView.Rebuild();
            }
            else
            {
                _listView.RefreshItems();
            }

            UpdateSummary();
            SetMessage(message, messageType);
            UpdateButtonStates();
        }

        private bool HasFilteredItemsChanged(IReadOnlyList<PartialImportItem> refreshedItems)
        {
            if (refreshedItems == null || refreshedItems.Count != _filteredItems.Count)
            {
                return true;
            }

            for (var i = 0; i < refreshedItems.Count; i++)
            {
                if (!ReferenceEquals(refreshedItems[i], _filteredItems[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesFilter(PartialImportItem item)
        {
            return item != null
                && (string.IsNullOrWhiteSpace(_searchText)
                    || item.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void SetVisibleSelection(bool selected)
        {
            foreach (var item in _filteredItems)
            {
                if (item.canApply)
                {
                    item.selected = selected;
                }
            }

            _listView.RefreshItems();
            UpdateSummary();
            UpdateButtonStates();
            RequestSelectionPreview();
        }

        private void SetSelected(PartialImportItem item, bool selected)
        {
            if (item == null || !item.canApply || item.selected == selected)
            {
                return;
            }

            item.selected = selected;
            UpdateSummary();
            UpdateButtonStates();
            RequestSelectionPreview();
        }

        private void ApplySelected()
        {
            ApplyRequested?.Invoke(GetSelectedValues());
        }

        private void UpdateSummary()
        {
            var selectedCount = 0;
            var importableCount = 0;
            foreach (var item in _items)
            {
                if (!item.canApply)
                {
                    continue;
                }

                importableCount++;
                if (item.selected)
                {
                    selectedCount++;
                }
            }

            var visibleCount = _filteredItems.Count;
            _summaryLabel.text = $"表示 {visibleCount} 件 / 取り込み可能 {importableCount} 件 / 選択 {selectedCount} 件";
            _summaryLabel.EnableInClassList("hidden", _model == null || _sourceClipField.value == null);
        }

        private void SetMessage(string message, HelpBoxMessageType messageType)
        {
            _messageBox.text = message;
            _messageBox.messageType = messageType;
            _messageBox.EnableInClassList("hidden", string.IsNullOrEmpty(message));
        }

        private void UpdateButtonStates()
        {
            var hasModel = _model != null;
            var hasClip = _sourceClipField.value is AnimationClip;
            var selectedCount = 0;
            foreach (var item in _items)
            {
                if (item.canApply && item.selected)
                {
                    selectedCount++;
                }
            }

            var visibleImportableCount = 0;
            foreach (var item in _filteredItems)
            {
                if (item.canApply)
                {
                    visibleImportableCount++;
                }
            }

            _sourceClipField.SetEnabled(hasModel);
            _searchField.SetEnabled(hasModel && hasClip && _items.Count > 0);
            _selectAllButton.SetEnabled(hasModel && hasClip && visibleImportableCount > 0);
            _clearAllButton.SetEnabled(hasModel && hasClip && visibleImportableCount > 0);
            _applyButton.SetEnabled(hasModel && hasClip && selectedCount > 0);
            _listView.SetEnabled(hasModel && hasClip);
            UpdatePreviewVisibility();
        }

        private void RequestSelectionPreview()
        {
            var values = GetSelectedValues();
            if (values.Count == 0)
            {
                ClearSelectionPreview();
                return;
            }

            PreviewRequested?.Invoke(values);
        }

        private void ClearSelectionPreview()
        {
            ClearPreviewTexture();
            PreviewRequested?.Invoke(new List<PartialImportValue>());
        }

        private List<PartialImportValue> GetSelectedValues()
        {
            return _items
                .Where(item => item.canApply && item.selected)
                .Select(item => new PartialImportValue(item.name, item.value, item.targetEntry != null ? item.targetEntry.index : -1))
                .ToList();
        }

        private void UpdatePreviewVisibility()
        {
            if (_previewContainer == null)
            {
                return;
            }

            _previewContainer.EnableInClassList("hidden", _model == null || _sourceClipField.value == null || _previewTexture == null);
        }

        private static bool CanApplyToEntry(BlendShapeEntry entry)
        {
            return entry != null && !entry.IsSystemLocked && !entry.userExcluded;
        }

        private static string GetDisabledReason(BlendShapeEntry entry)
        {
            if (entry == null)
            {
                return "対象なし";
            }

            if (entry.IsSystemLocked)
            {
                return "システム除外";
            }

            if (entry.userExcluded)
            {
                return "出力対象外";
            }

            return "";
        }

        private sealed class PartialImportItem
        {
            public string name;
            public float value;
            public bool selected;
            public bool canApply;
            public string disabledReason;
            public BlendShapeEntry targetEntry;
        }

        private sealed class SourceBlendShapeCurve
        {
            public SourceBlendShapeCurve(EditorCurveBinding binding, AnimationCurve curve)
            {
                this.binding = binding;
                this.curve = curve;
            }

            public EditorCurveBinding binding;
            public AnimationCurve curve;
        }

        private sealed class PartialImportRow : VisualElement
        {
            private readonly PartialImportView _owner;
            private readonly Toggle _toggle;
            private readonly Label _nameLabel;
            private readonly Label _valueLabel;
            private readonly Label _reasonLabel;
            private PartialImportItem _item;
            private bool _ignoreChange;

            public PartialImportRow(PartialImportView owner)
            {
                _owner = owner;
                AddToClassList("partial-import-row");

                _toggle = new Toggle();
                _toggle.AddToClassList("partial-import-toggle");
                _toggle.RegisterValueChangedCallback(OnToggleChanged);
                Add(_toggle);

                _nameLabel = new Label();
                _nameLabel.AddToClassList("partial-import-name");
                Add(_nameLabel);

                _valueLabel = new Label();
                _valueLabel.AddToClassList("partial-import-value");
                Add(_valueLabel);

                _reasonLabel = new Label();
                _reasonLabel.AddToClassList("partial-import-reason");
                Add(_reasonLabel);
            }

            public void Bind(PartialImportItem item)
            {
                _item = item;
                _ignoreChange = true;

                _toggle.SetValueWithoutNotify(item.selected);
                _toggle.SetEnabled(item.canApply);
                _nameLabel.text = item.name;
                _nameLabel.tooltip = item.name;
                _valueLabel.text = FormatValue(item.value);
                _reasonLabel.text = item.disabledReason;
                _reasonLabel.EnableInClassList("hidden", string.IsNullOrEmpty(item.disabledReason));
                EnableInClassList("disabled", !item.canApply);

                _ignoreChange = false;
            }

            public void Unbind()
            {
                _item = null;
            }

            private void OnToggleChanged(ChangeEvent<bool> evt)
            {
                if (_ignoreChange || _item == null)
                {
                    return;
                }

                _owner.SetSelected(_item, evt.newValue);
            }

            private static string FormatValue(float value)
            {
                return Mathf.Abs(value - Mathf.Round(value)) <= ValueTolerance
                    ? Mathf.RoundToInt(value).ToString()
                    : value.ToString("0.###");
            }
        }
    }

    public sealed class PartialImportValue
    {
        public PartialImportValue(string name, float value, int blendShapeIndex = -1)
        {
            Name = name;
            Value = value;
            BlendShapeIndex = blendShapeIndex;
        }

        public string Name { get; }
        public float Value { get; }
        public int BlendShapeIndex { get; }
    }
}
