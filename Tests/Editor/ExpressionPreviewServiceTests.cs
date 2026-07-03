using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class ExpressionPreviewServiceTests
    {
        private readonly List<Object> _temporaryObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
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
        public void SceneSave_RestoresOriginalWeightsDuringSaveAndReappliesPreviewAfterSave()
        {
            var renderer = CreateAvatarRenderer("Smile");
            renderer.SetBlendShapeWeight(0, 0f);
            var model = ExpressionClipIO.CreateModel(renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);
            model.entries[0].value = 75f;

            using (var service = new ExpressionPreviewService())
            {
                service.Sample(model);

                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(75f).Within(0.0001f));

                InvokeSceneSaving(service);

                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0f).Within(0.0001f));

                InvokeSceneSaved(service);

                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0f).Within(0.0001f));

                InvokeResampleAfterSceneSaved(service);

                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(75f).Within(0.0001f));
            }

            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(0f).Within(0.0001f));
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

        private static void InvokeSceneSaving(ExpressionPreviewService service)
        {
            var method = typeof(ExpressionPreviewService).GetMethod(
                "OnSceneSaving",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(service, new object[] { default(Scene), "Assets/TestScene.unity" });
        }

        private static void InvokeSceneSaved(ExpressionPreviewService service)
        {
            var method = typeof(ExpressionPreviewService).GetMethod(
                "OnSceneSaved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(service, new object[] { default(Scene) });
        }

        private static void InvokeResampleAfterSceneSaved(ExpressionPreviewService service)
        {
            var method = typeof(ExpressionPreviewService).GetMethod(
                "ResampleAfterSceneSaved",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(service, null);
        }
    }
}
