using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class ExpressionClipIOTests
    {
        private readonly List<string> _assetPaths = new List<string>();
        private readonly List<Object> _temporaryObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _assetPaths.Count; i++)
            {
                AssetDatabase.DeleteAsset(_assetPaths[i]);
            }

            _assetPaths.Clear();

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
        public void SaveClipToAsset_NewAssetPath_MakesSourceClipPersistent()
        {
            var clip = new AnimationClip { name = "NewExpression" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_NewAsset.anim");
            _assetPaths.Add(assetPath);

            var savedClip = ExpressionClipIO.SaveClipToAsset(clip, assetPath);

            Assert.That(EditorUtility.IsPersistent(clip), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(assetPath));
            Assert.That(savedClip, Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath)));
        }

        [Test]
        public void SaveClipToAsset_ExistingAssetPath_LeavesSourceClipTemporary()
        {
            var existingClip = new AnimationClip { name = "ExistingExpression" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_ExistingAsset.anim");
            AssetDatabase.CreateAsset(existingClip, assetPath);
            _assetPaths.Add(assetPath);
            var originalGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var originalName = existingClip.name;

            var replacementClip = new AnimationClip { name = "ReplacementExpression" };
            replacementClip.SetCurve(
                "",
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 1f)));
            _temporaryObjects.Add(replacementClip);

            var savedClip = ExpressionClipIO.SaveClipToAsset(replacementClip, assetPath);

            Assert.That(EditorUtility.IsPersistent(replacementClip), Is.False);
            Assert.That(savedClip, Is.Not.Null);
            Assert.That(savedClip.name, Is.EqualTo(originalName));
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(originalGuid));
            Assert.That(savedClip, Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath)));
            Assert.That(AnimationUtility.GetEditorCurve(savedClip, FindLocalPositionXBinding(savedClip, "")), Is.Not.Null);
        }

        [Test]
        public void SaveClipToAsset_NonAnimationAssetPath_ThrowsWithoutReplacingAsset()
        {
            var existingAsset = new Texture2D(1, 1) { name = "ExistingTexture" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_NonAnimation.asset");
            AssetDatabase.CreateAsset(existingAsset, assetPath);
            _assetPaths.Add(assetPath);
            var originalGuid = AssetDatabase.AssetPathToGUID(assetPath);

            var clip = new AnimationClip { name = "ReplacementExpression" };
            _temporaryObjects.Add(clip);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => ExpressionClipIO.SaveClipToAsset(clip, assetPath));

            Assert.That(exception.Message, Does.Contain("AnimationClip 以外"));
            var reloadedAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Assert.That(reloadedAsset, Is.Not.Null);
            Assert.That(reloadedAsset.name, Is.EqualTo(existingAsset.name));
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(originalGuid));
            Assert.That(EditorUtility.IsPersistent(clip), Is.False);
        }

        [Test]
        public void SaveClipToProject_InternalSaveFailure_ReturnsFalse()
        {
            var existingAsset = new Texture2D(1, 1) { name = "ExistingProjectTexture" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_ProjectFailure.asset");
            AssetDatabase.CreateAsset(existingAsset, assetPath);
            _assetPaths.Add(assetPath);
            var originalGuid = AssetDatabase.AssetPathToGUID(assetPath);
            var clip = new AnimationClip { name = "ProjectFailureClip" };
            _temporaryObjects.Add(clip);
            LogAssert.Expect(LogType.Exception, new Regex("AnimationClip 以外"));

            var saved = ExpressionClipIO.SaveClipToProject(clip, assetPath);

            Assert.That(saved, Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(originalGuid));
            Assert.That(EditorUtility.IsPersistent(clip), Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_SaveFailure_KeepsUnsavedStateAndCurrentClip()
        {
            var existingAsset = new Texture2D(1, 1) { name = "ExistingWindowTexture" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_WindowFailure.asset");
            AssetDatabase.CreateAsset(existingAsset, assetPath);
            _assetPaths.Add(assetPath);
            var renderer = CreateAvatarRenderer("Smile");
            var model = ExpressionClipIO.CreateModel(renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);
            var currentClip = new AnimationClip { name = "WindowFailureClip" };
            _temporaryObjects.Add(currentClip);
            var window = ScriptableObject.CreateInstance<FacialExpressionEditorWindow>();
            _temporaryObjects.Add(window);
            SetPrivateField(window, "_model", model);
            SetPrivateField(window, "_currentClip", currentClip);
            SetPrivateField(window, "_currentAssetPath", assetPath);
            GetPrivateMethod(typeof(FacialExpressionEditorWindow), "SetUnsavedChanges")
                .Invoke(window, new object[] { true });
            LogAssert.Expect(LogType.Exception, new Regex("AnimationClip 以外"));

            var saved = (bool)GetPrivateMethod(typeof(FacialExpressionEditorWindow), "TrySave")
                .Invoke(window, null);

            Assert.That(saved, Is.False);
            Assert.That(window.hasUnsavedChanges, Is.True);
            Assert.That(GetPrivateField<AnimationClip>(window, "_currentClip"), Is.SameAs(currentClip));
            Assert.That(GetPrivateField<string>(window, "_currentAssetPath"), Is.EqualTo(assetPath));
        }

        [Test]
        public void FacialExpressionAnimationGenerator_SaveFailure_ReturnsFalseAndDestroysTemporaryClip()
        {
            var existingAsset = new Texture2D(1, 1) { name = "ExistingGeneratorTexture" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_GeneratorFailure.asset");
            AssetDatabase.CreateAsset(existingAsset, assetPath);
            _assetPaths.Add(assetPath);
            var clip = new AnimationClip { name = "GeneratorFailureClip" };
            _temporaryObjects.Add(clip);
            var arguments = new object[] { clip, assetPath, null };

            var saved = (bool)GetPrivateMethod(
                    typeof(FacialExpressionAnimationGenerator),
                    "TrySaveGeneratedClip",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, arguments);

            Assert.That(saved, Is.False);
            Assert.That(arguments[2], Is.TypeOf<System.InvalidOperationException>());
            Assert.That(clip == null, Is.True);
        }

        [Test]
        public void Load_TwoKeyStaticBlendShape_UsesSingleFrameAndPreservesSourceCurve()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var sourceClip = new AnimationClip { name = "StaticTwoKeyExpression" };
            _temporaryObjects.Add(sourceClip);
            var sourceCurve = new AnimationCurve(
                new Keyframe(0f, 42f, 0.25f, 0.25f),
                new Keyframe(2f, 42f, -0.5f, -0.5f));
            sourceClip.SetCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Smile", sourceCurve);

            var model = ExpressionClipIO.Load(sourceClip, renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.SingleFrame));
            Assert.That(model.hasIntermediateKeys, Is.False);
            Assert.That(model.entries[0].value, Is.EqualTo(42f).Within(0.0001f));
            Assert.That(model.entries[0].endValue, Is.EqualTo(42f).Within(0.0001f));

            var roundTripClip = ExpressionClipIO.ToClip(model, "RoundTrip");
            _temporaryObjects.Add(roundTripClip);
            var roundTripCurve = GetBlendShapeCurve(roundTripClip, "Face", "Smile");

            Assert.That(roundTripCurve, Is.Not.Null);
            Assert.That(roundTripCurve.length, Is.EqualTo(2));
            Assert.That(roundTripCurve.keys[1].time, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[1].value, Is.EqualTo(42f).Within(0.0001f));
        }

        [Test]
        public void ToClip_UnchangedWeightBlend_PreservesSourceCurve()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var sourceClip = new AnimationClip { name = "WeightBlendExpression" };
            _temporaryObjects.Add(sourceClip);
            var sourceCurve = new AnimationCurve(
                new Keyframe(0.25f, 10f, 1.5f, 1.5f),
                new Keyframe(2.5f, 80f, -0.25f, -0.25f));
            sourceClip.SetCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Smile", sourceCurve);

            var model = ExpressionClipIO.Load(sourceClip, renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend));
            Assert.That(model.entries[0].value, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(model.entries[0].endValue, Is.EqualTo(80f).Within(0.0001f));

            var roundTripClip = ExpressionClipIO.ToClip(model, "RoundTrip");
            _temporaryObjects.Add(roundTripClip);
            var roundTripCurve = GetBlendShapeCurve(roundTripClip, "Face", "Smile");

            Assert.That(roundTripCurve, Is.Not.Null);
            Assert.That(roundTripCurve.length, Is.EqualTo(2));
            Assert.That(roundTripCurve.keys[0].time, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[1].time, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[0].outTangent, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(roundTripCurve.keys[1].inTangent, Is.EqualTo(-0.25f).Within(0.0001f));
        }

        [Test]
        public void LoadToClip_PreservesNonTargetFloatAndObjectReferenceCurves()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var sourceClip = new AnimationClip { name = "PreservedCurvesExpression" };
            _temporaryObjects.Add(sourceClip);
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.Smile",
                new AnimationCurve(new Keyframe(0f, 30f)));
            sourceClip.SetCurve(
                "Arm",
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2f)));
            sourceClip.SetCurve(
                "OtherFace",
                typeof(SkinnedMeshRenderer),
                "blendShape.Smile",
                new AnimationCurve(new Keyframe(0f, 77f)));
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.LegacyOnly",
                new AnimationCurve(new Keyframe(0f, 15f), new Keyframe(1f, 25f)));

            var spriteBinding = EditorCurveBinding.PPtrCurve("Icon", typeof(SpriteRenderer), "m_Sprite");
            var sprite = CreateSprite("ExpressionIcon");
            AnimationUtility.SetObjectReferenceCurve(
                sourceClip,
                spriteBinding,
                new[]
                {
                    new ObjectReferenceKeyframe
                    {
                        time = 0f,
                        value = sprite,
                    },
                });

            var model = ExpressionClipIO.Load(sourceClip, renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            var expectedPreservedBindings = AnimationUtility.GetCurveBindings(sourceClip)
                .Where(binding => binding.path != "Face"
                    || binding.type != typeof(SkinnedMeshRenderer)
                    || binding.propertyName != "blendShape.Smile")
                .Select(GetBindingKey);
            var actualPreservedBindings = model.preservedCurves
                .Select(curve => GetBindingKey(curve.ToBinding()));
            Assert.That(actualPreservedBindings, Is.EquivalentTo(expectedPreservedBindings));
            Assert.That(model.preservedObjectReferenceCurves, Has.Count.EqualTo(1));

            var roundTripClip = ExpressionClipIO.ToClip(model, "RoundTrip");
            _temporaryObjects.Add(roundTripClip);

            Assert.That(GetBlendShapeCurve(roundTripClip, "Face", "Smile"), Is.Not.Null);
            var transformCurve = AnimationUtility.GetEditorCurve(
                roundTripClip,
                FindLocalPositionXBinding(roundTripClip, "Arm"));
            Assert.That(transformCurve, Is.Not.Null);
            Assert.That(transformCurve.keys[1].value, Is.EqualTo(2f).Within(0.0001f));

            var otherBlendShapeCurve = GetBlendShapeCurve(roundTripClip, "OtherFace", "Smile");
            Assert.That(otherBlendShapeCurve, Is.Not.Null);
            Assert.That(otherBlendShapeCurve.keys[0].value, Is.EqualTo(77f).Within(0.0001f));

            var legacyBlendShapeCurve = GetBlendShapeCurve(roundTripClip, "Face", "LegacyOnly");
            Assert.That(legacyBlendShapeCurve, Is.Not.Null);
            Assert.That(legacyBlendShapeCurve.keys[0].value, Is.EqualTo(15f).Within(0.0001f));
            Assert.That(legacyBlendShapeCurve.keys[1].time, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(legacyBlendShapeCurve.keys[1].value, Is.EqualTo(25f).Within(0.0001f));

            var objectReferenceKeys = AnimationUtility.GetObjectReferenceCurve(roundTripClip, spriteBinding);
            Assert.That(objectReferenceKeys, Has.Length.EqualTo(1));
            Assert.That(objectReferenceKeys[0].value, Is.SameAs(sprite));
        }

        [Test]
        public void Load_IntermediateKeyWeightBlend_DetectsIntermediateKeysAndOnlyRewritesWhenEdited()
        {
            var renderer = CreateAvatarRenderer("Smile", "Blink");
            var sourceClip = new AnimationClip { name = "IntermediateKeyExpression" };
            _temporaryObjects.Add(sourceClip);
            var sourceCurve = new AnimationCurve(
                new Keyframe(0f, 10f, 1.25f, 1.25f),
                new Keyframe(0.5f, 60f, -0.5f, 0.75f),
                new Keyframe(2f, 80f, -0.25f, -0.25f))
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.PingPong,
            };
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.Smile",
                sourceCurve);
            sourceClip.SetCurve(
                "Face",
                typeof(SkinnedMeshRenderer),
                "blendShape.Blink",
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 25f),
                    new Keyframe(2f, 50f)));

            var model = ExpressionClipIO.Load(sourceClip, renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend));
            Assert.That(model.hasIntermediateKeys, Is.True);
            Assert.That(model.entries[0].value, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(model.entries[0].endValue, Is.EqualTo(80f).Within(0.0001f));

            var unchangedClip = ExpressionClipIO.ToClip(model, "UnchangedRoundTrip");
            _temporaryObjects.Add(unchangedClip);
            var unchangedCurve = GetBlendShapeCurve(unchangedClip, "Face", "Smile");
            Assert.That(unchangedCurve, Is.Not.Null);
            Assert.That(unchangedCurve.length, Is.EqualTo(3));
            Assert.That(unchangedCurve.preWrapMode, Is.EqualTo(WrapMode.ClampForever));
            Assert.That(unchangedCurve.postWrapMode, Is.EqualTo(WrapMode.PingPong));
            Assert.That(unchangedCurve.keys[0].time, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[0].value, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[0].outTangent, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[1].time, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[1].value, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[2].time, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[2].value, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(unchangedCurve.keys[2].inTangent, Is.EqualTo(-0.25f).Within(0.0001f));

            model.entries[0].endValue = 90f;

            var editedClip = ExpressionClipIO.ToClip(model, "EditedRoundTrip");
            _temporaryObjects.Add(editedClip);
            var editedCurve = GetBlendShapeCurve(editedClip, "Face", "Smile");
            Assert.That(editedCurve, Is.Not.Null);
            Assert.That(editedCurve.length, Is.EqualTo(2));
            Assert.That(editedCurve.keys[0].time, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(editedCurve.keys[1].time, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(editedCurve.keys[0].value, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(editedCurve.keys[1].value, Is.EqualTo(90f).Within(0.0001f));

            var editedBlinkCurve = GetBlendShapeCurve(editedClip, "Face", "Blink");
            Assert.That(editedBlinkCurve, Is.Not.Null);
            Assert.That(editedBlinkCurve.length, Is.EqualTo(3));
            Assert.That(editedBlinkCurve.keys[2].time, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void ToClip_OutputModes_WriteExpectedBlendShapeCurves()
        {
            var renderer = CreateAvatarRenderer(
                new[] { "Smile", "Blink", "Neutral" },
                new[] { 0f, 0f, 0f });
            var model = ExpressionClipIO.CreateModel(renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);
            GetEntry(model, "Smile").value = 50f;
            GetEntry(model, "Blink").value = 25f;

            var referenceClip = new AnimationClip { name = "ReferenceExpression" };
            _temporaryObjects.Add(referenceClip);
            SetBlendShapeCurve(referenceClip, "Face", "Smile", new AnimationCurve(new Keyframe(0f, 50f)));
            SetBlendShapeCurve(referenceClip, "Face", "Blink", new AnimationCurve(new Keyframe(0f, 0f)));
            SetBlendShapeCurve(referenceClip, "Face", "Neutral", new AnimationCurve(new Keyframe(0f, 0f)));

            var allTargetsClip = ExpressionClipIO.ToClip(model, "AllTargets");
            _temporaryObjects.Add(allTargetsClip);
            Assert.That(
                GetBlendShapeCurveNames(allTargetsClip, "Face"),
                Is.EquivalentTo(new[] { "Smile", "Blink", "Neutral" }));
            AssertBlendShapeValue(allTargetsClip, "Face", "Smile", 50f);
            AssertBlendShapeValue(allTargetsClip, "Face", "Blink", 25f);
            AssertBlendShapeValue(allTargetsClip, "Face", "Neutral", 0f);

            var sessionDiffClip = ExpressionClipIO.ToClip(
                model,
                "SessionDiff",
                outputSettings: new ExpressionOutputSettings { mode = BlendShapeOutputMode.SessionBaselineDiff });
            _temporaryObjects.Add(sessionDiffClip);
            Assert.That(
                GetBlendShapeCurveNames(sessionDiffClip, "Face"),
                Is.EquivalentTo(new[] { "Smile", "Blink" }));
            AssertBlendShapeValue(sessionDiffClip, "Face", "Smile", 50f);
            AssertBlendShapeValue(sessionDiffClip, "Face", "Blink", 25f);

            var referenceDiffClip = ExpressionClipIO.ToClip(
                model,
                "ReferenceDiff",
                outputSettings: new ExpressionOutputSettings
                {
                    mode = BlendShapeOutputMode.ReferenceClipDiff,
                    referenceClip = referenceClip,
                });
            _temporaryObjects.Add(referenceDiffClip);
            Assert.That(
                GetBlendShapeCurveNames(referenceDiffClip, "Face"),
                Is.EquivalentTo(new[] { "Blink" }));
            AssertBlendShapeValue(referenceDiffClip, "Face", "Blink", 25f);
        }

        [Test]
        public void ToClip_DuplicateEntry_DoesNotOverwriteEditableEntry()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var model = ExpressionClipIO.CreateModel(renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);
            model.entries[0].value = 10f;
            model.entries.Add(new BlendShapeEntry
            {
                index = 1,
                name = "Smile",
                value = 90f,
                endValue = 90f,
                initialValue = 0f,
                systemExclusion = BlendShapeSystemExclusionReason.Duplicate,
                systemExclusionUnlocked = true,
            });

            var clip = ExpressionClipIO.ToClip(model, "DuplicateName");
            _temporaryObjects.Add(clip);

            Assert.That(model.entries[1].IsSystemLocked, Is.True);
            Assert.That(model.entries[1].ShouldOutput, Is.False);
            AssertBlendShapeValue(clip, "Face", "Smile", 10f);
        }

        [Test]
        public void ToClip_DestroyedTargetRenderer_ThrowsBeforeWritingBrokenPath()
        {
            var renderer = CreateAvatarRenderer("Smile");
            var model = ExpressionClipIO.CreateModel(renderer.transform.root.gameObject, renderer);
            _temporaryObjects.Add(model);

            Object.DestroyImmediate(renderer);

            var ex = Assert.Throws<System.InvalidOperationException>(() => ExpressionClipIO.ToClip(model, "BrokenReference"));
            Assert.That(ex.Message, Does.Contain("Skinned Mesh Renderer"));
        }

        [Test]
        public void FromAvatar_InvalidResolvedRenderer_ReturnsNullInsteadOfThrowing()
        {
            var avatarRoot = new GameObject("Avatar");
            var face = new GameObject("Face");
            face.transform.SetParent(avatarRoot.transform, false);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            var descriptor = avatarRoot.AddComponent<VRCAvatarDescriptor>();
            descriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
            descriptor.VisemeSkinnedMesh = renderer;
            _temporaryObjects.Add(avatarRoot);

            var generator = new FacialExpressionAnimationGenerator();
            var clip = generator.FromAvatar("Invalid", avatarRoot, null);

            Assert.That(clip, Is.Null);
        }

        private static string GenerateAssetPath(string fileName)
        {
            return AssetDatabase.GenerateUniqueAssetPath($"Assets/{fileName}");
        }

        private SkinnedMeshRenderer CreateAvatarRenderer(params string[] blendShapeNames)
        {
            return CreateAvatarRenderer(blendShapeNames, null);
        }

        private SkinnedMeshRenderer CreateAvatarRenderer(IReadOnlyList<string> blendShapeNames, IReadOnlyList<float> weights)
        {
            var avatarRoot = new GameObject("Avatar");
            var face = new GameObject("Face");
            face.transform.SetParent(avatarRoot.transform, false);
            var renderer = face.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);
            if (weights != null)
            {
                for (var i = 0; i < weights.Count && i < blendShapeNames.Count; i++)
                {
                    renderer.SetBlendShapeWeight(i, weights[i]);
                }
            }

            _temporaryObjects.Add(renderer.sharedMesh);
            _temporaryObjects.Add(avatarRoot);
            return renderer;
        }

        private static Mesh CreateMesh(IEnumerable<string> blendShapeNames)
        {
            var mesh = new Mesh
            {
                name = "TestFaceMesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };
            var deltaVertices = new[]
            {
                Vector3.forward,
                Vector3.zero,
                Vector3.zero,
            };
            var deltaNormals = new Vector3[3];
            var deltaTangents = new Vector3[3];
            foreach (var blendShapeName in blendShapeNames)
            {
                mesh.AddBlendShapeFrame(blendShapeName, 100f, deltaVertices, deltaNormals, deltaTangents);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static AnimationCurve GetBlendShapeCurve(AnimationClip clip, string path, string blendShapeName)
        {
            return AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeName}"));
        }

        private static System.Reflection.MethodInfo GetPrivateMethod(
            System.Type type,
            string methodName,
            System.Reflection.BindingFlags bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        {
            var method = type.GetMethod(methodName, bindingFlags);
            Assert.That(method, Is.Not.Null, $"Private method was not found: {type.FullName}.{methodName}");
            return method;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {target.GetType().FullName}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {target.GetType().FullName}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static string GetBindingKey(EditorCurveBinding binding)
        {
            return $"{binding.path}|{binding.type?.AssemblyQualifiedName}|{binding.propertyName}";
        }

        private static EditorCurveBinding FindLocalPositionXBinding(AnimationClip clip, string path)
        {
            return AnimationUtility.GetCurveBindings(clip).Single(binding =>
                binding.path == path
                && binding.type == typeof(Transform)
                && (binding.propertyName == "localPosition.x" || binding.propertyName == "m_LocalPosition.x"));
        }

        private static BlendShapeEntry GetEntry(ExpressionEditModel model, string name)
        {
            return model.entries.First(entry => entry.name == name);
        }

        private static void SetBlendShapeCurve(
            AnimationClip clip,
            string path,
            string blendShapeName,
            AnimationCurve curve)
        {
            clip.SetCurve(path, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeName}", curve);
        }

        private static IEnumerable<string> GetBlendShapeCurveNames(AnimationClip clip, string path)
        {
            return AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.path == path
                    && binding.type == typeof(SkinnedMeshRenderer)
                    && binding.propertyName.StartsWith("blendShape."))
                .Select(binding => binding.propertyName.Substring("blendShape.".Length));
        }

        private static void AssertBlendShapeValue(AnimationClip clip, string path, string blendShapeName, float expectedValue)
        {
            var curve = GetBlendShapeCurve(clip, path, blendShapeName);
            Assert.That(curve, Is.Not.Null);
            Assert.That(curve.length, Is.EqualTo(1));
            Assert.That(curve.keys[0].time, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(curve.keys[0].value, Is.EqualTo(expectedValue).Within(0.0001f));
        }

        private Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(1, 1) { name = $"{name}Texture" };
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            sprite.name = name;
            _temporaryObjects.Add(texture);
            _temporaryObjects.Add(sprite);
            return sprite;
        }
    }
}
