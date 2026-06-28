using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public sealed class BlendShapeListView
    {
        private const float ChangedTolerance = 0.0001f;
        private readonly ExpressionEditModel _model;
        private readonly ListView _listView;
        private readonly Label _emptyLabel;
        private readonly List<BlendShapeEntry> _items = new List<BlendShapeEntry>();
        private string _searchText = "";
        private bool _showChangedOnly;
        private bool _showEditableOnly;
        private bool _showOutputOnly;
        private ExpressionEditFrame _editingFrame = ExpressionEditFrame.Start;
        private BlendShapeOutputMode _outputMode = BlendShapeOutputMode.AllTargets;
        private IReadOnlyDictionary<BlendShapeEntry, BlendShapeOutputDecision> _outputDecisions =
            new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>();
        private int _activeUndoGroup = -1;

        public event Action Changed;
        public event Action<BlendShapeEntry> PreviewValueChanged;
        public event Action PreviewResetRequested;

        public BlendShapeListView(ExpressionEditModel model, VisualElement container, Label emptyLabel)
        {
            _model = model;
            _emptyLabel = emptyLabel;

            _listView = CreateListView();
            container.Add(_listView);

            Refresh();
        }

        public void SetSearchText(string searchText)
        {
            _searchText = searchText ?? "";
            Refresh();
        }

        public void SetShowChangedOnly(bool showChangedOnly)
        {
            _showChangedOnly = showChangedOnly;
            Refresh();
        }

        public void SetShowEditableOnly(bool showEditableOnly)
        {
            _showEditableOnly = showEditableOnly;
            Refresh();
        }

        public void SetShowOutputOnly(bool showOutputOnly)
        {
            _showOutputOnly = showOutputOnly;
            Refresh();
        }

        public void SetOutputDecisions(
            BlendShapeOutputMode outputMode,
            IReadOnlyDictionary<BlendShapeEntry, BlendShapeOutputDecision> outputDecisions)
        {
            _outputMode = outputMode;
            _outputDecisions = outputDecisions ?? new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>();
            Refresh();
        }

        public void SetEditingFrame(ExpressionEditFrame editingFrame)
        {
            if (_editingFrame == editingFrame)
            {
                return;
            }

            _editingFrame = editingFrame;
            Refresh();
        }

        public void Refresh()
        {
            _items.Clear();

            if (_model != null)
            {
                foreach (var entry in _model.entries.Where(MatchesFilter))
                {
                    _items.Add(entry);
                }
            }

            _listView.itemsSource = _items;
            _listView.Rebuild();
            _emptyLabel.EnableInClassList("hidden", _items.Count > 0);
        }

        public void ResetEditableValues()
        {
            if (_model == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("表情ブレンドシェイプをリセット");
            Undo.RecordObject(_model, "表情ブレンドシェイプをリセット");

            foreach (var entry in _model.entries)
            {
                if (!CanEditValue(entry))
                {
                    continue;
                }

                entry.value = entry.initialValue;
                entry.endValue = entry.initialValue;
            }

            EditorUtility.SetDirty(_model);
            Undo.CollapseUndoOperations(group);
            Refresh();
            Changed?.Invoke();
        }

        private ListView CreateListView()
        {
            var listView = new ListView
            {
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 34,
                showBorder = true,
                makeItem = () => new BlendShapeRow(this),
            };
            listView.bindItem = (element, index) =>
            {
                var row = (BlendShapeRow)element;
                row.Bind((BlendShapeEntry)listView.itemsSource[index]);
            };
            listView.unbindItem = (element, _) =>
            {
                var row = (BlendShapeRow)element;
                row.Unbind();
            };

            listView.AddToClassList("blendshape-list");
            return listView;
        }

        private bool MatchesFilter(BlendShapeEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_searchText)
                && entry.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (_showEditableOnly && !CanEditValue(entry))
            {
                return false;
            }

            if (_showOutputOnly && !IsOutputScheduled(entry))
            {
                return false;
            }

            return !_showChangedOnly || IsChanged(entry);
        }

        private bool IsOutputScheduled(BlendShapeEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            if (_outputDecisions != null && _outputDecisions.TryGetValue(entry, out var decision))
            {
                return decision.shouldWriteCurve;
            }

            return _outputMode == BlendShapeOutputMode.AllTargets && entry.ShouldOutput;
        }

        private static bool IsChanged(BlendShapeEntry entry)
        {
            return Mathf.Abs(entry.value - entry.initialValue) > ChangedTolerance
                || Mathf.Abs(entry.endValue - entry.initialValue) > ChangedTolerance
                || entry.systemExclusionUnlocked
                || entry.userExcluded;
        }

        private void BeginGroupedUndo()
        {
            if (_activeUndoGroup >= 0 || _model == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            _activeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("表情ブレンドシェイプを編集");
            Undo.RecordObject(_model, "表情ブレンドシェイプを編集");
        }

        private void EndGroupedUndo()
        {
            if (_activeUndoGroup < 0)
            {
                return;
            }

            Undo.CollapseUndoOperations(_activeUndoGroup);
            _activeUndoGroup = -1;
        }

        private void RecordSingleUndo()
        {
            if (_model == null || _activeUndoGroup >= 0)
            {
                return;
            }

            Undo.RecordObject(_model, "表情ブレンドシェイプを編集");
        }

        private void SetValue(BlendShapeEntry entry, float value)
        {
            if (!CanEditValue(entry))
            {
                return;
            }

            RecordSingleUndo();
            var clampedValue = Mathf.Clamp(value, 0f, 100f);
            if (Mathf.Abs(GetDisplayValue(entry) - clampedValue) <= ChangedTolerance)
            {
                return;
            }

            SetDisplayValue(entry, clampedValue);

            EditorUtility.SetDirty(_model);
            PreviewValueChanged?.Invoke(entry);
            Changed?.Invoke();
        }

        private float GetDisplayValue(BlendShapeEntry entry)
        {
            if (_model != null
                && _model.frameMode == ExpressionFrameMode.WeightBlend
                && _editingFrame == ExpressionEditFrame.End)
            {
                return entry.endValue;
            }

            return entry.value;
        }

        private void SetDisplayValue(BlendShapeEntry entry, float value)
        {
            if (_model != null
                && _model.frameMode == ExpressionFrameMode.WeightBlend
                && _editingFrame == ExpressionEditFrame.End)
            {
                entry.endValue = value;
                return;
            }

            entry.value = value;
            if (_model == null || _model.frameMode == ExpressionFrameMode.SingleFrame)
            {
                entry.endValue = value;
            }
        }

        private static bool CanEditValue(BlendShapeEntry entry)
        {
            return entry != null && !entry.IsSystemLocked && !entry.userExcluded;
        }

        private static string GetSystemExclusionText(BlendShapeEntry entry)
        {
            if (entry == null || entry.systemExclusion == BlendShapeSystemExclusionReason.None)
            {
                return "";
            }

            var reasons = new List<string>();
            if ((entry.systemExclusion & BlendShapeSystemExclusionReason.Mmd) != 0)
            {
                reasons.Add("MMD");
            }

            if ((entry.systemExclusion & BlendShapeSystemExclusionReason.LipSync) != 0)
            {
                reasons.Add("リップシンク");
            }

            if ((entry.systemExclusion & BlendShapeSystemExclusionReason.EyeControl) != 0)
            {
                reasons.Add("目");
            }

            if ((entry.systemExclusion & BlendShapeSystemExclusionReason.Empty) != 0)
            {
                reasons.Add("空");
            }

            return string.Join(", ", reasons);
        }

        private string GetOutputStatusText(BlendShapeEntry entry, out string tooltip)
        {
            tooltip = "";
            if (_outputMode == BlendShapeOutputMode.AllTargets
                || entry == null
                || _outputDecisions == null
                || !_outputDecisions.TryGetValue(entry, out var decision))
            {
                return "";
            }

            tooltip = decision.reason;
            if (!entry.ShouldOutput)
            {
                return "対象外";
            }

            switch (decision.referenceStatus)
            {
                case ReferenceBlendShapeSampleStatus.Missing:
                    return "参照なし";
                case ReferenceBlendShapeSampleStatus.Ambiguous:
                    return "参照曖昧";
                case ReferenceBlendShapeSampleStatus.IntermediateKeysIgnored:
                    return "中間キー";
            }

            return decision.shouldWriteCurve ? "出力予定" : "差分なし";
        }

        private sealed class BlendShapeRow : VisualElement
        {
            private readonly BlendShapeListView _owner;
            private readonly Label _nameLabel;
            private readonly Slider _slider;
            private readonly FloatField _valueField;
            private readonly Toggle _outputToggle;
            private readonly Label _outputStatusLabel;
            private readonly Label _reasonLabel;
            private BlendShapeEntry _entry;
            private bool _ignoreChange;

            public BlendShapeRow(BlendShapeListView owner)
            {
                _owner = owner;
                AddToClassList("blendshape-row");

                _nameLabel = new Label();
                _nameLabel.AddToClassList("blendshape-name");
                Add(_nameLabel);

                _slider = new Slider(0f, 100f);
                _slider.AddToClassList("blendshape-slider");
                _slider.RegisterValueChangedCallback(OnSliderChanged);
                _slider.RegisterCallback<PointerDownEvent>(_ => _owner.BeginGroupedUndo());
                _slider.RegisterCallback<PointerUpEvent>(_ => _owner.EndGroupedUndo());
                _slider.RegisterCallback<PointerCancelEvent>(_ => _owner.EndGroupedUndo());
                _slider.RegisterCallback<PointerCaptureOutEvent>(_ => _owner.EndGroupedUndo());
                Add(_slider);

                _valueField = new FloatField();
                _valueField.AddToClassList("blendshape-value");
                _valueField.RegisterValueChangedCallback(OnValueChanged);
                Add(_valueField);

                _outputToggle = new Toggle();
                _outputToggle.text = "編集・出力対象";
                _outputToggle.tooltip = "編集・出力対象にするかどうかを切り替える";
                _outputToggle.AddToClassList("blendshape-output-toggle");
                _outputToggle.RegisterValueChangedCallback(OnOutputChanged);
                Add(_outputToggle);

                _outputStatusLabel = new Label();
                _outputStatusLabel.AddToClassList("blendshape-output-status");
                Add(_outputStatusLabel);

                _reasonLabel = new Label();
                _reasonLabel.AddToClassList("blendshape-reason");
                Add(_reasonLabel);
            }

            public void Bind(BlendShapeEntry entry)
            {
                _entry = entry;
                _ignoreChange = true;

                var isSystemExcluded = entry.IsSystemExcluded;
                var isSystemLocked = entry.IsSystemLocked;
                var canEditValue = CanEditValue(entry);
                var displayValue = _owner.GetDisplayValue(entry);
                _nameLabel.text = entry.name;
                _nameLabel.tooltip = entry.name;
                _slider.SetValueWithoutNotify(displayValue);
                _valueField.SetValueWithoutNotify(displayValue);
                _outputToggle.SetValueWithoutNotify(entry.ShouldOutput);
                var outputStatusText = _owner.GetOutputStatusText(entry, out var outputStatusTooltip);
                _outputStatusLabel.text = outputStatusText;
                _outputStatusLabel.tooltip = outputStatusTooltip;
                _reasonLabel.text = GetSystemExclusionText(entry);

                _slider.SetEnabled(canEditValue);
                _valueField.SetEnabled(canEditValue);
                _outputToggle.SetEnabled(true);
                _outputStatusLabel.style.visibility = string.IsNullOrEmpty(outputStatusText) ? Visibility.Hidden : Visibility.Visible;
                _reasonLabel.style.visibility = isSystemExcluded ? Visibility.Visible : Visibility.Hidden;
                EnableInClassList("system-excluded", isSystemLocked);
                EnableInClassList("system-unlocked", isSystemExcluded && !isSystemLocked);
                EnableInClassList("user-excluded", entry.userExcluded);
                EnableInClassList("changed", IsChanged(entry));
                EnableInClassList("output-scheduled", _owner.IsOutputScheduled(entry));
                EnableInClassList("output-skipped", _owner._outputMode != BlendShapeOutputMode.AllTargets && !_owner.IsOutputScheduled(entry));

                _ignoreChange = false;
            }

            public void Unbind()
            {
                _entry = null;
            }

            private void OnSliderChanged(ChangeEvent<float> evt)
            {
                if (_ignoreChange || _entry == null)
                {
                    return;
                }

                _owner.SetValue(_entry, evt.newValue);
                _ignoreChange = true;
                _valueField.SetValueWithoutNotify(_owner.GetDisplayValue(_entry));
                EnableInClassList("changed", IsChanged(_entry));
                _ignoreChange = false;
            }

            private void OnValueChanged(ChangeEvent<float> evt)
            {
                if (_ignoreChange || _entry == null)
                {
                    return;
                }

                _owner.SetValue(_entry, evt.newValue);
                _ignoreChange = true;
                var displayValue = _owner.GetDisplayValue(_entry);
                _slider.SetValueWithoutNotify(displayValue);
                _valueField.SetValueWithoutNotify(displayValue);
                EnableInClassList("changed", IsChanged(_entry));
                _ignoreChange = false;
            }

            private void OnOutputChanged(ChangeEvent<bool> evt)
            {
                if (_ignoreChange || _entry == null)
                {
                    return;
                }

                _owner.SetEditableTarget(_entry, evt.newValue);
                Bind(_entry);
            }
        }

        private void SetEditableTarget(BlendShapeEntry entry, bool editableTarget)
        {
            if (entry == null)
            {
                return;
            }

            var nextSystemExclusionUnlocked = entry.IsSystemExcluded && editableTarget;
            var nextUserExcluded = !entry.IsSystemExcluded && !editableTarget;
            if (entry.systemExclusionUnlocked == nextSystemExclusionUnlocked
                && entry.userExcluded == nextUserExcluded)
            {
                return;
            }

            RecordSingleUndo();
            entry.systemExclusionUnlocked = nextSystemExclusionUnlocked;
            entry.userExcluded = nextUserExcluded;
            EditorUtility.SetDirty(_model);
            PreviewResetRequested?.Invoke();
            Changed?.Invoke();
        }
    }
}
