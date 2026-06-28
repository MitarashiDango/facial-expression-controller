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

        public static IReadOnlyDictionary<string, ReferenceBlendShapeSample> Sample(
            AnimationClip referenceClip,
            ExpressionEditModel model)
        {
            var result = new Dictionary<string, ReferenceBlendShapeSample>();
            if (referenceClip == null || model == null || model.entries == null)
            {
                return result;
            }

            var rendererPath = model.targetRenderer != null
                ? MiscUtil.GetPathInHierarchy(model.targetRenderer.gameObject, model.avatarRootObject)
                : null;
            var curves = AnimationUtility.GetCurveBindings(referenceClip)
                .Where(IsBlendShapeBinding)
                .Select(binding => new BlendShapeCurve(binding, AnimationUtility.GetEditorCurve(referenceClip, binding)))
                .Where(curve => curve.Curve != null && curve.Curve.length > 0)
                .ToList();

            foreach (var entry in model.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name))
                {
                    continue;
                }

                result[entry.name] = ResolveSample(entry.name, rendererPath, curves);
            }

            return result;
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
    }
}
