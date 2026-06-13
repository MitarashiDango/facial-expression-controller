using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public static class FaceRendererResolver
    {
        /// <summary>
        /// アバタールートから表情編集に用いる SkinnedMeshRenderer を解決する。
        /// </summary>
        /// <param name="avatarRootObject">アバターのルートオブジェクト</param>
        /// <returns>表情用 Skinned Mesh Renderer。解決できなければ null。</returns>
        public static SkinnedMeshRenderer Resolve(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return null;
            }

            return Resolve(avatarRootObject.GetComponent<VRCAvatarDescriptor>(), avatarRootObject);
        }

        /// <summary>
        /// 表情アニメーション生成に用いる SkinnedMeshRenderer を解決する。<br />
        /// 優先度: (1) Viseme 用 Skinned Mesh Renderer → (2) まばたき用 Skinned Mesh Renderer → (3) 旧仕様互換の "Body" 直下 Skinned Mesh Renderer
        /// </summary>
        /// <param name="ad">アバターの VRCAvatarDescriptor</param>
        /// <param name="avatarRootObject">アバターのルートオブジェクト</param>
        /// <returns>表情用 Skinned Mesh Renderer。解決できなければ null。</returns>
        public static SkinnedMeshRenderer Resolve(VRCAvatarDescriptor ad, GameObject avatarRootObject)
        {
            if (ad != null
                && ad.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                && ad.VisemeSkinnedMesh != null)
            {
                return ad.VisemeSkinnedMesh;
            }

            if (ad != null
                && ad.enableEyeLook
                && ad.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && ad.customEyeLookSettings.eyelidsSkinnedMesh != null)
            {
                return ad.customEyeLookSettings.eyelidsSkinnedMesh;
            }

            var body = avatarRootObject != null ? avatarRootObject.transform.Find("Body") : null;
            return body != null ? body.GetComponent<SkinnedMeshRenderer>() : null;
        }
    }
}
