using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public enum ThumbnailBackgroundMode
    {
        Transparent,
        SolidColor,
    }

    [Serializable]
    public struct ThumbnailCaptureSettings
    {
        /// <summary>
        /// 撮影設定を作成した時点のワールド距離。
        /// Scale 変更後は distanceReferenceSize との比率を現在の顔サイズへ換算して使用する。
        /// </summary>
        public float distance;

        /// <summary>
        /// distance を設定した時点の顔幅または顔高。Scale 変更時の距離換算に使用する。
        /// </summary>
        [NonSerialized]
        public float distanceReferenceSize;

        public Vector3 eulerAngles;
        public Vector3 offset;
        public ThumbnailBackgroundMode backgroundMode;
        public Color backgroundColor;
    }

    /// <summary>
    /// 表情サムネイルの撮影と PNG 保存を行う。
    /// </summary>
    public static class ThumbnailCaptureService
    {
        public const int ThumbnailSize = 256;

        private const float FieldOfView = 30f;
        private const float AutoFrameMargin = 1.18f;
        private const float HeadBoneWeightThreshold = 0.05f;
        private const float DefaultDistanceRatio = 2.5f;
        private const float FallbackFaceReferenceRatio = 0.26f;
        private const float MinimumDistanceRatio = 0.01f;
        private const float FrontVertexDepthRatio = 0.35f;
        private const float NumericalEpsilon = 0.000001f;
        private const int DistanceRatioSettingsVersion = 2;
        private const int CurrentSettingsVersion = 3;
        private const string EditorPrefsPrefix = "MitarashiDango.FacialExpressionController.ThumbnailCapture.";

        /// <summary>
        /// アバター単位で保存された撮影設定を読み込む。
        /// </summary>
        public static ThumbnailCaptureSettings LoadSettings(GameObject avatarRootObject)
        {
            var defaults = GetDefaultSettings(avatarRootObject);
            var key = GetPrefsKey(avatarRootObject);
            if (string.IsNullOrEmpty(key) || !HasStoredSettings(key))
            {
                return defaults;
            }

            return LoadStoredSettings(key, defaults, defaults.distanceReferenceSize);
        }

        /// <summary>
        /// アバターと対象 Renderer 単位で保存された撮影設定を読み込む。
        /// 保存距離は顔サイズに対する比率から現在のワールド距離へ復元する。
        /// </summary>
        public static ThumbnailCaptureSettings LoadSettings(ExpressionEditModel model)
        {
            var defaults = GetDefaultSettings(model);
            if (model == null || model.avatarRootObject == null || model.targetRenderer == null)
            {
                return defaults;
            }

            var key = GetPrefsKey(model);
            var sourceKey = HasStoredSettings(key)
                ? key
                : GetPrefsKey(model.avatarRootObject);
            if (string.IsNullOrEmpty(sourceKey) || !HasStoredSettings(sourceKey))
            {
                return defaults;
            }

            var settings = LoadStoredSettings(sourceKey, defaults, defaults.distanceReferenceSize);
            var storedVersion = EditorPrefs.GetInt(sourceKey + ".version", 1);
            if (sourceKey != key || storedVersion < CurrentSettingsVersion)
            {
                SaveSettings(model, settings);
            }

            return settings;
        }

        private static ThumbnailCaptureSettings LoadStoredSettings(
            string key,
            ThumbnailCaptureSettings defaults,
            float currentReferenceSize)
        {
            var storedVersion = EditorPrefs.GetInt(key + ".version", 1);
            var distance = EditorPrefs.GetFloat(key + ".distance", defaults.distance);
            if (storedVersion >= DistanceRatioSettingsVersion && EditorPrefs.HasKey(key + ".distanceRatio"))
            {
                var ratio = EditorPrefs.GetFloat(
                    key + ".distanceRatio",
                    defaults.distance / Mathf.Max(defaults.distanceReferenceSize, NumericalEpsilon));
                ratio = IsFinitePositive(ratio) ? ratio : DefaultDistanceRatio;
                distance = Mathf.Max(MinimumDistanceRatio, ratio) * currentReferenceSize;
            }

            distance = IsFinitePositive(distance) ? distance : defaults.distance;

            return new ThumbnailCaptureSettings
            {
                distance = Mathf.Max(GetMinimumWorldDistance(currentReferenceSize), distance),
                distanceReferenceSize = currentReferenceSize,
                eulerAngles = new Vector3(
                    EditorPrefs.GetFloat(key + ".angle.x", defaults.eulerAngles.x),
                    EditorPrefs.GetFloat(key + ".angle.y", defaults.eulerAngles.y),
                    EditorPrefs.GetFloat(key + ".angle.z", defaults.eulerAngles.z)),
                // version 2 以前の自動調整値には、ボーン付きメッシュの Scale を重複適用した
                // 注視点から算出されたオフセットが保存されている可能性があるため引き継がない。
                offset = storedVersion >= CurrentSettingsVersion
                    ? new Vector3(
                        EditorPrefs.GetFloat(key + ".offset.x", defaults.offset.x),
                        EditorPrefs.GetFloat(key + ".offset.y", defaults.offset.y),
                        EditorPrefs.GetFloat(key + ".offset.z", defaults.offset.z))
                    : defaults.offset,
                backgroundMode = (ThumbnailBackgroundMode)EditorPrefs.GetInt(key + ".backgroundMode", (int)defaults.backgroundMode),
                backgroundColor = new Color(
                    EditorPrefs.GetFloat(key + ".background.r", defaults.backgroundColor.r),
                    EditorPrefs.GetFloat(key + ".background.g", defaults.backgroundColor.g),
                    EditorPrefs.GetFloat(key + ".background.b", defaults.backgroundColor.b),
                    EditorPrefs.GetFloat(key + ".background.a", defaults.backgroundColor.a)),
            };
        }

        /// <summary>
        /// アバター単位で撮影設定を保存する。
        /// </summary>
        public static void SaveSettings(GameObject avatarRootObject, ThumbnailCaptureSettings settings)
        {
            var key = GetPrefsKey(avatarRootObject);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            EditorPrefs.SetInt(key + ".version", CurrentSettingsVersion);
            EditorPrefs.DeleteKey(key + ".distanceRatio");
            EditorPrefs.SetFloat(key + ".distance", Mathf.Max(NumericalEpsilon, settings.distance));
            SaveCommonSettings(key, settings);
        }

        /// <summary>
        /// アバターと対象 Renderer 単位で撮影設定を保存する。
        /// 距離は設定作成時の顔サイズに対する比率として永続化する。
        /// </summary>
        public static void SaveSettings(ExpressionEditModel model, ThumbnailCaptureSettings settings)
        {
            var key = GetPrefsKey(model);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var currentReferenceSize = GetFramingReferenceSize(model);
            var settingsReferenceSize = IsFinitePositive(settings.distanceReferenceSize)
                ? settings.distanceReferenceSize
                : currentReferenceSize;
            var settingsDistance = IsFinitePositive(settings.distance)
                ? settings.distance
                : currentReferenceSize * DefaultDistanceRatio;
            var distanceRatio = Mathf.Max(
                MinimumDistanceRatio,
                settingsDistance / Mathf.Max(settingsReferenceSize, NumericalEpsilon));

            EditorPrefs.SetInt(key + ".version", CurrentSettingsVersion);
            EditorPrefs.SetFloat(key + ".distanceRatio", distanceRatio);
            EditorPrefs.SetFloat(key + ".distance", distanceRatio * currentReferenceSize);
            SaveCommonSettings(key, settings);
        }

        private static void SaveCommonSettings(string key, ThumbnailCaptureSettings settings)
        {
            EditorPrefs.SetFloat(key + ".angle.x", settings.eulerAngles.x);
            EditorPrefs.SetFloat(key + ".angle.y", settings.eulerAngles.y);
            EditorPrefs.SetFloat(key + ".angle.z", settings.eulerAngles.z);
            EditorPrefs.SetFloat(key + ".offset.x", settings.offset.x);
            EditorPrefs.SetFloat(key + ".offset.y", settings.offset.y);
            EditorPrefs.SetFloat(key + ".offset.z", settings.offset.z);
            EditorPrefs.SetInt(key + ".backgroundMode", (int)settings.backgroundMode);
            EditorPrefs.SetFloat(key + ".background.r", settings.backgroundColor.r);
            EditorPrefs.SetFloat(key + ".background.g", settings.backgroundColor.g);
            EditorPrefs.SetFloat(key + ".background.b", settings.backgroundColor.b);
            EditorPrefs.SetFloat(key + ".background.a", settings.backgroundColor.a);
        }

        /// <summary>
        /// 現在のシーン状態を 256x256 のサムネイルとして撮影する。
        /// </summary>
        public static Texture2D Capture(ExpressionEditModel model, ThumbnailCaptureSettings settings)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.avatarRootObject == null || model.targetRenderer == null)
            {
                throw new InvalidOperationException("Avatar root and target renderer are required to capture a thumbnail.");
            }

            var cameraObject = new GameObject("Facial Expression Thumbnail Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                ConfigureCamera(camera, model, settings);
                return Render(camera);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        /// <summary>
        /// 対象 Renderer の顔領域と正面側頂点から撮影設定を自動計算する。
        /// </summary>
        public static bool TryCalculateAutoSettings(
            ExpressionEditModel model,
            ThumbnailCaptureSettings currentSettings,
            out ThumbnailCaptureSettings calculatedSettings,
            out string message)
        {
            calculatedSettings = currentSettings;
            message = "";

            if (model == null || model.avatarRootObject == null || model.targetRenderer == null)
            {
                message = "アバターと対象 Skinned Mesh Renderer を指定してください。";
                return false;
            }

            if (!TryCalculateFaceFraming(model, out var faceBounds, out var focus, out var referenceSize))
            {
                message = "対象 Renderer から顔の撮影範囲を計算できませんでした。";
                return false;
            }

            var avatarTransform = model.avatarRootObject.transform;
            var baseFocus = GetBaseFocusPoint(model);
            var eulerAngles = Vector3.zero;
            calculatedSettings = new ThumbnailCaptureSettings
            {
                distance = CalculateDistanceToFitBounds(model, focus, faceBounds, eulerAngles, referenceSize),
                distanceReferenceSize = referenceSize,
                eulerAngles = eulerAngles,
                offset = avatarTransform.InverseTransformVector(focus - baseFocus),
                backgroundMode = currentSettings.backgroundMode,
                backgroundColor = currentSettings.backgroundColor,
            };

            return true;
        }

        /// <summary>
        /// 撮影したテクスチャを PNG アセットとして保存し、menuIcon 用のインポート設定を適用する。
        /// </summary>
        public static Texture2D SavePngAsset(Texture2D texture, string assetPath, bool hasAlpha)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                throw new ArgumentException("Asset path is required.", nameof(assetPath));
            }

            var absolutePath = ToAbsoluteProjectPath(assetPath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(assetPath, hasAlpha);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ConfigureCamera(Camera camera, ExpressionEditModel model, ThumbnailCaptureSettings settings)
        {
            var avatarTransform = model.avatarRootObject.transform;
            var focus = GetFocusPoint(model, settings.offset);
            var referenceSize = GetFramingReferenceSize(model);
            var distance = ResolveWorldDistance(settings, referenceSize);
            var orbitRotation = avatarTransform.rotation * Quaternion.Euler(settings.eulerAngles);
            var cameraPosition = focus + orbitRotation * Vector3.forward * distance;
            var viewDirection = focus - cameraPosition;
            var up = orbitRotation * Vector3.up;

            camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(viewDirection, up));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = settings.backgroundMode == ThumbnailBackgroundMode.Transparent
                ? new Color(0f, 0f, 0f, 0f)
                : settings.backgroundColor;
            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = Mathf.Max(referenceSize * MinimumDistanceRatio * 0.1f, NumericalEpsilon);
            camera.farClipPlane = Mathf.Max(
                camera.nearClipPlane * 2f,
                distance + Mathf.Max(referenceSize, GetRendererExtent(model.targetRenderer)) * 4f);
            camera.depth = -100f;
            camera.allowHDR = false;
            camera.enabled = false;
        }

        private static Texture2D Render(Camera camera)
        {
            var previousActiveRenderTexture = RenderTexture.active;
            var renderTexture = new RenderTexture(ThumbnailSize, ThumbnailSize, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var texture = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, ThumbnailSize, ThumbnailSize), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActiveRenderTexture;
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static Vector3 GetFocusPoint(ExpressionEditModel model, Vector3 offset)
        {
            return GetBaseFocusPoint(model) + model.avatarRootObject.transform.TransformVector(offset);
        }

        private static Vector3 GetBaseFocusPoint(ExpressionEditModel model)
        {
            var animator = model.avatarRootObject.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    return head.position;
                }
            }

            if (model.targetRenderer != null)
            {
                return model.targetRenderer.bounds.center;
            }

            return model.avatarRootObject.transform.position;
        }

        private static float GetRendererExtent(Renderer renderer)
        {
            return renderer != null ? renderer.bounds.extents.magnitude : 1f;
        }

        private static ThumbnailCaptureSettings GetDefaultSettings(GameObject avatarRootObject)
        {
            var referenceSize = GetAvatarWorldUnitScale(avatarRootObject) * FallbackFaceReferenceRatio;
            return new ThumbnailCaptureSettings
            {
                distance = GetDefaultDistance(avatarRootObject),
                distanceReferenceSize = referenceSize,
                eulerAngles = Vector3.zero,
                offset = new Vector3(0f, -0.02f, 0f),
                backgroundMode = ThumbnailBackgroundMode.Transparent,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
            };
        }

        private static ThumbnailCaptureSettings GetDefaultSettings(ExpressionEditModel model)
        {
            if (model == null || model.avatarRootObject == null || model.targetRenderer == null)
            {
                return GetDefaultSettings(model != null ? model.avatarRootObject : null);
            }

            var referenceSize = GetFramingReferenceSize(model);
            return new ThumbnailCaptureSettings
            {
                distance = referenceSize * DefaultDistanceRatio,
                distanceReferenceSize = referenceSize,
                eulerAngles = Vector3.zero,
                offset = new Vector3(0f, -0.02f, 0f),
                backgroundMode = ThumbnailBackgroundMode.Transparent,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
            };
        }

        private static float GetDefaultDistance(GameObject avatarRootObject)
        {
            var avatarUnitScale = GetAvatarWorldUnitScale(avatarRootObject);
            var fallbackReferenceSize = avatarUnitScale * FallbackFaceReferenceRatio;
            if (avatarRootObject == null)
            {
                return fallbackReferenceSize * DefaultDistanceRatio;
            }

            var renderers = avatarRootObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return fallbackReferenceSize * DefaultDistanceRatio;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Max(GetMinimumWorldDistance(fallbackReferenceSize), bounds.size.y * 0.18f);
        }

        /// <summary>
        /// 対象 Renderer だけを使い、顔として扱う頂点範囲と正面側の注視点を計算する。
        /// </summary>
        internal static bool TryCalculateFaceFraming(
            ExpressionEditModel model,
            out Bounds bounds,
            out Vector3 focus,
            out float referenceSize)
        {
            bounds = default;
            focus = default;
            referenceSize = 0f;
            if (model == null
                || model.avatarRootObject == null
                || model.targetRenderer == null
                || model.targetRenderer.sharedMesh == null)
            {
                return false;
            }

            var head = GetHeadTransform(model.avatarRootObject);
            if (!TryCollectFaceWorldVertices(model, head, out var worldVertices))
            {
                return false;
            }

            var hasBounds = false;
            foreach (var worldVertex in worldVertices)
            {
                Encapsulate(ref bounds, worldVertex, ref hasBounds);
            }

            if (!hasBounds)
            {
                return false;
            }

            focus = CalculateFrontSurfaceFocus(worldVertices, model.avatarRootObject.transform);
            referenceSize = CalculateProjectedReferenceSize(worldVertices, model.avatarRootObject.transform, bounds);
            return IsFinitePositive(referenceSize);
        }

        private static bool TryCollectFaceWorldVertices(
            ExpressionEditModel model,
            Transform head,
            out List<Vector3> selectedWorldVertices)
        {
            selectedWorldVertices = new List<Vector3>();
            var renderer = model.targetRenderer;
            if (renderer == null || renderer.sharedMesh == null || renderer.bones == null || renderer.bones.Length == 0)
            {
                return TryCollectAllWorldVertices(renderer, selectedWorldVertices);
            }

            var headBoneIndices = new HashSet<int>();
            for (var i = 0; i < renderer.bones.Length; i++)
            {
                var bone = renderer.bones[i];
                if (head != null && bone != null && (bone == head || bone.IsChildOf(head)))
                {
                    headBoneIndices.Add(i);
                }
            }

            var sourceMesh = renderer.sharedMesh;
            var bakedMesh = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                renderer.BakeMesh(bakedMesh, true);
                var bakedMeshToWorldMatrix = CreateBakeMeshUseScaleToWorldMatrix(renderer);
                var vertices = bakedMesh.vertices;
                var boneWeights = sourceMesh.boneWeights;
                var allWorldVertices = new List<Vector3>(vertices.Length);
                for (var i = 0; i < vertices.Length; i++)
                {
                    var worldVertex = bakedMeshToWorldMatrix.MultiplyPoint3x4(vertices[i]);
                    allWorldVertices.Add(worldVertex);
                    if (headBoneIndices.Count == 0
                        || boneWeights == null
                        || boneWeights.Length != vertices.Length
                        || !UsesAnyBone(boneWeights[i], headBoneIndices, HeadBoneWeightThreshold))
                    {
                        continue;
                    }

                    selectedWorldVertices.Add(worldVertex);
                }

                if (selectedWorldVertices.Count > 0)
                {
                    return true;
                }

                if (head != null)
                {
                    SelectVerticesNearHead(model, head.position, allWorldVertices, selectedWorldVertices);
                }

                if (selectedWorldVertices.Count == 0)
                {
                    selectedWorldVertices.AddRange(allWorldVertices);
                }

                return selectedWorldVertices.Count > 0;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bakedMesh);
            }
        }

        private static bool TryCollectAllWorldVertices(
            SkinnedMeshRenderer renderer,
            ICollection<Vector3> worldVertices)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                return false;
            }

            var bakedMesh = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                renderer.BakeMesh(bakedMesh, true);
                var bakedMeshToWorldMatrix = CreateBakeMeshUseScaleToWorldMatrix(renderer);
                foreach (var vertex in bakedMesh.vertices)
                {
                    worldVertices.Add(bakedMeshToWorldMatrix.MultiplyPoint3x4(vertex));
                }

                return worldVertices.Count > 0;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bakedMesh);
            }
        }

        private static Matrix4x4 CreateBakeMeshUseScaleToWorldMatrix(SkinnedMeshRenderer renderer)
        {
            // BakeMesh(..., true) は Renderer 自身の localScale を頂点へ焼き込む。
            // 行列側ではその分だけ除去し、アバタールートを含む親階層の Scale は保持する。
            return renderer.localToWorldMatrix * Matrix4x4.Scale(CreateInverseScale(renderer.transform.localScale));
        }

        private static Vector3 CreateInverseScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Abs(scale.x) > NumericalEpsilon ? 1f / scale.x : 0f,
                Mathf.Abs(scale.y) > NumericalEpsilon ? 1f / scale.y : 0f,
                Mathf.Abs(scale.z) > NumericalEpsilon ? 1f / scale.z : 0f);
        }

        private static void SelectVerticesNearHead(
            ExpressionEditModel model,
            Vector3 headPosition,
            IReadOnlyList<Vector3> sourceVertices,
            ICollection<Vector3> selectedVertices)
        {
            var avatarTransform = model.avatarRootObject.transform;
            var radius = GetApproximateHeadRadius(model, sourceVertices);
            var verticalMin = -radius * 1.25f;
            var verticalMax = radius * 1.55f;
            var lateralMax = radius * 1.45f;

            foreach (var worldVertex in sourceVertices)
            {
                var relative = worldVertex - headPosition;
                var vertical = Vector3.Dot(relative, avatarTransform.up);
                var lateral = relative - avatarTransform.up * vertical;
                if (vertical >= verticalMin && vertical <= verticalMax && lateral.magnitude <= lateralMax)
                {
                    selectedVertices.Add(worldVertex);
                }
            }
        }

        private static Vector3 CalculateFrontSurfaceFocus(
            IReadOnlyList<Vector3> worldVertices,
            Transform avatarTransform)
        {
            var right = avatarTransform.right;
            var up = avatarTransform.up;
            var forward = avatarTransform.forward;
            var minDepth = float.PositiveInfinity;
            var maxDepth = float.NegativeInfinity;

            foreach (var vertex in worldVertices)
            {
                var depth = Vector3.Dot(vertex, forward);
                minDepth = Mathf.Min(minDepth, depth);
                maxDepth = Mathf.Max(maxDepth, depth);
            }

            var frontThreshold = maxDepth - (maxDepth - minDepth) * FrontVertexDepthRatio;
            if (!TryCalculateProjectedCenter(
                    worldVertices,
                    right,
                    up,
                    forward,
                    frontThreshold,
                    out var projectedRight,
                    out var projectedUp))
            {
                TryCalculateProjectedCenter(
                    worldVertices,
                    right,
                    up,
                    forward,
                    float.NegativeInfinity,
                    out projectedRight,
                    out projectedUp);
            }

            var projectedDepth = (minDepth + maxDepth) * 0.5f;
            return right * projectedRight + up * projectedUp + forward * projectedDepth;
        }

        private static bool TryCalculateProjectedCenter(
            IReadOnlyList<Vector3> worldVertices,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            float minimumDepth,
            out float projectedRight,
            out float projectedUp)
        {
            projectedRight = 0f;
            projectedUp = 0f;
            var minRight = float.PositiveInfinity;
            var maxRight = float.NegativeInfinity;
            var minUp = float.PositiveInfinity;
            var maxUp = float.NegativeInfinity;
            var count = 0;

            foreach (var vertex in worldVertices)
            {
                if (Vector3.Dot(vertex, forward) < minimumDepth)
                {
                    continue;
                }

                var rightValue = Vector3.Dot(vertex, right);
                var upValue = Vector3.Dot(vertex, up);
                minRight = Mathf.Min(minRight, rightValue);
                maxRight = Mathf.Max(maxRight, rightValue);
                minUp = Mathf.Min(minUp, upValue);
                maxUp = Mathf.Max(maxUp, upValue);
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            projectedRight = (minRight + maxRight) * 0.5f;
            projectedUp = (minUp + maxUp) * 0.5f;
            return true;
        }

        private static float CalculateProjectedReferenceSize(
            IReadOnlyList<Vector3> worldVertices,
            Transform avatarTransform,
            Bounds fallbackBounds)
        {
            var minRight = float.PositiveInfinity;
            var maxRight = float.NegativeInfinity;
            var minUp = float.PositiveInfinity;
            var maxUp = float.NegativeInfinity;
            foreach (var vertex in worldVertices)
            {
                var right = Vector3.Dot(vertex, avatarTransform.right);
                var up = Vector3.Dot(vertex, avatarTransform.up);
                minRight = Mathf.Min(minRight, right);
                maxRight = Mathf.Max(maxRight, right);
                minUp = Mathf.Min(minUp, up);
                maxUp = Mathf.Max(maxUp, up);
            }

            var referenceSize = Mathf.Max(maxRight - minRight, maxUp - minUp);
            if (!IsFinitePositive(referenceSize))
            {
                referenceSize = fallbackBounds.extents.magnitude * 2f;
            }

            return IsFinitePositive(referenceSize)
                ? referenceSize
                : GetAvatarWorldUnitScale(avatarTransform != null ? avatarTransform.gameObject : null)
                    * FallbackFaceReferenceRatio;
        }

        private static float GetApproximateHeadRadius(
            ExpressionEditModel model,
            IReadOnlyList<Vector3> targetWorldVertices)
        {
            var animator = model.avatarRootObject.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                if (head != null && neck != null)
                {
                    var headNeckDistance = Vector3.Distance(head.position, neck.position);
                    if (IsFinitePositive(headNeckDistance))
                    {
                        return headNeckDistance * 1.8f;
                    }
                }
            }

            var bounds = default(Bounds);
            var hasBounds = false;
            foreach (var vertex in targetWorldVertices)
            {
                Encapsulate(ref bounds, vertex, ref hasBounds);
            }

            var targetBasedRadius = hasBounds
                ? Mathf.Max(bounds.size.x * 0.6f, bounds.size.y * 0.14f, bounds.size.z * 0.6f)
                : 0f;
            return IsFinitePositive(targetBasedRadius)
                ? targetBasedRadius
                : GetAvatarWorldUnitScale(model.avatarRootObject) * 0.22f;
        }

        private static float CalculateDistanceToFitBounds(
            ExpressionEditModel model,
            Vector3 focus,
            Bounds bounds,
            Vector3 eulerAngles,
            float referenceSize)
        {
            var avatarTransform = model.avatarRootObject.transform;
            var orbitRotation = avatarTransform.rotation * Quaternion.Euler(eulerAngles);
            var cameraRotation = Quaternion.LookRotation(-(orbitRotation * Vector3.forward), orbitRotation * Vector3.up);
            var tanVertical = Mathf.Tan(FieldOfView * Mathf.Deg2Rad * 0.5f);
            var tanHorizontal = tanVertical;
            var requiredDistance = GetMinimumWorldDistance(referenceSize);

            foreach (var corner in GetBoundsCorners(bounds))
            {
                var relative = corner - focus;
                var x = Mathf.Abs(Vector3.Dot(relative, cameraRotation * Vector3.right)) * AutoFrameMargin;
                var y = Mathf.Abs(Vector3.Dot(relative, cameraRotation * Vector3.up)) * AutoFrameMargin;
                var z = Vector3.Dot(relative, cameraRotation * Vector3.forward);
                requiredDistance = Mathf.Max(requiredDistance, x / tanHorizontal - z, y / tanVertical - z);
            }

            return Mathf.Max(GetMinimumWorldDistance(referenceSize), requiredDistance);
        }

        private static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }

        private static Transform GetHeadTransform(GameObject avatarRootObject)
        {
            var animator = avatarRootObject != null ? avatarRootObject.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            return animator.GetBoneTransform(HumanBodyBones.Head);
        }

        /// <summary>
        /// 設定作成時の顔サイズに対する距離比を、現在の Scale に合わせたワールド距離へ換算する。
        /// </summary>
        internal static float ResolveWorldDistance(ExpressionEditModel model, ThumbnailCaptureSettings settings)
        {
            return ResolveWorldDistance(settings, GetFramingReferenceSize(model));
        }

        private static float ResolveWorldDistance(
            ThumbnailCaptureSettings settings,
            float currentReferenceSize)
        {
            if (!IsFinitePositive(settings.distance))
            {
                return currentReferenceSize * DefaultDistanceRatio;
            }

            if (!IsFinitePositive(settings.distanceReferenceSize))
            {
                return Mathf.Max(GetMinimumWorldDistance(currentReferenceSize), settings.distance);
            }

            var distanceRatio = settings.distance / settings.distanceReferenceSize;
            return Mathf.Max(MinimumDistanceRatio, distanceRatio) * currentReferenceSize;
        }

        internal static float GetFramingReferenceSize(ExpressionEditModel model)
        {
            if (TryCalculateFaceFraming(model, out _, out _, out var referenceSize)
                && IsFinitePositive(referenceSize))
            {
                return referenceSize;
            }

            if (model != null && model.targetRenderer != null)
            {
                var rendererBounds = model.targetRenderer.bounds;
                var rendererReferenceSize = Mathf.Max(rendererBounds.size.x, rendererBounds.size.y);
                if (IsFinitePositive(rendererReferenceSize))
                {
                    return rendererReferenceSize;
                }
            }

            var avatarRootObject = model != null ? model.avatarRootObject : null;
            return GetAvatarWorldUnitScale(avatarRootObject) * FallbackFaceReferenceRatio;
        }

        private static float GetAvatarWorldUnitScale(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return 1f;
            }

            var avatarTransform = avatarRootObject.transform;
            var worldUnitScale = Mathf.Max(
                avatarTransform.TransformVector(Vector3.right).magnitude,
                avatarTransform.TransformVector(Vector3.up).magnitude,
                avatarTransform.TransformVector(Vector3.forward).magnitude);
            return IsFinitePositive(worldUnitScale) ? worldUnitScale : 1f;
        }

        private static float GetMinimumWorldDistance(float referenceSize)
        {
            var safeReferenceSize = IsFinitePositive(referenceSize)
                ? referenceSize
                : FallbackFaceReferenceRatio;
            return Mathf.Max(safeReferenceSize * MinimumDistanceRatio, NumericalEpsilon);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool UsesAnyBone(BoneWeight boneWeight, HashSet<int> boneIndices, float threshold)
        {
            return boneWeight.weight0 >= threshold && boneIndices.Contains(boneWeight.boneIndex0)
                || boneWeight.weight1 >= threshold && boneIndices.Contains(boneWeight.boneIndex1)
                || boneWeight.weight2 >= threshold && boneIndices.Contains(boneWeight.boneIndex2)
                || boneWeight.weight3 >= threshold && boneIndices.Contains(boneWeight.boneIndex3);
        }

        private static void Encapsulate(ref Bounds bounds, Vector3 point, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        private static void ConfigureTextureImporter(string assetPath, bool hasAlpha)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[FacialExpressionController] Cannot configure thumbnail texture importer: {assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = hasAlpha;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = ThumbnailSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                throw new InvalidOperationException("Cannot resolve Unity project root.");
            }

            return Path.Combine(projectRoot.FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static string GetPrefsKey(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return "";
            }

            var globalId = GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject).ToString();
            if (string.IsNullOrEmpty(globalId))
            {
                globalId = avatarRootObject.name;
            }

            return EditorPrefsPrefix + Hash128.Compute(globalId);
        }

        internal static string GetPrefsKey(ExpressionEditModel model)
        {
            if (model == null || model.avatarRootObject == null || model.targetRenderer == null)
            {
                return "";
            }

            var avatarId = GlobalObjectId.GetGlobalObjectIdSlow(model.avatarRootObject).ToString();
            var rendererId = GlobalObjectId.GetGlobalObjectIdSlow(model.targetRenderer).ToString();
            var rendererPath = MiscUtil.GetPathInHierarchy(
                model.targetRenderer.gameObject,
                model.avatarRootObject);
            return EditorPrefsPrefix + Hash128.Compute($"{avatarId}|{rendererId}|{rendererPath}");
        }

        private static bool HasStoredSettings(string key)
        {
            return !string.IsNullOrEmpty(key)
                && (EditorPrefs.HasKey(key + ".version") || EditorPrefs.HasKey(key + ".distance"));
        }
    }
}
