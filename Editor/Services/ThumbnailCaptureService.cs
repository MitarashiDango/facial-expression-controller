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
        public float distance;
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
        private const string EditorPrefsPrefix = "MitarashiDango.FacialExpressionController.ThumbnailCapture.";

        /// <summary>
        /// アバター単位で保存された撮影設定を読み込む。
        /// </summary>
        public static ThumbnailCaptureSettings LoadSettings(GameObject avatarRootObject)
        {
            var defaults = GetDefaultSettings(avatarRootObject);
            var key = GetPrefsKey(avatarRootObject);
            if (string.IsNullOrEmpty(key))
            {
                return defaults;
            }

            return new ThumbnailCaptureSettings
            {
                distance = EditorPrefs.GetFloat(key + ".distance", defaults.distance),
                eulerAngles = new Vector3(
                    EditorPrefs.GetFloat(key + ".angle.x", defaults.eulerAngles.x),
                    EditorPrefs.GetFloat(key + ".angle.y", defaults.eulerAngles.y),
                    EditorPrefs.GetFloat(key + ".angle.z", defaults.eulerAngles.z)),
                offset = new Vector3(
                    EditorPrefs.GetFloat(key + ".offset.x", defaults.offset.x),
                    EditorPrefs.GetFloat(key + ".offset.y", defaults.offset.y),
                    EditorPrefs.GetFloat(key + ".offset.z", defaults.offset.z)),
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

            EditorPrefs.SetFloat(key + ".distance", Mathf.Max(0.01f, settings.distance));
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
        /// Head ボーンと頭部周辺 Bounds から撮影設定を自動計算する。
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

            var avatarTransform = model.avatarRootObject.transform;
            var baseFocus = GetBaseFocusPoint(model);
            var focus = baseFocus;
            if (TryCalculateHeadBounds(model, out var headBounds))
            {
                focus = headBounds.center;
            }
            else
            {
                headBounds = new Bounds(baseFocus, Vector3.one * GetApproximateHeadRadius(model));
                message = "頭部 Bounds を特定できなかったため、Head ボーン位置から概算しました。";
            }

            var eulerAngles = Vector3.zero;
            calculatedSettings = new ThumbnailCaptureSettings
            {
                distance = CalculateDistanceToFitBounds(model, focus, headBounds, eulerAngles),
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
            var distance = Mathf.Max(0.01f, settings.distance);
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
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(10f, distance + GetRendererExtent(model.targetRenderer) * 4f);
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
            return new ThumbnailCaptureSettings
            {
                distance = GetDefaultDistance(avatarRootObject),
                eulerAngles = Vector3.zero,
                offset = new Vector3(0f, -0.02f, 0f),
                backgroundMode = ThumbnailBackgroundMode.Transparent,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
            };
        }

        private static float GetDefaultDistance(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return 0.65f;
            }

            var renderers = avatarRootObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0.65f;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Clamp(bounds.size.y * 0.18f, 0.45f, 0.9f);
        }

        private static bool TryCalculateHeadBounds(ExpressionEditModel model, out Bounds bounds)
        {
            bounds = default;

            var head = GetHeadTransform(model.avatarRootObject);
            var hasBounds = false;
            if (head != null)
            {
                foreach (var renderer in model.avatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (TryCalculateHeadBoneWeightedBounds(renderer, head, out var rendererBounds))
                    {
                        Encapsulate(ref bounds, rendererBounds, ref hasBounds);
                    }
                }

            }

            if (hasBounds)
            {
                return true;
            }

            return head != null && TryCalculateProximityHeadBounds(model, head.position, out bounds);
        }

        private static bool TryCalculateHeadBoneWeightedBounds(SkinnedMeshRenderer renderer, Transform head, out Bounds bounds)
        {
            bounds = default;
            if (renderer == null || renderer.sharedMesh == null || renderer.bones == null || renderer.bones.Length == 0)
            {
                return false;
            }

            var headBoneIndices = new HashSet<int>();
            for (var i = 0; i < renderer.bones.Length; i++)
            {
                var bone = renderer.bones[i];
                if (bone == head)
                {
                    headBoneIndices.Add(i);
                }
            }

            if (headBoneIndices.Count == 0)
            {
                return false;
            }

            var sourceMesh = renderer.sharedMesh;
            if (sourceMesh.boneWeights == null || sourceMesh.boneWeights.Length != sourceMesh.vertexCount)
            {
                return false;
            }

            var bakedMesh = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                renderer.BakeMesh(bakedMesh);
                var vertices = bakedMesh.vertices;
                var boneWeights = sourceMesh.boneWeights;
                var hasBounds = false;
                for (var i = 0; i < vertices.Length && i < boneWeights.Length; i++)
                {
                    if (!UsesAnyBone(boneWeights[i], headBoneIndices, HeadBoneWeightThreshold))
                    {
                        continue;
                    }

                    Encapsulate(ref bounds, renderer.transform.TransformPoint(vertices[i]), ref hasBounds);
                }

                return hasBounds;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bakedMesh);
            }
        }

        private static bool TryCalculateProximityHeadBounds(ExpressionEditModel model, Vector3 headPosition, out Bounds bounds)
        {
            bounds = default;
            var avatarTransform = model.avatarRootObject.transform;
            var radius = GetApproximateHeadRadius(model);
            var verticalMin = -radius * 1.25f;
            var verticalMax = radius * 1.55f;
            var lateralMax = radius * 1.45f;
            var hasBounds = false;

            foreach (var renderer in model.avatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var bakedMesh = new Mesh
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                try
                {
                    renderer.BakeMesh(bakedMesh);
                    foreach (var vertex in bakedMesh.vertices)
                    {
                        var worldVertex = renderer.transform.TransformPoint(vertex);
                        var relative = worldVertex - headPosition;
                        var vertical = Vector3.Dot(relative, avatarTransform.up);
                        var lateral = relative - avatarTransform.up * vertical;
                        if (vertical >= verticalMin && vertical <= verticalMax && lateral.magnitude <= lateralMax)
                        {
                            Encapsulate(ref bounds, worldVertex, ref hasBounds);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
            }

            return hasBounds;
        }

        private static float CalculateDistanceToFitBounds(
            ExpressionEditModel model,
            Vector3 focus,
            Bounds bounds,
            Vector3 eulerAngles)
        {
            var avatarTransform = model.avatarRootObject.transform;
            var orbitRotation = avatarTransform.rotation * Quaternion.Euler(eulerAngles);
            var cameraRotation = Quaternion.LookRotation(-(orbitRotation * Vector3.forward), orbitRotation * Vector3.up);
            var tanVertical = Mathf.Tan(FieldOfView * Mathf.Deg2Rad * 0.5f);
            var tanHorizontal = tanVertical;
            var requiredDistance = 0.01f;

            foreach (var corner in GetBoundsCorners(bounds))
            {
                var relative = corner - focus;
                var x = Mathf.Abs(Vector3.Dot(relative, cameraRotation * Vector3.right)) * AutoFrameMargin;
                var y = Mathf.Abs(Vector3.Dot(relative, cameraRotation * Vector3.up)) * AutoFrameMargin;
                var z = Vector3.Dot(relative, cameraRotation * Vector3.forward);
                requiredDistance = Mathf.Max(requiredDistance, x / tanHorizontal - z, y / tanVertical - z);
            }

            return Mathf.Clamp(requiredDistance, 0.05f, 10f);
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

        private static float GetApproximateHeadRadius(ExpressionEditModel model)
        {
            var avatarBounds = CalculateAvatarBounds(model.avatarRootObject);
            var radius = avatarBounds.HasValue ? avatarBounds.Value.size.y * 0.14f : 0.22f;
            var animator = model.avatarRootObject.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                if (head != null && neck != null)
                {
                    radius = Mathf.Max(radius, Vector3.Distance(head.position, neck.position) * 1.8f);
                }
            }

            return Mathf.Clamp(radius, 0.12f, 0.42f);
        }

        private static Bounds? CalculateAvatarBounds(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return null;
            }

            var renderers = avatarRootObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return null;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static bool UsesAnyBone(BoneWeight boneWeight, HashSet<int> boneIndices, float threshold)
        {
            return boneWeight.weight0 >= threshold && boneIndices.Contains(boneWeight.boneIndex0)
                || boneWeight.weight1 >= threshold && boneIndices.Contains(boneWeight.boneIndex1)
                || boneWeight.weight2 >= threshold && boneIndices.Contains(boneWeight.boneIndex2)
                || boneWeight.weight3 >= threshold && boneIndices.Contains(boneWeight.boneIndex3);
        }

        private static void Encapsulate(ref Bounds bounds, Bounds value, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                bounds = value;
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(value);
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

        private static string GetPrefsKey(GameObject avatarRootObject)
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
    }
}
