using MitarashiDango.FacialExpressionController.Editor;
using MitarashiDango.FacialExpressionController.Runtime;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEngine;

[assembly: ExportsPlugin(typeof(FacialExpressionControllerNDMFPlugin))]

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class FacialExpressionControllerNDMFPlugin : Plugin<FacialExpressionControllerNDMFPlugin>
    {
        private readonly bool matchAvatarWriteDefaults = true;
        private readonly int defaultFacialExpressionLayerPriority = -99999;

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Run Facial Expression Controller Processes", ctx => Processing(ctx));
        }

        private void Processing(BuildContext ctx)
        {
            var fec = ctx.AvatarRootObject.GetComponentInChildren<FacialExpressionControl>();
            if (fec == null)
            {
                DestroyComponents(ctx);
                return;
            }

            CreateMAParameters(fec);
            CreateAnimatorControllerProcess(ctx, fec);

            var fecMenus = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionControlMenu>();
            foreach (var fecMenu in fecMenus)
            {
                BuildMenuTree(fec, fecMenu);
            }

            DestroyComponents(ctx);
        }

        private void DestroyComponents(BuildContext ctx)
        {
            var facialExpressionControls = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionControl>();
            foreach (var facialExpressionControl in facialExpressionControls)
            {
                Object.DestroyImmediate(facialExpressionControl);
            }

            var facialExpressionControlMenus = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionControlMenu>();
            foreach (var facialExpressionControlMenu in facialExpressionControlMenus)
            {
                Object.DestroyImmediate(facialExpressionControlMenu);
            }
        }

        private void CreateMAParameters(FacialExpressionControl fec)
        {
            var parameters = new Parameters();
            var modularAvatarParameters = fec.gameObject.AddComponent<ModularAvatarParameters>();
            modularAvatarParameters.parameters = parameters.CreateNDMFParameterConfigs();
        }

        private void CreateAnimatorControllerProcess(BuildContext ctx, FacialExpressionControl fec)
        {
            CreateMainAnimatorController(ctx, fec);

            if (fec.defaultFace != null || fec.generateDefaultFacialAnimation)
            {
                CreateDefaultFacialExpressionAnimatorController(ctx, fec);
            }
        }

        private void CreateMainAnimatorController(BuildContext ctx, FacialExpressionControl fec)
        {
            var builder = new FacialExpressionControlAnimatorControllerBuilder();
            var ac = builder.CreateMainAnimatorController(ctx, fec);

            var mergeAnimator = fec.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = ac;
            mergeAnimator.layerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
        }

        private void CreateDefaultFacialExpressionAnimatorController(BuildContext ctx, FacialExpressionControl fec)
        {
            var builder = new FacialExpressionControlAnimatorControllerBuilder();
            var ac = builder.CreateDefaultFacialExpressionAnimatorController(ctx, fec);

            var mergeAnimator = fec.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = ac;
            mergeAnimator.layerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            mergeAnimator.layerPriority = defaultFacialExpressionLayerPriority;    // デフォルトレイヤーは優先度を下げる
        }

        private void BuildMenuTree(FacialExpressionControl fec, FacialExpressionControlMenu fecMenu)
        {
            var menuBuilder = new FacialExpressionControlMenuBuilder();
            menuBuilder.BuildMenuTree(fec, fecMenu);
        }
    }
}