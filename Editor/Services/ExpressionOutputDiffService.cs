using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public static class ExpressionOutputDiffService
    {
        public const float DefaultTolerance = 0.0001f;

        public static bool TryValidateSettings(ExpressionOutputSettings settings, out string message)
        {
            var normalizedSettings = NormalizeSettings(settings);
            if (normalizedSettings.mode == BlendShapeOutputMode.ReferenceClipDiff
                && normalizedSettings.referenceClip == null)
            {
                message = "参照クリップを指定してください。";
                return false;
            }

            message = "";
            return true;
        }

        public static IReadOnlyList<BlendShapeOutputDecision> Evaluate(
            ExpressionEditModel model,
            ExpressionOutputSettings settings = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var normalizedSettings = NormalizeSettings(settings);
            if (!TryValidateSettings(normalizedSettings, out var message))
            {
                throw new InvalidOperationException(message);
            }

            IReadOnlyDictionary<string, ReferenceBlendShapeSample> referenceSamples = null;
            if (normalizedSettings.mode == BlendShapeOutputMode.ReferenceClipDiff)
            {
                referenceSamples = ExpressionBlendShapeClipSampler.Sample(normalizedSettings.referenceClip, model);
            }

            var result = new List<BlendShapeOutputDecision>();
            foreach (var entry in model.entries)
            {
                if (entry == null)
                {
                    continue;
                }

                result.Add(EvaluateEntry(model, entry, normalizedSettings, referenceSamples));
            }

            return result;
        }

        public static Dictionary<BlendShapeEntry, BlendShapeOutputDecision> ToDecisionMap(
            IReadOnlyList<BlendShapeOutputDecision> decisions)
        {
            var result = new Dictionary<BlendShapeEntry, BlendShapeOutputDecision>();
            if (decisions == null)
            {
                return result;
            }

            foreach (var decision in decisions)
            {
                if (decision?.entry == null || result.ContainsKey(decision.entry))
                {
                    continue;
                }

                result.Add(decision.entry, decision);
            }

            return result;
        }

        public static BlendShapeOutputSummary Summarize(IReadOnlyList<BlendShapeOutputDecision> decisions)
        {
            var summary = new BlendShapeOutputSummary();
            if (decisions == null)
            {
                return summary;
            }

            foreach (var decision in decisions)
            {
                if (decision == null)
                {
                    continue;
                }

                if (decision.entry == null || !decision.entry.ShouldOutput)
                {
                    summary.excludedCount++;
                    continue;
                }

                if (decision.shouldWriteCurve)
                {
                    summary.outputCount++;
                }
                else
                {
                    summary.unchangedCount++;
                }

                if (decision.referenceStatus == ReferenceBlendShapeSampleStatus.Missing)
                {
                    summary.missingReferenceCount++;
                }
                else if (decision.referenceStatus == ReferenceBlendShapeSampleStatus.Ambiguous)
                {
                    summary.ambiguousReferenceCount++;
                }
                else if (decision.referenceStatus == ReferenceBlendShapeSampleStatus.IntermediateKeysIgnored)
                {
                    summary.intermediateKeyCount++;
                }
            }

            return summary;
        }

        private static BlendShapeOutputDecision EvaluateEntry(
            ExpressionEditModel model,
            BlendShapeEntry entry,
            ExpressionOutputSettings settings,
            IReadOnlyDictionary<string, ReferenceBlendShapeSample> referenceSamples)
        {
            if (!entry.ShouldOutput)
            {
                return new BlendShapeOutputDecision
                {
                    entry = entry,
                    shouldWriteCurve = false,
                    isDifferent = false,
                    hasReferenceValue = false,
                    referenceStatus = GetReferenceStatus(entry, referenceSamples),
                    reason = "編集・出力対象ではありません。",
                };
            }

            if (settings.mode == BlendShapeOutputMode.AllTargets)
            {
                return new BlendShapeOutputDecision
                {
                    entry = entry,
                    shouldWriteCurve = true,
                    isDifferent = true,
                    hasReferenceValue = false,
                    referenceStatus = ReferenceBlendShapeSampleStatus.NotUsed,
                    reason = "全ての編集・出力対象を出力します。",
                };
            }

            var referenceSample = GetReferenceSample(entry, referenceSamples);
            var startBaseline = entry.initialValue;
            var endBaseline = entry.initialValue;
            var hasReferenceValue = false;
            var referenceStatus = ReferenceBlendShapeSampleStatus.NotUsed;
            if (settings.mode == BlendShapeOutputMode.ReferenceClipDiff)
            {
                referenceStatus = referenceSample != null
                    ? referenceSample.status
                    : ReferenceBlendShapeSampleStatus.Missing;
                hasReferenceValue = referenceSample != null && referenceSample.hasReferenceValue;
                if (hasReferenceValue)
                {
                    startBaseline = referenceSample.startValue;
                    endBaseline = referenceSample.endValue;
                }
            }

            var isDifferent = IsEntryDifferentFromBaseline(model, entry, startBaseline, endBaseline, settings.tolerance);
            return new BlendShapeOutputDecision
            {
                entry = entry,
                shouldWriteCurve = isDifferent,
                isDifferent = isDifferent,
                hasReferenceValue = hasReferenceValue,
                referenceStatus = referenceStatus,
                reason = GetReason(settings.mode, isDifferent, referenceStatus, hasReferenceValue),
            };
        }

        private static bool IsEntryDifferentFromBaseline(
            ExpressionEditModel model,
            BlendShapeEntry entry,
            float startBaseline,
            float endBaseline,
            float tolerance)
        {
            if (model != null && model.frameMode == ExpressionFrameMode.WeightBlend)
            {
                return !Approximately(entry.value, startBaseline, tolerance)
                    || !Approximately(entry.endValue, endBaseline, tolerance);
            }

            return !Approximately(entry.value, startBaseline, tolerance);
        }

        private static ReferenceBlendShapeSample GetReferenceSample(
            BlendShapeEntry entry,
            IReadOnlyDictionary<string, ReferenceBlendShapeSample> referenceSamples)
        {
            if (entry == null || referenceSamples == null || string.IsNullOrEmpty(entry.name))
            {
                return null;
            }

            referenceSamples.TryGetValue(entry.name, out var sample);
            return sample;
        }

        private static ReferenceBlendShapeSampleStatus GetReferenceStatus(
            BlendShapeEntry entry,
            IReadOnlyDictionary<string, ReferenceBlendShapeSample> referenceSamples)
        {
            var sample = GetReferenceSample(entry, referenceSamples);
            return sample != null ? sample.status : ReferenceBlendShapeSampleStatus.NotUsed;
        }

        private static string GetReason(
            BlendShapeOutputMode mode,
            bool isDifferent,
            ReferenceBlendShapeSampleStatus referenceStatus,
            bool hasReferenceValue)
        {
            if (mode == BlendShapeOutputMode.SessionBaselineDiff)
            {
                return isDifferent
                    ? "セッション開始時の値との差分があります。"
                    : "セッション開始時の値との差分がありません。";
            }

            if (mode == BlendShapeOutputMode.ReferenceClipDiff)
            {
                if (!hasReferenceValue)
                {
                    return referenceStatus == ReferenceBlendShapeSampleStatus.Ambiguous
                        ? "参照クリップ上の同名ブレンドシェイプが複数あるため、セッション開始時の値で判定します。"
                        : "参照クリップ上に同名ブレンドシェイプがないため、セッション開始時の値で判定します。";
                }

                if (referenceStatus == ReferenceBlendShapeSampleStatus.IntermediateKeysIgnored)
                {
                    return "参照クリップの中間キーは無視し、始端と終端のみで判定します。";
                }

                return isDifferent
                    ? "参照クリップとの差分があります。"
                    : "参照クリップとの差分がありません。";
            }

            return "";
        }

        private static ExpressionOutputSettings NormalizeSettings(ExpressionOutputSettings settings)
        {
            if (settings == null)
            {
                return new ExpressionOutputSettings();
            }

            return new ExpressionOutputSettings
            {
                mode = settings.mode,
                referenceClip = settings.referenceClip,
                missingReferencePolicy = settings.missingReferencePolicy,
                tolerance = settings.tolerance > 0f ? settings.tolerance : DefaultTolerance,
            };
        }

        private static bool Approximately(float a, float b, float tolerance)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }
    }
}
