using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class BlendShapeInfluenceProbeTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TryRaycastMesh_ReturnsNearestTriangleHit()
        {
            var mesh = CreateTwoLayerMesh();
            var ray = new Ray(new Vector3(0.2f, 0.2f, -1f), Vector3.forward);

            var hitFound = BlendShapeInfluenceProbe.TryRaycastMesh(
                mesh,
                Matrix4x4.identity,
                ray,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.TriangleIndex, Is.EqualTo(0));
            Assert.That(hit.WorldPosition.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(hit.VertexIndices, Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void CalculateInfluences_UsesHitTriangleAndNearbyVertices()
        {
            var mesh = CreateInfluenceMesh();
            var hit = new BlendShapeInfluenceHit(
                new Vector3(0.01f, 0.01f, 0f),
                Vector3.back,
                0,
                0,
                1,
                2,
                new Vector3(0.98f, 0.01f, 0.01f));

            var results = BlendShapeInfluenceProbe.CalculateInfluences(
                mesh,
                mesh,
                Matrix4x4.identity,
                hit,
                new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = 0.05f,
                    minimumDelta = 0.0001f,
                    maxResults = 50,
                });

            Assert.That(results.Select(result => result.BlendShapeName), Is.EqualTo(new[] { "Near", "Triangle" }));
            Assert.That(results[0].Score, Is.GreaterThan(results[1].Score));
        }

        [Test]
        public void CalculateInfluences_ExcludesBelowThresholdAndRespectsMaxResults()
        {
            var mesh = CreateInfluenceMesh();
            var hit = new BlendShapeInfluenceHit(
                new Vector3(0.01f, 0.01f, 0f),
                Vector3.back,
                0,
                0,
                1,
                2,
                new Vector3(0.98f, 0.01f, 0.01f));

            var results = BlendShapeInfluenceProbe.CalculateInfluences(
                mesh,
                mesh,
                Matrix4x4.identity,
                hit,
                new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = 0.05f,
                    minimumDelta = 0.02f,
                    maxResults = 1,
                });

            Assert.That(results.Select(result => result.BlendShapeName), Is.EqualTo(new[] { "Near" }));
        }

        [Test]
        public void CalculateInfluences_ReusesContextWithoutMutatingReturnedResults()
        {
            var mesh = CreateInfluenceMesh();
            var context = new BlendShapeInfluenceProbeContext();
            var hit = new BlendShapeInfluenceHit(
                new Vector3(0.01f, 0.01f, 0f),
                Vector3.back,
                0,
                0,
                1,
                2,
                new Vector3(0.98f, 0.01f, 0.01f));

            try
            {
                var firstResults = BlendShapeInfluenceProbe.CalculateInfluences(
                    mesh,
                    mesh,
                    Matrix4x4.identity,
                    hit,
                    new BlendShapeInfluenceProbeOptions
                    {
                        worldRadius = 0.05f,
                        minimumDelta = 0.0001f,
                        maxResults = 50,
                    },
                    context);
                var secondResults = BlendShapeInfluenceProbe.CalculateInfluences(
                    mesh,
                    mesh,
                    Matrix4x4.identity,
                    hit,
                    new BlendShapeInfluenceProbeOptions
                    {
                        worldRadius = 0.05f,
                        minimumDelta = 0.02f,
                        maxResults = 1,
                    },
                    context);

                Assert.That(firstResults.Select(result => result.BlendShapeName), Is.EqualTo(new[] { "Near", "Triangle" }));
                Assert.That(secondResults.Select(result => result.BlendShapeName), Is.EqualTo(new[] { "Near" }));
            }
            finally
            {
                context.Dispose();
            }
        }

        private Mesh CreateTwoLayerMesh()
        {
            var mesh = new Mesh
            {
                name = "TwoLayerInfluenceRaycastMesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f),
                    new Vector3(0f, 0f, 1f),
                    new Vector3(1f, 0f, 1f),
                    new Vector3(0f, 1f, 1f),
                },
                triangles = new[] { 0, 1, 2, 3, 4, 5 },
            };
            mesh.RecalculateBounds();
            _createdObjects.Add(mesh);
            return mesh;
        }

        private Mesh CreateInfluenceMesh()
        {
            var mesh = new Mesh
            {
                name = "BlendShapeInfluenceMesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f),
                    new Vector3(0.02f, 0.02f, 0f),
                },
                triangles = new[] { 0, 1, 2 },
            };

            AddBlendShape(mesh, "Triangle", new[] { Vector3.forward * 0.01f, Vector3.zero, Vector3.zero, Vector3.zero });
            AddBlendShape(mesh, "Near", new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.forward * 0.05f });
            AddBlendShape(mesh, "Tiny", new[] { Vector3.forward * 0.00001f, Vector3.zero, Vector3.zero, Vector3.zero });
            AddBlendShape(mesh, "Empty", new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
            mesh.RecalculateBounds();
            _createdObjects.Add(mesh);
            return mesh;
        }

        private static void AddBlendShape(Mesh mesh, string name, Vector3[] deltaVertices)
        {
            mesh.AddBlendShapeFrame(
                name,
                100f,
                deltaVertices,
                new Vector3[deltaVertices.Length],
                new Vector3[deltaVertices.Length]);
        }
    }
}
