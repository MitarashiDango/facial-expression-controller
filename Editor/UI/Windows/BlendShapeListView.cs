using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public enum BlendShapeBulkEditableTargetAction
    {
        AllEditable,
        AllExcluded,
        RestoreEditingStart,
    }

    public struct BlendShapeBulkEditableTargetResult
    {
        public BlendShapeBulkEditableTargetResult(int changedCount, int unavailableCount)
        {
            ChangedCount = changedCount;
            UnavailableCount = unavailableCount;
        }

        public int ChangedCount { get; }
        public int UnavailableCount { get; }
    }

    /// <summary>
    /// 一覧ヘッダーのチェックボックスに表示する、全件の編集・出力対象の状態。
    /// 同名の 2 個目以降は編集・出力対象にできないため、集計に含めない。
    /// </summary>
    internal enum BlendShapeTargetAggregateState
    {
        /// <summary>集計対象のすべてが編集・出力対象。</summary>
        AllTargets,

        /// <summary>集計対象のすべてが編集・出力対象外。集計対象がない場合も含む。</summary>
        NoTargets,

        /// <summary>編集・出力対象と対象外が混在している。</summary>
        Mixed,
    }

    public sealed class BlendShapeListView
    {
        private const float ChangedTolerance = 0.0001f;
        private const float NarrowLayoutWidth = 720f;
        private const string NarrowClassName = "narrow";
        internal const string BulkRestoreEditingStartLabel = "編集開始時点の状態に戻す";
        internal const string ResetEditableValuesLabel = "編集可能なすべての値を初期値に戻す";
        internal const string HeaderTargetTooltip =
            "全件の編集・出力対象の状態です。クリックすると一括変更メニューを開きます。\n"
            + "一括変更は、検索や表示フィルターにかかわらず、すべてのブレンドシェイプへ適用します。";
        internal const string ValueCaptionTooltip =
            "スライダー上の縦線は初期値を示します。\n"
            + "2 フレームのときは、もう一方のフレームの値を中抜きのひし形で示します（始点の編集中は終点、終点の編集中は始点の値）。";

        /// <summary>
        /// システム除外理由ごとの呼び方。鍵アイコンと名前の tooltip には、この順で並べる。
        /// </summary>
        private static readonly SystemExclusionReasonName[] SystemExclusionReasonNames =
        {
            new SystemExclusionReasonName(BlendShapeSystemExclusionReason.Mmd, "MMD"),
            new SystemExclusionReasonName(BlendShapeSystemExclusionReason.LipSync, "リップシンク"),
            new SystemExclusionReasonName(BlendShapeSystemExclusionReason.EyeControl, "目"),
            new SystemExclusionReasonName(BlendShapeSystemExclusionReason.Empty, "空"),
            new SystemExclusionReasonName(BlendShapeSystemExclusionReason.Duplicate, "同名"),
        };

        private readonly ExpressionEditModel _model;
        private readonly VisualElement _header;
        private readonly HeaderTargetMenuButton _headerTargetButton;
        private readonly Clickable _headerTargetClickable;
        private readonly Toggle _headerTargetToggle;
        private readonly ExpressionEditorIconElement _headerMenuIcon;
        private readonly ListView _listView;
        private readonly Label _emptyLabel;
        private readonly List<BlendShapeEntry> _items = new List<BlendShapeEntry>();
        private string _searchText = "";
        private bool _showChangedOnly;
        private bool _showEditableOnly;
        private bool _showOutputOnly;
        private ExpressionEditFrame _editingFrame = ExpressionEditFrame.Start;
        private ExpressionFrameMode _refreshedFrameMode = ExpressionFrameMode.SingleFrame;
        private BlendShapeOutputMode _outputMode = BlendShapeOutputMode.AllTargets;
        private IReadOnlyDictionary<int, BlendShapeInfluenceResult> _spatialInfluenceResults;
        private IReadOnlyDictionary<BlendShapeEntry, BlendShapeOutputDecision> _outputDecisions =
            new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>();
        private int _activeUndoGroup = -1;

        public event Action Changed;
        public event Action<BlendShapeEntry> PreviewValueChanged;
        public event Action PreviewResetRequested;

        /// <summary>
        /// 一覧ヘッダーのメニューから一括変更を実行した後に通知する。結果の表示はウィンドウ側で行う。
        /// </summary>
        public event Action<BlendShapeBulkEditableTargetAction, BlendShapeBulkEditableTargetResult> BulkEditableTargetActionApplied;

        /// <summary>
        /// 表示する行を絞り込み直した後に通知する。件数が変わらない場合も通知する。
        /// 表示数や 0 件時の表示の更新はウィンドウ側で行う。
        /// </summary>
        public event Action VisibleEntriesChanged;

        public bool IsGroupedUndoActive => _activeUndoGroup >= 0;

        /// <summary>
        /// 絞り込みにかかわらない、一覧の全件数。一括変更メニューや表示数の件数表示に使う。
        /// </summary>
        public int TotalEntryCount => _model != null && _model.entries != null
            ? _model.entries.Count(entry => entry != null)
            : 0;

        /// <summary>
        /// 検索・表示フィルター・位置フィルタで絞り込んだ後、一覧に表示している件数。
        /// </summary>
        public int VisibleEntryCount => _items.Count;

        public BlendShapeListView(ExpressionEditModel model, VisualElement container, Label emptyLabel)
        {
            _model = model;
            _emptyLabel = emptyLabel;

            _header = CreateHeader(
                out _headerTargetButton,
                out _headerTargetClickable,
                out _headerTargetToggle,
                out _headerMenuIcon);
            container.Add(_header);

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

        /// <summary>
        /// 検索文字・表示フィルター・位置フィルタをまとめて設定し、一覧を 1 回だけ作り直す。
        /// <paramref name="spatialInfluenceResults"/> が null の場合は位置フィルタを外す。
        /// </summary>
        public void SetFilters(
            string searchText,
            bool showChangedOnly,
            bool showEditableOnly,
            bool showOutputOnly,
            IReadOnlyDictionary<int, BlendShapeInfluenceResult> spatialInfluenceResults)
        {
            _searchText = searchText ?? "";
            _showChangedOnly = showChangedOnly;
            _showEditableOnly = showEditableOnly;
            _showOutputOnly = showOutputOnly;
            _spatialInfluenceResults = spatialInfluenceResults;
            Refresh();
        }

        public void SetOutputDecisions(
            BlendShapeOutputMode outputMode,
            IReadOnlyDictionary<BlendShapeEntry, BlendShapeOutputDecision> outputDecisions)
        {
            _outputMode = outputMode;
            _outputDecisions = outputDecisions ?? new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>();
            if (HasVisibleItemsChanged())
            {
                Refresh();
                return;
            }

            _listView.RefreshItems();
        }

        public void SetSpatialInfluenceFilter(IReadOnlyDictionary<int, BlendShapeInfluenceResult> results)
        {
            _spatialInfluenceResults = results ?? new Dictionary<int, BlendShapeInfluenceResult>();
            Refresh();
        }

        public void ClearSpatialInfluenceFilter()
        {
            if (_spatialInfluenceResults == null)
            {
                return;
            }

            _spatialInfluenceResults = null;
            Refresh();
        }

        /// <summary>
        /// 一覧で編集するフレームを設定する。編集するフレームか、前回一覧を更新したときから表情のフレームモードが
        /// 変わっている場合は一覧を更新する（行の値と、もう一方のフレームの値の印を表示し直す）。
        /// </summary>
        public void SetEditingFrame(ExpressionEditFrame editingFrame)
        {
            if (_editingFrame == editingFrame && _refreshedFrameMode == GetFrameMode())
            {
                return;
            }

            _editingFrame = editingFrame;
            Refresh();
        }

        public void Refresh()
        {
            _refreshedFrameMode = GetFrameMode();
            _items.Clear();
            foreach (var entry in EnumerateVisibleEntries())
            {
                _items.Add(entry);
            }

            _listView.itemsSource = _items;
            _listView.Rebuild();
            _emptyLabel.EnableInClassList("hidden", _items.Count > 0);
            UpdateHeaderState();
            VisibleEntriesChanged?.Invoke();
        }

        private bool HasVisibleItemsChanged()
        {
            var index = 0;
            foreach (var entry in EnumerateVisibleEntries())
            {
                if (index >= _items.Count || !ReferenceEquals(_items[index], entry))
                {
                    return true;
                }

                index++;
            }

            return index != _items.Count;
        }

        private IEnumerable<BlendShapeEntry> EnumerateVisibleEntries()
        {
            if (_model == null)
            {
                return Enumerable.Empty<BlendShapeEntry>();
            }

            var entries = _model.entries.Where(MatchesFilter);
            if (_spatialInfluenceResults != null)
            {
                entries = entries
                    .OrderByDescending(entry => _spatialInfluenceResults[entry.index].Score)
                    .ThenBy(entry => entry.name, StringComparer.Ordinal);
            }

            return entries;
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

        public BlendShapeBulkEditableTargetResult ApplyBulkEditableTargetAction(
            BlendShapeBulkEditableTargetAction action)
        {
            if (_model == null || _model.entries == null)
            {
                return new BlendShapeBulkEditableTargetResult(0, 0);
            }

            EndGroupedUndo();

            var stateAction = GetEditableTargetStateAction(action);
            var changes = new List<EditableTargetChange>();
            var unavailableCount = 0;
            foreach (var entry in _model.entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (action == BlendShapeBulkEditableTargetAction.AllEditable && entry.IsDuplicateName)
                {
                    unavailableCount++;
                    continue;
                }

                var nextState = GetEditableTargetState(entry, stateAction);
                if (entry.systemExclusionUnlocked == nextState.SystemExclusionUnlocked
                    && entry.userExcluded == nextState.UserExcluded)
                {
                    continue;
                }

                changes.Add(new EditableTargetChange(entry, nextState));
            }

            if (changes.Count == 0)
            {
                return new BlendShapeBulkEditableTargetResult(0, unavailableCount);
            }

            var undoName = GetBulkEditableTargetUndoName(action);
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(_model, undoName);
            try
            {
                foreach (var change in changes)
                {
                    change.Apply();
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
            }

            EditorUtility.SetDirty(_model);
            Refresh();
            PreviewResetRequested?.Invoke();
            Changed?.Invoke();
            return new BlendShapeBulkEditableTargetResult(changes.Count, unavailableCount);
        }

        /// <summary>
        /// 一覧ヘッダーのチェックボックスに表示する、全件の編集・出力対象の状態を求める。
        /// 検索や表示フィルターには左右されない。同名の 2 個目以降は集計に含めない。
        /// </summary>
        internal BlendShapeTargetAggregateState GetTargetAggregateState()
        {
            var targetCount = 0;
            var excludedCount = 0;
            if (_model != null && _model.entries != null)
            {
                foreach (var entry in _model.entries)
                {
                    if (entry == null || entry.IsDuplicateName)
                    {
                        continue;
                    }

                    if (entry.ShouldOutput)
                    {
                        targetCount++;
                    }
                    else
                    {
                        excludedCount++;
                    }
                }
            }

            if (targetCount > 0 && excludedCount > 0)
            {
                return BlendShapeTargetAggregateState.Mixed;
            }

            return targetCount > 0
                ? BlendShapeTargetAggregateState.AllTargets
                : BlendShapeTargetAggregateState.NoTargets;
        }

        /// <summary>
        /// 「すべてを編集・出力対象にする」のメニュー項目名を、全件数を入れて返す。
        /// </summary>
        internal static string GetBulkAllEditableLabel(int totalCount)
        {
            return $"すべて（{totalCount} 件）を編集・出力対象にする";
        }

        /// <summary>
        /// 「すべてを編集・出力対象外にする」のメニュー項目名を、全件数を入れて返す。
        /// </summary>
        internal static string GetBulkAllExcludedLabel(int totalCount)
        {
            return $"すべて（{totalCount} 件）を編集・出力対象外にする";
        }

        /// <summary>
        /// 一覧ヘッダーの一括変更メニューを作る。件数は作成時点の全件数を使う。
        /// </summary>
        internal DropdownMenu CreateHeaderMenu()
        {
            var totalCount = TotalEntryCount;
            var menu = new DropdownMenu();
            menu.AppendAction(
                GetBulkAllEditableLabel(totalCount),
                _ => ApplyBulkEditableTargetActionFromHeaderMenu(BlendShapeBulkEditableTargetAction.AllEditable),
                GetHeaderMenuActionStatus);
            menu.AppendAction(
                GetBulkAllExcludedLabel(totalCount),
                _ => ApplyBulkEditableTargetActionFromHeaderMenu(BlendShapeBulkEditableTargetAction.AllExcluded),
                GetHeaderMenuActionStatus);
            menu.AppendAction(
                BulkRestoreEditingStartLabel,
                _ => ApplyBulkEditableTargetActionFromHeaderMenu(BlendShapeBulkEditableTargetAction.RestoreEditingStart),
                GetHeaderMenuActionStatus);
            menu.AppendSeparator();
            menu.AppendAction(
                ResetEditableValuesLabel,
                _ => ResetEditableValues(),
                GetHeaderMenuActionStatus);
            return menu;
        }

        /// <summary>
        /// 2 点間を移動するスライダーのつまみが、正規化した値の位置にあるときのつまみ中心の X 座標を求める。
        /// UI Toolkit の Slider は、つまみの左端を「つまみ置き場の幅 − つまみの幅」の範囲で動かすため、
        /// 両端ではつまみの幅の半分だけ内側が中心になる。座標はつまみの親要素の左端を 0 とする。
        /// </summary>
        internal static float CalculateSliderValueCenter(
            float normalizedValue,
            float containerWidth,
            float draggerWidth,
            float draggerMarginLeft,
            float draggerMarginRight)
        {
            var travel = Mathf.Max(0f, containerWidth - draggerWidth - draggerMarginLeft - draggerMarginRight);
            return draggerMarginLeft + draggerWidth * 0.5f + Mathf.Round(Mathf.Clamp01(normalizedValue) * travel);
        }

        private VisualElement CreateHeader(
            out HeaderTargetMenuButton targetButton,
            out Clickable targetClickable,
            out Toggle targetToggle,
            out ExpressionEditorIconElement menuIcon)
        {
            var header = new VisualElement();
            header.AddToClassList("blendshape-list-header");

            targetButton = new HeaderTargetMenuButton
            {
                tooltip = HeaderTargetTooltip,
            };
            targetButton.AddToClassList("blendshape-header-target");
            targetClickable = new Clickable(OpenHeaderMenu);
            targetButton.AddManipulator(targetClickable);
            header.Add(targetButton);

            // チェックボックスは状態の表示専用にし、クリックは親要素で受けてメニューを開く。
            targetToggle = new Toggle
            {
                focusable = false,
            };
            targetToggle.AddToClassList("blendshape-target-toggle");
            targetToggle.AddToClassList("blendshape-header-toggle");
            SetPickingModeIgnoreRecursive(targetToggle);
            targetToggle.RegisterValueChangedCallback(_ => UpdateHeaderState());
            targetButton.Add(targetToggle);

            menuIcon = new ExpressionEditorIconElement();
            menuIcon.AddToClassList("blendshape-header-menu-icon");
            menuIcon.SetPickingModeIgnore();
            targetButton.Add(menuIcon);

            var nameCaption = new Label("名前");
            nameCaption.AddToClassList("blendshape-header-name");
            header.Add(nameCaption);

            var valueCaption = new Label("値")
            {
                tooltip = ValueCaptionTooltip,
            };
            valueCaption.AddToClassList("blendshape-header-value");
            header.Add(valueCaption);

            return header;
        }

        private void OpenHeaderMenu()
        {
            if (_model == null)
            {
                return;
            }

            _headerTargetButton.menu = CreateHeaderMenu();
            _headerTargetButton.ShowMenu();
        }

        private DropdownMenuAction.Status GetHeaderMenuActionStatus(DropdownMenuAction action)
        {
            return _model != null && _model.entries != null
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;
        }

        private void ApplyBulkEditableTargetActionFromHeaderMenu(BlendShapeBulkEditableTargetAction action)
        {
            if (_model == null)
            {
                return;
            }

            var result = ApplyBulkEditableTargetAction(action);
            BulkEditableTargetActionApplied?.Invoke(action, result);
        }

        private void UpdateHeaderState()
        {
            var state = GetTargetAggregateState();
            _headerTargetToggle.SetValueWithoutNotify(state == BlendShapeTargetAggregateState.AllTargets);
            _headerTargetToggle.showMixedValue = state == BlendShapeTargetAggregateState.Mixed;

            // 混在表示の要素は showMixedValue を有効にしたときに作られるため、その都度クリックを受けないようにする。
            SetPickingModeIgnoreRecursive(_headerTargetToggle);
            _headerMenuIcon.SetIcon(ExpressionEditorIcons.Dropdown);
            _headerTargetButton.SetEnabled(_model != null);
        }

        private static void SetPickingModeIgnoreRecursive(VisualElement element)
        {
            element.Query<VisualElement>().ForEach(child => child.pickingMode = PickingMode.Ignore);
            element.pickingMode = PickingMode.Ignore;
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
            listView.RegisterCallback<GeometryChangedEvent>(evt => UpdateResponsiveLayout(evt.newRect.width));

            listView.AddToClassList("blendshape-list");
            return listView;
        }

        private void UpdateResponsiveLayout(float width)
        {
            var isNarrow = width > 0f && width < NarrowLayoutWidth;
            _listView.EnableInClassList(NarrowClassName, isNarrow);
            _header.EnableInClassList(NarrowClassName, isNarrow);
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

            if (_spatialInfluenceResults != null && !_spatialInfluenceResults.ContainsKey(entry.index))
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
            return IsValueChanged(entry)
                || entry.systemExclusionUnlocked
                || entry.userExcluded;
        }

        private static bool IsValueChanged(BlendShapeEntry entry)
        {
            return entry != null
                && (Mathf.Abs(entry.value - entry.initialValue) > ChangedTolerance
                    || Mathf.Abs(entry.endValue - entry.initialValue) > ChangedTolerance);
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

        private void RecordSingleUndo(string undoName = "表情ブレンドシェイプを編集")
        {
            if (_model == null || _activeUndoGroup >= 0)
            {
                return;
            }

            Undo.RecordObject(_model, undoName);
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

        private void ResetEntryValues(BlendShapeEntry entry)
        {
            if (_model == null || !CanEditValue(entry) || !IsValueChanged(entry))
            {
                return;
            }

            RecordSingleUndo("表情ブレンドシェイプをリセット");
            entry.value = entry.initialValue;
            entry.endValue = entry.initialValue;
            EditorUtility.SetDirty(_model);
            PreviewValueChanged?.Invoke(entry);
            Changed?.Invoke();
        }

        private ExpressionFrameMode GetFrameMode()
        {
            return _model != null ? _model.frameMode : ExpressionFrameMode.SingleFrame;
        }

        /// <summary>
        /// 2 フレームのとき、編集していない側のフレームの値（始点の編集中は終点、終点の編集中は始点の値）を返す。
        /// 1 フレームのときは false を返す。
        /// </summary>
        private bool TryGetOtherFrameValue(BlendShapeEntry entry, out float value)
        {
            value = 0f;
            if (entry == null || GetFrameMode() != ExpressionFrameMode.WeightBlend)
            {
                return false;
            }

            value = _editingFrame == ExpressionEditFrame.End ? entry.value : entry.endValue;
            return true;
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

        /// <summary>
        /// 鍵アイコンと名前の tooltip に出す除外理由。<see cref="SystemExclusionReasonNames"/> の順・呼び方で組み立てる。
        /// </summary>
        private static string GetSystemExclusionText(BlendShapeEntry entry)
        {
            if (entry == null || entry.systemExclusion == BlendShapeSystemExclusionReason.None)
            {
                return "";
            }

            return string.Join(
                ", ",
                SystemExclusionReasonNames
                    .Where(reasonName => (entry.systemExclusion & reasonName.Reason) != 0)
                    .Select(reasonName => reasonName.Name));
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
            private const string TargetToggleTooltip = "編集・出力対象にするかどうかを切り替える";
            private const string DuplicateTargetToggleTooltip =
                "同じ名前のブレンドシェイプは .anim 上で区別できないため、2 個目以降は編集・出力対象にできません。";
            private const string ResetButtonTooltip = "このブレンドシェイプの始点・終点を初期値に戻します。";
            private const string ResetUnavailableLockedTooltip =
                "システム除外のため戻せません。左のチェックボックスで編集・出力対象にすると戻せます。";
            private const string ResetUnavailableExcludedTooltip =
                "編集・出力対象外のため戻せません。左のチェックボックスで編集・出力対象にすると戻せます。";
            private const string ResetUnavailableDuplicateTooltip =
                "同じ名前のブレンドシェイプが先にあり、編集・出力対象にできないため戻せません。";
            private const string LockedTooltipHeading = "システム除外";
            private const string UnlockedTooltipHeading = "システム除外を解除中";
            private const string LockedTooltip =
                "編集・出力対象外になっています。左のチェックボックスで対象にできます。";
            private const string LockedDuplicateTooltip =
                "同じ名前のブレンドシェイプが先にあるため、編集・出力対象にできません。";
            private const string UnlockedTooltip =
                "編集・出力対象にしています。左のチェックボックスでシステム除外に戻せます。";

            private readonly BlendShapeListView _owner;
            private readonly Toggle _outputToggle;
            private readonly ExpressionEditorIconElement _lockIcon;
            private readonly Label _nameLabel;
            private readonly Slider _slider;
            private readonly VisualElement _sliderDragger;
            private readonly VisualElement _sliderTracker;
            private readonly VisualElement _initialValueTick;
            private readonly VisualElement _otherFrameMarker;
            private readonly FloatField _valueField;
            private readonly Button _entryResetButton;
            private readonly ExpressionEditorIconElement _entryResetIcon;
            private readonly Label _outputStatusLabel;
            private BlendShapeEntry _entry;
            private bool _ignoreChange;

            /// <summary>
            /// もう一方のフレームの値の印を最後に置いた値。印を隠しているときは null。
            /// レイアウト前で位置をまだ決められないときも、置く値として指定した値を持つ。
            /// テストで、編集・フレームの切り替え・割り当て直しの後に印を置き直したかを確かめるためのもので、通常の処理からは使わない。
            /// </summary>
            internal float? PlacedOtherFrameValue { get; private set; }

            public BlendShapeRow(BlendShapeListView owner)
            {
                _owner = owner;
                AddToClassList("blendshape-row");

                _outputToggle = new Toggle
                {
                    tooltip = TargetToggleTooltip,
                };
                _outputToggle.AddToClassList("blendshape-target-toggle");
                _outputToggle.RegisterValueChangedCallback(OnOutputChanged);
                Add(_outputToggle);

                var nameCell = new VisualElement();
                nameCell.AddToClassList("blendshape-name-cell");
                Add(nameCell);

                _lockIcon = new ExpressionEditorIconElement();
                _lockIcon.AddToClassList("blendshape-lock-icon");
                nameCell.Add(_lockIcon);

                _nameLabel = new Label();
                _nameLabel.AddToClassList("blendshape-name");
                nameCell.Add(_nameLabel);

                _slider = new Slider(0f, 100f);
                _slider.AddToClassList("blendshape-slider");
                _slider.RegisterValueChangedCallback(OnSliderChanged);
                _slider.RegisterCallback<PointerDownEvent>(_ => _owner.BeginGroupedUndo());
                _slider.RegisterCallback<PointerUpEvent>(_ => _owner.EndGroupedUndo());
                _slider.RegisterCallback<PointerCancelEvent>(_ => _owner.EndGroupedUndo());
                _slider.RegisterCallback<PointerCaptureOutEvent>(_ => _owner.EndGroupedUndo());
                Add(_slider);

                _initialValueTick = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                };
                _initialValueTick.AddToClassList("blendshape-initial-tick");
                _otherFrameMarker = new VisualElement
                {
                    pickingMode = PickingMode.Ignore,
                };
                _otherFrameMarker.AddToClassList("blendshape-other-frame-marker");
                _otherFrameMarker.AddToClassList(ExpressionEditorStyles.InvisibleClassName);
                _sliderDragger = _slider.Q(className: BaseSlider<float>.draggerUssClassName);
                _sliderTracker = _slider.Q(className: BaseSlider<float>.trackerUssClassName);
                var dragContainer = _sliderDragger?.parent;
                if (dragContainer != null)
                {
                    // つまみより下、溝より上に描くため、溝の直後へ入れる。もう一方のフレームの印は目盛りの上に重ねる。
                    var insertIndex = _sliderTracker != null && _sliderTracker.parent == dragContainer
                        ? dragContainer.IndexOf(_sliderTracker) + 1
                        : dragContainer.IndexOf(_sliderDragger);
                    dragContainer.Insert(insertIndex, _initialValueTick);
                    dragContainer.Insert(insertIndex + 1, _otherFrameMarker);
                    dragContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateSliderMarkers());
                }

                _valueField = new FloatField();
                _valueField.AddToClassList("blendshape-value");
                _valueField.RegisterValueChangedCallback(OnValueChanged);
                Add(_valueField);

                _entryResetButton = new Button(OnResetClicked)
                {
                    tooltip = ResetButtonTooltip,
                };
                _entryResetButton.AddToClassList("blendshape-reset-button");
                _entryResetIcon = new ExpressionEditorIconElement();
                _entryResetIcon.AddToClassList("blendshape-reset-icon");
                _entryResetIcon.SetPickingModeIgnore();
                _entryResetButton.Add(_entryResetIcon);
                Add(_entryResetButton);

                _outputStatusLabel = new Label();
                _outputStatusLabel.AddToClassList("blendshape-output-status");
                Add(_outputStatusLabel);
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
                _slider.SetValueWithoutNotify(displayValue);
                _valueField.SetValueWithoutNotify(displayValue);
                _outputToggle.SetValueWithoutNotify(entry.ShouldOutput);
                _outputToggle.tooltip = entry.IsDuplicateName
                    ? DuplicateTargetToggleTooltip
                    : TargetToggleTooltip;
                var outputStatusText = _owner.GetOutputStatusText(entry, out var outputStatusTooltip);
                _outputStatusLabel.text = outputStatusText;
                _outputStatusLabel.tooltip = outputStatusTooltip;
                var reasonText = GetSystemExclusionText(entry);
                _nameLabel.tooltip = GetNameTooltip(entry.name, outputStatusText, outputStatusTooltip, reasonText);
                UpdateLockIcon(entry, reasonText);

                _slider.SetEnabled(canEditValue);
                _valueField.SetEnabled(canEditValue);
                _entryResetIcon.SetIcon(ExpressionEditorIcons.Reset);
                UpdateResetButton(entry);
                _outputToggle.SetEnabled(!entry.IsDuplicateName);
                _outputStatusLabel.RemoveFromClassList("hidden");
                _outputStatusLabel.style.visibility = string.IsNullOrEmpty(outputStatusText)
                    ? Visibility.Hidden
                    : Visibility.Visible;
                EnableInClassList("system-excluded", isSystemLocked);
                EnableInClassList("system-unlocked", isSystemExcluded && !isSystemLocked);
                EnableInClassList("user-excluded", entry.userExcluded);
                EnableInClassList("changed", IsChanged(entry));
                EnableInClassList("output-scheduled", _owner.IsOutputScheduled(entry));
                EnableInClassList("output-skipped", _owner._outputMode != BlendShapeOutputMode.AllTargets && !_owner.IsOutputScheduled(entry));
                UpdateSliderMarkers();

                _ignoreChange = false;
            }

            private static string GetNameTooltip(
                string name,
                string outputStatusText,
                string outputStatusTooltip,
                string reasonText)
            {
                var details = new List<string> { name };
                if (!string.IsNullOrEmpty(outputStatusText))
                {
                    details.Add(string.IsNullOrEmpty(outputStatusTooltip)
                        ? outputStatusText
                        : $"{outputStatusText}: {outputStatusTooltip}");
                }

                if (!string.IsNullOrEmpty(reasonText))
                {
                    details.Add($"除外理由: {reasonText}");
                }

                return string.Join("\n", details);
            }

            /// <summary>
            /// システム除外の行は名前の前に鍵アイコンを出す。ロック解除済みの行は解錠アイコンにする。
            /// tooltip の 1 行目に除外理由（<paramref name="reasonText"/>）を出す。
            /// システム除外でない行も列幅を保つため、アイコンの場所は非表示（visibility）で残す。
            /// </summary>
            private void UpdateLockIcon(BlendShapeEntry entry, string reasonText)
            {
                if (!entry.IsSystemExcluded)
                {
                    _lockIcon.tooltip = "";
                    _lockIcon.AddToClassList(ExpressionEditorStyles.InvisibleClassName);
                    return;
                }

                var isLocked = entry.IsSystemLocked;
                _lockIcon.SetIcon(isLocked ? ExpressionEditorIcons.Locked : ExpressionEditorIcons.Unlocked);
                _lockIcon.tooltip = GetLockIconTooltip(entry, reasonText);
                _lockIcon.RemoveFromClassList(ExpressionEditorStyles.InvisibleClassName);
            }

            /// <summary>
            /// 鍵アイコンの tooltip。1 行目に状態と除外理由、2 行目にチェックボックスでできることを出す。
            /// </summary>
            private static string GetLockIconTooltip(BlendShapeEntry entry, string reasonText)
            {
                if (!entry.IsSystemLocked)
                {
                    return $"{UnlockedTooltipHeading}: {reasonText}\n{UnlockedTooltip}";
                }

                var detail = entry.IsDuplicateName ? LockedDuplicateTooltip : LockedTooltip;
                return $"{LockedTooltipHeading}: {reasonText}\n{detail}";
            }

            /// <summary>
            /// 値が初期値から変わっていない行では「戻す」ボタンを非表示にする。列幅は保つ。
            /// 値が変わっていても編集できない行では、表示したまま無効にし、tooltip を戻せない理由にする。
            /// </summary>
            private void UpdateResetButton(BlendShapeEntry entry)
            {
                var isValueChanged = IsValueChanged(entry);
                var canReset = CanEditValue(entry) && isValueChanged;
                _entryResetButton.SetEnabled(canReset);
                _entryResetButton.tooltip = canReset || !isValueChanged
                    ? ResetButtonTooltip
                    : GetResetUnavailableTooltip(entry);
                _entryResetButton.EnableInClassList(ExpressionEditorStyles.InvisibleClassName, !isValueChanged);
            }

            /// <summary>
            /// 値が変わっているのに「戻す」を使えない理由を返す。
            /// </summary>
            private static string GetResetUnavailableTooltip(BlendShapeEntry entry)
            {
                if (entry.IsDuplicateName)
                {
                    return ResetUnavailableDuplicateTooltip;
                }

                return entry.IsSystemLocked
                    ? ResetUnavailableLockedTooltip
                    : ResetUnavailableExcludedTooltip;
            }

            /// <summary>
            /// スライダー上の印（初期値の目盛りと、もう一方のフレームの値の印）を表示し直す。
            /// </summary>
            private void UpdateSliderMarkers()
            {
                UpdateInitialValueTick();
                UpdateOtherFrameMarker();
            }

            /// <summary>
            /// 初期値の目盛りを、スライダーのつまみが初期値にあるときの中心位置へ置く。
            /// </summary>
            private void UpdateInitialValueTick()
            {
                if (_entry == null)
                {
                    return;
                }

                PlaceSliderMarker(_initialValueTick, _entry.initialValue);
            }

            /// <summary>
            /// 2 フレームのときは、もう一方のフレームの値の印を、スライダーのつまみがその値にあるときの中心位置へ置いて表示する。
            /// 1 フレームのときと、行に項目を割り当てていないときは隠す。
            /// </summary>
            private void UpdateOtherFrameMarker()
            {
                var hasOtherFrame = _owner.TryGetOtherFrameValue(_entry, out var otherFrameValue);
                _otherFrameMarker.EnableInClassList(ExpressionEditorStyles.InvisibleClassName, !hasOtherFrame);
                PlacedOtherFrameValue = hasOtherFrame ? otherFrameValue : (float?)null;
                if (hasOtherFrame)
                {
                    PlaceSliderMarker(_otherFrameMarker, otherFrameValue);
                }
            }

            /// <summary>
            /// スライダー上の印を、つまみが <paramref name="value"/> にあるときの中心位置へ置く。縦位置は溝の中心に合わせる。
            /// レイアウトが確定していない間は何もしない（ジオメトリ変更時に再計算する）。
            /// </summary>
            private void PlaceSliderMarker(VisualElement marker, float value)
            {
                if (_sliderDragger == null || marker.parent == null)
                {
                    return;
                }

                var containerLayout = marker.parent.layout;
                var containerWidth = containerLayout.width;
                if (float.IsNaN(containerWidth) || containerWidth <= 0f)
                {
                    return;
                }

                var draggerStyle = _sliderDragger.resolvedStyle;
                var normalizedValue = Mathf.InverseLerp(_slider.lowValue, _slider.highValue, value);
                if (_slider.inverted)
                {
                    normalizedValue = 1f - normalizedValue;
                }

                var center = CalculateSliderValueCenter(
                    normalizedValue,
                    containerWidth,
                    GetFiniteOrZero(draggerStyle.width),
                    GetFiniteOrZero(draggerStyle.marginLeft),
                    GetFiniteOrZero(draggerStyle.marginRight));
                var markerWidth = GetFiniteOrZero(marker.resolvedStyle.width);
                marker.style.left = center - markerWidth * 0.5f;

                // 溝が見つからない場合はつまみ置き場の中心にする。
                var centerY = GetFiniteOrZero(containerLayout.height) * 0.5f;
                if (_sliderTracker != null && _sliderTracker.parent == marker.parent)
                {
                    var trackerLayout = _sliderTracker.layout;
                    if (!float.IsNaN(trackerLayout.y) && !float.IsNaN(trackerLayout.height))
                    {
                        centerY = trackerLayout.y + trackerLayout.height * 0.5f;
                    }
                }

                var markerHeight = GetFiniteOrZero(marker.resolvedStyle.height);
                marker.style.top = centerY - markerHeight * 0.5f;
            }

            private static float GetFiniteOrZero(float value)
            {
                return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
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

                var entry = _entry;
                _owner.SetValue(entry, evt.newValue);
                if (_entry != entry)
                {
                    return;
                }

                _ignoreChange = true;
                try
                {
                    _valueField.SetValueWithoutNotify(_owner.GetDisplayValue(entry));
                    UpdateResetButton(entry);
                    UpdateOtherFrameMarker();
                    EnableInClassList("changed", IsChanged(entry));
                }
                finally
                {
                    _ignoreChange = false;
                }
            }

            private void OnValueChanged(ChangeEvent<float> evt)
            {
                if (_ignoreChange || _entry == null)
                {
                    return;
                }

                var entry = _entry;
                _owner.SetValue(entry, evt.newValue);
                if (_entry != entry)
                {
                    return;
                }

                _ignoreChange = true;
                try
                {
                    var displayValue = _owner.GetDisplayValue(entry);
                    _slider.SetValueWithoutNotify(displayValue);
                    _valueField.SetValueWithoutNotify(displayValue);
                    UpdateResetButton(entry);
                    UpdateOtherFrameMarker();
                    EnableInClassList("changed", IsChanged(entry));
                }
                finally
                {
                    _ignoreChange = false;
                }
            }

            private void OnOutputChanged(ChangeEvent<bool> evt)
            {
                if (_ignoreChange || _entry == null)
                {
                    return;
                }

                var entry = _entry;
                _owner.SetEditableTarget(entry, evt.newValue);
                if (_entry != entry)
                {
                    return;
                }

                Bind(entry);
            }

            private void OnResetClicked()
            {
                if (_entry == null)
                {
                    return;
                }

                var entry = _entry;
                _owner.ResetEntryValues(entry);
                if (_entry != entry)
                {
                    return;
                }

                Bind(entry);
            }
        }

        private void SetEditableTarget(BlendShapeEntry entry, bool editableTarget)
        {
            if (entry == null || entry.IsDuplicateName)
            {
                return;
            }

            var nextState = GetEditableTargetState(
                entry,
                editableTarget ? EditableTargetStateAction.Editable : EditableTargetStateAction.Excluded);
            if (entry.systemExclusionUnlocked == nextState.SystemExclusionUnlocked
                && entry.userExcluded == nextState.UserExcluded)
            {
                return;
            }

            RecordSingleUndo();
            entry.systemExclusionUnlocked = nextState.SystemExclusionUnlocked;
            entry.userExcluded = nextState.UserExcluded;
            EditorUtility.SetDirty(_model);
            UpdateHeaderState();
            PreviewResetRequested?.Invoke();
            Changed?.Invoke();
        }

        private static EditableTargetStateAction GetEditableTargetStateAction(
            BlendShapeBulkEditableTargetAction action)
        {
            switch (action)
            {
                case BlendShapeBulkEditableTargetAction.AllEditable:
                    return EditableTargetStateAction.Editable;
                case BlendShapeBulkEditableTargetAction.AllExcluded:
                    return EditableTargetStateAction.Excluded;
                case BlendShapeBulkEditableTargetAction.RestoreEditingStart:
                    return EditableTargetStateAction.RestoreEditingStart;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private static EditableTargetState GetEditableTargetState(
            BlendShapeEntry entry,
            EditableTargetStateAction action)
        {
            if (entry.IsSystemExcluded)
            {
                return action == EditableTargetStateAction.Editable
                    ? new EditableTargetState(true, false)
                    : new EditableTargetState(false, false);
            }

            return action == EditableTargetStateAction.Excluded
                ? new EditableTargetState(false, true)
                : new EditableTargetState(false, false);
        }

        private static string GetBulkEditableTargetUndoName(BlendShapeBulkEditableTargetAction action)
        {
            switch (action)
            {
                case BlendShapeBulkEditableTargetAction.AllEditable:
                    return "ブレンドシェイプをすべて編集・出力対象に変更";
                case BlendShapeBulkEditableTargetAction.AllExcluded:
                    return "ブレンドシェイプをすべて編集・出力対象外に変更";
                case BlendShapeBulkEditableTargetAction.RestoreEditingStart:
                    return "ブレンドシェイプの対象設定を編集開始時点に戻す";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private enum EditableTargetStateAction
        {
            Editable,
            Excluded,
            RestoreEditingStart,
        }

        private struct EditableTargetState
        {
            public EditableTargetState(bool systemExclusionUnlocked, bool userExcluded)
            {
                SystemExclusionUnlocked = systemExclusionUnlocked;
                UserExcluded = userExcluded;
            }

            public bool SystemExclusionUnlocked { get; }
            public bool UserExcluded { get; }
        }

        private struct EditableTargetChange
        {
            private readonly BlendShapeEntry _entry;
            private readonly EditableTargetState _state;

            public EditableTargetChange(BlendShapeEntry entry, EditableTargetState state)
            {
                _entry = entry;
                _state = state;
            }

            public void Apply()
            {
                _entry.systemExclusionUnlocked = _state.SystemExclusionUnlocked;
                _entry.userExcluded = _state.UserExcluded;
            }
        }

        private readonly struct SystemExclusionReasonName
        {
            public SystemExclusionReasonName(BlendShapeSystemExclusionReason reason, string name)
            {
                Reason = reason;
                Name = name;
            }

            public BlendShapeSystemExclusionReason Reason { get; }
            public string Name { get; }
        }

        /// <summary>
        /// 一覧ヘッダー左端の、チェックボックスと ▾ をまとめたクリック領域。クリックで一括変更メニューを開く。
        /// </summary>
        private sealed class HeaderTargetMenuButton : VisualElement, IToolbarMenuElement
        {
            public DropdownMenu menu { get; set; } = new DropdownMenu();
        }
    }
}
