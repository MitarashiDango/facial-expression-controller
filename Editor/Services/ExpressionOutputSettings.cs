using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public enum BlendShapeOutputMode
    {
        AllTargets,
        SessionBaselineDiff,
        ReferenceClipDiff,
    }

    public enum MissingReferenceBlendShapePolicy
    {
        UseSessionBaseline,
    }

    public enum ReferenceBlendShapeSampleStatus
    {
        NotUsed,
        MatchedRendererPath,
        MatchedUniqueName,
        Missing,
        Ambiguous,
        IntermediateKeysIgnored,
    }

    public sealed class ExpressionOutputSettings
    {
        public BlendShapeOutputMode mode = BlendShapeOutputMode.AllTargets;
        public AnimationClip referenceClip;
        public MissingReferenceBlendShapePolicy missingReferencePolicy = MissingReferenceBlendShapePolicy.UseSessionBaseline;
        public float tolerance = ExpressionOutputDiffService.DefaultTolerance;
    }

    public sealed class BlendShapeOutputDecision
    {
        public BlendShapeEntry entry;
        public bool shouldWriteCurve;
        public bool isDifferent;
        public bool hasReferenceValue;
        public ReferenceBlendShapeSampleStatus referenceStatus;
        public string reason;
    }

    public sealed class BlendShapeOutputSummary
    {
        public int outputCount;
        public int unchangedCount;
        public int excludedCount;
        public int missingReferenceCount;
        public int ambiguousReferenceCount;
        public int intermediateKeyCount;
    }
}
