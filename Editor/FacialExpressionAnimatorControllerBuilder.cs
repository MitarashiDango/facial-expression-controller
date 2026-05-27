using MitarashiDango.FacialExpressionController.Editor.Builders;
using MitarashiDango.FacialExpressionController;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class FacialExpressionAnimatorControllerBuilder
    {
        private const string InternalRootName = "__FEC_Internal";
        private const string WaitClipTargetName = "__FEC_WaitClipTarget";

        private enum HandType
        {
            Left,
            Right
        }

        /// <summary>
        /// ハンドジェスチャー適用対象判定レイヤーの生成
        /// </summary>
        /// <returns>ハンドジェスチャー適用対象判定レイヤー</returns>
        public AnimatorController CreateMainAnimatorController(GameObject avatarRootObject, FacialExpressionController fec)
        {
            avatarRootObject = avatarRootObject != null ? avatarRootObject : MiscUtil.GetAvatarRoot(fec.transform);
            if (avatarRootObject == null)
            {
                throw new System.InvalidOperationException("Avatar root object is required to create the main animator controller.");
            }

            var blankClip = new AnimationClip
            {
                name = "blank"
            };
            var waitClipTargetPath = EnsureWaitClipTargetPath(avatarRootObject);

            var parameters = new Parameters();

            var ac = new AnimatorController
            {
                name = "FEC_MAIN",
                parameters = parameters.CreateAnimatorControllerParameters()
            };

            if (ac.layers.Length == 0)
            {
                ac.AddLayer("DUMMY_LAYER");
            }

            // ハンドジェスチャー判定
            ac.AddLayer(new HandGestureLayerBuilder(blankClip, HandGestureLayerBuilder.HandType.Left).Build());
            ac.AddLayer(new HandGestureLayerBuilder(blankClip, HandGestureLayerBuilder.HandType.Right).Build());
            ac.AddLayer(new SelectGestureHandLayerBuilder(blankClip).Build());

            // モード制御系
            if (fec.useAFKMode)
            {
                ac.AddLayer(new AFKModeControlLayerBuilder(blankClip, fec, waitClipTargetPath).Build());
            }
            ac.AddLayer(new DanceModeControlLayerBuilder(blankClip).Build());
            ac.AddLayer(new VehicleModeControlLayerBuilder(blankClip).Build());

            // ロック・ウェイト制御系
            ac.AddLayer(new FacialExpressionGestureLockLayerBuilder(blankClip, waitClipTargetPath).Build());
            ac.AddLayer(new CopyGestureWeightLayerBuilder(blankClip).Build());

            // 表情選択・適用系
            ac.AddLayer(new SelectFacialExpressionModeLayerBuilder(blankClip).Build());
            ac.AddLayer(new SelectFacialExpressionNumberLayerBuilder(blankClip, fec).Build());
            ac.AddLayer(new ApplyFacialExpressionLayerBuilder(blankClip, fec).Build());

            return ac;
        }

        private string EnsureWaitClipTargetPath(GameObject avatarRootObject)
        {
            var internalRoot = avatarRootObject.transform.Find(InternalRootName)?.gameObject;
            if (internalRoot == null)
            {
                internalRoot = new GameObject(InternalRootName);
                internalRoot.transform.SetParent(avatarRootObject.transform, false);
            }

            var waitClipTarget = internalRoot.transform.Find(WaitClipTargetName)?.gameObject;
            if (waitClipTarget == null)
            {
                waitClipTarget = new GameObject(WaitClipTargetName);
                waitClipTarget.transform.SetParent(internalRoot.transform, false);
            }

            return MiscUtil.GetPathInHierarchy(waitClipTarget, avatarRootObject);
        }

        public AnimatorController CreateDefaultFacialExpressionAnimatorController(BuildContext ctx, FacialExpressionController fec)
        {
            var blankClip = new AnimationClip
            {
                name = "blank"
            };

            var parameters = new Parameters();

            var ac = new AnimatorController
            {
                name = "FEC_DEFAULT_FACIAL_EXPRESSION",
                parameters = parameters.CreateAnimatorControllerParameters()
            };

            // デフォルト表情レイヤーの構築
            if (fec.defaultFacialExpressionMotion != null || fec.generateDefaultFacialExpressionAnimation)
            {
                ac.AddLayer(new DefaultFacialExpressionLayerBuilder(blankClip, fec, ctx.AvatarRootObject).Build());
            }

            return ac;
        }
    }
}
