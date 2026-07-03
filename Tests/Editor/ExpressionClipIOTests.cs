using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class ExpressionClipIOTests
    {
        private readonly List<string> _assetPaths = new List<string>();
        private readonly List<Object> _temporaryObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _assetPaths.Count; i++)
            {
                AssetDatabase.DeleteAsset(_assetPaths[i]);
            }

            _assetPaths.Clear();

            for (var i = _temporaryObjects.Count - 1; i >= 0; i--)
            {
                if (_temporaryObjects[i] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[i]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void SaveClipToAsset_NewAssetPath_MakesSourceClipPersistent()
        {
            var clip = new AnimationClip { name = "NewExpression" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_NewAsset.anim");
            _assetPaths.Add(assetPath);

            ExpressionClipIO.SaveClipToAsset(clip, assetPath);

            Assert.That(EditorUtility.IsPersistent(clip), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(assetPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath), Is.Not.Null);
        }

        [Test]
        public void SaveClipToAsset_ExistingAssetPath_LeavesSourceClipTemporary()
        {
            var existingClip = new AnimationClip { name = "ExistingExpression" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_ExistingAsset.anim");
            AssetDatabase.CreateAsset(existingClip, assetPath);
            _assetPaths.Add(assetPath);

            var replacementClip = new AnimationClip { name = "ReplacementExpression" };
            replacementClip.SetCurve(
                "",
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 1f)));
            _temporaryObjects.Add(replacementClip);

            ExpressionClipIO.SaveClipToAsset(replacementClip, assetPath);

            Assert.That(EditorUtility.IsPersistent(replacementClip), Is.False);
            var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Assert.That(savedClip, Is.Not.Null);
            Assert.That(AnimationUtility.GetEditorCurve(savedClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.x")), Is.Not.Null);
        }

        [Test]
        public void Load_TwoKeyStaticBlendShape_UsesSingleFrameAndPreservesSourceCurve()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var sourceClip = new AnimationClip { name = "StaticTwoKeyExpression" };
            _temporaryObjects.Add(sourceClip);
            var sourceCurve = new AnimationCurve(
                new Keyframe(0f, 42f, 0.25f, 0.25f),
                new Keyframe(2f, 42f, -0.5f, -0.5f));
            sourceClip.SetCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Smile", sourceCurve);

            var model = ExpressionClipIO.Load(sourceClip, renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.SingleFrame));
            Assert.That(model.hasIntermediateKeys, Is.False);
            Assert.That(model.entries[0].value, Is.EqualTo(42f).Within(0.0001f));
            Assert.That(model.entries[0].endValue, Is.EqualTo(42f).Within(0.0001f));

            var roundTripClip = ExpressionClipIO.ToClip(model, "RoundTrip");
            _temporaryObjects.Add(roundTripClip);
            var roundTripCurve = GetBlendShapeCurve(roundTripClip, "Face", "Smile");

            Assert.That(roundTripCurve, Is.Not.Null);
            Assert.That(roundTripCurve.length, Is.EqualTo(2));
            Assert.That(roundTripCurve.keys[1].time, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[1].value, Is.EqualTo(42f).Within(0.0001f));
        }

        [Test]
        public void ToClip_UnchangedWeightBlend_PreservesSourceCurve()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var sourceClip = new AnimationClip { name = "WeightBlendExpression" };
            _temporaryObjects.Add(sourceClip);
            var sourceCurve = new AnimationCurve(
                new Keyframe(0.25f, 10f, 1.5f, 1.5f),
                new Keyframe(2.5f, 80f, -0.25f, -0.25f));
            sourceClip.SetCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Smile", sourceCurve);

            var model = ExpressionClipIO.Load(sourceClip, renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend));
            Assert.That(model.entries[0].value, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(model.entries[0].endValue, Is.EqualTo(80f).Within(0.0001f));

            var roundTripClip = ExpressionClipIO.ToClip(model, "RoundTrip");
            _temporaryObjects.Add(roundTripClip);
            var roundTripCurve = GetBlendShapeCurve(roundTripClip, "Face", "Smile");

            Assert.That(roundTripCurve, Is.Not.Null);
            Assert.That(roundTripCurve.length, Is.EqualTo(2));
            Assert.That(roundTripCurve.keys[0].time, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[1].time, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[0].outTangent, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[1].inTangent, Is.EqualTo(-0.25f).Within(0.0001f));
        }

        private static string GenerateAssetPath(string fileName)
        {
            return AssetDatabase.GenerateUniqueAssetPath($"Assets/{fileName}");
        }

        private SkinnedMeshRenderer CreateAvatarRenderer(params string[] blendShapeNames)
        {
            var avatarRoot = new GameObject("Avatar");
            var face = new GameObject("Face");
            face.transform.SetParent(avatarRoot.transform, false);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);
            _temporaryObjects.Add(renderer.sharedMesh);
            _temporaryObjects.Add(avatarRoot);
            return renderer;
        }

        private static Mesh CreateMesh(IEnumerable<string> blendShapeNames)
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

        private static AnimationCurve GetBlendShapeCurve(AnimationClip clip, string path, string blendShapeName)
        {
            return AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeName}"));
        }
    }
}
