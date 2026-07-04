using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public sealed class ReferenceBlendShapeSample
    {
        public float startValue;
        public float endValue;
        public ReferenceBlendShapeSampleStatus status = ReferenceBlendShapeSampleStatus.Missing;
        public bool hasReferenceValue;
        public bool hasIntermediateKeys;
    }

    public static class ExpressionBlendShapeClipSampler
    {
        private const string BlendShapePrefix = "blendShape.";
        private static BlendShapeCurveCache _curveCache;
        private static SampleCache _sampleCache;

        public static IReadOnlyDictionary<string, ReferenceBlendShapeSample> Sample(
            AnimationClip referenceClip,
            ExpressionEditModel model)
        {
            var result = new Dictionary<string, ReferenceBlendShapeSample>(StringComparer.Ordinal);
            if (referenceClip == null || model == null || model.entries == null)
            {
                return result;
            }

            var rendererPath = model.targetRenderer != null
                ? MiscUtil.GetPathInHierarchy(model.targetRenderer.gameObject, model.avatarRootObject)
                : null;
            var entryNames = GetEntryNames(model);
            var dirtyCount = EditorUtility.GetDirtyCount(referenceClip);
            if (_sampleCache != null && _sampleCache.Matches(referenceClip, dirtyCount, rendererPath, entryNames))
            {
                return _sampleCache.Samples;
            }

            var curves = GetBlendShapeCurves(referenceClip, dirtyCount);
            foreach (var entryName in entryNames)
            {
                result[entryName] = ResolveSample(entryName, rendererPath, curves);
            }

            _sampleCache = new SampleCache(referenceClip, dirtyCount, rendererPath, entryNames, result);
            return result;
        }

        private static List<string> GetEntryNames(ExpressionEditModel model)
        {
            var result = new List<string>();
            foreach (var entry in model.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name))
                {
                    continue;
                }

                if (result.Contains(entry.name))
                {
                    continue;
                }

                result.Add(entry.name);
            }

            return result;
        }

        private static IReadOnlyList<BlendShapeCurve> GetBlendShapeCurves(AnimationClip referenceClip, int dirtyCount)
        {
            if (_curveCache != null && _curveCache.Matches(referenceClip, dirtyCount))
            {
                return _curveCache.Curves;
            }

            var curves = AnimationUtility.GetCurveBindings(referenceClip)
                .Where(IsBlendShapeBinding)
                .Select(binding => new BlendShapeCurve(binding, AnimationUtility.GetEditorCurve(referenceClip, binding)))
                .Where(curve => curve.Curve != null && curve.Curve.length > 0)
                .ToList();
            _curveCache = new BlendShapeCurveCache(referenceClip, dirtyCount, curves);
            return curves;
        }

        private static ReferenceBlendShapeSample ResolveSample(
            string blendShapeName,
            string rendererPath,
            IReadOnlyList<BlendShapeCurve> curves)
        {
            var pathMatches = curves
                .Where(curve => curve.Binding.path == rendererPath && curve.BlendShapeName == blendShapeName)
                .ToList();
            if (pathMatches.Count == 1)
            {
                return CreateSample(pathMatches[0], ReferenceBlendShapeSampleStatus.MatchedRendererPath);
            }

            if (pathMatches.Count > 1)
            {
                return CreateUnavailableSample(ReferenceBlendShapeSampleStatus.Ambiguous);
            }

            var nameMatches = curves
                .Where(curve => curve.BlendShapeName == blendShapeName)
                .ToList();
            if (nameMatches.Count == 1)
            {
                return CreateSample(nameMatches[0], ReferenceBlendShapeSampleStatus.MatchedUniqueName);
            }

            return CreateUnavailableSample(nameMatches.Count == 0
                ? ReferenceBlendShapeSampleStatus.Missing
                : ReferenceBlendShapeSampleStatus.Ambiguous);
        }

        private static ReferenceBlendShapeSample CreateSample(
            BlendShapeCurve curve,
            ReferenceBlendShapeSampleStatus matchStatus)
        {
            var hasIntermediateKeys = curve.Curve.length > 2;
            var firstKey = curve.Curve.keys[0];
            var lastKey = curve.Curve.keys[curve.Curve.length - 1];
            return new ReferenceBlendShapeSample
            {
                startValue = Mathf.Clamp(curve.Curve.Evaluate(firstKey.time), 0f, 100f),
                endValue = Mathf.Clamp(curve.Curve.Evaluate(lastKey.time), 0f, 100f),
                status = hasIntermediateKeys
                    ? ReferenceBlendShapeSampleStatus.IntermediateKeysIgnored
                    : matchStatus,
                hasReferenceValue = true,
                hasIntermediateKeys = hasIntermediateKeys,
            };
        }

        private static ReferenceBlendShapeSample CreateUnavailableSample(ReferenceBlendShapeSampleStatus status)
        {
            return new ReferenceBlendShapeSample
            {
                status = status,
                hasReferenceValue = false,
            };
        }

        private static bool IsBlendShapeBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer)
                && binding.propertyName.StartsWith(BlendShapePrefix, StringComparison.Ordinal);
        }

        private sealed class BlendShapeCurve
        {
            public BlendShapeCurve(EditorCurveBinding binding, AnimationCurve curve)
            {
                Binding = binding;
                Curve = curve;
                BlendShapeName = binding.propertyName.Substring(BlendShapePrefix.Length);
            }

            public EditorCurveBinding Binding { get; }
            public AnimationCurve Curve { get; }
            public string BlendShapeName { get; }
        }

        private sealed class BlendShapeCurveCache
        {
            public BlendShapeCurveCache(AnimationClip referenceClip, int dirtyCount, IReadOnlyList<BlendShapeCurve> curves)
            {
                ReferenceClip = referenceClip;
                DirtyCount = dirtyCount;
                Curves = curves;
            }

            public AnimationClip ReferenceClip { get; }
            public int DirtyCount { get; }
            public IReadOnlyList<BlendShapeCurve> Curves { get; }

            public bool Matches(AnimationClip referenceClip, int dirtyCount)
            {
                return ReferenceEquals(ReferenceClip, referenceClip) && DirtyCount == dirtyCount;
            }
        }

        private sealed class SampleCache
        {
            public SampleCache(
                AnimationClip referenceClip,
                int dirtyCount,
                string rendererPath,
                IReadOnlyList<string> entryNames,
                IReadOnlyDictionary<string, ReferenceBlendShapeSample> samples)
            {
                ReferenceClip = referenceClip;
                DirtyCount = dirtyCount;
                RendererPath = rendererPath;
                EntryNames = entryNames.ToArray();
                Samples = samples;
            }

            public AnimationClip ReferenceClip { get; }
            public int DirtyCount { get; }
            public string RendererPath { get; }
            public string[] EntryNames { get; }
            public IReadOnlyDictionary<string, ReferenceBlendShapeSample> Samples { get; }

            public bool Matches(
                AnimationClip referenceClip,
                int dirtyCount,
                string rendererPath,
                IReadOnlyList<string> entryNames)
            {
                if (!ReferenceEquals(ReferenceClip, referenceClip)
                    || DirtyCount != dirtyCount
                    || !string.Equals(RendererPath, rendererPath, StringComparison.Ordinal)
                    || EntryNames.Length != entryNames.Count)
                {
                    return false;
                }

                for (var i = 0; i < EntryNames.Length; i++)
                {
                    if (!string.Equals(EntryNames[i], entryNames[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
