using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class FacialExpressionAnimationGenerator
    {
        [MenuItem("GameObject/Create Facial Expression Animation Clip", validate = true)]
        private static bool CreateFacialExpressionAnimationClip_Validate()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                return false;
            }

            var avatarDescriptor = go.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor == null)
            {
                return false;
            }

            return true;
        }

        [MenuItem("GameObject/Create Facial Expression Animation Clip")]
        private static void CreateFacialExpressionAnimationClip()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                return;
            }

            var feag = new FacialExpressionAnimationGenerator();
            var ac = feag.FromAvatar(go, null);

            var filePath = EditorUtility.SaveFilePanelInProject("名前を付けて保存", $"AnimationClip_{go.name}", "anim", "アニメーションクリップの保存先を選択してください", "Assets");
            if (filePath == "")
            {
                EditorUtility.DisplayDialog("情報", "キャンセルされました", "OK");
                return;
            }

            // ファイル保存
            var asset = AssetDatabase.LoadAssetAtPath(filePath, typeof(AnimationClip));
            if (asset == null)
            {
                AssetDatabase.CreateAsset(ac, filePath);
            }
            else
            {
                EditorUtility.CopySerialized(ac, asset);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();
        }

        // MMD ワールド用ブレンドシェイプ名の一覧。行区切りのテキストアセットから読み込む。
        private const string MmdBlendShapesAssetPath = "Packages/com.matcha-soft.facial-expression-controller/Editor/MMD_BlendShapes.txt";
        private static string[] _cachedMmdBlendShapes;

        public static IReadOnlyList<string> MmdBlendShapes => GetMmdBlendShapes();

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

        /// <summary>
        /// アバターに設定されている表情シェイプキーの値をもとに表情アニメーションクリップを生成する
        /// </summary>
        /// <param name="avatarRootObject"></param>
        /// <param name="excludeBlendShapes"></param>
        /// <returns></returns>
        public AnimationClip FromAvatar(GameObject avatarRootObject, List<string> excludeBlendShapes)
        {
            return FromAvatar("", avatarRootObject, excludeBlendShapes);
        }

        /// <summary>
        /// アバターに設定されている表情シェイプキーの値をもとに表情アニメーションクリップを生成する
        /// </summary>
        /// <param name="animationClipName"></param>
        /// <param name="avatarRootObject"></param>
        /// <param name="excludeBlendShapes"></param>
        /// <returns></returns>
        public AnimationClip FromAvatar(string animationClipName, GameObject avatarRootObject, List<string> excludeBlendShapes)
        {
            var ad = avatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (ad == null)
            {
                return null;
            }

            var smr = ResolveFaceSkinnedMeshRenderer(ad, avatarRootObject);
            if (smr == null)
            {
                Debug.LogWarning($"[FacialExpressionController] Could not find a SkinnedMeshRenderer for facial expressions on avatar: {avatarRootObject.name}");
                return null;
            }

            excludeBlendShapes = excludeBlendShapes != null
                ? new List<string>(excludeBlendShapes)
                : new List<string>();

            // メッシュに影響を及ぼしていないブレンドシェイプ(Splitterなどの用途で定義されているブレンドシェイプなど)は除外対象とする
            excludeBlendShapes.AddRange(BlendShapeUtil.FindEmptyBlendShapes(smr));

            // MMDワールド用ブレンドシェイプは除外対象とする
            excludeBlendShapes.AddRange(GetMmdBlendShapes());

            // リップシンク制御用のブレンドシェイプは除外対象とする
            excludeBlendShapes.AddRange(GetLipSyncBlendShapes(ad));

            // 目制御用のブレンドシェイプは除外対象とする
            excludeBlendShapes.AddRange(GetEyeControlBlendShapes(ad));

            var blendShapes = BlendShapeUtil.GetBlendShapes(smr, excludeBlendShapes);

            var animationClip = new AnimationClip()
            {
                frameRate = 60,
                name = animationClipName,
            };

            var objectPath = MiscUtil.GetPathInHierarchy(smr.gameObject, avatarRootObject);

            foreach (var (blendShapeName, blendShapeWeight) in blendShapes)
            {
                var animationCurve = new AnimationCurve();
                animationCurve.AddKey(0, blendShapeWeight);
                animationClip.SetCurve(objectPath, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeName}", animationCurve);
            }

            return animationClip;
        }

        /// <summary>
        /// 表情アニメーション生成に用いる SkinnedMeshRenderer を解決する。<br />
        /// 優先度: (1) Viseme 用 SMR → (2) まばたき用 SMR → (3) 旧仕様互換の "Body" 直下 SMR
        /// </summary>
        /// <param name="ad">アバターの VRCAvatarDescriptor</param>
        /// <param name="avatarRootObject">アバターのルートオブジェクト</param>
        /// <returns>表情用 SMR (解決できなければ null)</returns>
        private SkinnedMeshRenderer ResolveFaceSkinnedMeshRenderer(VRCAvatarDescriptor ad, GameObject avatarRootObject)
        {
            if (ad.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                && ad.VisemeSkinnedMesh != null)
            {
                return ad.VisemeSkinnedMesh;
            }

            if (ad.enableEyeLook
                && ad.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && ad.customEyeLookSettings.eyelidsSkinnedMesh != null)
            {
                return ad.customEyeLookSettings.eyelidsSkinnedMesh;
            }

            var body = avatarRootObject.transform.Find("Body");
            return body != null ? body.GetComponent<SkinnedMeshRenderer>() : null;
        }

        /// <summary>
        /// リップシンク制御用のブレンドシェイプ名のリストを取得する
        /// </summary>
        /// <param name="ad">ブレンドシェイプ名の取得対象となるVRCAvatarDescriptor</param>
        /// <returns>リップシンク制御用のブレンドシェイプ名のリスト</returns>
        private List<string> GetLipSyncBlendShapes(VRCAvatarDescriptor ad)
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
        /// 目制御用のブレンドシェイプ名のリストを取得する
        /// </summary>
        /// <param name="ad">ブレンドシェイプ名の取得対象となるVRCAvatarDescriptor</param>
        /// <returns>目制御用のブレンドシェイプ名のリスト</returns>
        private List<string> GetEyeControlBlendShapes(VRCAvatarDescriptor ad)
        {
            var blendShapes = new List<string>();

            if (ad != null
                && ad.enableEyeLook
                && ad.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && ad.customEyeLookSettings.eyelidsSkinnedMesh != null)
            {
                var smr = ad.customEyeLookSettings.eyelidsSkinnedMesh;
                if (smr == null)
                {
                    return blendShapes;
                }

                var mesh = smr.sharedMesh;
                if (mesh == null)
                {
                    return blendShapes;
                }

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
    }
}
