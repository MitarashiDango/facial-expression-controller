using System.Linq;
using MitarashiDango.FacialExpressionController.Editor;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEngine;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

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
                .Run("Run Facial Expression Controller Generating Process", ctx => GeneratingProcess(ctx));

            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .WithRequiredExtension(typeof(AnimatorServicesContext), seq =>
                {
                    seq.Run("Run Facial Expression Controller Transforming Process", ctx => TransformingProcess(ctx));
                });
        }

        private void GeneratingProcess(BuildContext ctx)
        {
            var fecs = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionController>(true);
            if (fecs.Length == 0)
            {
                DestroyComponents(ctx);
                return;
            }

            if (fecs.Length > 1)
            {
                var paths = string.Join(", ", fecs.Select(f => MiscUtil.GetPathInHierarchy(f.gameObject, ctx.AvatarRootObject)));
                Debug.LogWarning($"[FacialExpressionController] Multiple FacialExpressionController components were detected. Only the first one will be processed. Detected at: {paths}");
            }

            var fec = fecs[0];

            if (!ValidateFacialExpressionCount(fec))
            {
                DestroyComponents(ctx);
                return;
            }

            WarnInvalidDefaultGesturePresetNumbers(fec);

            CreateMAParameters(fec);
            CreateAnimatorControllerProcess(ctx, fec);
            SetupContactReceiver(fec);

            var fecMenus = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionMenuRoot>(true);
            foreach (var fecMenu in fecMenus)
            {
                BuildMenuTree(fec, fecMenu);
            }
        }

        private void TransformingProcess(BuildContext ctx)
        {
            var fecs = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionController>(true);
            if (fecs.Length == 0)
            {
                DestroyComponents(ctx);
                return;
            }

            if (fecs.Length > 1)
            {
                var paths = string.Join(", ", fecs.Select(f => MiscUtil.GetPathInHierarchy(f.gameObject, ctx.AvatarRootObject)));
                Debug.LogWarning($"[FacialExpressionController] Multiple FacialExpressionController components were detected. Only the first one will be processed. Detected at: {paths}");
            }

            RemoveLayers(ctx, fecs[0]);

            DestroyComponents(ctx);
        }

        private void RemoveLayers(BuildContext ctx, FacialExpressionController fec)
        {
            if (fec == null || !fec.removeExistingFacialExpressionLayers || fec.layerRemovalTargets == null || fec?.layerRemovalTargets.Count == 0)
            {
                return;
            }

            var asc = ctx.Extension<AnimatorServicesContext>();
            var avatarDescriptor = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();

            foreach (var removalTarget in fec.layerRemovalTargets)
            {
                if (string.IsNullOrEmpty(removalTarget.layerName))
                {
                    Debug.LogWarning($"[FacialExpressionController] Layer removal target name is empty. ({removalTarget.layerType})");
                    continue;
                }

                if (!IsCustomAvatarLayer(avatarDescriptor, removalTarget.layerType))
                {
                    Debug.LogWarning($"[FacialExpressionController] Layer removal skipped because the avatar layer is not a custom layer: {removalTarget.layerType}");
                    continue;
                }

                if (!asc.ControllerContext.Controllers.TryGetValue(removalTarget.layerType, out var controller))
                {
                    Debug.LogWarning($"[FacialExpressionController] Animator controller for layer type was not found: {removalTarget.layerType}");
                    continue;
                }

                var removalTargetLayers = controller.Layers.Where(layer => layer.Name == removalTarget.layerName).ToList();
                if (removalTargetLayers.Count == 0)
                {
                    Debug.LogWarning($"[FacialExpressionController] Layer removal target was not found: {removalTarget.layerName} ({removalTarget.layerType})");
                    continue;
                }

                foreach (var removalTargetLayer in removalTargetLayers)
                {
                    Debug.Log($"[FacialExpressionController] Remove layer: {removalTargetLayer.Name} ({removalTarget.layerType})");
                    controller.RemoveLayer(removalTargetLayer);
                }
            }
        }

        private bool IsCustomAvatarLayer(VRCAvatarDescriptor avatarDescriptor, VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var customLayer = GetCustomAnimLayer(avatarDescriptor?.baseAnimationLayers, layerType)
                ?? GetCustomAnimLayer(avatarDescriptor?.specialAnimationLayers, layerType);

            return customLayer.HasValue
                && !customLayer.Value.isDefault
                && customLayer.Value.animatorController != null;
        }

        private VRCAvatarDescriptor.CustomAnimLayer? GetCustomAnimLayer(
            VRCAvatarDescriptor.CustomAnimLayer[] customAnimLayers,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            return customAnimLayers?
                .Where(layer => layer.type == layerType)
                .Cast<VRCAvatarDescriptor.CustomAnimLayer?>()
                .FirstOrDefault();
        }

        private bool ValidateFacialExpressionCount(FacialExpressionController fec)
        {
            var gestureMaxNumber = fec.facialExpressionGesturePresets.Count * FacialExpressionNumbering.GestureCountPerPreset;
            var menuMaxNumber = FacialExpressionBuildUtil.GetValidGroups(fec)
                .Sum(g => g.facialExpressions.Count);

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
            var facialExpressionControllers = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionController>(true);
            foreach (var facialExpressionController in facialExpressionControllers)
            {
                Object.DestroyImmediate(facialExpressionController);
            }

            var facialExpressionMenuRoots = ctx.AvatarRootObject.GetComponentsInChildren<FacialExpressionMenuRoot>(true);
            foreach (var facialExpressionMenuRoot in facialExpressionMenuRoots)
            {
                Object.DestroyImmediate(facialExpressionMenuRoot);
            }
        }

        private void CreateMAParameters(FacialExpressionController fec)
        {
            var parameters = new Parameters(fec);
            var modularAvatarParameters = fec.gameObject.AddComponent<ModularAvatarParameters>();
            modularAvatarParameters.parameters = parameters.CreateNDMFParameterConfigs();
        }

        private void WarnInvalidDefaultGesturePresetNumbers(FacialExpressionController fec)
        {
            if (!GesturePresetDefaultValueResolver.HasValidPreset(fec))
            {
                Debug.LogWarning("[FacialExpressionController] No valid facial expression gesture presets were found. The default left and right gesture preset numbers will use internal value 0.");
                return;
            }

            WarnInvalidDefaultGesturePresetNumber(fec, fec.defaultLeftGesturePresetNumber, "left hand");
            WarnInvalidDefaultGesturePresetNumber(fec, fec.defaultRightGesturePresetNumber, "right hand");
        }

        private void WarnInvalidDefaultGesturePresetNumber(FacialExpressionController fec, int presetNumber, string handLabel)
        {
            if (GesturePresetDefaultValueResolver.IsValidPresetNumber(fec, presetNumber))
            {
                return;
            }

            var resolvedPresetNumber = GesturePresetDefaultValueResolver.ResolveIndex(fec, presetNumber)
                + GesturePresetDefaultValueResolver.FirstPresetNumber;
            Debug.LogWarning($"[FacialExpressionController] The default {handLabel} gesture preset number is invalid. Value: {presetNumber}, Resolved: {resolvedPresetNumber}.");
        }

        private void CreateAnimatorControllerProcess(BuildContext ctx, FacialExpressionController fec)
        {
            CreateMainAnimatorController(ctx, fec);

            if (fec.defaultFacialExpressionMotion != null || fec.generateDefaultFacialExpressionAnimation)
            {
                CreateDefaultFacialExpressionAnimatorController(ctx, fec);
            }
        }

        private void SetupContactReceiver(FacialExpressionController fec)
        {
            var contactReceiver = fec.facialExpressionLockContactReceiver;
            if (contactReceiver != null)
            {
                contactReceiver.parameter = InternalParameters.FacialExpressionLockReceiverInContact.name;
                contactReceiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Constant;
            }
        }

        private void CreateMainAnimatorController(BuildContext ctx, FacialExpressionController fec)
        {
            var builder = new FacialExpressionAnimatorControllerBuilder();
            var ac = builder.CreateMainAnimatorController(ctx.AvatarRootObject, fec);

            var mergeAnimator = fec.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = ac;
            mergeAnimator.layerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
        }

        private void CreateDefaultFacialExpressionAnimatorController(BuildContext ctx, FacialExpressionController fec)
        {
            var builder = new FacialExpressionAnimatorControllerBuilder();
            var ac = builder.CreateDefaultFacialExpressionAnimatorController(ctx, fec);

            var mergeAnimator = fec.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = ac;
            mergeAnimator.layerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            mergeAnimator.layerPriority = defaultFacialExpressionLayerPriority;    // デフォルトレイヤーは優先度を下げる
        }

        private void BuildMenuTree(FacialExpressionController fec, FacialExpressionMenuRoot fecMenu)
        {
            var menuBuilder = new FacialExpressionMenuBuilder();
            menuBuilder.BuildMenuTree(fec, fecMenu);
        }
    }
}
