using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class BlendShapeInfluenceProbeTests
    {
        private const float PositionTolerance = 0.0005f;
        private const float ScoreTolerance = 0.00001f;

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
            var mesh = CreateSingleTriangleMesh();
            var matrix = Matrix4x4.TRS(
                new Vector3(1.25f, -0.35f, 2.5f),
                Quaternion.Euler(18f, -37f, 12f),
                new Vector3(2f, 1f, 0.5f));
            var targetLocalPosition =
                mesh.vertices[0] * 0.25f
                + mesh.vertices[1] * 0.35f
                + mesh.vertices[2] * 0.4f;
            var targetWorldPosition = matrix.MultiplyPoint3x4(targetLocalPosition);
            var worldNormal = Vector3.Cross(
                matrix.MultiplyPoint3x4(mesh.vertices[1]) - matrix.MultiplyPoint3x4(mesh.vertices[0]),
                matrix.MultiplyPoint3x4(mesh.vertices[2]) - matrix.MultiplyPoint3x4(mesh.vertices[0])).normalized;
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

        [TestCaseSource(nameof(TryProbeScaleCases))]
        public void TryProbe_ReturnsExpectedHitAcrossScaledHierarchy(
            Vector3 rootScale,
            Vector3 parentScale,
            Vector3 rendererScale)
        {
            var fixture = CreateProbeRendererFixture(rootScale, parentScale, rendererScale);
            var barycentric = new Vector3(0.25f, 0.35f, 0.4f);
            var worldVertex0 = fixture.TransformDeformedVertex(0);
            var worldVertex1 = fixture.TransformDeformedVertex(1);
            var worldVertex2 = fixture.TransformDeformedVertex(2);
            var expectedWorldPosition =
                worldVertex0 * barycentric.x
                + worldVertex1 * barycentric.y
                + worldVertex2 * barycentric.z;
            var worldNormal = Vector3.Cross(
                worldVertex1 - worldVertex0,
                worldVertex2 - worldVertex0).normalized;
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
            var expectedScore = CalculateExpectedRaiseScore(fixture, barycentric, out var expectedMaxDelta);
            Assert.That(raiseResult.Score, Is.EqualTo(expectedScore).Within(ScoreTolerance));
            Assert.That(raiseResult.MaxDelta, Is.EqualTo(expectedMaxDelta).Within(ScoreTolerance));
        }

        [Test]
        public void TryProbe_ReusedContext_RecomputesSkinningMatricesAfterBoneTransformChanges()
        {
            var fixture = CreateProbeRendererFixture(Vector3.one, Vector3.one, Vector3.one);
            var barycentric = new Vector3(0.25f, 0.35f, 0.4f);
            using (var context = new BlendShapeInfluenceProbeContext())
            {
                Assert.That(TryProbeFixture(fixture, barycentric, context, out var firstResults), Is.True);
                var firstRaise = firstResults.Single(result => result.BlendShapeName == "Raise");
                var firstScore = firstRaise.Score;
                var firstMaxDelta = firstRaise.MaxDelta;

                fixture.Bone.localScale = new Vector3(0.5f, 2f, 1.5f);

                Assert.That(TryProbeFixture(fixture, barycentric, context, out var secondResults), Is.True);
                var secondRaise = secondResults.Single(result => result.BlendShapeName == "Raise");
                var expectedScore = CalculateExpectedRaiseScore(fixture, barycentric, out var expectedMaxDelta);

                Assert.That(secondRaise.Score, Is.EqualTo(expectedScore).Within(ScoreTolerance));
                Assert.That(secondRaise.MaxDelta, Is.EqualTo(expectedMaxDelta).Within(ScoreTolerance));
                Assert.That(secondRaise.Score, Is.Not.EqualTo(firstScore).Within(ScoreTolerance));
                Assert.That(firstRaise.Score, Is.EqualTo(firstScore).Within(ScoreTolerance));
                Assert.That(firstRaise.MaxDelta, Is.EqualTo(firstMaxDelta).Within(ScoreTolerance));
            }
        }

        [Test]
        public void TryProbe_ReusedContext_UsesReplacedRendererBones()
        {
            var fixture = CreateProbeRendererFixture(Vector3.one, Vector3.one, Vector3.one);
            var barycentric = new Vector3(0.25f, 0.35f, 0.4f);
            using (var context = new BlendShapeInfluenceProbeContext())
            {
                Assert.That(TryProbeFixture(fixture, barycentric, context, out _), Is.True);

                var replacementBone = CreateGameObject("ReplacementProbeBone");
                replacementBone.transform.SetParent(fixture.Bone.parent, false);
                replacementBone.transform.localPosition = new Vector3(0.18f, -0.12f, 0.09f);
                replacementBone.transform.localRotation = Quaternion.Euler(-17f, 26f, 8f);
                replacementBone.transform.localScale = new Vector3(1.4f, 0.7f, 1.8f);
                fixture.Renderer.bones = new[] { replacementBone.transform };
                fixture.Renderer.rootBone = replacementBone.transform;

                var bakedAtZero = new Mesh { name = "ReplacedBonesBakedAtZero" };
                var bakedAtFull = new Mesh { name = "ReplacedBonesBakedAtFull" };
                _createdObjects.Add(bakedAtZero);
                _createdObjects.Add(bakedAtFull);
                fixture.Renderer.SetBlendShapeWeight(0, 0f);
                fixture.Renderer.BakeMesh(bakedAtZero, false);
                fixture.Renderer.SetBlendShapeWeight(0, 100f);
                fixture.Renderer.BakeMesh(bakedAtFull, false);

                var bakedWorldVertices = bakedAtFull.vertices
                    .Select(fixture.Renderer.localToWorldMatrix.MultiplyPoint3x4)
                    .ToArray();
                var expectedPosition =
                    bakedWorldVertices[0] * barycentric.x
                    + bakedWorldVertices[1] * barycentric.y
                    + bakedWorldVertices[2] * barycentric.z;
                var worldNormal = Vector3.Cross(
                    bakedWorldVertices[1] - bakedWorldVertices[0],
                    bakedWorldVertices[2] - bakedWorldVertices[0]).normalized;

                var hitFound = BlendShapeInfluenceProbe.TryProbe(
                    fixture.Renderer,
                    new Ray(expectedPosition + worldNormal * 2f, -worldNormal),
                    new BlendShapeInfluenceProbeOptions
                    {
                        worldRadius = 0f,
                        minimumDelta = 0.000001f,
                        maxResults = 50,
                    },
                    context,
                    out var hit,
                    out var results);

                Assert.That(hitFound, Is.True);
                AssertVector3Near(hit.WorldPosition, expectedPosition, PositionTolerance);
                var raiseResult = results.Single(result => result.BlendShapeName == "Raise");
                var barycentricWeights = new[] { barycentric.x, barycentric.y, barycentric.z };
                var expectedScore = 0f;
                var expectedMaxDelta = 0f;
                for (var i = 0; i < barycentricWeights.Length; i++)
                {
                    var bakedDelta = fixture.Renderer.localToWorldMatrix.MultiplyVector(
                        bakedAtFull.vertices[i] - bakedAtZero.vertices[i]);
                    var magnitude = bakedDelta.magnitude;
                    expectedScore = Mathf.Max(expectedScore, magnitude * barycentricWeights[i]);
                    expectedMaxDelta = Mathf.Max(expectedMaxDelta, magnitude);
                }

                Assert.That(raiseResult.Score, Is.EqualTo(expectedScore).Within(ScoreTolerance));
                Assert.That(raiseResult.MaxDelta, Is.EqualTo(expectedMaxDelta).Within(ScoreTolerance));
            }
        }

        [Test]
        public void TryProbe_MultiBoneNonUniformScale_UsesWeightedSkinningDelta()
        {
            var root = CreateGameObject("MultiBoneRoot");
            root.transform.position = new Vector3(-0.4f, 0.8f, 1.2f);
            root.transform.rotation = Quaternion.Euler(7f, -23f, 11f);

            var rendererObject = CreateGameObject("MultiBoneRenderer");
            rendererObject.transform.SetParent(root.transform, false);
            rendererObject.transform.localPosition = new Vector3(0.2f, -0.1f, 0.3f);
            rendererObject.transform.localRotation = Quaternion.Euler(13f, 19f, -5f);

            var bone0 = CreateGameObject("MultiBone0");
            bone0.transform.SetParent(root.transform, false);
            var bone1 = CreateGameObject("MultiBone1");
            bone1.transform.SetParent(root.transform, false);

            var vertices = new[]
            {
                new Vector3(0.1f, 0.05f, 0f),
                new Vector3(0.7f, 0.1f, 0.02f),
                new Vector3(0.15f, 0.6f, -0.03f),
            };
            var deltas = new[]
            {
                new Vector3(0.02f, 0.01f, 0.015f),
                new Vector3(0.01f, 0.025f, 0.005f),
                new Vector3(0.015f, 0.005f, 0.02f),
            };
            var boneWeights = new[]
            {
                CreateBoneWeight(0.75f),
                CreateBoneWeight(0.25f),
                CreateBoneWeight(0.5f),
            };
            var bindPoses = new[]
            {
                bone0.transform.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix,
                bone1.transform.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix,
            };
            var mesh = new Mesh
            {
                name = "MultiBoneProbeMesh",
                vertices = vertices,
                triangles = new[] { 0, 1, 2 },
                boneWeights = boneWeights,
                bindposes = bindPoses,
            };
            AddBlendShape(mesh, "Weighted", deltas);
            mesh.RecalculateBounds();
            _createdObjects.Add(mesh);

            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = new[] { bone0.transform, bone1.transform };
            renderer.rootBone = bone0.transform;
            renderer.SetBlendShapeWeight(0, 100f);

            bone0.transform.localScale = new Vector3(2f, 0.75f, 1.25f);
            bone1.transform.localPosition = new Vector3(0.1f, -0.05f, 0.08f);
            bone1.transform.localRotation = Quaternion.Euler(-9f, 14f, 6f);
            bone1.transform.localScale = new Vector3(0.6f, 1.8f, 0.9f);
            var skinningMatrices = new[]
            {
                bone0.transform.localToWorldMatrix * bindPoses[0],
                bone1.transform.localToWorldMatrix * bindPoses[1],
            };

            var worldVertices = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                worldVertices[i] = TransformWeightedPoint(
                    vertices[i] + deltas[i],
                    boneWeights[i],
                    skinningMatrices);
            }

            var barycentric = new Vector3(0.2f, 0.3f, 0.5f);
            var expectedPosition =
                worldVertices[0] * barycentric.x
                + worldVertices[1] * barycentric.y
                + worldVertices[2] * barycentric.z;
            var worldNormal = Vector3.Cross(
                worldVertices[1] - worldVertices[0],
                worldVertices[2] - worldVertices[0]).normalized;

            var hitFound = BlendShapeInfluenceProbe.TryProbe(
                renderer,
                new Ray(expectedPosition + worldNormal * 2f, -worldNormal),
                new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = 0f,
                    minimumDelta = 0.000001f,
                    maxResults = 50,
                },
                out var hit,
                out var results);

            Assert.That(hitFound, Is.True);
            AssertVector3Near(hit.WorldPosition, expectedPosition, PositionTolerance);
            var result = results.Single(item => item.BlendShapeName == "Weighted");
            var barycentricWeights = new[] { barycentric.x, barycentric.y, barycentric.z };
            var expectedScore = 0f;
            var expectedMaxDelta = 0f;
            for (var i = 0; i < deltas.Length; i++)
            {
                var magnitude = TransformWeightedVector(deltas[i], boneWeights[i], skinningMatrices).magnitude;
                expectedScore = Mathf.Max(expectedScore, magnitude * barycentricWeights[i]);
                expectedMaxDelta = Mathf.Max(expectedMaxDelta, magnitude);
            }

            Assert.That(result.Score, Is.EqualTo(expectedScore).Within(ScoreTolerance));
            Assert.That(result.MaxDelta, Is.EqualTo(expectedMaxDelta).Within(ScoreTolerance));
        }

        [TestCase(SkinQuality.Bone1, SkinWeights.Unlimited, TestName = "TryProbe matches BakeMesh with Bone1 quality")]
        [TestCase(SkinQuality.Bone2, SkinWeights.Unlimited, TestName = "TryProbe matches BakeMesh with Bone2 quality")]
        [TestCase(SkinQuality.Auto, SkinWeights.Unlimited, TestName = "TryProbe matches BakeMesh with Auto Unlimited and five weights")]
        public void TryProbe_QualityLimitedAndUnlimitedWeights_MatchesBakeMesh(
            SkinQuality rendererQuality,
            SkinWeights qualitySkinWeights)
        {
            var previousSkinWeights = QualitySettings.skinWeights;
            try
            {
                QualitySettings.skinWeights = qualitySkinWeights;
                var rendererObject = CreateGameObject("QualityProbeRenderer");
                var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
                var bones = new Transform[5];
                for (var i = 0; i < bones.Length; i++)
                {
                    bones[i] = CreateGameObject($"QualityProbeBone{i}").transform;
                }

                var vertices = new[]
                {
                    new Vector3(0.1f, 0.05f, 0.02f),
                    new Vector3(0.7f, 0.08f, -0.03f),
                    new Vector3(0.16f, 0.62f, 0.04f),
                };
                var deltas = new[]
                {
                    new Vector3(0.025f, 0.01f, 0.015f),
                    new Vector3(0.01f, 0.03f, 0.005f),
                    new Vector3(0.015f, 0.005f, 0.025f),
                };
                var mesh = CreateFiveWeightProbeMesh(vertices, deltas, bones.Length);
                renderer.sharedMesh = mesh;
                renderer.bones = bones;
                renderer.rootBone = bones[0];
                renderer.quality = rendererQuality;

                for (var i = 0; i < bones.Length; i++)
                {
                    bones[i].position = new Vector3(0.03f * i, -0.02f * i, 0.015f * i);
                    bones[i].rotation = Quaternion.Euler(3f * i, -7f * i, 5f * i);
                    bones[i].localScale = new Vector3(1f + 0.15f * i, 1f - 0.08f * i, 1f + 0.05f * i);
                }

                var bakedAtZero = new Mesh { name = "QualityBakedAtZero" };
                var bakedAtFull = new Mesh { name = "QualityBakedAtFull" };
                _createdObjects.Add(bakedAtZero);
                _createdObjects.Add(bakedAtFull);
                renderer.SetBlendShapeWeight(0, 0f);
                renderer.BakeMesh(bakedAtZero, false);
                renderer.SetBlendShapeWeight(0, 100f);
                renderer.BakeMesh(bakedAtFull, false);

                var bakedWorldVertices = bakedAtFull.vertices
                    .Select(renderer.localToWorldMatrix.MultiplyPoint3x4)
                    .ToArray();
                var barycentric = new Vector3(0.2f, 0.3f, 0.5f);
                var expectedPosition =
                    bakedWorldVertices[0] * barycentric.x
                    + bakedWorldVertices[1] * barycentric.y
                    + bakedWorldVertices[2] * barycentric.z;
                var worldNormal = Vector3.Cross(
                    bakedWorldVertices[1] - bakedWorldVertices[0],
                    bakedWorldVertices[2] - bakedWorldVertices[0]).normalized;

                var hitFound = BlendShapeInfluenceProbe.TryProbe(
                    renderer,
                    new Ray(expectedPosition + worldNormal * 2f, -worldNormal),
                    new BlendShapeInfluenceProbeOptions
                    {
                        worldRadius = 0f,
                        minimumDelta = 0.000001f,
                        maxResults = 50,
                    },
                    out var hit,
                    out var results);

                Assert.That(hitFound, Is.True);
                AssertVector3Near(hit.WorldPosition, expectedPosition, PositionTolerance);
                var result = results.Single(item => item.BlendShapeName == "QualityWeighted");
                var barycentricWeights = new[] { barycentric.x, barycentric.y, barycentric.z };
                var expectedScore = 0f;
                var expectedMaxDelta = 0f;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var bakedDelta = renderer.localToWorldMatrix.MultiplyVector(
                        bakedAtFull.vertices[i] - bakedAtZero.vertices[i]);
                    var magnitude = bakedDelta.magnitude;
                    expectedScore = Mathf.Max(expectedScore, magnitude * barycentricWeights[i]);
                    expectedMaxDelta = Mathf.Max(expectedMaxDelta, magnitude);
                }

                Assert.That(result.Score, Is.EqualTo(expectedScore).Within(ScoreTolerance));
                Assert.That(result.MaxDelta, Is.EqualTo(expectedMaxDelta).Within(ScoreTolerance));

                var bonesPerVertex = mesh.GetBonesPerVertex();
                Assert.That(bonesPerVertex[0], Is.EqualTo(5));
            }
            finally
            {
                QualitySettings.skinWeights = previousSkinWeights;
            }
        }

        private static bool TryProbeFixture(
            ProbeRendererFixture fixture,
            Vector3 barycentric,
            BlendShapeInfluenceProbeContext context,
            out IReadOnlyList<BlendShapeInfluenceResult> results)
        {
            var worldVertex0 = fixture.TransformDeformedVertex(0);
            var worldVertex1 = fixture.TransformDeformedVertex(1);
            var worldVertex2 = fixture.TransformDeformedVertex(2);
            var expectedWorldPosition =
                worldVertex0 * barycentric.x
                + worldVertex1 * barycentric.y
                + worldVertex2 * barycentric.z;
            var worldNormal = Vector3.Cross(worldVertex1 - worldVertex0, worldVertex2 - worldVertex0).normalized;
            var ray = new Ray(expectedWorldPosition + worldNormal * 2f, -worldNormal);

            return BlendShapeInfluenceProbe.TryProbe(
                fixture.Renderer,
                ray,
                new BlendShapeInfluenceProbeOptions
                {
                    worldRadius = 0f,
                    minimumDelta = 0.000001f,
                    maxResults = 50,
                },
                context,
                out _,
                out results);
        }

        private static BoneWeight CreateBoneWeight(float bone0Weight)
        {
            return new BoneWeight
            {
                boneIndex0 = 0,
                weight0 = bone0Weight,
                boneIndex1 = 1,
                weight1 = 1f - bone0Weight,
            };
        }

        private Mesh CreateFiveWeightProbeMesh(
            IReadOnlyList<Vector3> vertices,
            Vector3[] deltas,
            int boneCount)
        {
            var mesh = new Mesh
            {
                name = "FiveWeightProbeMesh",
                vertices = vertices.ToArray(),
                triangles = new[] { 0, 1, 2 },
                bindposes = Enumerable.Repeat(Matrix4x4.identity, boneCount).ToArray(),
            };
            var weightsByBone = new[] { 0.4f, 0.25f, 0.15f, 0.12f, 0.08f };
            var bonesPerVertex = new byte[vertices.Count];
            var allWeights = new BoneWeight1[vertices.Count * weightsByBone.Length];
            for (var vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
            {
                bonesPerVertex[vertexIndex] = (byte)weightsByBone.Length;
                for (var boneIndex = 0; boneIndex < weightsByBone.Length; boneIndex++)
                {
                    allWeights[vertexIndex * weightsByBone.Length + boneIndex] = new BoneWeight1
                    {
                        boneIndex = boneIndex,
                        weight = weightsByBone[boneIndex],
                    };
                }
            }

            var bonesPerVertexArray = new NativeArray<byte>(bonesPerVertex, Allocator.Temp);
            var allWeightsArray = new NativeArray<BoneWeight1>(allWeights, Allocator.Temp);
            try
            {
                mesh.SetBoneWeights(bonesPerVertexArray, allWeightsArray);
            }
            finally
            {
                bonesPerVertexArray.Dispose();
                allWeightsArray.Dispose();
            }

            AddBlendShape(mesh, "QualityWeighted", deltas);
            mesh.RecalculateBounds();
            _createdObjects.Add(mesh);
            return mesh;
        }

        private static Vector3 TransformWeightedPoint(
            Vector3 point,
            BoneWeight boneWeight,
            IReadOnlyList<Matrix4x4> skinningMatrices)
        {
            return skinningMatrices[boneWeight.boneIndex0].MultiplyPoint3x4(point) * boneWeight.weight0
                + skinningMatrices[boneWeight.boneIndex1].MultiplyPoint3x4(point) * boneWeight.weight1;
        }

        private static Vector3 TransformWeightedVector(
            Vector3 vector,
            BoneWeight boneWeight,
            IReadOnlyList<Matrix4x4> skinningMatrices)
        {
            return skinningMatrices[boneWeight.boneIndex0].MultiplyVector(vector) * boneWeight.weight0
                + skinningMatrices[boneWeight.boneIndex1].MultiplyVector(vector) * boneWeight.weight1;
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

        private Mesh CreateSingleTriangleMesh()
        {
            var mesh = new Mesh
            {
                name = "SingleTriangleInfluenceRaycastMesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f),
                },
                triangles = new[] { 0, 1, 2 },
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

            return new ProbeRendererFixture(
                renderer,
                boneObject.transform,
                mesh.bindposes[0],
                CreateProbeDeformedVertices());
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
                triangles = new[] { 0, 1, 2 },
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
            ProbeRendererFixture fixture,
            Vector3 barycentric,
            out float expectedMaxDelta)
        {
            var deltas = CreateProbeBlendShapeDelta();
            var weights = new[] { barycentric.x, barycentric.y, barycentric.z };
            var expectedScore = 0f;
            expectedMaxDelta = 0f;

            for (var i = 0; i < weights.Length; i++)
            {
                var deltaMagnitude = fixture.SkinningMatrix.MultiplyVector(deltas[i]).magnitude;
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
            public ProbeRendererFixture(
                SkinnedMeshRenderer renderer,
                Transform bone,
                Matrix4x4 bindPose,
                Vector3[] deformedVertices)
            {
                Renderer = renderer;
                Bone = bone;
                BindPose = bindPose;
                DeformedVertices = deformedVertices;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public Transform Bone { get; }
            public Matrix4x4 BindPose { get; }
            public Vector3[] DeformedVertices { get; }
            public Matrix4x4 SkinningMatrix => Bone.localToWorldMatrix * BindPose;

            public Vector3 TransformDeformedVertex(int vertexIndex)
            {
                return SkinningMatrix.MultiplyPoint3x4(DeformedVertices[vertexIndex]);
            }
        }
    }
}
