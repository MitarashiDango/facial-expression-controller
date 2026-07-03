using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

            ExpressionClipIO.SaveClipToAsset(clip, assetPath);

            Assert.That(EditorUtility.IsPersistent(clip), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(assetPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath), Is.Not.Null);
        }

        [Test]
        public void SaveClipToAsset_ExistingAssetPath_LeavesSourceClipTemporary()
        {
            var existingClip = new AnimationClip { name = "ExistingExpression" };
            var assetPath = GenerateAssetPath("ExpressionClipIOTests_ExistingAsset.anim");
            AssetDatabase.CreateAsset(existingClip, assetPath);
            _assetPaths.Add(assetPath);

            var replacementClip = new AnimationClip { name = "ReplacementExpression" };
            replacementClip.SetCurve(
                "",
                typeof(Transform),
                "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 1f)));
            _temporaryObjects.Add(replacementClip);

            ExpressionClipIO.SaveClipToAsset(replacementClip, assetPath);

            Assert.That(EditorUtility.IsPersistent(replacementClip), Is.False);
            var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Assert.That(savedClip, Is.Not.Null);
            Assert.That(AnimationUtility.GetEditorCurve(savedClip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.x")), Is.Not.Null);
        }

        private static string GenerateAssetPath(string fileName)
        {
            return AssetDatabase.GenerateUniqueAssetPath($"Assets/{fileName}");
        }
    }
}
