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

        public static readonly string[] mmdBlendShapes =
        {
            "通常",
            "まばたき",
            "笑い",
            "ウィンク",
            "ウィンク右",
            "ウィンク２",
            "ウィンク２右",
            "ｳｨﾝｸ２右",
            "なごみ",
            "なごみω",
            "はぅ",
            "びっくり",
            "じと目",
            "ｷﾘｯ",
            "はちゅ目",
            "はちゅ目縦潰れ",
            "はちゅ目横潰れ",
            "星目",
            "はぁと",
            "瞳大",
            "瞳小",
            "瞳縦潰れ",
            "光下",
            "恐ろしい子！",
            "ハイライト消し",
            "ハイライト消",
            "映り込み消し",
            "映り込み消",
            "喜び",
            "わぉ?!",
            "わぉ？！",
            "あ",
            "い",
            "う",
            "え",
            "お",
            "あ２",
            "ん",
            "ワ",
            "□",
            "ω",
            "ω□",
            "∧",
            "▲",
            "にやり",
            "にやり2",
            "にやり２",
            "にっこり",
            "ぺろっ",
            "てへぺろ",
            "てへぺろ2",
            "てへぺろ２",
            "口角上げ",
            "口角下げ",
            "口横広げ",
            "歯無し上",
            "歯無し下",
            "ハンサム",
            "真面目",
            "困る",
            "にこり",
            "怒り",
            "悲しむ",
            "敵意",
            "上",
            "下",
            "前",
            "眉頭左",
            "眉頭右",
            "照れ",
            "涙",
            "がーん",
            "青ざめる",
            "青ざめ",
            "髪影消",
            "輪郭",
            "メガネ",
            "みっぱい",
            "えー",
            "はんっ!",
            "はんっ！",
        };

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

            var body = avatarRootObject.transform.Find("Body");
            if (body == null)
            {
                return null;
            }

            var smr = body.GetComponent<SkinnedMeshRenderer>();
            if (smr == null)
            {
                return null;
            }

            if (excludeBlendShapes == null)
            {
                excludeBlendShapes = new List<string>();
            }

            // メッシュに影響を及ぼしていないブレンドシェイプ(Splitterなどの用途で定義されているブレンドシェイプなど)は除外対象とする
            excludeBlendShapes.AddRange(BlendShapeUtil.FindEmptyBlendShapes(smr));

            // MMDワールド用ブレンドシェイプは除外対象とする
            excludeBlendShapes.AddRange(mmdBlendShapes);

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

            var objectPath = MiscUtil.GetPathInHierarchy(body.gameObject, avatarRootObject);

            foreach (var (blendShapeName, blendShapeWeight) in blendShapes)
            {
                var animationCurve = new AnimationCurve();
                animationCurve.AddKey(0, blendShapeWeight);
                animationClip.SetCurve(objectPath, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeName}", animationCurve);
            }

            return animationClip;
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
