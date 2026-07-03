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
    public sealed class ExpressionOutputDiffServiceTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
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
        public void Evaluate_SessionBaselineDiff_UsesStartOrEndDifference()
        {
            var model = CreateModel(new[] { "Smile", "Blink" }, new[] { 0f, 20f });
            model.frameMode = ExpressionFrameMode.WeightBlend;
            var smile = GetEntry(model, "Smile");
            var blink = GetEntry(model, "Blink");
            smile.value = smile.initialValue;
            smile.endValue = 50f;
            blink.value = blink.initialValue;
            blink.endValue = blink.initialValue;

            var decisions = ExpressionOutputDiffService.Evaluate(
                model,
                new ExpressionOutputSettings { mode = BlendShapeOutputMode.SessionBaselineDiff });

            Assert.That(GetDecision(decisions, "Smile").shouldWriteCurve, Is.True);
            Assert.That(GetDecision(decisions, "Blink").shouldWriteCurve, Is.False);
        }

        [Test]
        public void Evaluate_ReferenceClipDiff_ResolvesPathUniqueNameAndAmbiguousFallback()
        {
            var model = CreateModel(new[] { "Smile", "Angry", "Blink", "Missing" }, new[] { 0f, 0f, 0f, 0f });
            GetEntry(model, "Smile").value = 10f;
            GetEntry(model, "Angry").value = 0f;
            GetEntry(model, "Blink").value = 5f;
            GetEntry(model, "Missing").value = 15f;

            var referenceClip = new AnimationClip();
            SetBlendShapeCurve(referenceClip, "Face", "Smile", new AnimationCurve(new Keyframe(0f, 10f)));
            SetBlendShapeCurve(referenceClip, "OtherFace", "Angry", new AnimationCurve(new Keyframe(0f, 25f)));
            SetBlendShapeCurve(referenceClip, "OtherA", "Blink", new AnimationCurve(new Keyframe(0f, 0f)));
            SetBlendShapeCurve(referenceClip, "OtherB", "Blink", new AnimationCurve(new Keyframe(0f, 100f)));
            _createdObjects.Add(referenceClip);

            var decisions = ExpressionOutputDiffService.Evaluate(
                model,
                new ExpressionOutputSettings
                {
                    mode = BlendShapeOutputMode.ReferenceClipDiff,
                    referenceClip = referenceClip,
                });

            var smileDecision = GetDecision(decisions, "Smile");
            Assert.That(smileDecision.shouldWriteCurve, Is.False);
            Assert.That(smileDecision.referenceStatus, Is.EqualTo(ReferenceBlendShapeSampleStatus.MatchedRendererPath));

            var angryDecision = GetDecision(decisions, "Angry");
            Assert.That(angryDecision.shouldWriteCurve, Is.True);
            Assert.That(angryDecision.referenceStatus, Is.EqualTo(ReferenceBlendShapeSampleStatus.MatchedUniqueName));

            var blinkDecision = GetDecision(decisions, "Blink");
            Assert.That(blinkDecision.shouldWriteCurve, Is.True);
            Assert.That(blinkDecision.hasReferenceValue, Is.False);
            Assert.That(blinkDecision.referenceStatus, Is.EqualTo(ReferenceBlendShapeSampleStatus.Ambiguous));

            var missingDecision = GetDecision(decisions, "Missing");
            Assert.That(missingDecision.shouldWriteCurve, Is.True);
            Assert.That(missingDecision.hasReferenceValue, Is.False);
            Assert.That(missingDecision.referenceStatus, Is.EqualTo(ReferenceBlendShapeSampleStatus.Missing));
        }

        [Test]
        public void Evaluate_ReferenceClipDiff_IgnoresIntermediateKeysForEndpointComparison()
        {
            var model = CreateModel(new[] { "Smile" }, new[] { 0f });
            model.frameMode = ExpressionFrameMode.WeightBlend;
            var smile = GetEntry(model, "Smile");
            smile.value = 0f;
            smile.endValue = 100f;

            var referenceClip = new AnimationClip();
            SetBlendShapeCurve(
                referenceClip,
                "Face",
                "Smile",
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 60f),
                    new Keyframe(1f, 100f)));
            _createdObjects.Add(referenceClip);

            var decisions = ExpressionOutputDiffService.Evaluate(
                model,
                new ExpressionOutputSettings
                {
                    mode = BlendShapeOutputMode.ReferenceClipDiff,
                    referenceClip = referenceClip,
                });

            var decision = GetDecision(decisions, "Smile");
            Assert.That(decision.shouldWriteCurve, Is.False);
            Assert.That(decision.hasReferenceValue, Is.True);
            Assert.That(decision.referenceStatus, Is.EqualTo(ReferenceBlendShapeSampleStatus.IntermediateKeysIgnored));
            Assert.That(ExpressionOutputDiffService.Summarize(decisions).intermediateKeyCount, Is.EqualTo(1));
        }

        [Test]
        public void ToClip_DefaultSettings_WritesAllOutputTargets()
        {
            var model = CreateModel(new[] { "Smile", "Neutral" }, new[] { 0f, 0f });
            GetEntry(model, "Smile").value = 50f;

            var clip = ExpressionClipIO.ToClip(model, "AllTargets");
            _createdObjects.Add(clip);

            Assert.That(HasCurve(clip, "Face", "blendShape.Smile"), Is.True);
            Assert.That(HasCurve(clip, "Face", "blendShape.Neutral"), Is.True);
        }

        [Test]
        public void ToClip_SessionBaselineDiff_WritesDiffAndKeepsPreservedAndLockedCurves()
        {
            var model = CreateModel(new[] { "Smile", "Neutral", "Locked" }, new[] { 0f, 0f, 0f });
            GetEntry(model, "Smile").value = 50f;
            var neutral = GetEntry(model, "Neutral");
            neutral.value = 75f;
            neutral.userExcluded = true;
            var locked = GetEntry(model, "Locked");
            locked.systemExclusion = BlendShapeSystemExclusionReason.Mmd;
            locked.hasSourceCurve = true;
            locked.sourceCurve = new AnimationCurve(new Keyframe(0f, 80f));

            model.preservedCurves.Add(new PreservedCurve
            {
                path = "Arm",
                propertyName = "m_LocalPosition.x",
                typeName = typeof(Transform).AssemblyQualifiedName,
                curve = new AnimationCurve(new Keyframe(0f, 1f)),
            });
            model.preservedCurves.Add(new PreservedCurve
            {
                path = "Face",
                propertyName = "blendShape.Neutral",
                typeName = typeof(SkinnedMeshRenderer).AssemblyQualifiedName,
                curve = new AnimationCurve(new Keyframe(0f, 40f)),
            });

            var clip = ExpressionClipIO.ToClip(
                model,
                "SessionDiff",
                outputSettings: new ExpressionOutputSettings { mode = BlendShapeOutputMode.SessionBaselineDiff });
            _createdObjects.Add(clip);

            Assert.That(HasCurve(clip, "Face", "blendShape.Smile"), Is.True);
            Assert.That(HasCurve(clip, "Face", "blendShape.Neutral"), Is.False);
            Assert.That(HasCurve(clip, "Face", "blendShape.Locked"), Is.True);
            Assert.That(HasCurve(clip, "Arm", "m_LocalPosition.x"), Is.True);
        }

        [Test]
        public void BlendShapeListView_OutputOnly_InvalidDiffModeDoesNotFallbackToAllOutputTargets()
        {
            var model = CreateModel(new[] { "Smile", "Neutral" }, new[] { 0f, 0f });
            var listView = new BlendShapeListView(model, new VisualElement(), new Label());
            var emptyDecisions = new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>();

            listView.SetShowOutputOnly(true);
            listView.SetOutputDecisions(BlendShapeOutputMode.ReferenceClipDiff, emptyDecisions);

            Assert.That(GetVisibleItems(listView), Is.Empty);

            listView.SetOutputDecisions(BlendShapeOutputMode.AllTargets, emptyDecisions);

            Assert.That(
                GetVisibleItems(listView).Select(entry => entry.name),
                Is.EquivalentTo(new[] { "Smile", "Neutral" }));
        }

        [TestCase("OnSliderChanged")]
        [TestCase("OnValueChanged")]
        public void BlendShapeListView_ValueChange_DoesNotThrowWhenChangedEventUnbindsRow(string handlerName)
        {
            var model = CreateModel(new[] { "Smile" }, new[] { 0f });
            var listView = new BlendShapeListView(model, new VisualElement(), new Label());
            var entry = model.entries[0];
            var row = CreateBoundBlendShapeRow(listView, entry);

            listView.Changed += () => InvokeBlendShapeRowMethod(row, "Unbind");

            InvokeBlendShapeRowFloatHandler(row, handlerName, 40f);

            Assert.That(entry.value, Is.EqualTo(40f).Within(0.0001f));
        }

        [Test]
        public void BlendShapeListView_OutputChange_DoesNotThrowWhenChangedEventUnbindsRow()
        {
            var model = CreateModel(new[] { "Smile" }, new[] { 0f });
            var listView = new BlendShapeListView(model, new VisualElement(), new Label());
            var entry = model.entries[0];
            var row = CreateBoundBlendShapeRow(listView, entry);

            listView.Changed += () => InvokeBlendShapeRowMethod(row, "Unbind");

            InvokeBlendShapeRowBoolHandler(row, "OnOutputChanged", false);

            Assert.That(entry.userExcluded, Is.True);
        }

        private ExpressionEditModel CreateModel(string[] blendShapeNames, float[] weights)
        {
            var avatarRoot = new GameObject("Avatar");
            var face = new GameObject("Face");
            face.transform.SetParent(avatarRoot.transform, false);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);
            for (var i = 0; i < weights.Length; i++)
            {
                renderer.SetBlendShapeWeight(i, weights[i]);
            }

            var model = ExpressionClipIO.CreateModel(avatarRoot, renderer);
            _createdObjects.Add(model);
            _createdObjects.Add(renderer.sharedMesh);
            _createdObjects.Add(avatarRoot);
            return model;
        }

        private Mesh CreateMesh(IEnumerable<string> blendShapeNames)
        {
            var mesh = new Mesh
            {
                name = "TestFaceMesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };
            var deltaVertices = new[]
            {
                Vector3.forward,
                Vector3.zero,
                Vector3.zero,
            };
            var deltaNormals = new Vector3[3];
            var deltaTangents = new Vector3[3];
            foreach (var blendShapeName in blendShapeNames)
            {
                mesh.AddBlendShapeFrame(blendShapeName, 100f, deltaVertices, deltaNormals, deltaTangents);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static BlendShapeEntry GetEntry(ExpressionEditModel model, string name)
        {
            return model.entries.First(entry => entry.name == name);
        }

        private static BlendShapeOutputDecision GetDecision(
            IEnumerable<BlendShapeOutputDecision> decisions,
            string blendShapeName)
        {
            return decisions.First(decision => decision.entry.name == blendShapeName);
        }

        private static void SetBlendShapeCurve(
            AnimationClip clip,
            string path,
            string blendShapeName,
            AnimationCurve curve)
        {
            clip.SetCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeName}", curve);
        }

        private static bool HasCurve(AnimationClip clip, string path, string propertyName)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Any(binding => binding.path == path && binding.propertyName == propertyName);
        }

        private static IReadOnlyList<BlendShapeEntry> GetVisibleItems(BlendShapeListView listView)
        {
            var field = typeof(BlendShapeListView).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            return (IReadOnlyList<BlendShapeEntry>)field.GetValue(listView);
        }

        private static object CreateBoundBlendShapeRow(BlendShapeListView listView, BlendShapeEntry entry)
        {
            var rowType = GetBlendShapeRowType();
            var row = Activator.CreateInstance(
                rowType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { listView },
                null);
            InvokeBlendShapeRowMethod(row, "Bind", entry);
            return row;
        }

        private static Type GetBlendShapeRowType()
        {
            var rowType = typeof(BlendShapeListView).GetNestedType("BlendShapeRow", BindingFlags.NonPublic);
            Assert.That(rowType, Is.Not.Null);
            return rowType;
        }

        private static void InvokeBlendShapeRowFloatHandler(object row, string methodName, float newValue)
        {
            using (var evt = ChangeEvent<float>.GetPooled(0f, newValue))
            {
                InvokeBlendShapeRowMethod(row, methodName, evt);
            }
        }

        private static void InvokeBlendShapeRowBoolHandler(object row, string methodName, bool newValue)
        {
            using (var evt = ChangeEvent<bool>.GetPooled(true, newValue))
            {
                InvokeBlendShapeRowMethod(row, methodName, evt);
            }
        }

        private static void InvokeBlendShapeRowMethod(object row, string methodName, params object[] args)
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
    }
}
