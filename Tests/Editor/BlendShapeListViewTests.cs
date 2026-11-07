using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class BlendShapeListViewTests
    {
        private readonly List<ExpressionEditModel> _models = new List<ExpressionEditModel>();
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private readonly List<string> _assetPaths = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var assetPath in _assetPaths)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            _assetPaths.Clear();

            foreach (var model in _models)
            {
                if (model == null)
                {
                    continue;
                }

                Undo.ClearUndo(model);
                UnityEngine.Object.DestroyImmediate(model);
            }

            _models.Clear();

            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ApplyBulkEditableTargetAction_AllExcludedAndAllEditable_NormalizeEveryEntry()
        {
            var model = CreateModel(
                CreateEntry("Normal"),
                CreateEntry("Mmd", BlendShapeSystemExclusionReason.Mmd, systemExclusionUnlocked: true, userExcluded: true),
                CreateEntry("Duplicate", BlendShapeSystemExclusionReason.Duplicate, systemExclusionUnlocked: true, userExcluded: true));
            var listView = CreateListView(model);

            var excludedResult = listView.ApplyBulkEditableTargetAction(
                BlendShapeBulkEditableTargetAction.AllExcluded);

            Assert.That(excludedResult.ChangedCount, Is.EqualTo(3));
            Assert.That(excludedResult.UnavailableCount, Is.Zero);
            Assert.That(model.entries[0].systemExclusionUnlocked, Is.False);
            Assert.That(model.entries[0].userExcluded, Is.True);
            Assert.That(model.entries[1].systemExclusionUnlocked, Is.False);
            Assert.That(model.entries[1].userExcluded, Is.False);
            Assert.That(model.entries[2].systemExclusionUnlocked, Is.False);
            Assert.That(model.entries[2].userExcluded, Is.False);

            var editableResult = listView.ApplyBulkEditableTargetAction(
                BlendShapeBulkEditableTargetAction.AllEditable);

            Assert.That(editableResult.ChangedCount, Is.EqualTo(2));
            Assert.That(editableResult.UnavailableCount, Is.EqualTo(1));
            Assert.That(model.entries[0].ShouldOutput, Is.True);
            Assert.That(model.entries[1].systemExclusionUnlocked, Is.True);
            Assert.That(model.entries[1].ShouldOutput, Is.True);
            Assert.That(model.entries[2].systemExclusionUnlocked, Is.False);
            Assert.That(model.entries[2].ShouldOutput, Is.False);
        }

        [Test]
        public void ApplyBulkEditableTargetAction_RestoreEditingStart_PreservesBlendShapeValues()
        {
            var normal = CreateEntry("Normal", userExcluded: true);
            normal.value = 30f;
            normal.endValue = 70f;
            normal.initialValue = 10f;
            normal.hasSourceCurve = true;
            normal.sourceValue = 5f;
            normal.sourceEndValue = 15f;
            var systemExcluded = CreateEntry(
                "LipSync",
                BlendShapeSystemExclusionReason.LipSync,
                systemExclusionUnlocked: true,
                userExcluded: true);
            var duplicate = CreateEntry(
                "Duplicate",
                BlendShapeSystemExclusionReason.Duplicate,
                systemExclusionUnlocked: true,
                userExcluded: true);
            var model = CreateModel(normal, systemExcluded, duplicate);
            var listView = CreateListView(model);

            var result = listView.ApplyBulkEditableTargetAction(
                BlendShapeBulkEditableTargetAction.RestoreEditingStart);

            Assert.That(result.ChangedCount, Is.EqualTo(3));
            Assert.That(result.UnavailableCount, Is.Zero);
            Assert.That(normal.systemExclusionUnlocked, Is.False);
            Assert.That(normal.userExcluded, Is.False);
            Assert.That(normal.ShouldOutput, Is.True);
            Assert.That(systemExcluded.systemExclusionUnlocked, Is.False);
            Assert.That(systemExcluded.userExcluded, Is.False);
            Assert.That(systemExcluded.ShouldOutput, Is.False);
            Assert.That(duplicate.systemExclusionUnlocked, Is.False);
            Assert.That(duplicate.userExcluded, Is.False);
            Assert.That(duplicate.ShouldOutput, Is.False);
            Assert.That(normal.value, Is.EqualTo(30f));
            Assert.That(normal.endValue, Is.EqualTo(70f));
            Assert.That(normal.initialValue, Is.EqualTo(10f));
            Assert.That(normal.hasSourceCurve, Is.True);
            Assert.That(normal.sourceValue, Is.EqualTo(5f));
            Assert.That(normal.sourceEndValue, Is.EqualTo(15f));
        }

        [Test]
        public void ApplyBulkEditableTargetAction_AppliesToEntriesHiddenByEveryListFilter()
        {
            var visible = CreateEntry("Visible");
            visible.systemExclusionUnlocked = true;
            var hiddenBySearch = CreateEntry("HiddenBySearch");
            var hiddenByScene = CreateEntry("HiddenByScene");
            var model = CreateModel(visible, hiddenBySearch, hiddenByScene);
            var listView = CreateListView(model);
            listView.SetSearchText("Visible");
            listView.SetShowChangedOnly(true);
            listView.SetShowEditableOnly(true);
            listView.SetSpatialInfluenceFilter(
                new Dictionary<int, BlendShapeInfluenceResult>
                {
                    [visible.index] = new BlendShapeInfluenceResult(visible.index, visible.name, 1f, 1f),
                });
            listView.SetOutputDecisions(
                BlendShapeOutputMode.SessionBaselineDiff,
                new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>
                {
                    [visible] = CreateOutputDecision(visible, true),
                    [hiddenBySearch] = CreateOutputDecision(hiddenBySearch, false),
                    [hiddenByScene] = CreateOutputDecision(hiddenByScene, false),
                });
            listView.SetShowOutputOnly(true);

            Assert.That(GetVisibleItems(listView), Is.EqualTo(new[] { visible }));

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);

            Assert.That(model.entries.All(entry => entry.userExcluded), Is.True);
        }

        [Test]
        public void ApplyBulkEditableTargetAction_RefreshesEditableAndChangedFiltersImmediately()
        {
            var model = CreateModel(CreateEntry("Smile"), CreateEntry("Blink"));
            var listView = CreateListView(model);
            listView.SetShowEditableOnly(true);

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);

            Assert.That(GetVisibleItems(listView), Is.Empty);

            listView.SetShowEditableOnly(false);
            listView.SetShowChangedOnly(true);

            Assert.That(GetVisibleItems(listView), Has.Count.EqualTo(2));

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.RestoreEditingStart);

            Assert.That(GetVisibleItems(listView), Is.Empty);
        }

        [Test]
        public void ApplyBulkEditableTargetAction_NotifiesOnceAndNoOpDoesNotDirtyOrNotify()
        {
            var model = CreateModel(CreateEntry("Smile"), CreateEntry("Blink"));
            var listView = CreateListView(model);
            var changedCount = 0;
            var previewResetCount = 0;
            listView.Changed += () => changedCount++;
            listView.PreviewResetRequested += () => previewResetCount++;
            var dirtyCountBeforeNoOp = EditorUtility.GetDirtyCount(model);

            var noOpResult = listView.ApplyBulkEditableTargetAction(
                BlendShapeBulkEditableTargetAction.AllEditable);

            Assert.That(noOpResult.ChangedCount, Is.Zero);
            Assert.That(EditorUtility.GetDirtyCount(model), Is.EqualTo(dirtyCountBeforeNoOp));
            Assert.That(changedCount, Is.Zero);
            Assert.That(previewResetCount, Is.Zero);

            var changedResult = listView.ApplyBulkEditableTargetAction(
                BlendShapeBulkEditableTargetAction.AllExcluded);

            Assert.That(changedResult.ChangedCount, Is.EqualTo(2));
            Assert.That(EditorUtility.GetDirtyCount(model), Is.GreaterThan(dirtyCountBeforeNoOp));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(previewResetCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyBulkEditableTargetAction_EachActionIsOneIndependentUndoOperation()
        {
            var model = CreateModel(CreateEntry("Smile"), CreateEntry("Blink"));
            var listView = CreateListView(model);

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);
            Undo.FlushUndoRecordObjects();
            Assert.That(model.entries.All(entry => entry.userExcluded), Is.True);

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllEditable);
            Undo.FlushUndoRecordObjects();
            Assert.That(model.entries.All(entry => !entry.userExcluded), Is.True);

            Undo.PerformUndo();
            Assert.That(model.entries.All(entry => entry.userExcluded), Is.True);

            Undo.PerformUndo();
            Assert.That(model.entries.All(entry => !entry.userExcluded), Is.True);

            Undo.PerformRedo();
            Assert.That(model.entries.All(entry => entry.userExcluded), Is.True);

            Undo.PerformRedo();
            Assert.That(model.entries.All(entry => !entry.userExcluded), Is.True);
        }

        [TestCase(BlendShapeBulkEditableTargetAction.AllEditable)]
        [TestCase(BlendShapeBulkEditableTargetAction.AllExcluded)]
        [TestCase(BlendShapeBulkEditableTargetAction.RestoreEditingStart)]
        public void ApplyBulkEditableTargetAction_EachActionSupportsSingleStepUndoRedo(
            BlendShapeBulkEditableTargetAction action)
        {
            var normal = CreateEntry("Normal");
            var systemExcluded = CreateEntry("Mmd", BlendShapeSystemExclusionReason.Mmd);
            var duplicate = CreateEntry("Duplicate", BlendShapeSystemExclusionReason.Duplicate);
            PrepareEntriesForBulkAction(action, normal, systemExcluded, duplicate);
            var model = CreateModel(normal, systemExcluded, duplicate);
            var listView = CreateListView(model);
            var before = GetTargetStateSnapshot(model);

            listView.ApplyBulkEditableTargetAction(action);
            Undo.FlushUndoRecordObjects();
            var after = GetTargetStateSnapshot(model);

            Assert.That(after, Is.Not.EqualTo(before));

            Undo.PerformUndo();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo(before));

            Undo.PerformRedo();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo(after));
        }

        [Test]
        public void ApplyBulkEditableTargetAction_DoesNotMergeWithAdjacentIndividualOperations()
        {
            var smile = CreateEntry("Smile");
            var blink = CreateEntry("Blink");
            var model = CreateModel(smile, blink);
            var listView = CreateListView(model);

            InvokeListViewMethod(listView, "SetEditableTarget", smile, false);
            Undo.FlushUndoRecordObjects();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo("False,True;False,False"));

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);
            Undo.FlushUndoRecordObjects();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo("False,True;False,True"));

            InvokeListViewMethod(listView, "SetEditableTarget", smile, true);
            Undo.FlushUndoRecordObjects();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo("False,False;False,True"));

            Undo.PerformUndo();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo("False,True;False,True"));

            Undo.PerformUndo();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo("False,True;False,False"));

            Undo.PerformUndo();
            Assert.That(GetTargetStateSnapshot(model), Is.EqualTo("False,False;False,False"));
        }

        [Test]
        public void ApplyBulkEditableTargetAction_SaveToAnimationClip_ExcludesTargetsAndPreservesSourceCurves()
        {
            var sourceClip = new AnimationClip { name = "BulkTargetSource" };
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.Smile",
                new AnimationCurve(new Keyframe(0f, 25f)));
            var lockedSourceCurve = new AnimationCurve(
                new Keyframe(0.2f, 40f, 0.5f, 0.75f),
                new Keyframe(1.5f, 60f, -0.25f, -0.5f))
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.PingPong,
            };
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.Locked",
                lockedSourceCurve);
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.LegacyOnly",
                new AnimationCurve(new Keyframe(0f, 15f), new Keyframe(1f, 30f)));
            sourceClip.SetCurve(
                "Arm",
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2f)));

            var model = CreateClipModel(sourceClip, "Smile", "Locked");
            var locked = model.entries.First(entry => entry.name == "Locked");
            locked.systemExclusion = BlendShapeSystemExclusionReason.Mmd;
            var listView = CreateListView(model);

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);

            var outputClip = ExpressionClipIO.ToClip(model, "BulkTargetOutput");
            _createdObjects.Add(outputClip);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/BlendShapeBulkTargetToggle_SaveIntegration.anim");
            _assetPaths.Add(assetPath);
            ExpressionClipIO.SaveClipToAsset(outputClip, assetPath);
            var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

            Assert.That(savedClip, Is.Not.Null);
            Assert.That(GetBlendShapeCurve(savedClip, "Face", "Smile"), Is.Null);
            AssertCurveMatches(GetBlendShapeCurve(savedClip, "Face", "Locked"), lockedSourceCurve);

            var legacyCurve = GetBlendShapeCurve(savedClip, "Face", "LegacyOnly");
            Assert.That(legacyCurve, Is.Not.Null);
            Assert.That(legacyCurve.length, Is.EqualTo(2));
            Assert.That(legacyCurve.keys[0].value, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(legacyCurve.keys[1].time, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(legacyCurve.keys[1].value, Is.EqualTo(30f).Within(0.0001f));

            var transformBinding = model.preservedCurves.First(curve => curve.path == "Arm").ToBinding();
            var transformCurve = AnimationUtility.GetEditorCurve(savedClip, transformBinding);
            Assert.That(transformCurve, Is.Not.Null);
            Assert.That(transformCurve.keys[1].value, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void ApplyBulkEditableTargetAction_OutputOnlyUsesReevaluatedDecisionMap()
        {
            var model = CreateModel(CreateEntry("Smile"), CreateEntry("Blink"));
            var listView = CreateListView(model);
            var settings = new ExpressionOutputSettings { mode = BlendShapeOutputMode.AllTargets };
            listView.SetOutputDecisions(
                settings.mode,
                ExpressionOutputDiffService.ToDecisionMap(ExpressionOutputDiffService.Evaluate(model, settings)));
            listView.SetShowOutputOnly(true);
            Assert.That(GetVisibleItems(listView), Has.Count.EqualTo(2));

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);

            Assert.That(GetVisibleItems(listView), Has.Count.EqualTo(2), "再評価前は古い判定を維持する");

            listView.SetOutputDecisions(
                settings.mode,
                ExpressionOutputDiffService.ToDecisionMap(ExpressionOutputDiffService.Evaluate(model, settings)));

            Assert.That(GetVisibleItems(listView), Is.Empty);
        }

        [Test]
        public void SetEditableTarget_DuplicateNameRemainsLocked()
        {
            var duplicate = CreateEntry("Duplicate", BlendShapeSystemExclusionReason.Duplicate);
            var model = CreateModel(duplicate);
            var listView = CreateListView(model);

            InvokeListViewMethod(listView, "SetEditableTarget", duplicate, true);

            Assert.That(duplicate.systemExclusionUnlocked, Is.False);
            Assert.That(duplicate.userExcluded, Is.False);
            Assert.That(duplicate.ShouldOutput, Is.False);
        }

        [Test]
        public void HeaderMenu_UsesJapaneseLabelsWithTotalCountAndResultMessage()
        {
            var model = CreateModel(
                CreateEntry("Smile"),
                CreateEntry("Blink", BlendShapeSystemExclusionReason.LipSync),
                CreateEntry("Smile", BlendShapeSystemExclusionReason.Duplicate));
            var listView = CreateListView(model);
            listView.SetSearchText("Blink");

            var items = listView.CreateHeaderMenu().MenuItems();

            Assert.That(items, Has.Count.EqualTo(5));
            Assert.That(items[3], Is.InstanceOf<DropdownMenuSeparator>(), "一括変更と値のリセットを区切る");
            Assert.That(
                items.OfType<DropdownMenuAction>().Select(action => action.name),
                Is.EqualTo(new[]
                {
                    "すべて（3 件）を編集・出力対象にする",
                    "すべて（3 件）を編集・出力対象外にする",
                    "編集開始時点の状態に戻す",
                    "編集可能なすべての値を初期値に戻す",
                }),
                "件数は絞り込みにかかわらず全件数を表示する");
            Assert.That(listView.TotalEntryCount, Is.EqualTo(3));
            foreach (var action in items.OfType<DropdownMenuAction>())
            {
                action.UpdateActionStatus(null);
                Assert.That(action.status, Is.EqualTo(DropdownMenuAction.Status.Normal), action.name);
            }

            var method = typeof(FacialExpressionEditorWindow).GetMethod(
                "GetBulkEditableTargetResultMessage",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var message = (string)method.Invoke(
                null,
                new object[]
                {
                    BlendShapeBulkEditableTargetAction.AllEditable,
                    new BlendShapeBulkEditableTargetResult(126, 2),
                });

            Assert.That(message, Is.EqualTo("126 件を編集・出力対象にしました。 同名のため変更できない項目: 2 件"));
        }

        [Test]
        public void HeaderMenu_BulkActionsUseSameLogicAndNotifyResult()
        {
            var normal = CreateEntry("Normal");
            var systemExcluded = CreateEntry("Mmd", BlendShapeSystemExclusionReason.Mmd);
            var duplicate = CreateEntry("Duplicate", BlendShapeSystemExclusionReason.Duplicate);
            var model = CreateModel(normal, systemExcluded, duplicate);
            var listView = CreateListView(model);
            listView.SetSearchText("Normal");
            var notifications = new List<(BlendShapeBulkEditableTargetAction Action, BlendShapeBulkEditableTargetResult Result)>();
            listView.BulkEditableTargetActionApplied += (action, result) => notifications.Add((action, result));

            ExecuteHeaderMenuAction(listView, BlendShapeListView.GetBulkAllEditableLabel(3));

            Assert.That(normal.ShouldOutput, Is.True);
            Assert.That(systemExcluded.systemExclusionUnlocked, Is.True);
            Assert.That(duplicate.ShouldOutput, Is.False);
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].Action, Is.EqualTo(BlendShapeBulkEditableTargetAction.AllEditable));
            Assert.That(notifications[0].Result.ChangedCount, Is.EqualTo(1));
            Assert.That(notifications[0].Result.UnavailableCount, Is.EqualTo(1));

            ExecuteHeaderMenuAction(listView, BlendShapeListView.GetBulkAllExcludedLabel(3));

            Assert.That(model.entries.All(entry => !entry.ShouldOutput), Is.True, "非表示の行にも適用する");
            Assert.That(notifications[1].Action, Is.EqualTo(BlendShapeBulkEditableTargetAction.AllExcluded));
            Assert.That(notifications[1].Result.ChangedCount, Is.EqualTo(2));

            ExecuteHeaderMenuAction(listView, BlendShapeListView.BulkRestoreEditingStartLabel);

            Assert.That(normal.ShouldOutput, Is.True);
            Assert.That(systemExcluded.ShouldOutput, Is.False);
            Assert.That(notifications[2].Action, Is.EqualTo(BlendShapeBulkEditableTargetAction.RestoreEditingStart));
            Assert.That(notifications[2].Result.ChangedCount, Is.EqualTo(1));
        }

        [Test]
        public void HeaderMenu_ResetEditableValuesResetsAllEditableEntriesAsOneUndo()
        {
            var visible = CreateEntry("Visible");
            visible.initialValue = 10f;
            visible.value = 60f;
            visible.endValue = 60f;
            var hidden = CreateEntry("Hidden");
            hidden.value = 30f;
            hidden.endValue = 30f;
            var locked = CreateEntry("Locked", BlendShapeSystemExclusionReason.Mmd);
            locked.value = 40f;
            locked.endValue = 40f;
            var model = CreateModel(visible, hidden, locked);
            var listView = CreateListView(model);
            listView.SetSearchText("Visible");
            var changedCount = 0;
            listView.Changed += () => changedCount++;

            ExecuteHeaderMenuAction(listView, BlendShapeListView.ResetEditableValuesLabel);
            Undo.FlushUndoRecordObjects();

            Assert.That(visible.value, Is.EqualTo(10f));
            Assert.That(visible.endValue, Is.EqualTo(10f));
            Assert.That(hidden.value, Is.Zero);
            Assert.That(locked.value, Is.EqualTo(40f), "編集できない行の値は変えない");
            Assert.That(changedCount, Is.EqualTo(1));

            Undo.PerformUndo();

            Assert.That(visible.value, Is.EqualTo(60f));
            Assert.That(hidden.value, Is.EqualTo(30f));
        }

        [Test]
        public void HeaderAggregateState_ReflectsAllEntriesRegardlessOfFiltersAndIgnoresDuplicates()
        {
            var smile = CreateEntry("Smile");
            var blink = CreateEntry("Blink");
            var duplicate = CreateEntry("Smile", BlendShapeSystemExclusionReason.Duplicate);
            var model = CreateModel(smile, blink, duplicate);
            var listView = CreateListView(model);

            AssertHeaderState(listView, BlendShapeTargetAggregateState.AllTargets);

            blink.userExcluded = true;
            listView.SetSearchText("Smile");

            AssertHeaderState(listView, BlendShapeTargetAggregateState.Mixed);

            smile.userExcluded = true;
            listView.Refresh();

            AssertHeaderState(listView, BlendShapeTargetAggregateState.NoTargets);

            var onlyDuplicate = CreateModel(CreateEntry("Duplicate", BlendShapeSystemExclusionReason.Duplicate));

            AssertHeaderState(CreateListView(onlyDuplicate), BlendShapeTargetAggregateState.NoTargets);
        }

        [Test]
        public void HeaderAggregateState_UpdatesAfterRowToggleBulkActionAndUndoRedo()
        {
            var smile = CreateEntry("Smile");
            var blink = CreateEntry("Blink");
            var model = CreateModel(smile, blink);
            var listView = CreateListView(model);

            InvokeListViewMethod(listView, "SetEditableTarget", smile, false);
            Undo.FlushUndoRecordObjects();

            AssertHeaderState(listView, BlendShapeTargetAggregateState.Mixed);

            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);
            Undo.FlushUndoRecordObjects();

            AssertHeaderState(listView, BlendShapeTargetAggregateState.NoTargets);

            Undo.PerformUndo();
            listView.Refresh();

            AssertHeaderState(listView, BlendShapeTargetAggregateState.Mixed);

            Undo.PerformRedo();
            listView.Refresh();

            AssertHeaderState(listView, BlendShapeTargetAggregateState.NoTargets);

            ExecuteHeaderMenuAction(listView, BlendShapeListView.GetBulkAllEditableLabel(2));

            AssertHeaderState(listView, BlendShapeTargetAggregateState.AllTargets);
        }

        [Test]
        public void VisibleEntryCount_FollowsFiltersAndNotifiesWhenVisibleEntriesAreRefreshed()
        {
            var smile = CreateEntry("Smile");
            var blink = CreateEntry("Blink");
            var angry = CreateEntry("Angry");
            var model = CreateModel(smile, blink, angry);
            var listView = CreateListView(model);
            var notificationCount = 0;
            listView.VisibleEntriesChanged += () => notificationCount++;

            Assert.That(listView.TotalEntryCount, Is.EqualTo(3));
            Assert.That(listView.VisibleEntryCount, Is.EqualTo(3));

            listView.SetSearchText("i");

            Assert.That(listView.VisibleEntryCount, Is.EqualTo(2));
            Assert.That(notificationCount, Is.EqualTo(1));

            listView.SetShowEditableOnly(true);
            listView.ApplyBulkEditableTargetAction(BlendShapeBulkEditableTargetAction.AllExcluded);

            Assert.That(listView.VisibleEntryCount, Is.Zero, "一括変更の後も表示数を合わせる");
            Assert.That(listView.TotalEntryCount, Is.EqualTo(3), "全件数は絞り込みに左右されない");
            Assert.That(notificationCount, Is.EqualTo(3));

            listView.SetShowEditableOnly(false);
            listView.SetSearchText("");
            listView.SetSpatialInfluenceFilter(
                new Dictionary<int, BlendShapeInfluenceResult>
                {
                    [angry.index] = new BlendShapeInfluenceResult(angry.index, angry.name, 1f, 1f),
                });

            Assert.That(listView.VisibleEntryCount, Is.EqualTo(1));
            Assert.That(notificationCount, Is.EqualTo(6));

            listView.ClearSpatialInfluenceFilter();

            Assert.That(listView.VisibleEntryCount, Is.EqualTo(3));
            Assert.That(notificationCount, Is.EqualTo(7));

            listView.SetOutputDecisions(
                BlendShapeOutputMode.AllTargets,
                new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>());

            Assert.That(notificationCount, Is.EqualTo(7), "表示する行が変わらなければ作り直さない");
        }

        [Test]
        public void Header_ShowsCaptionsAndOpensMenuInsteadOfTogglingCheckbox()
        {
            var model = CreateModel(CreateEntry("Smile"));
            var container = new VisualElement();
            var listView = new BlendShapeListView(model, container, new Label());
            var header = GetListViewField<VisualElement>(listView, "_header");
            var headerButton = GetListViewField<VisualElement>(listView, "_headerTargetButton");
            var headerToggle = GetListViewField<Toggle>(listView, "_headerTargetToggle");
            var menuIcon = GetListViewField<ExpressionEditorIconElement>(listView, "_headerMenuIcon");

            Assert.That(container.IndexOf(header), Is.Zero, "ヘッダーは一覧の上に置く");
            Assert.That(container.IndexOf(GetListViewField<ListView>(listView, "_listView")), Is.EqualTo(1));
            Assert.That(header.Query<Label>().ToList().Select(label => label.text), Is.SupersetOf(new[] { "名前", "値" }));
            Assert.That(
                header.Query<Label>().ToList().Single(label => label.text == "値").tooltip,
                Is.EqualTo(BlendShapeListView.ValueCaptionTooltip),
                "「値」の tooltip で、初期値の目盛りともう一方のフレームの印を説明する");
            Assert.That(headerButton.tooltip, Is.EqualTo(BlendShapeListView.HeaderTargetTooltip));
            Assert.That(headerButton.Contains(headerToggle), Is.True);
            Assert.That(headerButton.Contains(menuIcon), Is.True);
            Assert.That(headerToggle.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(headerToggle.focusable, Is.False);
            Assert.That(
                headerToggle.Query<VisualElement>().ToList().All(element => element.pickingMode == PickingMode.Ignore),
                Is.True,
                "チェックボックスはクリックを受け取らず、親のクリック領域でメニューを開く");
            Assert.That(menuIcon.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(menuIcon.Icon, Is.SameAs(ExpressionEditorIcons.Dropdown));
            Assert.That(headerButton, Is.InstanceOf<UnityEditor.UIElements.IToolbarMenuElement>());

            // パネルに載っていない要素ではクリックのイベントを送れないため、クリック時の処理の登録先を確認する。
            var clickable = GetListViewField<Clickable>(listView, "_headerTargetClickable");
            var clickedField = typeof(Clickable).GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(clickedField, Is.Not.Null, "Clickable.clicked was not found.");
            var clicked = (Action)clickedField.GetValue(clickable);
            Assert.That(clickable.target, Is.SameAs(headerButton), "クリックはチェックボックスと ▾ をまとめた領域で受ける");
            Assert.That(clicked, Is.Not.Null);
            Assert.That(clicked.Target, Is.SameAs(listView));
            Assert.That(clicked.Method.Name, Is.EqualTo("OpenHeaderMenu"), "クリックで一括変更メニューを開く");
        }

        [Test]
        public void SetFilters_AppliesAllFiltersWithSingleRefresh()
        {
            var smile = CreateEntry("Smile");
            smile.value = 50f;
            smile.endValue = 50f;
            var blink = CreateEntry("Blink");
            var model = CreateModel(smile, blink, CreateEntry("Angry"));
            var listView = CreateListView(model);
            var refreshCount = 0;
            listView.VisibleEntriesChanged += () => refreshCount++;
            var spatialResults = new Dictionary<int, BlendShapeInfluenceResult>
            {
                [smile.index] = new BlendShapeInfluenceResult(smile.index, smile.name, 1f, 1f),
                [blink.index] = new BlendShapeInfluenceResult(blink.index, blink.name, 0.5f, 1f),
            };

            listView.SetFilters("i", true, true, false, spatialResults);

            Assert.That(refreshCount, Is.EqualTo(1));
            Assert.That(GetVisibleItems(listView), Is.EqualTo(new[] { smile }));

            listView.SetFilters("", false, false, false, null);

            Assert.That(refreshCount, Is.EqualTo(2));
            Assert.That(listView.VisibleEntryCount, Is.EqualTo(3), "null は位置フィルタを外す");
        }

        [Test]
        public void Row_ResetButton_DisabledRowTooltipExplainsWhyValueCannotBeReset()
        {
            const string resetTooltip = "このブレンドシェイプの始点・終点を初期値に戻します。";
            var entry = CreateEntry("Smile");
            entry.initialValue = 10f;
            entry.value = 60f;
            entry.endValue = 60f;
            var model = CreateModel(entry);
            var listView = CreateListView(model);
            var row = CreateBoundRow(listView, entry);
            var resetButton = GetRowField<Button>(row, "_entryResetButton");

            Assert.That(resetButton.enabledSelf, Is.True);
            Assert.That(resetButton.tooltip, Is.EqualTo(resetTooltip));

            entry.userExcluded = true;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(resetButton.enabledSelf, Is.False);
            Assert.That(resetButton.ClassListContains("invisible"), Is.False);
            Assert.That(resetButton.tooltip, Is.EqualTo("編集・出力対象外のため戻せません。左のチェックボックスで編集・出力対象にすると戻せます。"));

            entry.userExcluded = false;
            entry.systemExclusion = BlendShapeSystemExclusionReason.Mmd;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(resetButton.enabledSelf, Is.False);
            Assert.That(resetButton.tooltip, Is.EqualTo("システム除外のため戻せません。左のチェックボックスで編集・出力対象にすると戻せます。"));

            entry.systemExclusion = BlendShapeSystemExclusionReason.Duplicate;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(resetButton.tooltip, Is.EqualTo("同じ名前のブレンドシェイプが先にあり、編集・出力対象にできないため戻せません。"));

            entry.systemExclusion = BlendShapeSystemExclusionReason.Mmd;
            entry.systemExclusionUnlocked = true;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(resetButton.enabledSelf, Is.True, "ロック解除した行は戻せる");
            Assert.That(resetButton.tooltip, Is.EqualTo(resetTooltip));
        }

        [TestCase(BlendShapeSystemExclusionReason.Mmd, "MMD")]
        [TestCase(BlendShapeSystemExclusionReason.LipSync, "リップシンク")]
        [TestCase(BlendShapeSystemExclusionReason.EyeControl, "目")]
        [TestCase(BlendShapeSystemExclusionReason.Empty, "空")]
        [TestCase(BlendShapeSystemExclusionReason.Duplicate, "同名")]
        [TestCase(
            BlendShapeSystemExclusionReason.Empty | BlendShapeSystemExclusionReason.LipSync | BlendShapeSystemExclusionReason.Mmd,
            "MMD, リップシンク, 空")]
        [TestCase(
            BlendShapeSystemExclusionReason.Duplicate
                | BlendShapeSystemExclusionReason.Empty
                | BlendShapeSystemExclusionReason.EyeControl
                | BlendShapeSystemExclusionReason.LipSync
                | BlendShapeSystemExclusionReason.Mmd,
            "MMD, リップシンク, 目, 空, 同名")]
        public void Row_Bind_SystemExcludedRowShowsLockIconWithReasonsInTooltips(
            BlendShapeSystemExclusionReason reason,
            string expectedReasons)
        {
            var entry = CreateEntry("Excluded", reason);
            var model = CreateModel(entry);
            var listView = CreateListView(model);
            var row = CreateBoundRow(listView, entry);
            var toggle = GetRowField<Toggle>(row, "_outputToggle");
            var lockIcon = GetRowField<ExpressionEditorIconElement>(row, "_lockIcon");
            var nameLabel = GetRowField<Label>(row, "_nameLabel");
            var isDuplicate = (reason & BlendShapeSystemExclusionReason.Duplicate) != 0;
            var lockedTooltip = isDuplicate
                ? $"システム除外: {expectedReasons}\n同じ名前のブレンドシェイプが先にあるため、編集・出力対象にできません。"
                : $"システム除外: {expectedReasons}\n編集・出力対象外になっています。左のチェックボックスで対象にできます。";

            AssertRowHasNoExclusionBadges(row);
            Assert.That(lockIcon.ClassListContains("invisible"), Is.False);
            Assert.That(lockIcon.Icon, Is.SameAs(ExpressionEditorIcons.Locked));
            Assert.That(lockIcon.tooltip, Is.EqualTo(lockedTooltip), "鍵アイコンの tooltip に除外理由を定義順で出す");
            Assert.That(toggle.value, Is.False);
            Assert.That(toggle.text, Is.Null.Or.Empty);
            Assert.That(row.IndexOf(toggle), Is.Zero, "編集・出力対象のチェックボックスは左端に置く");
            Assert.That(
                nameLabel.tooltip,
                Does.Contain($"除外理由: {expectedReasons}"),
                "名前の tooltip にも除外理由を定義順で出す");
            Assert.That(row.ClassListContains("system-excluded"), Is.True);

            Assert.That(toggle.enabledSelf, Is.EqualTo(!isDuplicate));
            Assert.That(
                toggle.tooltip,
                Is.EqualTo(isDuplicate
                    ? "同じ名前のブレンドシェイプは .anim 上で区別できないため、2 個目以降は編集・出力対象にできません。"
                    : "編集・出力対象にするかどうかを切り替える"));

            entry.systemExclusionUnlocked = true;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(
                lockIcon.Icon,
                Is.SameAs(isDuplicate ? ExpressionEditorIcons.Locked : ExpressionEditorIcons.Unlocked),
                "同名はロック解除できないため鍵アイコンのまま");
            Assert.That(lockIcon.ClassListContains("invisible"), Is.False);
            Assert.That(
                lockIcon.tooltip,
                Is.EqualTo(isDuplicate
                    ? lockedTooltip
                    : $"システム除外を解除中: {expectedReasons}\n編集・出力対象にしています。左のチェックボックスでシステム除外に戻せます。"),
                "ロック解除後も鍵アイコンの tooltip で除外理由を確認できる");
            Assert.That(toggle.value, Is.EqualTo(!isDuplicate));
            Assert.That(nameLabel.tooltip, Does.Contain($"除外理由: {expectedReasons}"), "ロック解除後も名前の tooltip に除外理由を残す");
            AssertRowHasNoExclusionBadges(row);
        }

        [Test]
        public void Row_Bind_NormalRowShowsCheckedToggleWithoutLockIcon()
        {
            var entry = CreateEntry("Smile");
            var model = CreateModel(entry);
            var listView = CreateListView(model);
            var row = CreateBoundRow(listView, entry);
            var toggle = GetRowField<Toggle>(row, "_outputToggle");
            var lockIcon = GetRowField<ExpressionEditorIconElement>(row, "_lockIcon");
            var nameLabel = GetRowField<Label>(row, "_nameLabel");

            Assert.That(toggle.value, Is.True);
            Assert.That(toggle.enabledSelf, Is.True);
            Assert.That(toggle.ClassListContains("blendshape-target-toggle"), Is.True);
            Assert.That(lockIcon.ClassListContains("invisible"), Is.True, "鍵アイコンの列幅は残す");
            Assert.That(lockIcon.ClassListContains("hidden"), Is.False);
            Assert.That(lockIcon.tooltip, Is.Empty);
            Assert.That(nameLabel.tooltip, Is.EqualTo("Smile"));
            AssertRowHasNoExclusionBadges(row);
            Assert.That(row.ClassListContains("changed"), Is.False);
            Assert.That(row.Q(className: "blendshape-reason"), Is.Null, "右端の除外理由列は廃止した");
            Assert.That(row.Q(className: "blendshape-output-toggle"), Is.Null);

            entry.userExcluded = true;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(toggle.value, Is.False);
            Assert.That(row.ClassListContains("changed"), Is.True);
            Assert.That(lockIcon.ClassListContains("invisible"), Is.True);
        }

        [Test]
        public void Row_Bind_ChangedClassFollowsValueAndTargetChanges()
        {
            var entry = CreateEntry("Smile");
            entry.initialValue = 20f;
            entry.value = 20f;
            entry.endValue = 20f;
            var model = CreateModel(entry);
            var listView = CreateListView(model);
            var row = CreateBoundRow(listView, entry);
            var resetButton = GetRowField<Button>(row, "_entryResetButton");

            Assert.That(row.ClassListContains("changed"), Is.False);
            Assert.That(resetButton.ClassListContains("invisible"), Is.True);

            entry.value = 50f;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(row.ClassListContains("changed"), Is.True);
            Assert.That(resetButton.ClassListContains("invisible"), Is.False);
            Assert.That(resetButton.enabledSelf, Is.True);

            entry.value = 20f;
            entry.systemExclusion = BlendShapeSystemExclusionReason.Mmd;
            entry.systemExclusionUnlocked = true;
            InvokeRowMethod(row, "Bind", entry);

            Assert.That(row.ClassListContains("changed"), Is.True, "ロック解除も変更として示す");
            Assert.That(resetButton.ClassListContains("invisible"), Is.True, "値が変わっていなければ戻すボタンは隠す");
        }

        [Test]
        public void Row_InitialValueTick_IsInsideSliderTrackAndIgnoresPointer()
        {
            var entry = CreateEntry("Smile");
            entry.initialValue = 30f;
            var model = CreateModel(entry);
            var listView = CreateListView(model);
            var row = CreateBoundRow(listView, entry);
            var slider = GetRowField<Slider>(row, "_slider");
            var tick = GetRowField<VisualElement>(row, "_initialValueTick");
            var dragger = slider.Q(className: BaseSlider<float>.draggerUssClassName);

            Assert.That(tick.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(tick.ClassListContains("blendshape-initial-tick"), Is.True);
            Assert.That(dragger, Is.Not.Null);
            Assert.That(tick.parent, Is.SameAs(dragger.parent), "つまみと同じ座標系に置く");
            Assert.That(tick.parent.IndexOf(tick), Is.LessThan(tick.parent.IndexOf(dragger)), "つまみより下に描く");
        }

        [Test]
        public void Row_OtherFrameMarker_ShowsOtherFrameValueOnlyInTwoFrameMode()
        {
            var smile = CreateEntry("Smile");
            smile.initialValue = 10f;
            smile.value = 20f;
            smile.endValue = 80f;
            var blink = CreateEntry("Blink");
            blink.value = 5f;
            blink.endValue = 45f;
            var model = CreateModel(smile, blink);
            var listView = CreateListView(model);
            var refreshCount = 0;
            listView.VisibleEntriesChanged += () => refreshCount++;
            var row = CreateBoundRow(listView, smile);
            var slider = GetRowField<Slider>(row, "_slider");
            var tick = GetRowField<VisualElement>(row, "_initialValueTick");
            var marker = GetRowField<VisualElement>(row, "_otherFrameMarker");
            var dragger = slider.Q(className: BaseSlider<float>.draggerUssClassName);

            Assert.That(marker.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(marker.ClassListContains("blendshape-other-frame-marker"), Is.True);
            Assert.That(marker.ClassListContains("blendshape-initial-tick"), Is.False, "初期値の目盛りとは別の見た目にする");
            Assert.That(marker.parent, Is.SameAs(dragger.parent), "つまみと同じ座標系に置く");
            Assert.That(marker.parent.IndexOf(marker), Is.GreaterThan(marker.parent.IndexOf(tick)), "初期値の目盛りより上に描く");
            Assert.That(marker.parent.IndexOf(marker), Is.LessThan(marker.parent.IndexOf(dragger)), "つまみより下に描く");
            AssertOtherFrameMarkerHidden(row, "1 フレームでは出さない");

            model.frameMode = ExpressionFrameMode.WeightBlend;
            listView.SetEditingFrame(ExpressionEditFrame.Start);

            Assert.That(refreshCount, Is.EqualTo(1), "フレームモードが変わったら、編集するフレームが同じでも一覧を更新する");

            InvokeRowMethod(row, "Bind", smile);

            AssertOtherFrameMarker(row, 80f, "始点の編集中は終点の値を示す");
            Assert.That(slider.value, Is.EqualTo(20f), "つまみは編集中のフレームの値");

            listView.SetEditingFrame(ExpressionEditFrame.End);
            InvokeRowMethod(row, "Bind", smile);

            Assert.That(refreshCount, Is.EqualTo(2));
            AssertOtherFrameMarker(row, 20f, "終点の編集中は始点の値を示す");
            Assert.That(slider.value, Is.EqualTo(80f));

            Undo.IncrementCurrentGroup();
            InvokeRowFloatHandler(row, "OnSliderChanged", 60f);
            Undo.FlushUndoRecordObjects();

            Assert.That(smile.endValue, Is.EqualTo(60f));
            AssertOtherFrameMarker(row, 20f, "編集中のフレームの値を変えても、もう一方のフレームの印は動かない");

            listView.SetEditingFrame(ExpressionEditFrame.Start);
            InvokeRowMethod(row, "Bind", smile);

            AssertOtherFrameMarker(row, 60f, "フレームを切り替えると、編集した終点の値を示す");

            Undo.IncrementCurrentGroup();
            InvokeRowFloatHandler(row, "OnValueChanged", 30f);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            Assert.That(smile.value, Is.EqualTo(30f));
            AssertOtherFrameMarker(row, 60f);

            listView.SetEditingFrame(ExpressionEditFrame.End);
            InvokeRowMethod(row, "Bind", smile);

            AssertOtherFrameMarker(row, 30f, "編集した始点の値を示す");

            Undo.PerformUndo();
            var undoneSmile = model.entries.Single(entry => entry.name == "Smile");
            listView.Refresh();
            InvokeRowMethod(row, "Bind", undoneSmile);

            Assert.That(undoneSmile.value, Is.EqualTo(20f));
            AssertOtherFrameMarker(row, 20f, "Undo の後は戻した値を示す");

            Undo.PerformRedo();
            var redoneSmile = model.entries.Single(entry => entry.name == "Smile");
            listView.Refresh();
            InvokeRowMethod(row, "Bind", redoneSmile);

            AssertOtherFrameMarker(row, 30f, "Redo の後はやり直した値を示す");

            // 一覧の行の使い回しと同じく、割り当てを外してから別の項目を割り当てる。
            var currentBlink = model.entries.Single(entry => entry.name == "Blink");
            InvokeRowMethod(row, "Unbind");
            InvokeRowMethod(row, "Bind", currentBlink);

            AssertOtherFrameMarker(row, 5f, "別の項目を割り当て直すと、その項目の値を示す");

            InvokeRowMethod(row, "Unbind");
            InvokeRowMethod(row, "Bind", redoneSmile);

            AssertOtherFrameMarker(row, 30f, "元の項目を割り当て直すと、その項目の値を示す");

            Undo.IncrementCurrentGroup();
            InvokeRowMethod(row, "OnResetClicked");
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            Assert.That(redoneSmile.value, Is.EqualTo(10f));
            Assert.That(redoneSmile.endValue, Is.EqualTo(10f));
            AssertOtherFrameMarker(row, 10f, "行で両方のフレームを初期値に戻すと、もう一方のフレームの印も初期値へ置き直す");

            listView.SetEditingFrame(ExpressionEditFrame.Start);
            model.frameMode = ExpressionFrameMode.SingleFrame;
            var refreshCountBeforeSingleFrame = refreshCount;
            listView.SetEditingFrame(ExpressionEditFrame.Start);
            InvokeRowMethod(row, "Bind", currentBlink);

            Assert.That(refreshCount, Is.EqualTo(refreshCountBeforeSingleFrame + 1), "1 フレームに戻したときも、編集するフレームが同じでも一覧を更新する");
            AssertOtherFrameMarkerHidden(row, "1 フレームに戻したら出さない");

            listView.SetEditingFrame(ExpressionEditFrame.Start);

            Assert.That(refreshCount, Is.EqualTo(refreshCountBeforeSingleFrame + 1), "フレームもフレームモードも変わらなければ更新しない");
        }

        [TestCase(0f, 200f, 10f, 0f, 0f, 5f)]
        [TestCase(1f, 200f, 10f, 0f, 0f, 195f)]
        [TestCase(0.5f, 200f, 10f, 0f, 0f, 100f)]
        [TestCase(0.25f, 110f, 10f, 2f, 2f, 31f)]
        [TestCase(1.5f, 100f, 20f, 0f, 0f, 90f)]
        [TestCase(0.5f, 8f, 10f, 0f, 0f, 5f)]
        public void CalculateSliderValueCenter_AccountsForDraggerWidthAndMargins(
            float normalizedValue,
            float containerWidth,
            float draggerWidth,
            float draggerMarginLeft,
            float draggerMarginRight,
            float expectedCenter)
        {
            var center = BlendShapeListView.CalculateSliderValueCenter(
                normalizedValue,
                containerWidth,
                draggerWidth,
                draggerMarginLeft,
                draggerMarginRight);

            Assert.That(center, Is.EqualTo(expectedCenter).Within(0.0001f));
        }

        [Test]
        public void ExpressionEditorIcons_AllIconsResolveForBothSkins()
        {
            Assert.That(
                ExpressionEditorIcons.All,
                Is.SupersetOf(new[]
                {
                    ExpressionEditorIcons.SaveMenu,
                    ExpressionEditorIcons.PreviewVisible,
                    ExpressionEditorIcons.PreviewHidden,
                    ExpressionEditorIcons.Warning,
                    ExpressionEditorIcons.StartFrameMenu,
                }),
                "ドキュメントバー・下部タブ・フレームバーのアイコンも確認する");

            foreach (var icon in ExpressionEditorIcons.All)
            {
                Assert.That(icon.FallbackText, Is.Not.Empty);
                Assert.That(
                    EditorGUIUtility.FindTexture(icon.DarkSkinName),
                    Is.Not.Null,
                    $"Dark skin icon was not found: {icon.DarkSkinName}");
                Assert.That(
                    EditorGUIUtility.FindTexture(icon.LightSkinName),
                    Is.Not.Null,
                    $"Light skin icon was not found: {icon.LightSkinName}");

                var element = new ExpressionEditorIconElement();
                element.SetIcon(icon);

                Assert.That(element.Texture, Is.Not.Null, icon.LightSkinName);
                Assert.That(element.FallbackText, Is.Empty);
                Assert.That(element.ClassListContains(ExpressionEditorIconElement.FallbackUssClassName), Is.False);
            }
        }

        [Test]
        public void ExpressionEditorIcons_FallbackTextsAreDistinct()
        {
            var fallbackTexts = ExpressionEditorIcons.All.Select(icon => icon.FallbackText).ToList();

            Assert.That(fallbackTexts, Is.Unique, "アイコンを読み込めない場合でも、代わりの文字で区別できるようにする");
            Assert.That(ExpressionEditorIcons.Unlocked.FallbackText, Is.EqualTo("解錠"));
        }

        [Test]
        public void ExpressionEditorIcons_PreferHighResolution_UsesAt2xWhenAvailable()
        {
            var refresh = EditorGUIUtility.FindTexture("d_Refresh");
            var refreshAt2x = EditorGUIUtility.FindTexture("d_Refresh@2x");
            Assert.That(refreshAt2x, Is.Not.Null, "d_Refresh@2x is expected in the Unity 2022.3 editor resources.");
            Assert.That(refreshAt2x, Is.Not.SameAs(refresh));

            Assert.That(ExpressionEditorIcons.LoadTexture("d_Refresh", true), Is.SameAs(refreshAt2x), "高 DPI では @2x を優先する");
            Assert.That(ExpressionEditorIcons.LoadTexture("d_Refresh", false), Is.SameAs(refresh));

            foreach (var icon in ExpressionEditorIcons.All)
            {
                foreach (var iconName in new[] { icon.DarkSkinName, icon.LightSkinName })
                {
                    var highResolution = EditorGUIUtility.FindTexture(iconName + "@2x");
                    var expected = highResolution != null ? highResolution : EditorGUIUtility.FindTexture(iconName);
                    Assert.That(expected, Is.Not.Null, iconName);
                    Assert.That(ExpressionEditorIcons.LoadTexture(iconName, true), Is.SameAs(expected), iconName);
                }
            }

            // @2x の無い組み込みアイコンで、通常の画像に戻ることを確かめる。
            var normalOnlyIconName = new[] { "CrossIcon", "editicon.sml", "d_editicon.sml" }
                .FirstOrDefault(name => EditorGUIUtility.FindTexture(name) != null
                    && EditorGUIUtility.FindTexture(name + "@2x") == null);
            Assume.That(normalOnlyIconName, Is.Not.Null, "No built-in icon without an @2x variant was found.");
            Assert.That(
                ExpressionEditorIcons.LoadTexture(normalOnlyIconName, true),
                Is.SameAs(EditorGUIUtility.FindTexture(normalOnlyIconName)),
                "@2x が無いアイコンは通常の画像を使う");
        }

        [Test]
        public void ExpressionEditorIconElement_UsesJapaneseFallbackWhenIconIsMissing()
        {
            var element = new ExpressionEditorIconElement();
            var missingIcon = new ExpressionEditorIcon("", "", "戻す");

            element.SetIcon(missingIcon);

            Assert.That(element.Texture, Is.Null);
            Assert.That(element.FallbackText, Is.EqualTo("戻す"));
            Assert.That(element.ClassListContains(ExpressionEditorIconElement.FallbackUssClassName), Is.True);
        }

        private ExpressionEditModel CreateModel(params BlendShapeEntry[] entries)
        {
            var model = ExpressionEditModel.Create();
            model.entries.AddRange(entries);
            _models.Add(model);
            return model;
        }

        private ExpressionEditModel CreateClipModel(AnimationClip sourceClip, params string[] blendShapeNames)
        {
            var avatarRoot = new GameObject("Avatar");
            var face = new GameObject("Face");
            face.transform.SetParent(avatarRoot.transform, false);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);

            var model = ExpressionClipIO.Load(sourceClip, avatarRoot, renderer);
            _models.Add(model);
            _createdObjects.Add(sourceClip);
            _createdObjects.Add(renderer.sharedMesh);
            _createdObjects.Add(avatarRoot);
            return model;
        }

        private static Mesh CreateMesh(IEnumerable<string> blendShapeNames)
        {
            var mesh = new Mesh
            {
                name = "BulkTargetTestMesh",
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            };
            var deltaVertices = new[] { Vector3.forward, Vector3.zero, Vector3.zero };
            var deltaNormals = new Vector3[3];
            var deltaTangents = new Vector3[3];
            foreach (var blendShapeName in blendShapeNames)
            {
                mesh.AddBlendShapeFrame(
                    blendShapeName,
                    100f,
                    deltaVertices,
                    deltaNormals,
                    deltaTangents);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static BlendShapeEntry CreateEntry(
            string name,
            BlendShapeSystemExclusionReason systemExclusion = BlendShapeSystemExclusionReason.None,
            bool systemExclusionUnlocked = false,
            bool userExcluded = false)
        {
            return new BlendShapeEntry
            {
                index = _nextEntryIndex++,
                name = name,
                systemExclusion = systemExclusion,
                systemExclusionUnlocked = systemExclusionUnlocked,
                userExcluded = userExcluded,
            };
        }

        private static BlendShapeListView CreateListView(ExpressionEditModel model)
        {
            return new BlendShapeListView(model, new VisualElement(), new Label());
        }

        private static BlendShapeOutputDecision CreateOutputDecision(BlendShapeEntry entry, bool shouldWriteCurve)
        {
            return new BlendShapeOutputDecision
            {
                entry = entry,
                shouldWriteCurve = shouldWriteCurve,
            };
        }

        private static void PrepareEntriesForBulkAction(
            BlendShapeBulkEditableTargetAction action,
            BlendShapeEntry normal,
            BlendShapeEntry systemExcluded,
            BlendShapeEntry duplicate)
        {
            switch (action)
            {
                case BlendShapeBulkEditableTargetAction.AllEditable:
                    normal.userExcluded = true;
                    break;
                case BlendShapeBulkEditableTargetAction.AllExcluded:
                    systemExcluded.systemExclusionUnlocked = true;
                    duplicate.systemExclusionUnlocked = true;
                    break;
                case BlendShapeBulkEditableTargetAction.RestoreEditingStart:
                    normal.userExcluded = true;
                    systemExcluded.systemExclusionUnlocked = true;
                    duplicate.systemExclusionUnlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        private static string GetTargetStateSnapshot(ExpressionEditModel model)
        {
            return string.Join(
                ";",
                model.entries.Select(entry => $"{entry.systemExclusionUnlocked},{entry.userExcluded}"));
        }

        private static AnimationCurve GetBlendShapeCurve(
            AnimationClip clip,
            string path,
            string blendShapeName)
        {
            return AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    path,
                    typeof(SkinnedMeshRenderer),
                    $"blendShape.{blendShapeName}"));
        }

        private static void AssertCurveMatches(AnimationCurve actual, AnimationCurve expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.length, Is.EqualTo(expected.length));
            Assert.That(actual.preWrapMode, Is.EqualTo(expected.preWrapMode));
            Assert.That(actual.postWrapMode, Is.EqualTo(expected.postWrapMode));
            for (var i = 0; i < expected.length; i++)
            {
                Assert.That(actual.keys[i].time, Is.EqualTo(expected.keys[i].time).Within(0.0001f));
                Assert.That(actual.keys[i].value, Is.EqualTo(expected.keys[i].value).Within(0.0001f));
                Assert.That(actual.keys[i].inTangent, Is.EqualTo(expected.keys[i].inTangent).Within(0.0001f));
                Assert.That(actual.keys[i].outTangent, Is.EqualTo(expected.keys[i].outTangent).Within(0.0001f));
            }
        }

        private static IReadOnlyList<BlendShapeEntry> GetVisibleItems(BlendShapeListView listView)
        {
            var field = typeof(BlendShapeListView).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IReadOnlyList<BlendShapeEntry>)field.GetValue(listView);
        }

        private static void InvokeListViewMethod(BlendShapeListView listView, string methodName, params object[] args)
        {
            var method = typeof(BlendShapeListView).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            try
            {
                method.Invoke(listView, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static void ExecuteHeaderMenuAction(BlendShapeListView listView, string actionName)
        {
            var action = listView.CreateHeaderMenu()
                .MenuItems()
                .OfType<DropdownMenuAction>()
                .SingleOrDefault(item => item.name == actionName);
            Assert.That(action, Is.Not.Null, $"Header menu action was not found: {actionName}");
            action.UpdateActionStatus(null);
            Assert.That(action.status, Is.EqualTo(DropdownMenuAction.Status.Normal));
            action.Execute();
        }

        private static void AssertHeaderState(BlendShapeListView listView, BlendShapeTargetAggregateState expectedState)
        {
            var headerToggle = GetListViewField<Toggle>(listView, "_headerTargetToggle");
            Assert.That(listView.GetTargetAggregateState(), Is.EqualTo(expectedState));
            Assert.That(headerToggle.value, Is.EqualTo(expectedState == BlendShapeTargetAggregateState.AllTargets));
            Assert.That(headerToggle.showMixedValue, Is.EqualTo(expectedState == BlendShapeTargetAggregateState.Mixed));
            Assert.That(
                headerToggle.Query<VisualElement>().ToList().All(element => element.pickingMode == PickingMode.Ignore),
                Is.True,
                "混在表示の要素もクリックを受け取らない");
        }

        private static T GetListViewField<T>(BlendShapeListView listView, string fieldName)
        {
            var field = typeof(BlendShapeListView).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field was not found: {fieldName}");
            return (T)field.GetValue(listView);
        }

        private static VisualElement CreateBoundRow(BlendShapeListView listView, BlendShapeEntry entry)
        {
            var rowType = typeof(BlendShapeListView).GetNestedType("BlendShapeRow", BindingFlags.NonPublic);
            Assert.That(rowType, Is.Not.Null);
            var row = (VisualElement)Activator.CreateInstance(
                rowType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { listView },
                null);
            InvokeRowMethod(row, "Bind", entry);
            return row;
        }

        private static T GetRowField<T>(VisualElement row, string fieldName)
        {
            var field = row.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field was not found: {fieldName}");
            return (T)field.GetValue(row);
        }

        /// <summary>
        /// 行のスライダー・数値欄の変更時の処理を、UI から値を変えた場合と同じ値の変更イベントで呼ぶ。
        /// </summary>
        private static void InvokeRowFloatHandler(VisualElement row, string methodName, float newValue)
        {
            using (var evt = ChangeEvent<float>.GetPooled(0f, newValue))
            {
                InvokeRowMethod(row, methodName, evt);
            }
        }

        /// <summary>
        /// もう一方のフレームの印が表示され、行が最後に印を置いた値が <paramref name="expectedValue"/> であることを確かめる。
        /// 期待値は一覧の処理から求めず、テストで項目に設定した値（編集していない側のフレームの値）を渡す。
        /// </summary>
        private static void AssertOtherFrameMarker(VisualElement row, float expectedValue, string message = null)
        {
            var marker = GetRowField<VisualElement>(row, "_otherFrameMarker");
            Assert.That(marker.ClassListContains(ExpressionEditorStyles.InvisibleClassName), Is.False, message);
            var placedValue = GetRowPlacedOtherFrameValue(row);
            Assert.That(placedValue.HasValue, Is.True, message);
            Assert.That(placedValue.Value, Is.EqualTo(expectedValue).Within(0.0001f), message);
        }

        private static void AssertOtherFrameMarkerHidden(VisualElement row, string message)
        {
            var marker = GetRowField<VisualElement>(row, "_otherFrameMarker");
            Assert.That(marker.ClassListContains(ExpressionEditorStyles.InvisibleClassName), Is.True, message);
            Assert.That(GetRowPlacedOtherFrameValue(row), Is.Null, message);
        }

        /// <summary>
        /// 行がもう一方のフレームの印を最後に置いた値（<c>BlendShapeRow.PlacedOtherFrameValue</c>）を読む。印を隠しているときは null。
        /// </summary>
        private static float? GetRowPlacedOtherFrameValue(VisualElement row)
        {
            var property = row.GetType().GetProperty(
                "PlacedOtherFrameValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, "BlendShapeRow.PlacedOtherFrameValue was not found.");
            return (float?)property.GetValue(row);
        }

        private static void InvokeRowMethod(VisualElement row, string methodName, params object[] args)
        {
            var method = row.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            try
            {
                method.Invoke(row, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// 除外理由のバッジは Phase 2 の受入レビューで廃止した。行に残っていないことを確かめる。
        /// </summary>
        private static void AssertRowHasNoExclusionBadges(VisualElement row)
        {
            Assert.That(row.Q(className: "blendshape-badges"), Is.Null, "除外理由のバッジは廃止した");
            Assert.That(row.Q(className: "blendshape-badge"), Is.Null, "除外理由のバッジは廃止した");
        }

        private static int _nextEntryIndex;
    }
}
