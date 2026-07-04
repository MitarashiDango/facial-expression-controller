using System.Collections.Generic;
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
            if (ac == null)
            {
                EditorUtility.DisplayDialog("エラー", "表情アニメーションクリップを生成できませんでした。Console の警告を確認してください。", "OK");
                return;
            }

            var filePath = EditorUtility.SaveFilePanelInProject("名前を付けて保存", $"AnimationClip_{go.name}", "anim", "アニメーションクリップの保存先を選択してください", "Assets");
            if (filePath == "")
            {
                EditorUtility.DisplayDialog("情報", "キャンセルされました", "OK");
                return;
            }

            ExpressionClipIO.SaveClipToAsset(ac, filePath);
        }

        public static IReadOnlyList<string> MmdBlendShapes => BlendShapeCatalog.MmdBlendShapes;

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

            var smr = FaceRendererResolver.Resolve(ad, avatarRootObject);
            if (smr == null)
            {
                Debug.LogWarning($"[FacialExpressionController] Could not find a SkinnedMeshRenderer for facial expressions on avatar: {avatarRootObject.name}");
                return null;
            }

            var model = ExpressionClipIO.CreateModel(avatarRootObject, smr, excludeBlendShapes);
            try
            {
                if (!ExpressionClipIO.TryValidateModelReferences(model, out var validationMessage))
                {
                    Debug.LogWarning($"[FacialExpressionController] Failed to auto-generate facial expression animation: {validationMessage}");
                    return null;
                }

                return ExpressionClipIO.ToClip(model, animationClipName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }
    }
}
