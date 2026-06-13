using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public enum ExpressionFrameMode
    {
        SingleFrame,
        WeightBlend,
    }

    [Flags]
    public enum BlendShapeSystemExclusionReason
    {
        None = 0,
        Mmd = 1 << 0,
        LipSync = 1 << 1,
        EyeControl = 1 << 2,
        Empty = 1 << 3,
    }

    public class ExpressionEditModel : ScriptableObject
    {
        public GameObject avatarRootObject;
        public SkinnedMeshRenderer targetRenderer;
        public ExpressionFrameMode frameMode = ExpressionFrameMode.SingleFrame;
        public bool hasSourceClip;
        public string sourceClipName = "";
        public float sourceFrameRate = 60f;
        public bool hasIntermediateKeys;
        public List<BlendShapeEntry> entries = new List<BlendShapeEntry>();
        public List<PreservedCurve> preservedCurves = new List<PreservedCurve>();
        public List<PreservedObjectReferenceCurve> preservedObjectReferenceCurves = new List<PreservedObjectReferenceCurve>();

        /// <summary>
        /// 非アセットの編集モデルを作成する。
        /// </summary>
        public static ExpressionEditModel Create()
        {
            var model = CreateInstance<ExpressionEditModel>();
            model.hideFlags = HideFlags.HideAndDontSave;
            return model;
        }
    }

    [Serializable]
    public class BlendShapeEntry
    {
        public int index;
        public string name;
        public float value;
        public float endValue;
        public float initialValue;
        public bool hasSourceCurve;
        public ExpressionFrameMode sourceFrameMode;
        public float sourceValue;
        public float sourceEndValue;
        public AnimationCurve sourceCurve;
        public BlendShapeSystemExclusionReason systemExclusion;
        public bool userExcluded;

        public bool IsSystemExcluded => systemExclusion != BlendShapeSystemExclusionReason.None;
        public bool ShouldOutput => !IsSystemExcluded && !userExcluded;
    }

    [Serializable]
    public class PreservedCurve
    {
        public string path;
        public string propertyName;
        public string typeName;
        public AnimationCurve curve;

        public static PreservedCurve FromBinding(EditorCurveBinding binding, AnimationCurve sourceCurve)
        {
            return new PreservedCurve
            {
                path = binding.path,
                propertyName = binding.propertyName,
                typeName = binding.type.AssemblyQualifiedName,
                curve = ExpressionClipIO.CopyCurve(sourceCurve),
            };
        }

        public EditorCurveBinding ToBinding()
        {
            return new EditorCurveBinding
            {
                path = path,
                propertyName = propertyName,
                type = Type.GetType(typeName),
            };
        }
    }

    [Serializable]
    public class PreservedObjectReferenceCurve
    {
        public string path;
        public string propertyName;
        public string typeName;
        public List<PreservedObjectReferenceKeyframe> keyframes = new List<PreservedObjectReferenceKeyframe>();

        public static PreservedObjectReferenceCurve FromBinding(EditorCurveBinding binding, ObjectReferenceKeyframe[] sourceKeyframes)
        {
            var preserved = new PreservedObjectReferenceCurve
            {
                path = binding.path,
                propertyName = binding.propertyName,
                typeName = binding.type.AssemblyQualifiedName,
            };

            foreach (var keyframe in sourceKeyframes)
            {
                preserved.keyframes.Add(new PreservedObjectReferenceKeyframe
                {
                    time = keyframe.time,
                    value = keyframe.value,
                });
            }

            return preserved;
        }

        public EditorCurveBinding ToBinding()
        {
            return new EditorCurveBinding
            {
                path = path,
                propertyName = propertyName,
                type = Type.GetType(typeName),
            };
        }

        public ObjectReferenceKeyframe[] ToKeyframes()
        {
            var result = new ObjectReferenceKeyframe[keyframes.Count];
            for (var i = 0; i < keyframes.Count; i++)
            {
                result[i] = new ObjectReferenceKeyframe
                {
                    time = keyframes[i].time,
                    value = keyframes[i].value,
                };
            }

            return result;
        }
    }

    [Serializable]
    public class PreservedObjectReferenceKeyframe
    {
        public float time;
        public UnityEngine.Object value;
    }
}
