using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public static class ExpressionClipIO
    {
        private const float WeightBlendEndTime = 1f;
        private const float KeyTimeTolerance = 0.0001f;

        /// <summary>
        /// 対象 Skinned Mesh Renderer の現在値から編集モデルを作成する。
        /// </summary>
        public static ExpressionEditModel CreateModel(GameObject avatarRootObject, SkinnedMeshRenderer targetRenderer, IEnumerable<string> userExcludedBlendShapes = null)
        {
            var model = ExpressionEditModel.Create();
            model.avatarRootObject = avatarRootObject;
            model.targetRenderer = targetRenderer;
            model.frameMode = ExpressionFrameMode.SingleFrame;
            model.sourceFrameRate = 60f;

            PopulateEntriesFromRenderer(model, userExcludedBlendShapes);
            return model;
        }

        /// <summary>
        /// AnimationClip を読み込み、編集対象外カーブを保全した編集モデルを作成する。
        /// </summary>
        public static ExpressionEditModel Load(AnimationClip clip, GameObject avatarRootObject, SkinnedMeshRenderer targetRenderer, IEnumerable<string> userExcludedBlendShapes = null)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            var model = CreateModel(avatarRootObject, targetRenderer, userExcludedBlendShapes);
            model.hasSourceClip = true;
            model.sourceClipName = clip.name;
            model.sourceFrameRate = clip.frameRate;

            var rendererPath = GetRendererPath(model);
            var entryByName = model.entries
                .GroupBy(entry => entry.name)
                .ToDictionary(group => group.Key, group => group.First());
            var targetBlendShapeKeyTimes = new List<float>();

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (TryGetTargetBlendShapeEntry(binding, rendererPath, entryByName, out var entry))
                {
                    AddKeyTimes(targetBlendShapeKeyTimes, curve);
                    entry.hasSourceCurve = true;
                    entry.sourceCurve = CopyCurve(curve);
                    continue;
                }

                model.preservedCurves.Add(PreservedCurve.FromBinding(binding, curve));
            }

            var frameTimes = NormalizeKeyTimes(targetBlendShapeKeyTimes);
            ApplyFrameMode(model, frameTimes);

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (TryGetTargetBlendShapeEntry(binding, rendererPath, entryByName, out var entry))
                {
                    ApplyCurveToEntry(model, entry, curve, frameTimes);
                }
            }

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                model.preservedObjectReferenceCurves.Add(PreservedObjectReferenceCurve.FromBinding(binding, keyframes));
            }

            return model;
        }

        /// <summary>
        /// 編集モデルから AnimationClip を構築する。
        /// </summary>
        public static AnimationClip ToClip(
            ExpressionEditModel model,
            string animationClipName = "",
            bool includePreservedCurves = true,
            bool useTargetRendererAsRoot = false,
            ExpressionOutputSettings outputSettings = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var animationClip = new AnimationClip
            {
                frameRate = model.sourceFrameRate > 0f ? model.sourceFrameRate : 60f,
                name = !string.IsNullOrEmpty(animationClipName) ? animationClipName : model.sourceClipName,
            };

            var rendererPath = useTargetRendererAsRoot ? "" : GetRendererPath(model);
            var outputDecisions = ExpressionOutputDiffService.ToDecisionMap(
                ExpressionOutputDiffService.Evaluate(model, outputSettings));
            if (includePreservedCurves)
            {
                foreach (var preservedCurve in model.preservedCurves)
                {
                    var binding = preservedCurve.ToBinding();
                    if (binding.type == null)
                    {
                        Debug.LogWarning($"[FacialExpressionController] Cannot restore curve type: {preservedCurve.typeName}");
                        continue;
                    }

                    if (ShouldSkipPreservedTargetBlendShapeCurve(model, binding, rendererPath))
                    {
                        continue;
                    }

                    AnimationUtility.SetEditorCurve(animationClip, binding, CopyCurve(preservedCurve.curve));
                }

                foreach (var preservedCurve in model.preservedObjectReferenceCurves)
                {
                    var binding = preservedCurve.ToBinding();
                    if (binding.type == null)
                    {
                        Debug.LogWarning($"[FacialExpressionController] Cannot restore object reference curve type: {preservedCurve.typeName}");
                        continue;
                    }

                    AnimationUtility.SetObjectReferenceCurve(animationClip, binding, preservedCurve.ToKeyframes());
                }
            }

            foreach (var entry in model.entries)
            {
                if (!entry.ShouldOutput)
                {
                    RestoreLockedSystemSourceCurve(animationClip, rendererPath, entry);
                    continue;
                }

                if (!ShouldWriteManagedBlendShapeCurve(entry, outputDecisions))
                {
                    continue;
                }

                var animationCurve = GetOutputBlendShapeCurve(model, entry);
                if (animationCurve == null)
                {
                    continue;
                }

                animationClip.SetCurve(rendererPath, typeof(SkinnedMeshRenderer), $"blendShape.{entry.name}", animationCurve);
            }

            return animationClip;
        }

        /// <summary>
        /// AnimationClip をプロジェクト内のアセットとして保存する。
        /// </summary>
        public static bool SaveClipToProject(AnimationClip clip, string title, string defaultName, string message, string defaultPath = "Assets")
        {
            var filePath = EditorUtility.SaveFilePanelInProject(title, defaultName, "anim", message, defaultPath);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            SaveClipToAsset(clip, filePath);
            return true;
        }

        /// <summary>
        /// AnimationClip を指定パスのアセットとして保存する。
        /// </summary>
        /// <remarks>
        /// 保存先に AnimationClip アセットが存在しない場合は、渡された <paramref name="clip"/> 自体が
        /// 新規アセットになる。呼び出し側は保存後に <see cref="EditorUtility.IsPersistent"/> を確認してから破棄すること。
        /// 既存アセットへの上書き時は内容だけをコピーするため、<paramref name="clip"/> は一時オブジェクトのまま残る。
        /// </remarks>
        public static void SaveClipToAsset(AnimationClip clip, string filePath)
        {
            var asset = AssetDatabase.LoadAssetAtPath(filePath, typeof(AnimationClip));
            if (asset == null)
            {
                AssetDatabase.CreateAsset(clip, filePath);
            }
            else
            {
                EditorUtility.CopySerialized(clip, asset);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        public static AnimationCurve CopyCurve(AnimationCurve sourceCurve)
        {
            if (sourceCurve == null)
            {
                return null;
            }

            var curve = new AnimationCurve(sourceCurve.keys)
            {
                preWrapMode = sourceCurve.preWrapMode,
                postWrapMode = sourceCurve.postWrapMode,
            };
            return curve;
        }

        private static void PopulateEntriesFromRenderer(ExpressionEditModel model, IEnumerable<string> userExcludedBlendShapes)
        {
            var ad = model.avatarRootObject != null ? model.avatarRootObject.GetComponent<VRCAvatarDescriptor>() : null;
            var catalog = BlendShapeCatalog.Build(model.targetRenderer, ad, userExcludedBlendShapes);

            model.entries.Clear();
            foreach (var item in catalog)
            {
                model.entries.Add(new BlendShapeEntry
                {
                    index = item.index,
                    name = item.name,
                    value = item.value,
                    endValue = item.value,
                    initialValue = item.value,
                    sourceFrameMode = ExpressionFrameMode.SingleFrame,
                    sourceValue = item.value,
                    sourceEndValue = item.value,
                    systemExclusion = item.systemExclusion,
                    userExcluded = item.userExcluded,
                });
            }
        }

        private static string GetRendererPath(ExpressionEditModel model)
        {
            return MiscUtil.GetPathInHierarchy(model.targetRenderer != null ? model.targetRenderer.gameObject : null, model.avatarRootObject);
        }

        private static bool TryGetTargetBlendShapeEntry(
            EditorCurveBinding binding,
            string rendererPath,
            IReadOnlyDictionary<string, BlendShapeEntry> entryByName,
            out BlendShapeEntry entry)
        {
            entry = null;

            if (binding.path != rendererPath
                || binding.type != typeof(SkinnedMeshRenderer)
                || !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
            {
                return false;
            }

            var blendShapeName = binding.propertyName.Substring("blendShape.".Length);
            if (!entryByName.TryGetValue(blendShapeName, out entry))
            {
                return false;
            }

            return true;
        }

        private static bool ShouldSkipPreservedTargetBlendShapeCurve(
            ExpressionEditModel model,
            EditorCurveBinding binding,
            string rendererPath)
        {
            if (model == null
                || binding.path != rendererPath
                || binding.type != typeof(SkinnedMeshRenderer)
                || !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
            {
                return false;
            }

            var blendShapeName = binding.propertyName.Substring("blendShape.".Length);
            return model.entries.Any(entry => entry.name == blendShapeName);
        }

        private static bool ShouldWriteManagedBlendShapeCurve(
            BlendShapeEntry entry,
            IReadOnlyDictionary<BlendShapeEntry, BlendShapeOutputDecision> outputDecisions)
        {
            if (entry == null)
            {
                return false;
            }

            if (outputDecisions == null || !outputDecisions.TryGetValue(entry, out var decision))
            {
                return entry.ShouldOutput;
            }

            return decision.shouldWriteCurve;
        }

        private static void AddKeyTimes(ICollection<float> keyTimes, AnimationCurve curve)
        {
            if (curve == null)
            {
                return;
            }

            foreach (var key in curve.keys)
            {
                keyTimes.Add(key.time);
            }
        }

        private static List<float> NormalizeKeyTimes(IEnumerable<float> keyTimes)
        {
            var result = new List<float>();
            foreach (var keyTime in keyTimes.OrderBy(time => time))
            {
                if (result.Any(time => Mathf.Abs(time - keyTime) <= KeyTimeTolerance))
                {
                    continue;
                }

                result.Add(keyTime);
            }

            return result;
        }

        private static void ApplyFrameMode(ExpressionEditModel model, IReadOnlyList<float> frameTimes)
        {
            model.hasIntermediateKeys = frameTimes.Count > 2;
            model.frameMode = frameTimes.Count == 2 || frameTimes.Count > 2
                ? ExpressionFrameMode.WeightBlend
                : ExpressionFrameMode.SingleFrame;
        }

        private static void ApplyCurveToEntry(ExpressionEditModel model, BlendShapeEntry entry, AnimationCurve curve, IReadOnlyList<float> frameTimes)
        {
            if (curve == null || curve.length == 0)
            {
                return;
            }

            if (model.frameMode == ExpressionFrameMode.WeightBlend && frameTimes.Count > 0)
            {
                var startTime = frameTimes[0];
                var endTime = frameTimes[frameTimes.Count - 1];
                entry.value = curve.Evaluate(startTime);
                entry.endValue = curve.Evaluate(endTime);
                MarkSourceValues(model, entry);
                return;
            }

            entry.value = curve.Evaluate(0f);
            entry.endValue = entry.value;
            MarkSourceValues(model, entry);
        }

        private static void MarkSourceValues(ExpressionEditModel model, BlendShapeEntry entry)
        {
            entry.sourceFrameMode = model.frameMode;
            entry.sourceValue = entry.value;
            entry.sourceEndValue = entry.endValue;
        }

        private static AnimationCurve GetOutputBlendShapeCurve(ExpressionEditModel model, BlendShapeEntry entry)
        {
            if (ShouldRestoreSourceCurve(model, entry))
            {
                return CopyCurve(entry.sourceCurve);
            }

            return CreateBlendShapeCurve(model, entry);
        }

        private static bool ShouldRestoreSourceCurve(ExpressionEditModel model, BlendShapeEntry entry)
        {
            return model.hasSourceClip
                && entry.hasSourceCurve
                && entry.sourceCurve != null
                && model.frameMode == ExpressionFrameMode.SingleFrame
                && model.frameMode == entry.sourceFrameMode
                && Approximately(entry.value, entry.sourceValue)
                && Approximately(entry.endValue, entry.sourceEndValue);
        }

        private static void RestoreLockedSystemSourceCurve(AnimationClip animationClip, string rendererPath, BlendShapeEntry entry)
        {
            if (!entry.IsSystemLocked || !entry.hasSourceCurve || entry.sourceCurve == null)
            {
                return;
            }

            animationClip.SetCurve(rendererPath, typeof(SkinnedMeshRenderer), $"blendShape.{entry.name}", CopyCurve(entry.sourceCurve));
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static AnimationCurve CreateBlendShapeCurve(ExpressionEditModel model, BlendShapeEntry entry)
        {
            if (model.frameMode == ExpressionFrameMode.WeightBlend)
            {
                var curve = AnimationCurve.Linear(0f, entry.value, WeightBlendEndTime, entry.endValue);
                for (var i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }

                return curve;
            }

            var animationCurve = new AnimationCurve();
            animationCurve.AddKey(0f, entry.value);
            return animationCurve;
        }
    }
}
