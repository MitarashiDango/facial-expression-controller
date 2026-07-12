using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public sealed class BlendShapeInfluenceProbeOptions
    {
        public const float DefaultWorldRadius = 0.02f;
        public const float DefaultMinimumDelta = 0.0001f;
        public const int DefaultMaxResults = 50;

        public float worldRadius = DefaultWorldRadius;
        public float minimumDelta = DefaultMinimumDelta;
        public int maxResults = DefaultMaxResults;
    }

    public sealed class BlendShapeInfluenceHit
    {
        public BlendShapeInfluenceHit(
            Vector3 worldPosition,
            Vector3 worldNormal,
            int triangleIndex,
            int vertexIndex0,
            int vertexIndex1,
            int vertexIndex2,
            Vector3 barycentricCoordinate)
        {
            WorldPosition = worldPosition;
            WorldNormal = worldNormal;
            TriangleIndex = triangleIndex;
            VertexIndices = new[] { vertexIndex0, vertexIndex1, vertexIndex2 };
            BarycentricCoordinate = barycentricCoordinate;
        }

        public Vector3 WorldPosition { get; }
        public Vector3 WorldNormal { get; }
        public int TriangleIndex { get; }
        public IReadOnlyList<int> VertexIndices { get; }
        public Vector3 BarycentricCoordinate { get; }
    }

    public sealed class BlendShapeInfluenceResult
    {
        public BlendShapeInfluenceResult(int blendShapeIndex, string blendShapeName, float score, float maxDelta)
        {
            BlendShapeIndex = blendShapeIndex;
            BlendShapeName = blendShapeName;
            Score = score;
            MaxDelta = maxDelta;
        }

        public int BlendShapeIndex { get; }
        public string BlendShapeName { get; }
        public float Score { get; }
        public float MaxDelta { get; }
    }

    internal readonly struct BlendShapeInfluenceWeightedVertex
    {
        public BlendShapeInfluenceWeightedVertex(int index, float weight)
        {
            this.index = index;
            this.weight = weight;
        }

        public readonly int index;
        public readonly float weight;
    }

    public sealed class BlendShapeInfluenceProbeContext : IDisposable
    {
        private readonly List<Vector3> _sourceVertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<int> _subMeshTriangles = new List<int>();
        private readonly Dictionary<int, float> _weightsByIndex = new Dictionary<int, float>();
        private readonly List<BlendShapeInfluenceWeightedVertex> _weightedVertices = new List<BlendShapeInfluenceWeightedVertex>();
        private readonly List<BlendShapeInfluenceResult> _results = new List<BlendShapeInfluenceResult>();
        private Mesh _sampleMesh;
        private Vector3[] _worldVertices = Array.Empty<Vector3>();
        private Vector3[] _deltaVertices = Array.Empty<Vector3>();
        private Vector3[] _deltaNormals = Array.Empty<Vector3>();
        private Vector3[] _deltaTangents = Array.Empty<Vector3>();
        private Vector3[] _deformedVertices = Array.Empty<Vector3>();
        private Vector3[] _blendShapeDelta0 = Array.Empty<Vector3>();
        private Vector3[] _blendShapeDelta1 = Array.Empty<Vector3>();
        private Vector3[] _worldSkinnedVertices = Array.Empty<Vector3>();
        private Mesh _cachedSkinningMesh;
        private byte[] _bonesPerVertex = Array.Empty<byte>();
        private int[] _boneWeightStarts = Array.Empty<int>();
        private BoneWeight1[] _allBoneWeights = Array.Empty<BoneWeight1>();
        private Matrix4x4[] _bindPoses = Array.Empty<Matrix4x4>();
        private Transform[] _bones = Array.Empty<Transform>();
        private Matrix4x4[] _skinningMatrices = Array.Empty<Matrix4x4>();
        private int _maxBonesPerVertex;
        private bool _hasCachedMeshSkinningData;
        private bool _hasValidSkinningData;

        internal Dictionary<int, float> WeightsByIndex => _weightsByIndex;
        internal List<BlendShapeInfluenceWeightedVertex> WeightedVertices => _weightedVertices;
        internal List<BlendShapeInfluenceResult> Results => _results;

        public void Dispose()
        {
            if (_sampleMesh == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(_sampleMesh);
            _sampleMesh = null;
        }

        internal Mesh GetSampleMesh(Mesh sourceMesh)
        {
            if (_sampleMesh == null)
            {
                _sampleMesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            else
            {
                _sampleMesh.Clear();
            }

            _sampleMesh.name = sourceMesh != null
                ? $"{sourceMesh.name}_BlendShapeInfluenceProbe"
                : "BlendShapeInfluenceProbe";
            return _sampleMesh;
        }

        internal Vector3[] BuildWorldVertices(Mesh mesh, Matrix4x4 localToWorldMatrix, out int vertexCount)
        {
            _sourceVertices.Clear();
            mesh.GetVertices(_sourceVertices);
            vertexCount = _sourceVertices.Count;
            EnsureVector3Array(ref _worldVertices, vertexCount);
            for (var i = 0; i < vertexCount; i++)
            {
                _worldVertices[i] = localToWorldMatrix.MultiplyPoint3x4(_sourceVertices[i]);
            }

            return _worldVertices;
        }

        internal List<int> GetTriangles(Mesh mesh)
        {
            _triangles.Clear();
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                _subMeshTriangles.Clear();
                mesh.GetTriangles(_subMeshTriangles, i, true);
                _triangles.AddRange(_subMeshTriangles);
            }

            return _triangles;
        }

        internal void GetDeltaArrays(
            int vertexCount,
            out Vector3[] deltaVertices,
            out Vector3[] deltaNormals,
            out Vector3[] deltaTangents)
        {
            EnsureExactVector3Array(ref _deltaVertices, vertexCount);
            EnsureExactVector3Array(ref _deltaNormals, vertexCount);
            EnsureExactVector3Array(ref _deltaTangents, vertexCount);
            deltaVertices = _deltaVertices;
            deltaNormals = _deltaNormals;
            deltaTangents = _deltaTangents;
        }

        internal bool PrepareSkinning(SkinnedMeshRenderer renderer, Mesh mesh)
        {
            _hasValidSkinningData = false;
            if (renderer == null || mesh == null)
            {
                return false;
            }

            var meshChanged = _cachedSkinningMesh != mesh;
            if (meshChanged)
            {
                CacheMeshSkinningData(mesh);
            }

            if (!_hasCachedMeshSkinningData)
            {
                return false;
            }

            // Unity 2022.3 には bones を非割当で取得する公開APIがない。
            // 同じ renderer のまま配列参照や順序が変更される場合を取りこぼさないよう、毎回更新する。
            _bones = renderer.bones;

            if (_bones.Length < _bindPoses.Length)
            {
                return false;
            }

            if (_skinningMatrices.Length < _bindPoses.Length)
            {
                _skinningMatrices = new Matrix4x4[_bindPoses.Length];
            }

            for (var i = 0; i < _bindPoses.Length; i++)
            {
                if (_bones[i] == null)
                {
                    return false;
                }

                // Transform は呼び出し間で変化し得るため、current matrix は TryProbe ごとに必ず更新する。
                _skinningMatrices[i] = _bones[i].localToWorldMatrix * _bindPoses[i];
            }

            _maxBonesPerVertex = ResolveMaxBonesPerVertex(renderer);
            _hasValidSkinningData = true;
            return true;
        }

        private void CacheMeshSkinningData(Mesh mesh)
        {
            _cachedSkinningMesh = mesh;
            _hasCachedMeshSkinningData = false;

            var bonesPerVertex = mesh.GetBonesPerVertex();
            var allBoneWeights = mesh.GetAllBoneWeights();
            if (bonesPerVertex.Length != mesh.vertexCount || allBoneWeights.Length == 0)
            {
                return;
            }

            if (_bonesPerVertex.Length != bonesPerVertex.Length)
            {
                _bonesPerVertex = new byte[bonesPerVertex.Length];
            }

            if (_boneWeightStarts.Length != bonesPerVertex.Length + 1)
            {
                _boneWeightStarts = new int[bonesPerVertex.Length + 1];
            }

            if (_allBoneWeights.Length != allBoneWeights.Length)
            {
                _allBoneWeights = new BoneWeight1[allBoneWeights.Length];
            }

            var boneWeightIndex = 0;
            for (var vertexIndex = 0; vertexIndex < bonesPerVertex.Length; vertexIndex++)
            {
                _bonesPerVertex[vertexIndex] = bonesPerVertex[vertexIndex];
                _boneWeightStarts[vertexIndex] = boneWeightIndex;
                boneWeightIndex += bonesPerVertex[vertexIndex];
            }

            _boneWeightStarts[bonesPerVertex.Length] = boneWeightIndex;
            if (boneWeightIndex != allBoneWeights.Length)
            {
                return;
            }

            for (var i = 0; i < allBoneWeights.Length; i++)
            {
                _allBoneWeights[i] = allBoneWeights[i];
            }

            _bindPoses = mesh.bindposes;
            _hasCachedMeshSkinningData = _bindPoses.Length > 0;
        }

        private static int ResolveMaxBonesPerVertex(SkinnedMeshRenderer renderer)
        {
            switch (renderer.quality)
            {
                case SkinQuality.Bone1:
                    return 1;
                case SkinQuality.Bone2:
                    return 2;
                case SkinQuality.Bone4:
                    return 4;
                default:
                    switch (QualitySettings.skinWeights)
                    {
                        case SkinWeights.OneBone:
                            return 1;
                        case SkinWeights.TwoBones:
                            return 2;
                        case SkinWeights.FourBones:
                            return 4;
                        case SkinWeights.Unlimited:
                            return int.MaxValue;
                        default:
                            return 0;
                    }
            }
        }

        internal Vector3 TransformSkinnedDelta(int vertexIndex, Vector3 delta)
        {
            if (!_hasValidSkinningData || vertexIndex < 0 || vertexIndex >= _bonesPerVertex.Length)
            {
                return Vector3.zero;
            }

            var worldDelta = Vector3.zero;
            var totalWeight = 0f;
            var start = _boneWeightStarts[vertexIndex];
            var count = Mathf.Min(_bonesPerVertex[vertexIndex], _maxBonesPerVertex);
            for (var i = 0; i < count; i++)
            {
                var boneWeight = _allBoneWeights[start + i];
                if (!IsValidBoneWeight(boneWeight))
                {
                    continue;
                }

                worldDelta += _skinningMatrices[boneWeight.boneIndex].MultiplyVector(delta) * boneWeight.weight;
                totalWeight += boneWeight.weight;
            }

            return totalWeight > Mathf.Epsilon ? worldDelta / totalWeight : Vector3.zero;
        }

        internal Mesh BuildSkinnedSampleMesh(SkinnedMeshRenderer renderer, Mesh sourceMesh)
        {
            var vertexCount = sourceMesh.vertexCount;
            EnsureExactVector3Array(ref _deformedVertices, vertexCount);
            EnsureExactVector3Array(ref _blendShapeDelta0, vertexCount);
            EnsureExactVector3Array(ref _blendShapeDelta1, vertexCount);
            EnsureExactVector3Array(ref _worldSkinnedVertices, vertexCount);
            EnsureExactVector3Array(ref _deltaNormals, vertexCount);
            EnsureExactVector3Array(ref _deltaTangents, vertexCount);
            _sourceVertices.Clear();
            sourceMesh.GetVertices(_sourceVertices);
            for (var i = 0; i < vertexCount; i++)
            {
                _deformedVertices[i] = _sourceVertices[i];
            }

            for (var blendShapeIndex = 0; blendShapeIndex < sourceMesh.blendShapeCount; blendShapeIndex++)
            {
                var weight = renderer.GetBlendShapeWeight(blendShapeIndex);
                if (Mathf.Approximately(weight, 0f))
                {
                    continue;
                }

                ApplyBlendShape(sourceMesh, blendShapeIndex, weight, vertexCount);
            }

            for (var i = 0; i < vertexCount; i++)
            {
                _worldSkinnedVertices[i] = _hasValidSkinningData
                    ? TransformSkinnedPoint(i, _deformedVertices[i])
                    : renderer.localToWorldMatrix.MultiplyPoint3x4(_deformedVertices[i]);
            }

            var sampleMesh = GetSampleMesh(sourceMesh);
            sampleMesh.vertices = _worldSkinnedVertices;
            sampleMesh.SetTriangles(GetTriangles(sourceMesh), 0, true);
            sampleMesh.RecalculateBounds();
            return sampleMesh;
        }

        private void ApplyBlendShape(Mesh mesh, int blendShapeIndex, float weight, int vertexCount)
        {
            var frameCount = mesh.GetBlendShapeFrameCount(blendShapeIndex);
            if (frameCount <= 0)
            {
                return;
            }

            if (frameCount == 1 || weight <= mesh.GetBlendShapeFrameWeight(blendShapeIndex, 0))
            {
                var frameWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, 0);
                mesh.GetBlendShapeFrameVertices(
                    blendShapeIndex,
                    0,
                    _blendShapeDelta0,
                    _deltaNormals,
                    _deltaTangents);
                AddScaledDelta(_blendShapeDelta0, SafeDivide(weight, frameWeight), vertexCount);
                return;
            }

            for (var frameIndex = 1; frameIndex < frameCount; frameIndex++)
            {
                var upperWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex);
                if (weight > upperWeight)
                {
                    continue;
                }

                var lowerWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex - 1);
                mesh.GetBlendShapeFrameVertices(
                    blendShapeIndex,
                    frameIndex - 1,
                    _blendShapeDelta0,
                    _deltaNormals,
                    _deltaTangents);
                mesh.GetBlendShapeFrameVertices(
                    blendShapeIndex,
                    frameIndex,
                    _blendShapeDelta1,
                    _deltaNormals,
                    _deltaTangents);
                var interpolation = Mathf.InverseLerp(lowerWeight, upperWeight, weight);
                for (var i = 0; i < vertexCount; i++)
                {
                    _deformedVertices[i] += Vector3.LerpUnclamped(
                        _blendShapeDelta0[i],
                        _blendShapeDelta1[i],
                        interpolation);
                }

                return;
            }

            var lastFrameIndex = frameCount - 1;
            var previousFrameIndex = lastFrameIndex - 1;
            var previousFrameWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, previousFrameIndex);
            var lastFrameWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, lastFrameIndex);
            mesh.GetBlendShapeFrameVertices(
                blendShapeIndex,
                previousFrameIndex,
                _blendShapeDelta0,
                _deltaNormals,
                _deltaTangents);
            mesh.GetBlendShapeFrameVertices(
                blendShapeIndex,
                lastFrameIndex,
                _blendShapeDelta1,
                _deltaNormals,
                _deltaTangents);
            var extrapolation = SafeDivide(weight - previousFrameWeight, lastFrameWeight - previousFrameWeight);
            for (var i = 0; i < vertexCount; i++)
            {
                _deformedVertices[i] += Vector3.LerpUnclamped(
                    _blendShapeDelta0[i],
                    _blendShapeDelta1[i],
                    extrapolation);
            }
        }

        private void AddScaledDelta(Vector3[] delta, float scale, int vertexCount)
        {
            for (var i = 0; i < vertexCount; i++)
            {
                _deformedVertices[i] += delta[i] * scale;
            }
        }

        private Vector3 TransformSkinnedPoint(int vertexIndex, Vector3 point)
        {
            var worldPoint = Vector3.zero;
            var totalWeight = 0f;
            var start = _boneWeightStarts[vertexIndex];
            var count = Mathf.Min(_bonesPerVertex[vertexIndex], _maxBonesPerVertex);
            for (var i = 0; i < count; i++)
            {
                var boneWeight = _allBoneWeights[start + i];
                if (!IsValidBoneWeight(boneWeight))
                {
                    continue;
                }

                worldPoint += _skinningMatrices[boneWeight.boneIndex].MultiplyPoint3x4(point) * boneWeight.weight;
                totalWeight += boneWeight.weight;
            }

            return totalWeight > Mathf.Epsilon ? worldPoint / totalWeight : Vector3.zero;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > Mathf.Epsilon ? value / divisor : 1f;
        }

        private bool IsValidBoneWeight(BoneWeight1 boneWeight)
        {
            return boneWeight.weight > 0f
                && boneWeight.boneIndex >= 0
                && boneWeight.boneIndex < _bindPoses.Length;
        }

        private static void EnsureVector3Array(ref Vector3[] array, int requiredLength)
        {
            if (array.Length < requiredLength)
            {
                array = new Vector3[requiredLength];
            }
        }

        private static void EnsureExactVector3Array(ref Vector3[] array, int requiredLength)
        {
            if (array.Length != requiredLength)
            {
                array = new Vector3[requiredLength];
            }
        }
    }

    public static class BlendShapeInfluenceProbe
    {
        private const float RaycastEpsilon = 1e-7f;
        private const float MinimumTriangleVertexWeight = 0.0001f;

        public static bool TryProbe(
            SkinnedMeshRenderer renderer,
            Ray worldRay,
            BlendShapeInfluenceProbeOptions options,
            out BlendShapeInfluenceHit hit,
            out IReadOnlyList<BlendShapeInfluenceResult> results)
        {
            using (var context = new BlendShapeInfluenceProbeContext())
            {
                return TryProbe(renderer, worldRay, options, context, out hit, out results);
            }
        }

        public static bool TryProbe(
            SkinnedMeshRenderer renderer,
            Ray worldRay,
            BlendShapeInfluenceProbeOptions options,
            BlendShapeInfluenceProbeContext context,
            out BlendShapeInfluenceHit hit,
            out IReadOnlyList<BlendShapeInfluenceResult> results)
        {
            hit = null;
            results = Array.Empty<BlendShapeInfluenceResult>();

            var blendShapeMesh = renderer != null ? renderer.sharedMesh : null;
            if (renderer == null || blendShapeMesh == null || blendShapeMesh.blendShapeCount == 0)
            {
                return false;
            }

            var ownsContext = context == null;
            var resolvedContext = context ?? new BlendShapeInfluenceProbeContext();

            try
            {
                var hasSkinning = resolvedContext.PrepareSkinning(renderer, blendShapeMesh);
                var bakedMesh = resolvedContext.BuildSkinnedSampleMesh(renderer, blendShapeMesh);
                var sampleToWorldMatrix = Matrix4x4.identity;
                if (bakedMesh.vertexCount != blendShapeMesh.vertexCount)
                {
                    return false;
                }

                if (!TryRaycastMesh(bakedMesh, sampleToWorldMatrix, worldRay, resolvedContext, out hit))
                {
                    return false;
                }

                results = CalculateSkinnedInfluences(
                    renderer,
                    blendShapeMesh,
                    bakedMesh,
                    sampleToWorldMatrix,
                    hit,
                    options,
                    resolvedContext,
                    hasSkinning);
                return true;
            }
            finally
            {
                if (ownsContext)
                {
                    resolvedContext.Dispose();
                }
            }
        }

        public static bool TryRaycastMesh(
            Mesh mesh,
            Matrix4x4 localToWorldMatrix,
            Ray worldRay,
            out BlendShapeInfluenceHit hit)
        {
            return TryRaycastMesh(mesh, localToWorldMatrix, worldRay, null, out hit);
        }

        public static bool TryRaycastMesh(
            Mesh mesh,
            Matrix4x4 localToWorldMatrix,
            Ray worldRay,
            BlendShapeInfluenceProbeContext context,
            out BlendShapeInfluenceHit hit)
        {
            hit = null;
            if (mesh == null || mesh.vertexCount == 0)
            {
                return false;
            }

            var resolvedContext = context ?? new BlendShapeInfluenceProbeContext();
            var triangles = resolvedContext.GetTriangles(mesh);
            if (triangles.Count < 3)
            {
                return false;
            }

            var worldVertices = resolvedContext.BuildWorldVertices(mesh, localToWorldMatrix, out var worldVertexCount);
            var closestDistance = float.PositiveInfinity;
            var closestTriangle = -1;
            var closestBarycentric = Vector3.zero;
            var closestNormal = Vector3.up;

            for (var i = 0; i <= triangles.Count - 3; i += 3)
            {
                var index0 = triangles[i];
                var index1 = triangles[i + 1];
                var index2 = triangles[i + 2];
                if (!IsValidVertexIndex(index0, worldVertexCount)
                    || !IsValidVertexIndex(index1, worldVertexCount)
                    || !IsValidVertexIndex(index2, worldVertexCount))
                {
                    continue;
                }

                if (!TryIntersectTriangle(
                    worldRay,
                    worldVertices[index0],
                    worldVertices[index1],
                    worldVertices[index2],
                    out var distance,
                    out var barycentric))
                {
                    continue;
                }

                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closestTriangle = i;
                closestBarycentric = barycentric;
                closestNormal = Vector3.Cross(
                    worldVertices[index1] - worldVertices[index0],
                    worldVertices[index2] - worldVertices[index0]).normalized;
                if (Vector3.Dot(closestNormal, worldRay.direction) > 0f)
                {
                    closestNormal = -closestNormal;
                }
            }

            if (closestTriangle < 0)
            {
                return false;
            }

            var vertexIndex0 = triangles[closestTriangle];
            var vertexIndex1 = triangles[closestTriangle + 1];
            var vertexIndex2 = triangles[closestTriangle + 2];
            var worldPosition =
                worldVertices[vertexIndex0] * closestBarycentric.x
                + worldVertices[vertexIndex1] * closestBarycentric.y
                + worldVertices[vertexIndex2] * closestBarycentric.z;

            hit = new BlendShapeInfluenceHit(
                worldPosition,
                closestNormal,
                closestTriangle / 3,
                vertexIndex0,
                vertexIndex1,
                vertexIndex2,
                closestBarycentric);
            return true;
        }

        public static IReadOnlyList<BlendShapeInfluenceResult> CalculateInfluences(
            Mesh blendShapeMesh,
            Mesh sampleMesh,
            Matrix4x4 localToWorldMatrix,
            BlendShapeInfluenceHit hit,
            BlendShapeInfluenceProbeOptions options)
        {
            return CalculateInfluences(
                blendShapeMesh,
                sampleMesh,
                localToWorldMatrix,
                localToWorldMatrix,
                hit,
                options,
                null);
        }

        public static IReadOnlyList<BlendShapeInfluenceResult> CalculateInfluences(
            Mesh blendShapeMesh,
            Mesh sampleMesh,
            Matrix4x4 localToWorldMatrix,
            BlendShapeInfluenceHit hit,
            BlendShapeInfluenceProbeOptions options,
            BlendShapeInfluenceProbeContext context)
        {
            return CalculateInfluences(
                blendShapeMesh,
                sampleMesh,
                localToWorldMatrix,
                localToWorldMatrix,
                hit,
                options,
                context);
        }

        public static IReadOnlyList<BlendShapeInfluenceResult> CalculateInfluences(
            Mesh blendShapeMesh,
            Mesh sampleMesh,
            Matrix4x4 sampleToWorldMatrix,
            Matrix4x4 deltaToWorldMatrix,
            BlendShapeInfluenceHit hit,
            BlendShapeInfluenceProbeOptions options)
        {
            return CalculateInfluences(
                blendShapeMesh,
                sampleMesh,
                sampleToWorldMatrix,
                deltaToWorldMatrix,
                hit,
                options,
                null);
        }

        public static IReadOnlyList<BlendShapeInfluenceResult> CalculateInfluences(
            Mesh blendShapeMesh,
            Mesh sampleMesh,
            Matrix4x4 sampleToWorldMatrix,
            Matrix4x4 deltaToWorldMatrix,
            BlendShapeInfluenceHit hit,
            BlendShapeInfluenceProbeOptions options,
            BlendShapeInfluenceProbeContext context)
        {
            if (blendShapeMesh == null
                || sampleMesh == null
                || hit == null
                || blendShapeMesh.vertexCount == 0
                || blendShapeMesh.vertexCount != sampleMesh.vertexCount
                || blendShapeMesh.blendShapeCount == 0)
            {
                return Array.Empty<BlendShapeInfluenceResult>();
            }

            var resolvedOptions = options ?? new BlendShapeInfluenceProbeOptions();
            var resolvedContext = context ?? new BlendShapeInfluenceProbeContext();
            var weightedVertices = CollectWeightedVertices(
                sampleMesh,
                sampleToWorldMatrix,
                hit,
                Mathf.Max(0f, resolvedOptions.worldRadius),
                resolvedContext);
            if (weightedVertices.Count == 0)
            {
                return Array.Empty<BlendShapeInfluenceResult>();
            }

            var vertexCount = blendShapeMesh.vertexCount;
            resolvedContext.GetDeltaArrays(vertexCount, out var deltaVertices, out var deltaNormals, out var deltaTangents);
            var minimumDelta = Mathf.Max(0f, resolvedOptions.minimumDelta);
            var results = resolvedContext.Results;
            results.Clear();

            for (var blendShapeIndex = 0; blendShapeIndex < blendShapeMesh.blendShapeCount; blendShapeIndex++)
            {
                var frameCount = blendShapeMesh.GetBlendShapeFrameCount(blendShapeIndex);
                if (frameCount <= 0)
                {
                    continue;
                }

                blendShapeMesh.GetBlendShapeFrameVertices(
                    blendShapeIndex,
                    frameCount - 1,
                    deltaVertices,
                    deltaNormals,
                    deltaTangents);

                var score = 0f;
                var maxDelta = 0f;
                for (var i = 0; i < weightedVertices.Count; i++)
                {
                    var weightedVertex = weightedVertices[i];
                    var worldDelta = deltaToWorldMatrix.MultiplyVector(deltaVertices[weightedVertex.index]);
                    var deltaMagnitude = worldDelta.magnitude;
                    if (deltaMagnitude > maxDelta)
                    {
                        maxDelta = deltaMagnitude;
                    }

                    var weightedScore = deltaMagnitude * weightedVertex.weight;
                    if (weightedScore > score)
                    {
                        score = weightedScore;
                    }
                }

                if (score < minimumDelta)
                {
                    continue;
                }

                results.Add(new BlendShapeInfluenceResult(
                    blendShapeIndex,
                    blendShapeMesh.GetBlendShapeName(blendShapeIndex),
                    score,
                    maxDelta));
            }

            SortAndLimitResults(results, resolvedOptions.maxResults);

            return results.Count == 0 ? Array.Empty<BlendShapeInfluenceResult>() : results.ToArray();
        }

        private static void SortAndLimitResults(List<BlendShapeInfluenceResult> results, int maxResults)
        {
            results.Sort((left, right) =>
            {
                var scoreComparison = right.Score.CompareTo(left.Score);
                return scoreComparison != 0
                    ? scoreComparison
                    : string.Compare(left.BlendShapeName, right.BlendShapeName, StringComparison.Ordinal);
            });
            if (maxResults > 0 && results.Count > maxResults)
            {
                results.RemoveRange(maxResults, results.Count - maxResults);
            }
        }

        private static IReadOnlyList<BlendShapeInfluenceResult> CalculateSkinnedInfluences(
            SkinnedMeshRenderer renderer,
            Mesh blendShapeMesh,
            Mesh sampleMesh,
            Matrix4x4 sampleToWorldMatrix,
            BlendShapeInfluenceHit hit,
            BlendShapeInfluenceProbeOptions options,
            BlendShapeInfluenceProbeContext context,
            bool hasSkinning)
        {
            if (!hasSkinning)
            {
                return CalculateInfluences(
                    blendShapeMesh,
                    sampleMesh,
                    sampleToWorldMatrix,
                    renderer.localToWorldMatrix,
                    hit,
                    options,
                    context);
            }

            var resolvedOptions = options ?? new BlendShapeInfluenceProbeOptions();
            var weightedVertices = CollectWeightedVertices(
                sampleMesh,
                sampleToWorldMatrix,
                hit,
                Mathf.Max(0f, resolvedOptions.worldRadius),
                context);
            if (weightedVertices.Count == 0)
            {
                return Array.Empty<BlendShapeInfluenceResult>();
            }

            var vertexCount = blendShapeMesh.vertexCount;
            context.GetDeltaArrays(vertexCount, out var deltaVertices, out var deltaNormals, out var deltaTangents);
            var minimumDelta = Mathf.Max(0f, resolvedOptions.minimumDelta);
            var results = context.Results;
            results.Clear();

            for (var blendShapeIndex = 0; blendShapeIndex < blendShapeMesh.blendShapeCount; blendShapeIndex++)
            {
                var frameCount = blendShapeMesh.GetBlendShapeFrameCount(blendShapeIndex);
                if (frameCount <= 0)
                {
                    continue;
                }

                blendShapeMesh.GetBlendShapeFrameVertices(
                    blendShapeIndex,
                    frameCount - 1,
                    deltaVertices,
                    deltaNormals,
                    deltaTangents);

                var score = 0f;
                var maxDelta = 0f;
                for (var i = 0; i < weightedVertices.Count; i++)
                {
                    var weightedVertex = weightedVertices[i];
                    var deltaMagnitude = context.TransformSkinnedDelta(
                        weightedVertex.index,
                        deltaVertices[weightedVertex.index]).magnitude;
                    maxDelta = Mathf.Max(maxDelta, deltaMagnitude);
                    score = Mathf.Max(score, deltaMagnitude * weightedVertex.weight);
                }

                if (score < minimumDelta)
                {
                    continue;
                }

                results.Add(new BlendShapeInfluenceResult(
                    blendShapeIndex,
                    blendShapeMesh.GetBlendShapeName(blendShapeIndex),
                    score,
                    maxDelta));
            }

            SortAndLimitResults(results, resolvedOptions.maxResults);
            return results.Count == 0 ? Array.Empty<BlendShapeInfluenceResult>() : results.ToArray();
        }

        private static List<BlendShapeInfluenceWeightedVertex> CollectWeightedVertices(
            Mesh sampleMesh,
            Matrix4x4 sampleToWorldMatrix,
            BlendShapeInfluenceHit hit,
            float worldRadius,
            BlendShapeInfluenceProbeContext context)
        {
            var worldVertices = context.BuildWorldVertices(sampleMesh, sampleToWorldMatrix, out var vertexCount);
            var weightsByIndex = context.WeightsByIndex;
            weightsByIndex.Clear();

            AddWeight(weightsByIndex, hit.VertexIndices[0], hit.BarycentricCoordinate.x);
            AddWeight(weightsByIndex, hit.VertexIndices[1], hit.BarycentricCoordinate.y);
            AddWeight(weightsByIndex, hit.VertexIndices[2], hit.BarycentricCoordinate.z);

            if (worldRadius > 0f)
            {
                var radiusSqr = worldRadius * worldRadius;
                for (var i = 0; i < vertexCount; i++)
                {
                    var distanceSqr = (worldVertices[i] - hit.WorldPosition).sqrMagnitude;
                    if (distanceSqr > radiusSqr)
                    {
                        continue;
                    }

                    var distance = Mathf.Sqrt(distanceSqr);
                    var weight = Mathf.Clamp01(1f - distance / worldRadius);
                    AddWeight(weightsByIndex, i, weight);
                }
            }

            var weightedVertices = context.WeightedVertices;
            weightedVertices.Clear();
            foreach (var pair in weightsByIndex)
            {
                if (IsValidVertexIndex(pair.Key, sampleMesh.vertexCount) && pair.Value > 0f)
                {
                    weightedVertices.Add(new BlendShapeInfluenceWeightedVertex(pair.Key, pair.Value));
                }
            }

            return weightedVertices;
        }

        private static void AddWeight(Dictionary<int, float> weightsByIndex, int index, float weight)
        {
            if (index < 0)
            {
                return;
            }

            var normalizedWeight = Mathf.Max(weight, MinimumTriangleVertexWeight);
            if (!weightsByIndex.TryGetValue(index, out var currentWeight) || normalizedWeight > currentWeight)
            {
                weightsByIndex[index] = normalizedWeight;
            }
        }

        private static bool TryIntersectTriangle(
            Ray ray,
            Vector3 vertex0,
            Vector3 vertex1,
            Vector3 vertex2,
            out float distance,
            out Vector3 barycentric)
        {
            distance = 0f;
            barycentric = Vector3.zero;

            var edge1 = vertex1 - vertex0;
            var edge2 = vertex2 - vertex0;
            var p = Vector3.Cross(ray.direction, edge2);
            var determinant = Vector3.Dot(edge1, p);
            if (Mathf.Abs(determinant) < RaycastEpsilon)
            {
                return false;
            }

            var inverseDeterminant = 1f / determinant;
            var t = ray.origin - vertex0;
            var u = Vector3.Dot(t, p) * inverseDeterminant;
            if (u < -RaycastEpsilon || u > 1f + RaycastEpsilon)
            {
                return false;
            }

            var q = Vector3.Cross(t, edge1);
            var v = Vector3.Dot(ray.direction, q) * inverseDeterminant;
            if (v < -RaycastEpsilon || u + v > 1f + RaycastEpsilon)
            {
                return false;
            }

            distance = Vector3.Dot(edge2, q) * inverseDeterminant;
            if (distance < -RaycastEpsilon)
            {
                return false;
            }

            barycentric = new Vector3(1f - u - v, u, v);
            return true;
        }

        private static bool IsValidVertexIndex(int index, int vertexCount)
        {
            return index >= 0 && index < vertexCount;
        }

    }
}
