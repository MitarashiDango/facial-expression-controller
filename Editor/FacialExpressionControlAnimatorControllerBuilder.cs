using MitarashiDango.FacialExpressionController.Editor.Builders;
using MitarashiDango.FacialExpressionController.Runtime;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class FacialExpressionControlAnimatorControllerBuilder
    {
        private enum HandType
        {
            Left,
            Right
        }

        /// <summary>
        /// ハンドジェスチャー適用対象判定レイヤーの生成
        /// </summary>
        /// <returns>ハンドジェスチャー適用対象判定レイヤー</returns>
        public AnimatorController CreateMainAnimatorController(FacialExpressionControl fec)
        {
            var blankClip = new AnimationClip
            {
                name = "blank"
            };

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
                ac.AddLayer(new AFKModeControlLayerBuilder(blankClip, fec).Build());
            }
            ac.AddLayer(new DanceModeControlLayerBuilder(blankClip).Build());
            ac.AddLayer(new VehicleModeControlLayerBuilder(blankClip).Build());

            // ロック・ウェイト制御系
            ac.AddLayer(new FacialExpressionGestureLockLayerBuilder(blankClip).Build());
            ac.AddLayer(new CopyGestureWeightLayerBuilder(blankClip).Build());

            // 表情選択・適用系
            ac.AddLayer(new SelectFacialExpressionControlModeLayerBuilder(blankClip).Build());
            ac.AddLayer(new SelectFacialExpressionNumberLayerBuilder(blankClip, fec).Build());
            ac.AddLayer(new FacialExpressionControlLayerBuilder(blankClip, fec).Build());

            return ac;
        }

        public AnimatorController CreateDefaultFacialExpressionAnimatorController(BuildContext ctx, FacialExpressionControl fec)
        {
            var blankClip = new AnimationClip
            {
                name = "blank"
            };

            var parameters = new Parameters();

            var ac = new AnimatorController
            {
                name = "FEC_DEFAULT_FACIAL_EXPRESION",
                parameters = parameters.CreateAnimatorControllerParameters()
            };

            // デフォルト表情レイヤーの構築
            if (fec.defaultFace != null || fec.generateDefaultFacialAnimation)
            {
                ac.AddLayer(new DefaultFacialExpressionLayerBuilder(blankClip, fec, ctx.AvatarRootObject).Build());
            }

            return ac;
        }
    }
}
