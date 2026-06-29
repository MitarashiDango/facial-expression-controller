using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class BlendShapeInfluenceProbeTests
    {
        private const float PositionTolerance = 0.0005f;
        private const float ScoreTolerance = 0.00001f;
        private const float ScaleEpsilon = 1e-7f;

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
        public void TryRaycastMesh_AppliesNonUniformScaleAndRotationMatrix()
        {
            var mesh = CreateTwoLayerMesh();
            var matrix = Matrix4x4.TRS(
                new Vector3(1.25f, -0.35f, 2.5f),
                Quaternion.Euler(18f, -37f, 12f),
                new Vector3(2f, 1f, 0.5f));
            var targetLocalPosition =
                mesh.vertices[0] * 0.25f
                + mesh.vertices[1] * 0.35f
                + mesh.vertices[2] * 0.4f;
            var targetWorldPosition = matrix.MultiplyPoint3x4(targetLocalPosition);
            var worldNormal = matrix.MultiplyVector(Vector3.Cross(
                mesh.vertices[1] - mesh.vertices[0],
                mesh.vertices[2] - mesh.vertices[0])).normalized;
            var ray = new Ray(targetWorldPosition + worldNormal * 2f, -worldNormal);

            var hitFound = BlendShapeInfluenceProbe.TryRaycastMesh(
                mesh,
                matrix,
                ray,
                out var hit);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.TriangleIndex, Is.EqualTo(0));
            AssertVector3Near(hit.WorldPosition, targetWorldPosition, PositionTolerance);
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
        public void CalculateInfluences_UsesSeparateSampleAndDeltaMatrices()
        {
            var mesh = CreateInfluenceMesh();
            var sampleToWorldMatrix = Matrix4x4.Translate(new Vector3(10f, -2f, 3f));
            var deltaToWorldMatrix = Matrix4x4.Scale(new Vector3(3f, 5f, 2f));
            var hit = new BlendShapeInfluenceHit(
                sampleToWorldMatrix.MultiplyPoint3x4(new Vector3(0.02f, 0.02f, 0f)),
                Vector3.back,
                0,
                0,
                1,
                2,
                new Vector3(0.8f, 0.1f, 0.1f));

            var results = BlendShapeInfluenceProbe.CalculateInfluences(
                mesh,
                mesh,
                sampleToWorldMatrix,
                deltaToWorldMatrix,
                hit,
                new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = 0.05f,
                    minimumDelta = 0.0001f,
                    maxResults = 50,
                });

            Assert.That(results.Select(result => result.BlendShapeName), Is.EqualTo(new[] { "Near", "Triangle" }));
            Assert.That(results[0].Score, Is.EqualTo(0.1f).Within(ScoreTolerance));
            Assert.That(results[0].MaxDelta, Is.EqualTo(0.1f).Within(ScoreTolerance));
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

        [Test]
        public void BakeMeshUseScaleFalse_ScaledRendererDoesNotMatchVisualWorldWithRendererScaleRemovedMatrix()
        {
            var fixture = CreateProbeRendererFixture(
                new Vector3(2f, 1f, 0.5f),
                new Vector3(0.8f, 1.3f, 1.1f),
                new Vector3(0.7f, 1.4f, 0.6f));
            var bakedWithoutScale = CreateTemporaryMesh("BakedWithoutScale");

            fixture.Renderer.BakeMesh(bakedWithoutScale, false);

            Assert.That(bakedWithoutScale.vertexCount, Is.EqualTo(fixture.DeformedVertices.Length));
            for (var i = 0; i < fixture.DeformedVertices.Length; i++)
            {
                var expectedWorldPosition = CreateRendererScaleRemovedWorldMatrix(fixture.Renderer)
                    .MultiplyPoint3x4(bakedWithoutScale.vertices[i]);
                var scaledWorldPosition = fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(
                    fixture.DeformedVertices[i]);
                Assert.That(
                    (expectedWorldPosition - scaledWorldPosition).magnitude,
                    Is.GreaterThan(PositionTolerance));
            }
        }

        [TestCaseSource(nameof(BakeMeshUseScaleCases))]
        public void BakeMeshUseScaleTrue_OutputMatchesVisualWorldWithRendererScaleRemovedMatrix(
            Vector3 rootScale,
            Vector3 parentScale,
            Vector3 rendererScale)
        {
            var fixture = CreateProbeRendererFixture(rootScale, parentScale, rendererScale);
            var bakedWithScale = CreateTemporaryMesh("BakedWithScale");

            fixture.Renderer.BakeMesh(bakedWithScale, true);

            Assert.That(bakedWithScale.vertexCount, Is.EqualTo(fixture.DeformedVertices.Length));
            for (var i = 0; i < fixture.DeformedVertices.Length; i++)
            {
                var actualWorldPosition = CreateRendererScaleRemovedWorldMatrix(fixture.Renderer)
                    .MultiplyPoint3x4(bakedWithScale.vertices[i]);
                var expectedWorldPosition = fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(
                    fixture.DeformedVertices[i]);
                AssertVector3Near(actualWorldPosition, expectedWorldPosition, PositionTolerance);
            }
        }

        [TestCaseSource(nameof(TryProbeScaleCases))]
        public void TryProbe_ReturnsExpectedHitAcrossScaledHierarchy(
            Vector3 rootScale,
            Vector3 parentScale,
            Vector3 rendererScale)
        {
            var fixture = CreateProbeRendererFixture(rootScale, parentScale, rendererScale);
            var barycentric = new Vector3(0.25f, 0.35f, 0.4f);
            var expectedWorldPosition =
                fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[0]) * barycentric.x
                + fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[1]) * barycentric.y
                + fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[2]) * barycentric.z;
            var worldNormal = Vector3.Cross(
                fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[1])
                - fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[0]),
                fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[2])
                - fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4(fixture.DeformedVertices[0])).normalized;
            var ray = new Ray(expectedWorldPosition + worldNormal * 2f, -worldNormal);

            var hitFound = BlendShapeInfluenceProbe.TryProbe(
                fixture.Renderer,
                ray,
                new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = 0f,
                    minimumDelta = 0.000001f,
                    maxResults = 50,
                },
                out var hit,
                out var results);

            Assert.That(hitFound, Is.True);
            Assert.That(hit.TriangleIndex, Is.EqualTo(0));
            Assert.That(hit.VertexIndices, Is.EqualTo(new[] { 0, 1, 2 }));
            AssertVector3Near(hit.WorldPosition, expectedWorldPosition, PositionTolerance);
            Assert.That(results.Select(result => result.BlendShapeName), Does.Contain("Raise"));
            Assert.That(results.Select(result => result.BlendShapeName), Does.Not.Contain("Tiny"));

            var raiseResult = results.Single(result => result.BlendShapeName == "Raise");
            var expectedScore = CalculateExpectedRaiseScore(fixture.Renderer, barycentric, out var expectedMaxDelta);
            Assert.That(raiseResult.Score, Is.EqualTo(expectedScore).Within(ScoreTolerance));
            Assert.That(raiseResult.MaxDelta, Is.EqualTo(expectedMaxDelta).Within(ScoreTolerance));
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

        private Mesh CreateTemporaryMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
            };
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

        private ProbeRendererFixture CreateProbeRendererFixture(
            Vector3 rootScale,
            Vector3 parentScale,
            Vector3 rendererScale)
        {
            var root = CreateGameObject("ProbeRoot");
            root.transform.position = new Vector3(1.25f, -0.5f, 2.5f);
            root.transform.rotation = Quaternion.Euler(13f, 41f, -7f);
            root.transform.localScale = Vector3.one;

            var parent = CreateGameObject("ProbeParent");
            parent.transform.SetParent(root.transform, false);
            parent.transform.localPosition = new Vector3(-0.2f, 0.3f, 0.4f);
            parent.transform.localRotation = Quaternion.Euler(-11f, 17f, 5f);
            parent.transform.localScale = Vector3.one;

            var rendererObject = CreateGameObject("ProbeRenderer");
            rendererObject.transform.SetParent(parent.transform, false);
            rendererObject.transform.localPosition = new Vector3(0.31f, -0.27f, 0.19f);
            rendererObject.transform.localRotation = Quaternion.Euler(21f, -33f, 9f);
            rendererObject.transform.localScale = Vector3.one;

            var boneObject = CreateGameObject("ProbeBone");
            boneObject.transform.SetParent(parent.transform, false);
            boneObject.transform.localPosition = Vector3.zero;
            boneObject.transform.localRotation = Quaternion.identity;
            boneObject.transform.localScale = Vector3.one;

            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = CreateProbeMesh(rendererObject.transform, boneObject.transform);
            renderer.sharedMesh = mesh;
            renderer.bones = new[] { boneObject.transform };
            renderer.rootBone = boneObject.transform;
            root.transform.localScale = rootScale;
            parent.transform.localScale = parentScale;
            rendererObject.transform.localScale = rendererScale;
            renderer.SetBlendShapeWeight(0, 100f);

            return new ProbeRendererFixture(renderer, CreateProbeDeformedVertices());
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        private Mesh CreateProbeMesh(Transform rendererTransform, Transform boneTransform)
        {
            var vertices = CreateProbeVertices();
            var mesh = new Mesh
            {
                name = "ProbeSkinnedMesh",
                vertices = vertices,
                triangles = new[] { 0, 1, 2, 1, 3, 2 },
                boneWeights = Enumerable.Repeat(
                    new BoneWeight
                    {
                        boneIndex0 = 0,
                        weight0 = 1f,
                    },
                    vertices.Length).ToArray(),
                bindposes = new[] { boneTransform.worldToLocalMatrix * rendererTransform.localToWorldMatrix },
            };

            AddBlendShape(mesh, "Raise", CreateProbeBlendShapeDelta());
            AddBlendShape(
                mesh,
                "Tiny",
                Enumerable.Repeat(Vector3.forward * 0.0000001f, vertices.Length).ToArray());
            mesh.RecalculateBounds();
            _createdObjects.Add(mesh);
            return mesh;
        }

        private static Vector3[] CreateProbeVertices()
        {
            return new[]
            {
                new Vector3(0.11f, 0.03f, 0.02f),
                new Vector3(0.64f, 0.09f, -0.04f),
                new Vector3(0.18f, 0.52f, 0.07f),
                new Vector3(0.39f, 0.21f, 0.18f),
            };
        }

        private static Vector3[] CreateProbeBlendShapeDelta()
        {
            return new[]
            {
                new Vector3(0.01f, 0.02f, 0.015f),
                new Vector3(0.015f, 0.005f, 0.02f),
                new Vector3(0.02f, 0.015f, 0.005f),
                new Vector3(0.005f, 0.01f, 0.025f),
            };
        }

        private static Vector3[] CreateProbeDeformedVertices()
        {
            var vertices = CreateProbeVertices();
            var deltas = CreateProbeBlendShapeDelta();
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] += deltas[i];
            }

            return vertices;
        }

        private static float CalculateExpectedRaiseScore(
            SkinnedMeshRenderer renderer,
            Vector3 barycentric,
            out float expectedMaxDelta)
        {
            var deltas = CreateProbeBlendShapeDelta();
            var weights = new[] { barycentric.x, barycentric.y, barycentric.z };
            var expectedScore = 0f;
            expectedMaxDelta = 0f;

            for (var i = 0; i < weights.Length; i++)
            {
                var deltaMagnitude = renderer.localToWorldMatrix.MultiplyVector(deltas[i]).magnitude;
                if (deltaMagnitude > expectedMaxDelta)
                {
                    expectedMaxDelta = deltaMagnitude;
                }

                var weightedScore = deltaMagnitude * weights[i];
                if (weightedScore > expectedScore)
                {
                    expectedScore = weightedScore;
                }
            }

            return expectedScore;
        }

        private static Matrix4x4 CreateRendererScaleRemovedWorldMatrix(SkinnedMeshRenderer renderer)
        {
            return renderer.localToWorldMatrix * Matrix4x4.Scale(CreateInverseScale(renderer.transform.localScale));
        }

        private static Vector3 CreateInverseScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Abs(scale.x) > ScaleEpsilon ? 1f / scale.x : 0f,
                Mathf.Abs(scale.y) > ScaleEpsilon ? 1f / scale.y : 0f,
                Mathf.Abs(scale.z) > ScaleEpsilon ? 1f / scale.z : 0f);
        }

        private static IEnumerable<TestCaseData> BakeMeshUseScaleCases()
        {
            yield return new TestCaseData(Vector3.one * 2f, Vector3.one, Vector3.one)
                .SetName("BakeMesh true root scale 2");
            yield return new TestCaseData(Vector3.one, Vector3.one, new Vector3(0.7f, 1.4f, 0.6f))
                .SetName("BakeMesh true renderer scale only");
            yield return new TestCaseData(
                    new Vector3(2f, 1f, 0.5f),
                    new Vector3(0.8f, 1.3f, 1.1f),
                    new Vector3(0.7f, 1.4f, 0.6f))
                .SetName("BakeMesh true mixed non-uniform scale");
        }

        private static IEnumerable<TestCaseData> TryProbeScaleCases()
        {
            yield return new TestCaseData(Vector3.one, Vector3.one, Vector3.one)
                .SetName("TryProbe root scale 1");
            yield return new TestCaseData(Vector3.one * 0.01f, Vector3.one, Vector3.one)
                .SetName("TryProbe root scale 0.01");
            yield return new TestCaseData(Vector3.one * 0.5f, Vector3.one, Vector3.one)
                .SetName("TryProbe root scale 0.5");
            yield return new TestCaseData(Vector3.one * 2f, Vector3.one, Vector3.one)
                .SetName("TryProbe root scale 2");
            yield return new TestCaseData(Vector3.one, new Vector3(2f, 1f, 0.5f), Vector3.one)
                .SetName("TryProbe parent scale only");
            yield return new TestCaseData(Vector3.one, Vector3.one, new Vector3(0.7f, 1.4f, 0.6f))
                .SetName("TryProbe renderer scale only");
            yield return new TestCaseData(
                    new Vector3(2f, 1f, 0.5f),
                    new Vector3(0.8f, 1.3f, 1.1f),
                    new Vector3(0.7f, 1.4f, 0.6f))
                .SetName("TryProbe mixed non-uniform parent and renderer scale");
        }

        private static void AssertVector3Near(Vector3 actual, Vector3 expected, float tolerance)
        {
            Assert.That(
                (actual - expected).magnitude,
                Is.LessThanOrEqualTo(tolerance),
                $"Expected {expected} but was {actual}.");
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

        private sealed class ProbeRendererFixture
        {
            public ProbeRendererFixture(SkinnedMeshRenderer renderer, Vector3[] deformedVertices)
            {
                Renderer = renderer;
                DeformedVertices = deformedVertices;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public Vector3[] DeformedVertices { get; }
        }
    }
}
