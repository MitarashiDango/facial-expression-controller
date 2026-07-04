using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public static class BlendShapeCatalog
    {
        private const string MmdBlendShapesAssetPath = "Packages/com.matcha-soft.facial-expression-controller/Editor/MMD_BlendShapes.txt";
        private static string[] _cachedMmdBlendShapes;

        public static IReadOnlyList<string> MmdBlendShapes => GetMmdBlendShapes();

        /// <summary>
        /// 対象 Skinned Mesh Renderer の全ブレンドシェイプを除外理由付きで取得する。
        /// </summary>
        public static List<BlendShapeCatalogEntry> Build(SkinnedMeshRenderer smr, VRCAvatarDescriptor ad, IEnumerable<string> userExcludedBlendShapes = null)
        {
            var entries = new List<BlendShapeCatalogEntry>();
            if (smr == null || smr.sharedMesh == null)
            {
                return entries;
            }

            var mesh = smr.sharedMesh;
            var mmdBlendShapeSet = new HashSet<string>(GetMmdBlendShapes(), StringComparer.Ordinal);
            var lipSyncBlendShapeSet = new HashSet<string>(GetLipSyncBlendShapes(ad), StringComparer.Ordinal);
            var eyeControlBlendShapeSet = new HashSet<string>(GetEyeControlBlendShapes(ad), StringComparer.Ordinal);
            var emptyBlendShapeSet = new HashSet<string>(BlendShapeUtil.FindEmptyBlendShapes(smr), StringComparer.Ordinal);
            var userExcludedBlendShapeSet = userExcludedBlendShapes != null
                ? new HashSet<string>(userExcludedBlendShapes, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            var seenBlendShapeNames = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var blendShapeName = mesh.GetBlendShapeName(i);
                var exclusion = BlendShapeSystemExclusionReason.None;

                if (!seenBlendShapeNames.Add(blendShapeName))
                {
                    exclusion |= BlendShapeSystemExclusionReason.Duplicate;
                }

                if (mmdBlendShapeSet.Contains(blendShapeName))
                {
                    exclusion |= BlendShapeSystemExclusionReason.Mmd;
                }

                if (lipSyncBlendShapeSet.Contains(blendShapeName))
                {
                    exclusion |= BlendShapeSystemExclusionReason.LipSync;
                }

                if (eyeControlBlendShapeSet.Contains(blendShapeName))
                {
                    exclusion |= BlendShapeSystemExclusionReason.EyeControl;
                }

                if (emptyBlendShapeSet.Contains(blendShapeName))
                {
                    exclusion |= BlendShapeSystemExclusionReason.Empty;
                }

                entries.Add(new BlendShapeCatalogEntry
                {
                    index = i,
                    name = blendShapeName,
                    value = smr.GetBlendShapeWeight(i),
                    systemExclusion = exclusion,
                    userExcluded = userExcludedBlendShapeSet.Contains(blendShapeName),
                });
            }

            return entries;
        }

        /// <summary>
        /// アバターのリップシンク制御用ブレンドシェイプ名を取得する。
        /// </summary>
        public static List<string> GetLipSyncBlendShapes(VRCAvatarDescriptor ad)
        {
            if (ad != null && ad.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape)
            {
                return ad.VisemeBlendShapes
                    .Where(bs => !string.IsNullOrEmpty(bs) && bs != "-none-")
                    .ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// アバターの目制御用ブレンドシェイプ名を取得する。
        /// </summary>
        public static List<string> GetEyeControlBlendShapes(VRCAvatarDescriptor ad)
        {
            var blendShapes = new List<string>();

            if (ad != null
                && ad.enableEyeLook
                && ad.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && ad.customEyeLookSettings.eyelidsSkinnedMesh != null)
            {
                var smr = ad.customEyeLookSettings.eyelidsSkinnedMesh;
                if (smr == null || smr.sharedMesh == null)
                {
                    return blendShapes;
                }

                var mesh = smr.sharedMesh;
                foreach (var index in ad.customEyeLookSettings.eyelidsBlendshapes)
                {
                    if (index < 0 || index >= mesh.blendShapeCount)
                    {
                        continue;
                    }

                    var blendShapeName = mesh.GetBlendShapeName(index);
                    if (!string.IsNullOrEmpty(blendShapeName))
                    {
                        blendShapes.Add(blendShapeName);
                    }
                }
            }

            return blendShapes;
        }

        private static string[] GetMmdBlendShapes()
        {
            if (_cachedMmdBlendShapes != null)
            {
                return _cachedMmdBlendShapes;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(MmdBlendShapesAssetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[FacialExpressionController] Cannot load MMD blend shape list: {MmdBlendShapesAssetPath}");
                _cachedMmdBlendShapes = Array.Empty<string>();
                return _cachedMmdBlendShapes;
            }

            _cachedMmdBlendShapes = asset.text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToArray();

            return _cachedMmdBlendShapes;
        }
    }

    public class BlendShapeCatalogEntry
    {
        public int index;
        public string name;
        public float value;
        public BlendShapeSystemExclusionReason systemExclusion;
        public bool userExcluded;

        public bool IsSystemExcluded => systemExclusion != BlendShapeSystemExclusionReason.None;
        public bool ShouldOutput => !IsSystemExcluded && !userExcluded;
    }
}
