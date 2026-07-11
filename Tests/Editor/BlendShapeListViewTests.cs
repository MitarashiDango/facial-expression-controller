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
        public void BulkTargetMenu_UsesSpecifiedJapaneseLabelsAndResultMessage()
        {
            Assert.That(GetWindowConstant("BulkAllEditableLabel"), Is.EqualTo("すべてのブレンドシェイプを編集・出力対象にする"));
            Assert.That(GetWindowConstant("BulkAllExcludedLabel"), Is.EqualTo("すべてのブレンドシェイプを編集・出力対象外にする"));
            Assert.That(GetWindowConstant("BulkRestoreEditingStartLabel"), Is.EqualTo("編集開始時点の状態に戻す"));

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

        private static string GetWindowConstant(string fieldName)
        {
            var field = typeof(FacialExpressionEditorWindow).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (string)field.GetRawConstantValue();
        }

        private static int _nextEntryIndex;
    }
}
