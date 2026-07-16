using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class ThumbnailCaptureServiceTests
    {
        private static readonly string[] SettingSuffixes =
        {
            ".version",
            ".distance",
            ".distanceRatio",
            ".angle.x",
            ".angle.y",
            ".angle.z",
            ".offset.x",
            ".offset.y",
            ".offset.z",
            ".backgroundMode",
            ".background.r",
            ".background.g",
            ".background.b",
            ".background.a",
        };

        private readonly List<Object> _temporaryObjects = new List<Object>();
        private readonly List<string> _settingsKeys = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var settingsKey in _settingsKeys)
            {
                DeleteSettings(settingsKey);
            }

            _settingsKeys.Clear();
            for (var i = _temporaryObjects.Count - 1; i >= 0; i--)
            {
                if (_temporaryObjects[i] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[i]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void AutoFrame_UsesOnlyTargetRendererAndIgnoresAccessoryRenderer()
        {
            var model = CreateModel(new[]
            {
                new Vector3(-0.2f, -0.15f, -0.1f),
                new Vector3(0.2f, -0.15f, -0.1f),
                new Vector3(0f, 0.2f, 0.15f),
            });

            Assert.That(
                ThumbnailCaptureService.TryCalculateAutoSettings(
                    model,
                    default,
                    out var settingsWithoutAccessory,
                    out _),
                Is.True);

            var accessory = CreateRenderer(
                model.avatarRootObject.transform,
                "Accessory",
                new[]
                {
                    new Vector3(-100f, -100f, -100f),
                    new Vector3(100f, -100f, -100f),
                    new Vector3(0f, 100f, 100f),
                });
            accessory.transform.localPosition = new Vector3(500f, 500f, 500f);

            Assert.That(
                ThumbnailCaptureService.TryCalculateAutoSettings(
                    model,
                    default,
                    out var settingsWithAccessory,
                    out _),
                Is.True);
            Assert.That(
                settingsWithAccessory.distance,
                Is.EqualTo(settingsWithoutAccessory.distance).Within(0.0001f));
            AssertVector3Near(settingsWithAccessory.offset, settingsWithoutAccessory.offset, 0.0001f);
            Assert.That(
                settingsWithAccessory.distanceReferenceSize,
                Is.EqualTo(settingsWithoutAccessory.distanceReferenceSize).Within(0.0001f));
        }

        [Test]
        public void FaceFraming_CentersOnFrontSurfaceInsteadOfFullDepthBoundsCenter()
        {
            var model = CreateModel(new[]
            {
                new Vector3(1f, -1f, 1f),
                new Vector3(3f, -1f, 1f),
                new Vector3(2f, 1f, 1f),
                new Vector3(-12f, -1f, -1f),
                new Vector3(-8f, -1f, -1f),
                new Vector3(-10f, 1f, -1f),
            });

            Assert.That(
                ThumbnailCaptureService.TryCalculateFaceFraming(
                    model,
                    out var bounds,
                    out var focus,
                    out _),
                Is.True);
            Assert.That(bounds.center.x, Is.LessThan(-4f));
            Assert.That(focus.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(focus.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [TestCase(0.01f)]
        [TestCase(0.5f)]
        [TestCase(3f)]
        [TestCase(100f)]
        public void AutoFrameAndStoredDistance_ScaleProportionallyWithAvatarScale(float avatarScale)
        {
            var model = CreateModel(new[]
            {
                new Vector3(-0.2f, -0.15f, -0.1f),
                new Vector3(0.2f, -0.15f, -0.1f),
                new Vector3(0f, 0.2f, 0.15f),
            });
            Assert.That(
                ThumbnailCaptureService.TryCalculateAutoSettings(
                    model,
                    default,
                    out var scaleOneSettings,
                    out _),
                Is.True);
            var scaleOneReference = scaleOneSettings.distanceReferenceSize;
            var scaleOneDistance = scaleOneSettings.distance;

            model.avatarRootObject.transform.localScale = Vector3.one * avatarScale;
            Assert.That(
                ThumbnailCaptureService.TryCalculateAutoSettings(
                    model,
                    default,
                    out var scaledSettings,
                    out _),
                Is.True);

            var expectedReferenceSize = scaleOneReference * avatarScale;
            var expectedDistance = scaleOneDistance * avatarScale;
            Assert.That(
                scaledSettings.distanceReferenceSize,
                Is.EqualTo(expectedReferenceSize).Within(GetScaleTolerance(expectedReferenceSize)));
            Assert.That(
                scaledSettings.distance,
                Is.EqualTo(expectedDistance).Within(GetScaleTolerance(expectedDistance)));
            Assert.That(
                scaledSettings.distance / scaledSettings.distanceReferenceSize,
                Is.EqualTo(scaleOneDistance / scaleOneReference).Within(0.0001f));
            Assert.That(
                ThumbnailCaptureService.ResolveWorldDistance(model, scaleOneSettings),
                Is.EqualTo(expectedDistance).Within(GetScaleTolerance(expectedDistance)));
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        public void FaceFraming_BoneSkinnedRenderer_AvatarRootScaleIsAppliedOnce(float avatarScale)
        {
            var model = CreateBoneSkinnedModel(CreateAsymmetricFaceVertices());
            Assert.That(
                ThumbnailCaptureService.TryCalculateFaceFraming(
                    model,
                    out _,
                    out var scaleOneFocus,
                    out var scaleOneReferenceSize),
                Is.True);
            Assert.That(
                ThumbnailCaptureService.TryCalculateAutoSettings(
                    model,
                    default,
                    out var scaleOneSettings,
                    out _),
                Is.True);

            var avatarTransform = model.avatarRootObject.transform;
            var avatarPosition = avatarTransform.position;
            avatarTransform.localScale = Vector3.one * avatarScale;

            Assert.That(
                ThumbnailCaptureService.TryCalculateFaceFraming(
                    model,
                    out _,
                    out var scaledFocus,
                    out var scaledReferenceSize),
                Is.True);
            Assert.That(
                ThumbnailCaptureService.TryCalculateAutoSettings(
                    model,
                    default,
                    out var scaledSettings,
                    out _),
                Is.True);
            AssertVector3Near(
                scaledFocus,
                avatarPosition + (scaleOneFocus - avatarPosition) * avatarScale,
                GetScaleTolerance(scaleOneReferenceSize * avatarScale));
            Assert.That(
                scaledReferenceSize,
                Is.EqualTo(scaleOneReferenceSize * avatarScale)
                    .Within(GetScaleTolerance(scaleOneReferenceSize * avatarScale)));
            AssertVector3Near(scaledSettings.offset, scaleOneSettings.offset, 0.0001f);
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        public void FaceFraming_BoneSkinnedRenderer_AncestorScaleIsAppliedOnce(float ancestorScale)
        {
            var ancestor = CreateGameObject("AvatarAncestor");
            ancestor.transform.position = new Vector3(-2f, 0.75f, 1.5f);
            var model = CreateBoneSkinnedModel(CreateAsymmetricFaceVertices());
            model.avatarRootObject.transform.SetParent(ancestor.transform, false);

            Assert.That(
                ThumbnailCaptureService.TryCalculateFaceFraming(
                    model,
                    out _,
                    out var scaleOneFocus,
                    out var scaleOneReferenceSize),
                Is.True);

            var ancestorPosition = ancestor.transform.position;
            ancestor.transform.localScale = Vector3.one * ancestorScale;

            Assert.That(
                ThumbnailCaptureService.TryCalculateFaceFraming(
                    model,
                    out _,
                    out var scaledFocus,
                    out var scaledReferenceSize),
                Is.True);
            AssertVector3Near(
                scaledFocus,
                ancestorPosition + (scaleOneFocus - ancestorPosition) * ancestorScale,
                GetScaleTolerance(scaleOneReferenceSize * ancestorScale));
            Assert.That(
                scaledReferenceSize,
                Is.EqualTo(scaleOneReferenceSize * ancestorScale)
                    .Within(GetScaleTolerance(scaleOneReferenceSize * ancestorScale)));
        }

        [Test]
        public void FaceFraming_AccountsForParentAndRendererScale()
        {
            var model = CreateModel(new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(0f, 1f, 0f),
            });
            var parent = CreateGameObject("ScaledParent");
            parent.transform.localScale = new Vector3(2f, 0.5f, 1f);
            model.avatarRootObject.transform.SetParent(parent.transform, false);
            model.avatarRootObject.transform.localScale = new Vector3(0.5f, 3f, 1f);
            model.targetRenderer.transform.localScale = new Vector3(1.5f, 0.5f, 2f);

            var referenceSize = ThumbnailCaptureService.GetFramingReferenceSize(model);
            Assert.That(referenceSize, Is.EqualTo(3f).Within(0.0001f));

            var settings = new ThumbnailCaptureSettings
            {
                distance = referenceSize * 2.5f,
                distanceReferenceSize = referenceSize,
            };
            parent.transform.localScale *= 2f;
            Assert.That(
                ThumbnailCaptureService.ResolveWorldDistance(model, settings),
                Is.EqualTo(settings.distance * 2f).Within(0.0001f));
        }

        [Test]
        public void SaveAndLoadSettings_PreservesDistanceRatioAfterScaleChange()
        {
            var model = CreateModel(new[]
            {
                new Vector3(-0.2f, -0.15f, -0.1f),
                new Vector3(0.2f, -0.15f, -0.1f),
                new Vector3(0f, 0.2f, 0.15f),
            });
            var key = ThumbnailCaptureService.GetPrefsKey(model);
            _settingsKeys.Add(key);
            DeleteSettings(key);

            var referenceAtScaleOne = ThumbnailCaptureService.GetFramingReferenceSize(model);
            var settings = new ThumbnailCaptureSettings
            {
                distance = referenceAtScaleOne * 4.2f,
                distanceReferenceSize = referenceAtScaleOne,
                eulerAngles = new Vector3(1f, 2f, 3f),
                offset = new Vector3(0.1f, 0.2f, 0.3f),
                backgroundMode = ThumbnailBackgroundMode.SolidColor,
                backgroundColor = Color.magenta,
            };
            ThumbnailCaptureService.SaveSettings(model, settings);

            model.avatarRootObject.transform.localScale = Vector3.one * 0.25f;
            var loaded = ThumbnailCaptureService.LoadSettings(model);
            var referenceAtSmallScale = ThumbnailCaptureService.GetFramingReferenceSize(model);

            Assert.That(loaded.distanceReferenceSize, Is.EqualTo(referenceAtSmallScale).Within(0.0001f));
            Assert.That(loaded.distance, Is.EqualTo(referenceAtSmallScale * 4.2f).Within(0.0001f));
            AssertVector3Near(loaded.eulerAngles, settings.eulerAngles, 0.0001f);
            AssertVector3Near(loaded.offset, settings.offset, 0.0001f);
        }

        [Test]
        public void SaveAndLoadAvatarSettings_CurrentVersionPreservesOffset()
        {
            var avatarRootObject = CreateGameObject("AvatarSettingsRoot");
            var key = ThumbnailCaptureService.GetPrefsKey(avatarRootObject);
            _settingsKeys.Add(key);
            DeleteSettings(key);
            var settings = new ThumbnailCaptureSettings
            {
                distance = 0.75f,
                eulerAngles = new Vector3(1f, 2f, 3f),
                offset = new Vector3(0.1f, 0.2f, 0.3f),
                backgroundMode = ThumbnailBackgroundMode.SolidColor,
                backgroundColor = Color.magenta,
            };

            ThumbnailCaptureService.SaveSettings(avatarRootObject, settings);
            var loaded = ThumbnailCaptureService.LoadSettings(avatarRootObject);

            Assert.That(loaded.distance, Is.EqualTo(settings.distance).Within(0.0001f));
            AssertVector3Near(loaded.eulerAngles, settings.eulerAngles, 0.0001f);
            AssertVector3Near(loaded.offset, settings.offset, 0.0001f);
            Assert.That(EditorPrefs.GetInt(key + ".version"), Is.EqualTo(3));
        }

        [Test]
        public void LoadSettings_Version2PreservesDistanceRatioButResetsPotentiallyInvalidOffset()
        {
            var model = CreateModel(new[]
            {
                new Vector3(-0.2f, -0.15f, -0.1f),
                new Vector3(0.2f, -0.15f, -0.1f),
                new Vector3(0f, 0.2f, 0.15f),
            });
            var key = ThumbnailCaptureService.GetPrefsKey(model);
            _settingsKeys.Add(key);
            DeleteSettings(key);

            var referenceSize = ThumbnailCaptureService.GetFramingReferenceSize(model);
            var storedSettings = new ThumbnailCaptureSettings
            {
                distance = referenceSize * 4.2f,
                distanceReferenceSize = referenceSize,
                eulerAngles = new Vector3(1f, 2f, 3f),
                offset = new Vector3(10f, 20f, 30f),
                backgroundMode = ThumbnailBackgroundMode.SolidColor,
                backgroundColor = Color.magenta,
            };
            ThumbnailCaptureService.SaveSettings(model, storedSettings);
            EditorPrefs.SetInt(key + ".version", 2);

            var loaded = ThumbnailCaptureService.LoadSettings(model);

            Assert.That(loaded.distance, Is.EqualTo(referenceSize * 4.2f).Within(0.0001f));
            AssertVector3Near(loaded.eulerAngles, storedSettings.eulerAngles, 0.0001f);
            AssertVector3Near(loaded.offset, new Vector3(0f, -0.02f, 0f), 0.0001f);
            Assert.That(loaded.backgroundMode, Is.EqualTo(storedSettings.backgroundMode));
            Assert.That(loaded.backgroundColor, Is.EqualTo(storedSettings.backgroundColor));
            Assert.That(EditorPrefs.GetInt(key + ".version"), Is.EqualTo(3));
        }

        [Test]
        public void MatchPreviewAvatarTransform_PreservesParentedWorldTransformAndAddsOffset()
        {
            var grandparent = CreateGameObject("Grandparent");
            grandparent.transform.SetPositionAndRotation(
                new Vector3(-3f, 6f, 2f),
                Quaternion.Euler(-8f, 19f, 23f));
            grandparent.transform.localScale = new Vector3(0.6f, 1.4f, 2.2f);

            var parent = CreateGameObject("Parent");
            parent.transform.SetParent(grandparent.transform, false);
            parent.transform.localPosition = new Vector3(4f, -2f, 7f);
            parent.transform.localRotation = Quaternion.Euler(17f, -31f, 9f);
            parent.transform.localScale = new Vector3(2f, 0.75f, 1.5f);

            var source = CreateGameObject("Source");
            source.transform.SetParent(parent.transform, false);
            source.transform.localPosition = new Vector3(0.7f, 1.2f, -0.4f);
            source.transform.localRotation = Quaternion.Euler(-12f, 28f, 6f);
            source.transform.localScale = new Vector3(0.8f, 1.3f, 0.6f);

            var preview = CreateGameObject("Preview");
            var worldOffset = new Vector3(11f, -5f, 3f);
            var samplePoints = new[]
            {
                Vector3.zero,
                Vector3.one,
                new Vector3(-0.3f, 0.8f, 1.7f),
            };
            var expectedWorldPoints = new Vector3[samplePoints.Length];
            for (var i = 0; i < samplePoints.Length; i++)
            {
                expectedWorldPoints[i] = source.transform.TransformPoint(samplePoints[i]) + worldOffset;
            }

            var previewCleanupRootObject = FacialExpressionEditorWindow.MatchPreviewAvatarTransform(
                preview.transform,
                source.transform,
                worldOffset);
            _temporaryObjects.Add(previewCleanupRootObject);

            Assert.That(preview.transform.parent, Is.Not.SameAs(parent.transform));
            Assert.That(preview.transform.root.gameObject, Is.SameAs(previewCleanupRootObject));
            Assert.That(previewCleanupRootObject.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
            AssertVector3Near(preview.transform.localScale, source.transform.localScale, 0.0001f);
            for (var i = 0; i < samplePoints.Length; i++)
            {
                AssertVector3Near(
                    preview.transform.TransformPoint(samplePoints[i]),
                    expectedWorldPoints[i],
                    0.0001f);
            }
        }

        private ExpressionEditModel CreateModel(IReadOnlyList<Vector3> vertices)
        {
            var avatarRootObject = CreateGameObject("Avatar");
            var renderer = CreateRenderer(avatarRootObject.transform, "Face", vertices);
            var model = ExpressionEditModel.Create();
            model.avatarRootObject = avatarRootObject;
            model.targetRenderer = renderer;
            _temporaryObjects.Add(model);
            return model;
        }

        private ExpressionEditModel CreateBoneSkinnedModel(IReadOnlyList<Vector3> vertices)
        {
            var avatarRootObject = CreateGameObject("BoneSkinnedAvatar");
            avatarRootObject.transform.position = new Vector3(1.25f, -0.5f, 2.5f);

            var rendererObject = CreateGameObject("BoneSkinnedFace");
            rendererObject.transform.SetParent(avatarRootObject.transform, false);
            var boneObject = CreateGameObject("FaceBone");
            boneObject.transform.SetParent(avatarRootObject.transform, false);

            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh
            {
                name = "BoneSkinnedFaceMesh",
                vertices = ToArray(vertices),
                triangles = CreateTriangleIndices(vertices.Count),
                boneWeights = CreateSingleBoneWeights(vertices.Count),
                bindposes = new[]
                {
                    boneObject.transform.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix,
                },
            };
            mesh.RecalculateBounds();
            renderer.sharedMesh = mesh;
            renderer.bones = new[] { boneObject.transform };
            renderer.rootBone = boneObject.transform;
            renderer.localBounds = mesh.bounds;
            _temporaryObjects.Add(mesh);

            var model = ExpressionEditModel.Create();
            model.avatarRootObject = avatarRootObject;
            model.targetRenderer = renderer;
            _temporaryObjects.Add(model);
            return model;
        }

        private SkinnedMeshRenderer CreateRenderer(
            Transform parent,
            string name,
            IReadOnlyList<Vector3> vertices)
        {
            var rendererObject = new GameObject(name);
            rendererObject.transform.SetParent(parent, false);
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh
            {
                name = $"{name}Mesh",
                vertices = ToArray(vertices),
                triangles = CreateTriangleIndices(vertices.Count),
            };
            mesh.RecalculateBounds();
            renderer.sharedMesh = mesh;
            renderer.localBounds = mesh.bounds;
            _temporaryObjects.Add(mesh);
            return renderer;
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _temporaryObjects.Add(gameObject);
            return gameObject;
        }

        private static Vector3[] ToArray(IReadOnlyList<Vector3> values)
        {
            var result = new Vector3[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                result[i] = values[i];
            }

            return result;
        }

        private static int[] CreateTriangleIndices(int vertexCount)
        {
            var triangleCount = vertexCount / 3;
            var result = new int[triangleCount * 3];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = i;
            }

            return result;
        }

        private static BoneWeight[] CreateSingleBoneWeights(int vertexCount)
        {
            var result = new BoneWeight[vertexCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new BoneWeight
                {
                    boneIndex0 = 0,
                    weight0 = 1f,
                };
            }

            return result;
        }

        private static Vector3[] CreateAsymmetricFaceVertices()
        {
            return new[]
            {
                new Vector3(-0.2f, 1.45f, 0.12f),
                new Vector3(0.2f, 1.45f, 0.12f),
                new Vector3(0f, 1.85f, 0.12f),
                new Vector3(-0.15f, 1f, -0.2f),
                new Vector3(0.15f, 1f, -0.2f),
                new Vector3(0f, 1.3f, -0.2f),
            };
        }

        private static void DeleteSettings(string key)
        {
            foreach (var suffix in SettingSuffixes)
            {
                EditorPrefs.DeleteKey(key + suffix);
            }
        }

        private static void AssertVector3Near(Vector3 actual, Vector3 expected, float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
        }

        private static float GetScaleTolerance(float expected)
        {
            return Mathf.Max(0.000001f, Mathf.Abs(expected) * 0.0001f);
        }
    }
}
