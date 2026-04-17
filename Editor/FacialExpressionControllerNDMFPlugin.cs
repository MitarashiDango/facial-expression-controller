using System.Linq;
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
            var fecs = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionControl>(true);
            if (fecs.Length == 0)
            {
                DestroyComponents(ctx);
                return;
            }

            if (fecs.Length > 1)
            {
                var paths = string.Join(", ", fecs.Select(f => MiscUtil.GetPathInHierarchy(f.gameObject, ctx.AvatarRootObject)));
                Debug.LogWarning($"[FacialExpressionController] Multiple FacialExpressionControl components were detected. Only the first one will be processed. Detected at: {paths}");
            }

            var fec = fecs[0];

            if (!ValidateFacialExpressionCount(fec))
            {
                DestroyComponents(ctx);
                return;
            }

            CreateMAParameters(fec);
            CreateAnimatorControllerProcess(ctx, fec);
            SetupContactReceiver(fec);

            var fecMenus = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionControlMenu>(true);
            foreach (var fecMenu in fecMenus)
            {
                BuildMenuTree(fec, fecMenu);
            }

            DestroyComponents(ctx);
        }

        private bool ValidateFacialExpressionCount(FacialExpressionControl fec)
        {
            var gestureMaxNumber = fec.facialExpressionGesturePresets.Count * FacialExpressionNumbering.GestureCountPerPreset;
            var menuMaxNumber = fec.facialExpressionGroups
                .Where(g => g != null)
                .Sum(g => g.facialExpressions != null ? g.facialExpressions.Count : 0);

            var valid = true;

            if (gestureMaxNumber > FacialExpressionNumbering.MaxNumber)
            {
                Debug.LogError($"[FacialExpressionController] Gesture preset capacity exceeds the limit. Limit: {FacialExpressionNumbering.MaxNumber}, Required: {gestureMaxNumber} ({fec.facialExpressionGesturePresets.Count} presets * {FacialExpressionNumbering.GestureCountPerPreset} gestures).");
                valid = false;
            }

            if (menuMaxNumber > FacialExpressionNumbering.MaxNumber)
            {
                Debug.LogError($"[FacialExpressionController] Menu facial expression count exceeds the limit. Limit: {FacialExpressionNumbering.MaxNumber}, Required: {menuMaxNumber}.");
                valid = false;
            }

            return valid;
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
            CreateMainAnimatorController(fec);

            if (fec.defaultFace != null || fec.generateDefaultFacialAnimation)
            {
                CreateDefaultFacialExpressionAnimatorController(ctx, fec);
            }
        }

        private void SetupContactReceiver(FacialExpressionControl fec)
        {
            var contactReceiver = fec.facialExpressionLockContactReceiver;
            if (contactReceiver != null)
            {
                contactReceiver.parameter = InternalParameters.FacialExpressionLockReceiverInContact.name;
                contactReceiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Constant;
            }
        }

        private void CreateMainAnimatorController(FacialExpressionControl fec)
        {
            var builder = new FacialExpressionControlAnimatorControllerBuilder();
            var ac = builder.CreateMainAnimatorController(fec);

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