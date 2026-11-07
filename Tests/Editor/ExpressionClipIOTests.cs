using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor.Tests
{
    public sealed class ExpressionClipIOTests
    {
        private readonly List<string> _assetPaths = new List<string>();
        private readonly List<Object> _temporaryObjects = new List<Object>();
        private readonly List<string> _notifications = new List<string>();
        private Object[] _previousSelection;

        /// <summary>
        /// エディターウィンドウの OnEnable が選択中のアバターを拾わないよう、選択を外しておく。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _previousSelection = Selection.objects;
            Selection.activeObject = null;
            _notifications.Clear();
        }

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

            Selection.objects = _previousSelection ?? new Object[0];
            _previousSelection = null;
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
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var currentClip = CreateClipAsset(folderPath, "WindowFailureClip", "Smile", 40f);
            var clipPath = AssetDatabase.GetAssetPath(currentClip);
            var window = CreateEditorWindow(() => true);
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, currentClip);
            EditWindowEntry(window, "Smile", 90f);
            var model = GetWindowModel(window);
            // 保存処理の try 内（ExpressionClipIO.ToClip）で例外を起こすための注入。保存後に取り除く。
            model.entries.Add(null);
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

            bool saved;
            try
            {
                saved = (bool)InvokeWindowMethod(window, "TrySave");
                Assert.Throws<System.OperationCanceledException>(() => window.SaveChanges());
            }
            finally
            {
                model.entries.RemoveAt(model.entries.Count - 1);
            }

            Assert.That(saved, Is.False);
            Assert.That(window.hasUnsavedChanges, Is.True);
            AssertUnsavedIndicator(window, true);
            AssertSaveTarget(window, currentClip, clipPath);
            Assert.That(
                GetFirstBlendShapeKeyValue(AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), "Smile"),
                Is.EqualTo(40f).Within(0.0001f));
        }

        [Test]
        public void FacialExpressionEditorWindow_ChangeAvatarAfterLoad_ClearsSaveTarget()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var firstRenderer = CreateDescriptorAvatarRenderer("Smile");
            var secondRenderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var clipPath = AssetDatabase.GetAssetPath(clip);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            SetWindowAvatar(window, firstRenderer);

            ChangeClipField(window, clip);

            AssertSaveTarget(window, clip, clipPath);
            Assert.That(GetEntry(GetWindowModel(window), "Smile").value, Is.EqualTo(40f).Within(0.0001f));

            EditWindowEntry(window, "Smile", 90f);
            SetWindowAvatar(window, secondRenderer);

            Assert.That(confirmationCount, Is.EqualTo(1));
            var model = GetWindowModel(window);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.avatarRootObject, Is.SameAs(secondRenderer.transform.root.gameObject));
            Assert.That(model.targetRenderer, Is.SameAs(secondRenderer));
            Assert.That(model.hasSourceClip, Is.False);
            AssertNoSaveTarget(window);
            Assert.That(window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_ChangeRendererAfterLoad_ClearsSaveTarget()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var firstRenderer = CreateDescriptorAvatarRenderer("Smile");
            var secondRenderer = AddRenderer(firstRenderer.transform.root.gameObject, "Face2", "Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var clipPath = AssetDatabase.GetAssetPath(clip);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            SetWindowAvatar(window, firstRenderer);
            Assert.That(GetWindowModel(window).targetRenderer, Is.SameAs(firstRenderer));

            ChangeClipField(window, clip);
            AssertSaveTarget(window, clip, clipPath);

            SetWindowRenderer(window, secondRenderer);

            Assert.That(confirmationCount, Is.EqualTo(0));
            var model = GetWindowModel(window);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.avatarRootObject, Is.SameAs(firstRenderer.transform.root.gameObject));
            Assert.That(model.targetRenderer, Is.SameAs(secondRenderer));
            Assert.That(model.hasSourceClip, Is.False);
            AssertNoSaveTarget(window);
            Assert.That(window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_SameAvatarWithDestroyedRenderer_RebuildsWithResolvedRenderer()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var firstRenderer = CreateDescriptorAvatarRenderer("Smile");
            var avatarRoot = firstRenderer.transform.root.gameObject;
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var secondRenderer = AddRenderer(avatarRoot, "Face2", "Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            SetWindowAvatar(window, descriptor);
            Assert.That(GetWindowModel(window).targetRenderer, Is.SameAs(firstRenderer));
            ChangeClipField(window, clip);
            EditWindowEntry(window, "Smile", 90f);
            var editedModel = GetWindowModel(window);

            Object.DestroyImmediate(firstRenderer.gameObject);
            SetWindowAvatar(window, descriptor);

            Assert.That(confirmationCount, Is.EqualTo(1));
            var model = GetWindowModel(window);
            Assert.That(model, Is.Not.Null);
            Assert.That(model, Is.Not.SameAs(editedModel));
            Assert.That(model.avatarRootObject, Is.SameAs(avatarRoot));
            Assert.That(model.targetRenderer, Is.SameAs(secondRenderer));
            Assert.That(GetPrivateField<ObjectField>(window, "_rendererField").value, Is.SameAs(secondRenderer));
            AssertNoSaveTarget(window);
            Assert.That(window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_NewSession_ClearsClipField()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, clip);
            AssertSaveTarget(window, clip, AssetDatabase.GetAssetPath(clip));

            InvokeWindowMethod(window, "CreateNewSession");

            Assert.That(confirmationCount, Is.EqualTo(0));
            AssertNoSaveTarget(window);
            Assert.That(GetWindowModel(window), Is.Not.Null);
            Assert.That(GetWindowModel(window).hasSourceClip, Is.False);
            Assert.That(window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_ClearClipField_StartsNewSessionAfterConfirmation()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, clip);
            EditWindowEntry(window, "Smile", 90f);
            var editedModel = GetWindowModel(window);

            ChangeClipField(window, null);

            Assert.That(confirmationCount, Is.EqualTo(1));
            AssertNoSaveTarget(window);
            Assert.That(GetWindowModel(window), Is.Not.Null);
            Assert.That(GetWindowModel(window), Is.Not.SameAs(editedModel));
            Assert.That(GetWindowModel(window).hasSourceClip, Is.False);
            Assert.That(window.hasUnsavedChanges, Is.False);
            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("新規セッションを開始しました。"));
            AssertMessageBoxHidden(window);
        }

        [Test]
        public void FacialExpressionEditorWindow_ClipFieldChangeWithUnsavedChanges_CancelKeepsFieldAndEdits()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var firstClip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var secondClip = CreateClipAsset(folderPath, "Angry", "Smile", 70f);
            var firstClipPath = AssetDatabase.GetAssetPath(firstClip);
            var secondClipPath = AssetDatabase.GetAssetPath(secondClip);
            var discard = false;
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return discard;
            });
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, firstClip);
            EditWindowEntry(window, "Smile", 90f);
            var editedModel = GetWindowModel(window);

            ChangeClipField(window, secondClip);

            Assert.That(confirmationCount, Is.EqualTo(1));
            AssertSaveTarget(window, firstClip, firstClipPath);
            Assert.That(GetWindowModel(window), Is.SameAs(editedModel));
            Assert.That(GetEntry(editedModel, "Smile").value, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(window.hasUnsavedChanges, Is.True);

            ChangeClipField(window, null);

            Assert.That(confirmationCount, Is.EqualTo(2));
            AssertSaveTarget(window, firstClip, firstClipPath);
            Assert.That(GetWindowModel(window), Is.SameAs(editedModel));
            Assert.That(GetEntry(editedModel, "Smile").value, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(window.hasUnsavedChanges, Is.True);

            discard = true;
            ChangeClipField(window, secondClip);

            Assert.That(confirmationCount, Is.EqualTo(3));
            AssertSaveTarget(window, secondClip, secondClipPath);
            Assert.That(GetEntry(GetWindowModel(window), "Smile").value, Is.EqualTo(70f).Within(0.0001f));
            Assert.That(window.hasUnsavedChanges, Is.False);
            AssertUnsavedIndicator(window, false);
        }

        [Test]
        public void FacialExpressionEditorWindow_ClipFieldWithoutTarget_RevertsFieldAndWarns()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });

            ChangeClipField(window, clip);

            Assert.That(confirmationCount, Is.EqualTo(0));
            Assert.That(GetWindowModel(window), Is.Null);
            AssertNoSaveTarget(window);
            var messageBox = GetPrivateField<HelpBox>(window, "_messageBox");
            Assert.That(messageBox.text, Is.EqualTo("アバターと対象 Skinned Mesh Renderer を指定してください。"));
            Assert.That(messageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            Assert.That(messageBox.ClassListContains(ExpressionEditorStyles.HiddenClassName), Is.False);
            Assert.That(_notifications, Is.Empty, "警告は通知に出さない");
        }

        [Test]
        public void FacialExpressionEditorWindow_OverwriteSave_KeepsSavedClipAsSaveTarget()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var clipPath = AssetDatabase.GetAssetPath(clip);
            var window = CreateEditorWindow(() => true);
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, clip);
            EditWindowEntry(window, "Smile", 90f);

            var saved = (bool)InvokeWindowMethod(window, "TrySave");

            Assert.That(saved, Is.True);
            AssertSaveTarget(window, AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), clipPath);
            Assert.That(window.hasUnsavedChanges, Is.False);
            AssertUnsavedIndicator(window, false);
            Assert.That(
                GetFirstBlendShapeKeyValue(AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), "Smile"),
                Is.EqualTo(90f).Within(0.0001f));

            EditWindowEntry(window, "Smile", 70f);
            AssertUnsavedIndicator(window, true);

            saved = (bool)InvokeWindowMethod(window, "TrySave");

            Assert.That(saved, Is.True);
            AssertSaveTarget(window, AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), clipPath);
            Assert.That(window.hasUnsavedChanges, Is.False);
            AssertUnsavedIndicator(window, false);
            Assert.That(
                GetFirstBlendShapeKeyValue(AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), "Smile"),
                Is.EqualTo(70f).Within(0.0001f));
            Assert.That(AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath }), Has.Length.EqualTo(1));
        }

        [Test]
        public void FacialExpressionEditorWindow_OverwriteSaveAfterMove_SavesToMovedClip()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var originalPath = AssetDatabase.GetAssetPath(clip);
            var window = CreateEditorWindow(() => true);
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, clip);
            EditWindowEntry(window, "Smile", 90f);
            var movedFolderPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(folderPath, "Moved"));
            var movedPath = $"{movedFolderPath}/Smile.anim";
            Assert.That(AssetDatabase.MoveAsset(originalPath, movedPath), Is.Empty);

            var saved = (bool)InvokeWindowMethod(window, "TrySave");

            Assert.That(saved, Is.True);
            var movedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(movedPath);
            AssertSaveTarget(window, movedClip, movedPath);
            Assert.That(GetFirstBlendShapeKeyValue(movedClip, "Smile"), Is.EqualTo(90f).Within(0.0001f));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(originalPath), Is.Null);
            Assert.That(System.IO.File.Exists(originalPath), Is.False);
            Assert.That(AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath }), Has.Length.EqualTo(1));
        }

        [Test]
        public void FacialExpressionEditorWindow_LoadSubAssetClip_HasNoOverwriteSaveTarget()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var containerClip = CreateClipAsset(folderPath, "Container", "Smile", 10f);
            var containerPath = AssetDatabase.GetAssetPath(containerClip);
            var subClip = new AnimationClip { name = "SubSmile" };
            SetBlendShapeCurve(subClip, "Face", "Smile", new AnimationCurve(new Keyframe(0f, 60f)));
            AssetDatabase.AddObjectToAsset(subClip, containerPath);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.GetAssetPath(subClip), Is.EqualTo(containerPath));
            Assert.That(AssetDatabase.IsMainAsset(subClip), Is.False);
            var window = CreateEditorWindow(() => true);
            SetWindowAvatar(window, renderer);

            ChangeClipField(window, subClip);

            Assert.That(GetPrivateField<AnimationClip>(window, "_currentClip"), Is.SameAs(subClip));
            Assert.That(GetPrivateField<ObjectField>(window, "_clipField").value, Is.SameAs(subClip));
            Assert.That(GetEntry(GetWindowModel(window), "Smile").value, Is.EqualTo(60f).Within(0.0001f));
            Assert.That(GetOverwriteSaveAssetPath(window), Is.Empty);
        }

        [Test]
        public void FacialExpressionEditorWindow_IsWritableAssetPath_AcceptsAssetsAndRejectsEmpty()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var assetsClipPath = AssetDatabase.GetAssetPath(CreateClipAsset(folderPath, "Smile", "Smile", 40f));

            Assert.That(FacialExpressionEditorWindow.IsWritableAssetPath(assetsClipPath), Is.True);
            Assert.That(FacialExpressionEditorWindow.IsWritableAssetPath(""), Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_IsWritableAssetPath_RejectsRegistryPackage()
        {
            const string registryPackageFilePath = "Packages/com.unity.test-framework/package.json";
            Assume.That(
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(registryPackageFilePath)?.source,
                Is.EqualTo(UnityEditor.PackageManager.PackageSource.Registry),
                "com.unity.test-framework is not installed from the registry in this project.");

            Assert.That(FacialExpressionEditorWindow.IsWritableAssetPath(registryPackageFilePath), Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_IsWritableAssetPath_AcceptsEmbeddedPackage()
        {
            const string embeddedPackageFilePath = "Packages/com.matcha-soft.facial-expression-controller/package.json";
            Assume.That(
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath(embeddedPackageFilePath)?.source,
                Is.EqualTo(UnityEditor.PackageManager.PackageSource.Embedded),
                "com.matcha-soft.facial-expression-controller is not embedded in this project.");

            Assert.That(FacialExpressionEditorWindow.IsWritableAssetPath(embeddedPackageFilePath), Is.True);
        }

        [Test]
        public void FacialExpressionEditorWindow_UndoRedoWithoutModel_DoesNotMarkUnsaved()
        {
            var window = CreateEditorWindow(() => true);
            Assert.That(GetWindowModel(window), Is.Null);

            InvokeWindowMethod(window, "OnUndoRedoPerformed");

            Assert.That(window.hasUnsavedChanges, Is.False);
            AssertUnsavedIndicator(window, false);
        }

        [Test]
        public void FacialExpressionEditorWindow_ListHeader_ReplacesBulkMenuAndResetButton()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var window = CreateEditorWindow(() => true);
            var instanceFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            Assert.That(typeof(FacialExpressionEditorWindow).GetField("_bulkTargetMenu", instanceFields), Is.Null);
            Assert.That(typeof(FacialExpressionEditorWindow).GetField("_resetButton", instanceFields), Is.Null);
            Assert.That(window.rootVisualElement.Query<ToolbarMenu>().ToList().Select(menu => menu.text), Has.None.EqualTo("一括変更"));
            Assert.That(window.rootVisualElement.Query<Button>().ToList().Select(button => button.text), Has.None.EqualTo("リセット"));
            Assert.That(
                window.rootVisualElement.ClassListContains(EditorGUIUtility.isProSkin
                    ? ExpressionEditorStyles.DarkSkinClassName
                    : ExpressionEditorStyles.LightSkinClassName),
                Is.True);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
                var headerToggle = GetPrivateField<Toggle>(listView, "_headerTargetToggle");
                Assert.That(window.rootVisualElement.Q(className: "blendshape-list-header"), Is.Not.Null);
                Assert.That(headerToggle.value, Is.True);
                Assert.That(headerToggle.showMixedValue, Is.False);

                var excludeAction = listView.CreateHeaderMenu()
                    .MenuItems()
                    .OfType<DropdownMenuAction>()
                    .Single(action => action.name == BlendShapeListView.GetBulkAllExcludedLabel(2));
                excludeAction.Execute();
                Undo.FlushUndoRecordObjects();

                Assert.That(model.entries.All(entry => !entry.ShouldOutput), Is.True);
                Assert.That(headerToggle.value, Is.False);
                Assert.That(_notifications.LastOrDefault(), Is.EqualTo("2 件を編集・出力対象外にしました。"), "一括変更の結果は通知で表示する");
                AssertMessageBoxHidden(window);

                Undo.PerformUndo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                Assert.That(model.entries.All(entry => entry.ShouldOutput), Is.True);
                Assert.That(headerToggle.value, Is.True);

                Undo.PerformRedo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                Assert.That(headerToggle.value, Is.False);

                Undo.ClearUndo(model);
                InvokeWindowMethod(window, "CreateNewSession");
                var newListView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");

                Assert.That(newListView, Is.Not.SameAs(listView));
                Assert.That(GetPrivateField<Toggle>(newListView, "_headerTargetToggle").value, Is.True, "新規セッションでは全件が対象");
            }
            finally
            {
                if (model != null)
                {
                    Undo.ClearUndo(model);
                }
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_FilterBar_ChipsReplaceMenuAndFollowListFilters()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink", "Angry");
            var window = CreateEditorWindow(() => true);
            var instanceFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (var removedField in new[]
            {
                "_filterMenu",
                "_sceneInfluenceFilterToggle",
                "_clearSceneInfluenceFilterButton",
                "_sceneInfluenceFilterStatusLabel",
            })
            {
                Assert.That(typeof(FacialExpressionEditorWindow).GetField(removedField, instanceFields), Is.Null, removedField);
            }

            var root = window.rootVisualElement;
            Assert.That(root.Q(className: "filter-primary-row"), Is.Null, "絞り込みは 1 行にまとめた");
            Assert.That(root.Q(className: "filter-scene-row"), Is.Null, "2 段目の行は廃止した");
            Assert.That(root.Query<ToolbarMenu>().ToList(), Is.Empty, "「絞り込み」メニューは廃止した");
            Assert.That(root.Query<Button>().ToList().Select(button => button.text), Has.None.EqualTo("位置フィルタ解除"));

            var filterContainer = root.Q<VisualElement>("filter-container");
            var filterItems = filterContainer.Q(className: "filter-items");
            var changedChip = GetPrivateField<Button>(window, "_changedOnlyFilterChip");
            var editableChip = GetPrivateField<Button>(window, "_editableOnlyFilterChip");
            var outputChip = GetPrivateField<Button>(window, "_outputOnlyFilterChip");
            var countLabel = GetPrivateField<Label>(window, "_filterCountLabel");
            Assert.That(
                new[] { changedChip, editableChip, outputChip }.Select(chip => (chip.text, chip.tooltip)),
                Is.EqualTo(new[]
                {
                    ("変更あり", "変更された項目のみ表示"),
                    ("対象のみ", "編集・出力対象のみ表示"),
                    ("出力予定", "出力予定のみ表示"),
                }));
            Assert.That(filterItems.Contains(GetPrivateField<ToolbarSearchField>(window, "_searchField")), Is.True);
            Assert.That(filterItems.Contains(changedChip), Is.True);
            Assert.That(filterItems.Contains(GetPrivateField<Button>(window, "_sceneInfluenceFilterButton")), Is.True);
            Assert.That(filterItems.Contains(GetPrivateField<VisualElement>(window, "_sceneInfluenceFilterChip")), Is.True);
            Assert.That(filterContainer.IndexOf(countLabel), Is.EqualTo(filterContainer.childCount - 1), "表示数は右端に置く");
            Assert.That(countLabel.tooltip, Is.EqualTo(FacialExpressionEditorWindow.FilterCountTooltip));
            Assert.That(IsHidden(countLabel), Is.True, "編集中の表情がなければ表示数は出さない");
            Assert.That(
                new[] { changedChip, editableChip, outputChip }.Any(chip => chip.ClassListContains(ExpressionEditorStyles.SelectedClassName)),
                Is.False);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
                Assert.That(GetFilterCountText(window), Is.EqualTo("3 / 3"));

                InvokeWindowMethod(window, "ToggleChangedOnlyFilter");

                Assert.That(changedChip.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
                Assert.That(listView.VisibleEntryCount, Is.Zero);
                Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 3"));

                EditWindowEntry(window, "Smile", 50f);
                InvokeWindowMethod(window, "RefreshOutputDecisionUi");

                Assert.That(GetFilterCountText(window), Is.EqualTo("1 / 3"), "編集で絞り込みの結果が変わったら表示数も変わる");

                InvokeWindowMethod(window, "ToggleChangedOnlyFilter");
                InvokeWindowMethod(window, "ToggleEditableOnlyFilter");

                Assert.That(changedChip.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.False);
                Assert.That(editableChip.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
                Assert.That(GetFilterCountText(window), Is.EqualTo("3 / 3"));

                ExecuteHeaderMenuAction(listView, BlendShapeListView.GetBulkAllExcludedLabel(3));

                Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 3"), "一括変更の後も表示数を合わせる");
                Assert.That(editableChip.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
                Assert.That(_notifications.LastOrDefault(), Is.EqualTo("3 件を編集・出力対象外にしました。"));

                Undo.PerformUndo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                Assert.That(GetFilterCountText(window), Is.EqualTo("3 / 3"), "Undo の後も表示数を合わせる");
                Assert.That(editableChip.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);

                Undo.PerformRedo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 3"), "Redo の後も表示数を合わせる");

                InvokeWindowMethod(window, "ToggleEditableOnlyFilter");
                InvokeWindowMethod(window, "ToggleOutputOnlyFilter");

                // 出力予定の判定は、エディターの更新時にまとめて計算し直す。
                InvokeWindowMethod(window, "RefreshOutputDecisionUi");

                Assert.That(outputChip.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
                Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 3"), "対象外は出力予定に含めない");

                InvokeWindowMethod(window, "ToggleOutputOnlyFilter");

                Assert.That(GetFilterCountText(window), Is.EqualTo("3 / 3"));
            }
            finally
            {
                Undo.ClearUndo(model);
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_SceneInfluenceFilter_ShowsLocationChipAndClearsWithCrossButton()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink", "Angry");
            var window = CreateEditorWindow(() => true);
            var sceneButton = GetPrivateField<Button>(window, "_sceneInfluenceFilterButton");
            var sceneIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_sceneInfluenceFilterIcon");
            var locationChip = GetPrivateField<VisualElement>(window, "_sceneInfluenceFilterChip");
            var locationLabel = GetPrivateField<Label>(window, "_sceneInfluenceFilterChipLabel");
            var clearButton = GetPrivateField<Button>(window, "_sceneInfluenceFilterClearButton");
            var clearIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_sceneInfluenceFilterClearIcon");
            Assert.That(sceneButton.text, Is.Null.Or.Empty, "文字ではなくアイコンで示す");
            Assert.That(sceneButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.SceneInfluenceFilterTooltip));
            Assert.That(sceneButton.Contains(sceneIcon), Is.True);
            Assert.That(sceneIcon.Icon, Is.SameAs(ExpressionEditorIcons.ScenePick));
            Assert.That(sceneButton.enabledSelf, Is.False, "編集中の表情がなければ使えない");
            Assert.That(clearButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.ClearSceneInfluenceFilterTooltip));
            Assert.That(clearButton.Contains(clearIcon), Is.True);
            Assert.That(clearIcon.Icon, Is.SameAs(ExpressionEditorIcons.Clear));
            Assert.That(locationChip.Contains(clearButton), Is.True);
            Assert.That(IsHidden(locationChip), Is.True);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);

            ClickButton(sceneButton);

            Assert.That(sceneButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
            Assert.That(IsHidden(locationChip), Is.True, "クリック位置を指定するまでは位置フィルタのチップを出さない");

            ApplySceneInfluenceFilter(window, GetEntry(model, "Smile"));

            Assert.That(IsHidden(locationChip), Is.False);
            Assert.That(locationLabel.text, Is.EqualTo("位置 1"));
            Assert.That(GetFilterCountText(window), Is.EqualTo("1 / 3"));
            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("1 件のブレンドシェイプをクリック位置で絞り込みました。"));
            AssertMessageBoxHidden(window);

            ClickButton(clearButton);

            Assert.That(IsHidden(locationChip), Is.True);
            Assert.That(GetFilterCountText(window), Is.EqualTo("3 / 3"));
            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("位置フィルタを解除しました。"));
            Assert.That(sceneButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True, "× は絞り込みだけを解除する");

            var notificationCount = _notifications.Count;
            ApplySceneInfluenceFilter(window);

            Assert.That(locationLabel.text, Is.EqualTo("位置 0"));
            Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 3"));
            var messageBox = GetPrivateField<HelpBox>(window, "_messageBox");
            Assert.That(messageBox.text, Is.EqualTo("クリック位置に影響するブレンドシェイプは見つかりませんでした。"));
            Assert.That(messageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            Assert.That(_notifications, Has.Count.EqualTo(notificationCount), "警告は通知に出さない");

            ClickButton(sceneButton);

            Assert.That(sceneButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_EmptyByFilters_ShowsClearButtonThatClearsAllFilters()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink", "Angry");
            var window = CreateEditorWindow(() => true);
            var emptyLabel = GetPrivateField<Label>(window, "_emptyLabel");
            var clearFiltersButton = GetPrivateField<Button>(window, "_clearFiltersButton");
            Assert.That(clearFiltersButton.text, Is.EqualTo("絞り込みを解除"));
            Assert.That(clearFiltersButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.ClearFiltersTooltip));
            Assert.That(clearFiltersButton.parent, Is.SameAs(emptyLabel.parent), "0 件の文言の横に置く");
            Assert.That(IsHidden(emptyLabel), Is.False);
            Assert.That(emptyLabel.text, Is.EqualTo(FacialExpressionEditorWindow.EmptyListText));
            Assert.That(IsHidden(clearFiltersButton), Is.True, "編集中の表情がないときは解除ボタンを出さない");

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
            var searchField = GetPrivateField<ToolbarSearchField>(window, "_searchField");
            Assert.That(IsHidden(emptyLabel), Is.True);
            Assert.That(IsHidden(clearFiltersButton), Is.True);

            searchField.SetValueWithoutNotify("NotFound");
            listView.SetSearchText("NotFound");

            Assert.That(IsHidden(emptyLabel), Is.False);
            Assert.That(emptyLabel.text, Is.EqualTo(FacialExpressionEditorWindow.FilteredEmptyListText));
            Assert.That(IsHidden(clearFiltersButton), Is.False);
            Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 3"));
            var listHeader = window.rootVisualElement.Q(className: "blendshape-list-header");
            Assert.That(listHeader, Is.Not.Null);
            Assert.That(IsHidden(listHeader), Is.False, "0 件でも一覧ヘッダーは表示する");

            InvokeWindowMethod(window, "ToggleChangedOnlyFilter");
            InvokeWindowMethod(window, "ToggleEditableOnlyFilter");
            InvokeWindowMethod(window, "ToggleOutputOnlyFilter");
            var sceneButton = GetPrivateField<Button>(window, "_sceneInfluenceFilterButton");
            ClickButton(sceneButton);
            ApplySceneInfluenceFilter(window, GetEntry(model, "Blink"));
            var refreshCount = 0;
            listView.VisibleEntriesChanged += () => refreshCount++;

            ClickButton(clearFiltersButton);

            Assert.That(refreshCount, Is.EqualTo(1), "すべての絞り込みを外しても一覧の作り直しは 1 回にする");
            Assert.That(GetPrivateField<bool>(window, "_sceneInfluenceFilterEnabled"), Is.True, "シーンから絞り込むモードは続ける");
            Assert.That(sceneButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
            Assert.That(searchField.value, Is.Empty);
            Assert.That(GetPrivateField<bool>(window, "_showChangedOnly"), Is.False);
            Assert.That(GetPrivateField<bool>(window, "_showEditableOnly"), Is.False);
            Assert.That(GetPrivateField<bool>(window, "_showOutputOnly"), Is.False);
            Assert.That(GetPrivateField<object>(window, "_sceneInfluenceResults"), Is.Null);
            foreach (var chipFieldName in new[] { "_changedOnlyFilterChip", "_editableOnlyFilterChip", "_outputOnlyFilterChip" })
            {
                Assert.That(
                    GetPrivateField<Button>(window, chipFieldName).ClassListContains(ExpressionEditorStyles.SelectedClassName),
                    Is.False,
                    chipFieldName);
            }

            Assert.That(IsHidden(GetPrivateField<VisualElement>(window, "_sceneInfluenceFilterChip")), Is.True);
            Assert.That(listView.VisibleEntryCount, Is.EqualTo(3));
            Assert.That(GetFilterCountText(window), Is.EqualTo("3 / 3"));
            Assert.That(IsHidden(emptyLabel), Is.True);
            Assert.That(IsHidden(clearFiltersButton), Is.True);

            ClickButton(sceneButton);

            Assert.That(GetPrivateField<bool>(window, "_sceneInfluenceFilterEnabled"), Is.False);
        }

        [Test]
        public void FacialExpressionEditorWindow_EmptyWithoutBlendShapes_KeepsDefaultTextWithoutClearButton()
        {
            var renderer = CreateDescriptorAvatarRenderer();
            var window = CreateEditorWindow(() => true);
            var emptyLabel = GetPrivateField<Label>(window, "_emptyLabel");
            var clearFiltersButton = GetPrivateField<Button>(window, "_clearFiltersButton");

            SetWindowAvatar(window, renderer);

            Assert.That(GetWindowModel(window), Is.Not.Null);
            Assert.That(GetWindowModel(window).entries, Is.Empty);
            Assert.That(IsHidden(emptyLabel), Is.False);
            Assert.That(emptyLabel.text, Is.EqualTo(FacialExpressionEditorWindow.EmptyListText));
            Assert.That(IsHidden(clearFiltersButton), Is.True);
            Assert.That(GetFilterCountText(window), Is.EqualTo("0 / 0"));
        }

        [Test]
        public void FacialExpressionEditorWindow_SuccessMessagesUseNotificationAndWarningsStayInMessageBox()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var window = CreateEditorWindow(() => true);
            var messageBox = GetPrivateField<HelpBox>(window, "_messageBox");
            Assert.That(messageBox.text, Is.EqualTo("アバターを指定してください。"), "操作を続けるための案内はメッセージ欄に出す");
            Assert.That(IsHidden(messageBox), Is.False);
            Assert.That(_notifications, Is.Empty);

            SetWindowAvatar(window, renderer);
            ChangeClipField(window, clip);

            Assert.That(_notifications, Is.EqualTo(new[] { "クリップを読み込みました。" }));
            AssertMessageBoxHidden(window);

            InvokeWindowMethod(window, "ApplyPartialImportValues", (IReadOnlyList<PartialImportValue>)new List<PartialImportValue>());

            Assert.That(messageBox.text, Is.EqualTo("取り込むブレンドシェイプを選択してください。"));
            Assert.That(messageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            Assert.That(IsHidden(messageBox), Is.False);
            Assert.That(_notifications, Has.Count.EqualTo(1), "警告は通知に出さない");

            EditWindowEntry(window, "Smile", 90f);
            var saved = (bool)InvokeWindowMethod(window, "TrySave");

            Assert.That(saved, Is.True);
            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("保存しました。"));
            AssertMessageBoxHidden(window);
        }

        [Test]
        public void FacialExpressionEditorWindow_IntermediateKeysWarning_StaysInOutputSettingsWhileEditingClip()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var twoFrameClip = CreateCurveClipAsset(
                folderPath,
                "TwoFrame",
                "Smile",
                new AnimationCurve(new Keyframe(0f, 10f), new Keyframe(1f, 80f)));
            var intermediateClip = CreateCurveClipAsset(
                folderPath,
                "Intermediate",
                "Smile",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 50f), new Keyframe(1f, 100f)));
            var plainClip = CreateClipAsset(folderPath, "Plain", "Smile", 40f);
            var window = CreateEditorWindow(() => true);
            var outputSettingsMessageBox = GetPrivateField<HelpBox>(window, "_outputSettingsMessageBox");
            SetWindowAvatar(window, renderer);

            ChangeClipField(window, twoFrameClip);

            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("2 フレームのクリップとして読み込みました。"));
            AssertMessageBoxHidden(window);
            Assert.That(IsHidden(outputSettingsMessageBox), Is.True, "中間キーがなければ警告を出さない");

            var notificationCount = _notifications.Count;
            ChangeClipField(window, intermediateClip);

            Assert.That(_notifications, Has.Count.EqualTo(notificationCount + 1), "読み込めたことは通知する");
            AssertMessageBoxHidden(window);
            AssertIntermediateKeysWarningShown(outputSettingsMessageBox);

            var model = GetWindowModel(window);
            var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
            ApplySceneInfluenceFilter(window, GetEntry(model, "Smile"));
            ExecuteHeaderMenuAction(listView, BlendShapeListView.GetBulkAllExcludedLabel(2));
            Undo.ClearUndo(model);
            InvokeWindowMethod(window, "RefreshOutputDecisionUi");

            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("2 件を編集・出力対象外にしました。"));
            AssertMessageBoxHidden(window);
            AssertIntermediateKeysWarningShown(outputSettingsMessageBox);

            InvokeWindowMethod(window, "CreateNewSession");

            Assert.That(IsHidden(outputSettingsMessageBox), Is.True, "新規セッションでは出さない");

            ChangeClipField(window, intermediateClip);
            AssertIntermediateKeysWarningShown(outputSettingsMessageBox);
            ChangeClipField(window, plainClip);

            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("クリップを読み込みました。"));
            Assert.That(IsHidden(outputSettingsMessageBox), Is.True, "中間キーのないクリップに切り替えたら出さない");
        }

        [Test]
        public void FacialExpressionEditorWindow_IntermediateKeysWarning_FollowsFrameModeChangeAndUndo()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var intermediateClip = CreateCurveClipAsset(
                folderPath,
                "Intermediate",
                "Smile",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 50f), new Keyframe(1f, 100f)));
            var window = CreateEditorWindow(() => true);
            var outputSettingsMessageBox = GetPrivateField<HelpBox>(window, "_outputSettingsMessageBox");
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, intermediateClip);
            var model = GetWindowModel(window);
            try
            {
                Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend), "中間キーのあるクリップは 2 フレームとして読み込む");
                AssertIntermediateKeysWarning(outputSettingsMessageBox, FacialExpressionEditorWindow.IntermediateKeysWarningText);

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.SingleFrame);
                Undo.FlushUndoRecordObjects();
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                AssertIntermediateKeysWarning(outputSettingsMessageBox, FacialExpressionEditorWindow.IntermediateKeysSingleFrameWarningText);
                var singleFrameClip = ExpressionClipIO.ToClip(model, "SingleFrame");
                _temporaryObjects.Add(singleFrameClip);
                Assert.That(
                    GetBlendShapeCurve(singleFrameClip, "Face", "Smile").length,
                    Is.EqualTo(1),
                    "警告どおり、1 フレームでは未編集の項目も 1 キーで保存される");

                Undo.PerformUndo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend));
                AssertIntermediateKeysWarning(outputSettingsMessageBox, FacialExpressionEditorWindow.IntermediateKeysWarningText);

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.SingleFrame);
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                AssertIntermediateKeysWarning(outputSettingsMessageBox, FacialExpressionEditorWindow.IntermediateKeysSingleFrameWarningText);

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.WeightBlend);
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                AssertIntermediateKeysWarning(outputSettingsMessageBox, FacialExpressionEditorWindow.IntermediateKeysWarningText);
            }
            finally
            {
                Undo.ClearUndo(model);
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_SkinChange_ReappliesSkinClassIconsAndList()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var window = CreateEditorWindow(() => true);
            SetWindowAvatar(window, renderer);
            var root = window.rootVisualElement;
            var isProSkin = EditorGUIUtility.isProSkin;
            var expectedClassName = isProSkin ? ExpressionEditorStyles.DarkSkinClassName : ExpressionEditorStyles.LightSkinClassName;
            var otherClassName = isProSkin ? ExpressionEditorStyles.LightSkinClassName : ExpressionEditorStyles.DarkSkinClassName;
            var iconFields = new[]
            {
                ("_sceneInfluenceFilterIcon", ExpressionEditorIcons.ScenePick),
                ("_sceneInfluenceFilterClearIcon", ExpressionEditorIcons.Clear),
                ("_frameModeInfoIcon", ExpressionEditorIcons.Info),
                ("_outputModeInfoIcon", ExpressionEditorIcons.Info),
                ("_saveMenuIcon", ExpressionEditorIcons.SaveMenu),
                ("_startFrameMenuIcon", ExpressionEditorIcons.StartFrameMenu),
                ("_previewIcon", ExpressionEditorIcons.PreviewVisible),
                ("_outputSettingsTabWarningIcon", ExpressionEditorIcons.Warning),
            };
            ExpressionEditorStyles.ApplySkinClass(root, !isProSkin);
            SetPrivateField(window, "_appliedProSkin", (bool?)!isProSkin);
            foreach (var (fieldName, _) in iconFields)
            {
                GetPrivateField<ExpressionEditorIconElement>(window, fieldName).SetIcon(null);
            }

            var refreshCount = 0;
            GetPrivateField<BlendShapeListView>(window, "_blendShapeListView").VisibleEntriesChanged += () => refreshCount++;

            InvokeWindowMethod(window, "UpdateSkinClass");

            Assert.That(root.ClassListContains(expectedClassName), Is.True);
            Assert.That(root.ClassListContains(otherClassName), Is.False);
            foreach (var (fieldName, expectedIcon) in iconFields)
            {
                var icon = GetPrivateField<ExpressionEditorIconElement>(window, fieldName);
                Assert.That(icon.Icon, Is.SameAs(expectedIcon), fieldName);
                Assert.That(icon.Texture, Is.Not.Null, fieldName);
            }

            Assert.That(refreshCount, Is.EqualTo(1), "行のアイコンも付け直すため一覧を作り直す");

            InvokeWindowMethod(window, "UpdateSkinClass");

            Assert.That(refreshCount, Is.EqualTo(1), "スキンが変わらなければ作り直さない");
        }

        [Test]
        public void FacialExpressionEditorWindow_ThumbnailCompletionMessages_UseNotification()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var window = CreateEditorWindow(() => true);
            var messageBox = GetPrivateField<HelpBox>(window, "_messageBox");
            SetWindowAvatar(window, renderer);
            var thumbnailCaptureView = GetPrivateField<ThumbnailCaptureView>(window, "_thumbnailCaptureView");
            var messageRequested = GetPrivateField<System.Action<string, HelpBoxMessageType>>(thumbnailCaptureView, "MessageRequested");
            Assert.That(messageRequested, Is.Not.Null, "サムネイル撮影欄のメッセージをウィンドウで受け取る");

            messageRequested("割り当てる PNG を指定してください。", HelpBoxMessageType.Warning);

            Assert.That(messageBox.text, Is.EqualTo("割り当てる PNG を指定してください。"));
            Assert.That(messageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            Assert.That(_notifications, Is.Empty, "警告は通知に出さない");

            messageRequested("メニューアイコンに設定しました。", HelpBoxMessageType.Info);

            Assert.That(_notifications, Is.EqualTo(new[] { "メニューアイコンに設定しました。" }));
            AssertMessageBoxHidden(window);

            InvokeWindowMethod(window, "AutoFrameThumbnail");

            Assert.That(_notifications.LastOrDefault(), Is.EqualTo("距離・角度・オフセットを自動調整しました。"));
            AssertMessageBoxHidden(window);
        }

        [Test]
        public void FacialExpressionEditorWindow_InfoIcons_ReplaceExplanatoryHelpBoxes()
        {
            var uxmlRoot = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/com.matcha-soft.facial-expression-controller/Editor/UI/Windows/FacialExpressionEditorWindow.uxml")
                .CloneTree();
            Assert.That(uxmlRoot.Q(className: "weight-mode-note"), Is.Null);
            Assert.That(uxmlRoot.Query<HelpBox>().ToList().Select(helpBox => helpBox.name), Is.EqualTo(new[] { "message-box" }));

            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;
            var frameInfoIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_frameModeInfoIcon");
            var outputInfoIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_outputModeInfoIcon");
            var outputSettingsMessageBox = GetPrivateField<HelpBox>(window, "_outputSettingsMessageBox");
            Assert.That(root.Q(className: "weight-mode-note"), Is.Null);
            Assert.That(root.Query<HelpBox>().ToList().Select(helpBox => helpBox.text ?? ""), Has.None.Contains("Fist"));
            Assert.That(frameInfoIcon.Icon, Is.SameAs(ExpressionEditorIcons.Info));
            Assert.That(frameInfoIcon.tooltip, Is.EqualTo(FacialExpressionEditorWindow.WeightModeNoteText));
            Assert.That(frameInfoIcon.parent, Is.SameAs(root.Q("frame-mode-container")));
            Assert.That(frameInfoIcon.parent.Contains(GetPrivateField<Button>(window, "_weightBlendModeButton")), Is.True, "フレームモードの切り替えと同じ行に置く");
            Assert.That(outputInfoIcon.Icon, Is.SameAs(ExpressionEditorIcons.Info));
            Assert.That(outputInfoIcon.tooltip, Is.EqualTo(FacialExpressionEditorWindow.DiffOutputNoteText));
            Assert.That(outputInfoIcon.parent.Contains(GetPrivateField<VisualElement>(window, "_outputModeField")), Is.True);
            Assert.That(IsHidden(frameInfoIcon), Is.True);
            Assert.That(IsHidden(outputInfoIcon), Is.True);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                Assert.That(IsHidden(frameInfoIcon), Is.True, "1 フレームでは出さない");

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.WeightBlend);

                Assert.That(IsHidden(frameInfoIcon), Is.False, "2 フレームのときだけ出す");

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.SingleFrame);

                Assert.That(IsHidden(frameInfoIcon), Is.True);

                SetPrivateField(window, "_outputMode", BlendShapeOutputMode.SessionBaselineDiff);
                InvokeWindowMethod(window, "RefreshOutputDecisionUi");

                Assert.That(IsHidden(outputInfoIcon), Is.False);
                Assert.That(IsHidden(outputSettingsMessageBox), Is.True, "差分出力の説明は HelpBox に出さない");

                SetPrivateField(window, "_outputMode", BlendShapeOutputMode.ReferenceClipDiff);
                InvokeWindowMethod(window, "RefreshOutputDecisionUi");

                Assert.That(IsHidden(outputInfoIcon), Is.False);
                Assert.That(IsHidden(outputSettingsMessageBox), Is.False, "設定不備の警告は HelpBox のまま");
                Assert.That(outputSettingsMessageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
                Assert.That(outputSettingsMessageBox.text, Is.Not.Empty);

                SetPrivateField(window, "_outputMode", BlendShapeOutputMode.AllTargets);
                InvokeWindowMethod(window, "RefreshOutputDecisionUi");

                Assert.That(IsHidden(outputInfoIcon), Is.True);
                Assert.That(IsHidden(outputSettingsMessageBox), Is.True);
            }
            finally
            {
                Undo.ClearUndo(model);
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_SameAvatarOrRenderer_KeepsSessionAndEdits()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var clip = CreateClipAsset(folderPath, "Smile", "Smile", 40f);
            var clipPath = AssetDatabase.GetAssetPath(clip);
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            SetWindowAvatar(window, renderer);
            ChangeClipField(window, clip);
            EditWindowEntry(window, "Smile", 90f);
            var editedModel = GetWindowModel(window);

            SetWindowAvatar(window, renderer);
            SetWindowRenderer(window, renderer);

            Assert.That(confirmationCount, Is.EqualTo(0));
            Assert.That(GetWindowModel(window), Is.SameAs(editedModel));
            Assert.That(GetEntry(editedModel, "Smile").value, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(window.hasUnsavedChanges, Is.True);
            AssertSaveTarget(window, clip, clipPath);
        }

        [Test]
        public void FacialExpressionEditorWindow_UnsavedLabel_FollowsUnsavedState()
        {
            const string existingFieldTooltip = "ExistingFieldTooltip";
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            var clipField = GetPrivateField<ObjectField>(window, "_clipField");
            var labelElement = clipField.labelElement;
            clipField.tooltip = existingFieldTooltip;
            AssertUnsavedIndicator(window, false);

            SetWindowAvatar(window, renderer);
            AssertUnsavedIndicator(window, false);

            EditWindowEntry(window, "Smile", 90f);
            AssertUnsavedIndicator(window, true);
            Assert.That(clipField.labelElement, Is.SameAs(labelElement));
            Assert.That(labelElement.style.width.value.value, Is.EqualTo(180f));
            Assert.That(labelElement.style.minWidth.value.value, Is.EqualTo(160f));

            window.DiscardChanges();
            AssertUnsavedIndicator(window, false);
            Assert.That(clipField.tooltip, Is.EqualTo(existingFieldTooltip));

            InvokeWindowMethod(window, "OnUndoRedoPerformed");
            AssertUnsavedIndicator(window, true);
            Assert.That(clipField.tooltip, Is.EqualTo(existingFieldTooltip));

            var notificationCount = _notifications.Count;
            InvokeWindowMethod(window, "OnDisable");
            InvokeWindowMethod(window, "OnEnable");

            Assert.That(GetPrivateField<ObjectField>(window, "_clipField"), Is.Not.SameAs(clipField));
            AssertUnsavedIndicator(window, true);
            Assert.That(_notifications, Has.Count.EqualTo(notificationCount), "未保存はラベルの * で示すため、復元しても通知しない");
            AssertMessageBoxHidden(window);

            InvokeWindowMethod(window, "CreateNewSession");

            Assert.That(confirmationCount, Is.EqualTo(1));
            AssertUnsavedIndicator(window, false);
        }

        [Test]
        public void FacialExpressionEditorWindow_HeaderLayout_PutsDocumentBarFirstAndKeepsHeaderOrder()
        {
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;
            var headerContainer = root.Q<VisualElement>("header-container");

            Assert.That(
                headerContainer.Children().Select(child => child.name),
                Is.EqualTo(new[]
                {
                    "document-bar",
                    "target-foldout",
                    "frame-mode-container",
                    "message-box",
                    "filter-container",
                }));
            Assert.That(root.Q("weight-mode-container"), Is.Null, "2 フレームの操作はフレームモードの行のフレームバーにまとめた");
            Assert.That(root.Q("frame-bar").parent, Is.SameAs(root.Q("frame-mode-container")));
            Assert.That(root.Q("toolbar-container"), Is.Null, "ツールバーの行はドキュメントバーにまとめた");
            Assert.That(
                root.Q("output-settings-container").GetFirstAncestorOfType<ScrollView>(),
                Is.SameAs(root.Q("output-settings-tab-page")),
                "出力設定は見出し部から下部タブへ移した");
        }

        [Test]
        public void FacialExpressionEditorWindow_FrameModeSegment_ReplacesPopupAndFollowsFrameModeAndUndo()
        {
            var instanceFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (var removedFieldName in new[]
            {
                "_frameModeField",
                "_startFrameButton",
                "_endFrameButton",
                "_loadStartFromRendererButton",
                "_weightModeContainer",
            })
            {
                Assert.That(
                    typeof(FacialExpressionEditorWindow).GetField(removedFieldName, instanceFields),
                    Is.Null,
                    $"{removedFieldName} はセグメント切替とフレームバーに置き換えた");
            }

            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;
            var frameModeContainer = root.Q("frame-mode-container");
            var singleFrameButton = GetPrivateField<Button>(window, "_singleFrameModeButton");
            var weightBlendButton = GetPrivateField<Button>(window, "_weightBlendModeButton");
            Assert.That(frameModeContainer.Query<PopupField<string>>().ToList(), Is.Empty, "フレームモードのプルダウンは廃止した");
            Assert.That(singleFrameButton.parent, Is.SameAs(root.Q("frame-mode-segment")));
            Assert.That(weightBlendButton.parent, Is.SameAs(singleFrameButton.parent), "2 つのボタンをつなげて置く");
            Assert.That(
                singleFrameButton.parent.IndexOf(singleFrameButton),
                Is.LessThan(singleFrameButton.parent.IndexOf(weightBlendButton)),
                "「1 フレーム｜2 フレーム」の順");
            Assert.That(singleFrameButton.text, Is.EqualTo(FacialExpressionEditorWindow.SingleFrameModeLabel));
            Assert.That(weightBlendButton.text, Is.EqualTo(FacialExpressionEditorWindow.WeightBlendModeLabel));
            Assert.That(singleFrameButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.SingleFrameModeTooltip));
            Assert.That(weightBlendButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.WeightBlendModeTooltip));
            AssertFrameModeSegment(window, ExpressionFrameMode.SingleFrame, false);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                AssertFrameModeSegment(window, ExpressionFrameMode.SingleFrame, true);
                Assert.That(window.hasUnsavedChanges, Is.False);

                ClickButton(weightBlendButton);
                Undo.FlushUndoRecordObjects();

                AssertFrameModeSegment(window, ExpressionFrameMode.WeightBlend, true);
                Assert.That(window.hasUnsavedChanges, Is.True);
                Assert.That(GetPrivateField<ExpressionEditFrame>(window, "_editingFrame"), Is.EqualTo(ExpressionEditFrame.End), "2 フレームにしたら終点を編集する");
                Assert.That(GetPrivateField<float>(window, "_previewWeight"), Is.EqualTo(1f));

                ClickButton(weightBlendButton);

                AssertFrameModeSegment(window, ExpressionFrameMode.WeightBlend, true);
                Assert.That(GetPrivateField<ExpressionEditFrame>(window, "_editingFrame"), Is.EqualTo(ExpressionEditFrame.End), "選んでいる側をクリックしても変わらない");

                GetEntry(model, "Smile").endValue = 80f;
                ClickButton(singleFrameButton);
                Undo.FlushUndoRecordObjects();

                AssertFrameModeSegment(window, ExpressionFrameMode.SingleFrame, true);
                Assert.That(GetEntry(model, "Smile").endValue, Is.EqualTo(GetEntry(model, "Smile").value), "1 フレームでは終点を始点に揃える");

                Undo.PerformUndo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                AssertFrameModeSegment(window, ExpressionFrameMode.WeightBlend, true);
                Assert.That(GetEntry(model, "Smile").endValue, Is.EqualTo(80f).Within(0.0001f), "1 回の Undo で終点の値も戻る");

                Undo.PerformRedo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                AssertFrameModeSegment(window, ExpressionFrameMode.SingleFrame, true);
                Assert.That(GetEntry(model, "Smile").endValue, Is.EqualTo(GetEntry(model, "Smile").value));

                Undo.ClearUndo(model);
                InvokeWindowMethod(window, "CreateNewSession");
                model = GetWindowModel(window);

                AssertFrameModeSegment(window, ExpressionFrameMode.SingleFrame, true);

                SetWindowAvatar(window, (VRCAvatarDescriptor)null);

                AssertFrameModeSegment(window, ExpressionFrameMode.SingleFrame, false);
            }
            finally
            {
                if (model != null)
                {
                    Undo.ClearUndo(model);
                }
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_FrameBar_MarkersSwitchEditingFrameAndSliderOnlyPreviewsWeight()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;
            var frameBar = GetPrivateField<VisualElement>(window, "_frameBar");
            var startMarker = GetPrivateField<Button>(window, "_startFrameMarker");
            var startMenuButton = GetPrivateField<Button>(window, "_startFrameMenuButton");
            var endMarker = GetPrivateField<Button>(window, "_endFrameMarker");
            var slider = GetPrivateField<Slider>(window, "_previewWeightSlider");
            var infoIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_frameModeInfoIcon");

            Assert.That(
                root.Q("frame-mode-container").Children(),
                Is.EqualTo(new[] { root.Q("frame-mode-segment"), frameBar, infoIcon }),
                "セグメント切替・フレームバー・ⓘ を 1 行に並べる");
            var frameBarElements = frameBar.Query<VisualElement>().ToList();
            var barControlIndices = new VisualElement[] { startMarker, startMenuButton, slider, endMarker }
                .Select(control => frameBarElements.IndexOf(control))
                .ToList();
            Assert.That(barControlIndices, Has.All.GreaterThanOrEqualTo(0), "◆・▾・つまみを 1 本のバーにまとめる");
            Assert.That(barControlIndices, Is.Ordered, "始点の◆と ▾・ウェイトプレビューのつまみ・終点の◆の順");
            Assert.That(startMenuButton.parent, Is.SameAs(startMarker.parent), "始点の◆と ▾ はつなげて置く");
            foreach (var (marker, expectedLabel, expectedTooltip) in new[]
            {
                (startMarker, FacialExpressionEditorWindow.StartFrameMarkerLabel, FacialExpressionEditorWindow.StartFrameMarkerTooltip),
                (endMarker, FacialExpressionEditorWindow.EndFrameMarkerLabel, FacialExpressionEditorWindow.EndFrameMarkerTooltip),
            })
            {
                var diamond = marker.Q(className: "frame-marker__diamond");
                Assert.That(marker.text, Is.Null.Or.Empty, "◆は文字ではなく USS で描く");
                Assert.That(diamond, Is.Not.Null);
                Assert.That(diamond.pickingMode, Is.EqualTo(PickingMode.Ignore));
                Assert.That(marker.Q<Label>(className: "frame-marker__label").text, Is.EqualTo(expectedLabel));
                Assert.That(marker.tooltip, Is.EqualTo(expectedTooltip));
            }

            Assert.That(slider.label, Is.Null.Or.Empty, "ウェイトプレビューの説明は tooltip に出す");
            Assert.That(slider.tooltip, Is.EqualTo(FacialExpressionEditorWindow.PreviewWeightSliderTooltip));
            Assert.That(slider.lowValue, Is.EqualTo(0f));
            Assert.That(slider.highValue, Is.EqualTo(1f));
            Assert.That(IsHidden(frameBar), Is.True);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                Assert.That(IsHidden(frameBar), Is.True, "1 フレームでは出さない");
                Assert.That(startMarker.enabledSelf, Is.False);
                Assert.That(endMarker.enabledSelf, Is.False);
                Assert.That(slider.enabledSelf, Is.False);

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.WeightBlend);

                AssertFrameBar(window, ExpressionEditFrame.End, 1f);

                ClickButton(startMarker);

                AssertFrameBar(window, ExpressionEditFrame.Start, 0f);

                var unsavedBeforeSlider = window.hasUnsavedChanges;
                var valuesBeforeSlider = model.entries.Select(entry => (entry.value, entry.endValue)).ToList();
                ChangePreviewWeightSlider(window, 0.4f);

                AssertFrameBar(window, ExpressionEditFrame.Start, 0.4f);
                Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend));
                Assert.That(model.entries.Select(entry => (entry.value, entry.endValue)), Is.EqualTo(valuesBeforeSlider), "つまみは表情の値を変えない");
                Assert.That(window.hasUnsavedChanges, Is.EqualTo(unsavedBeforeSlider), "つまみはプレビューのウェイトだけを変える");

                ClickButton(endMarker);

                AssertFrameBar(window, ExpressionEditFrame.End, 1f);

                ChangePreviewWeightSlider(window, 0.25f);

                AssertFrameBar(window, ExpressionEditFrame.End, 0.25f);

                ClickButton(endMarker);

                AssertFrameBar(window, ExpressionEditFrame.End, 1f);

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.SingleFrame);

                Assert.That(IsHidden(frameBar), Is.True);
                Assert.That(startMarker.enabledSelf, Is.False);
                Assert.That(startMenuButton.enabledSelf, Is.False);
                Assert.That(endMarker.enabledSelf, Is.False);
                Assert.That(slider.enabledSelf, Is.False);
                Assert.That(GetPrivateField<ExpressionEditFrame>(window, "_editingFrame"), Is.EqualTo(ExpressionEditFrame.Start));
                Assert.That(GetPrivateField<float>(window, "_previewWeight"), Is.EqualTo(0f));
            }
            finally
            {
                Undo.ClearUndo(model);
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_StartFrameMenu_LoadsRendererValuesIntoStart()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;
            var startMarker = GetPrivateField<Button>(window, "_startFrameMarker");
            var menuButton = GetPrivateField<Button>(window, "_startFrameMenuButton");
            var menuIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_startFrameMenuIcon");
            Assert.That(menuButton, Is.InstanceOf<IToolbarMenuElement>());
            Assert.That(menuButton.parent, Is.SameAs(startMarker.parent), "始点の◆の横に置く");
            Assert.That(menuButton.text, Is.Null.Or.Empty, "文字ではなくアイコンで示す");
            Assert.That(menuButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.StartFrameMenuTooltip));
            Assert.That(menuButton.Contains(menuIcon), Is.True);
            Assert.That(menuIcon.Icon, Is.SameAs(ExpressionEditorIcons.StartFrameMenu));
            Assert.That(
                root.Query<Button>().ToList().Select(button => button.text),
                Has.None.EqualTo(FacialExpressionEditorWindow.LoadStartFromRendererMenuItemLabel),
                "単独のボタンは始点の ▾ のメニューへ移した");
            Assert.That(
                window.CreateStartFrameMenu().MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Is.EqualTo(new[] { FacialExpressionEditorWindow.LoadStartFromRendererMenuItemLabel }));
            Assert.That(GetLoadStartFromRendererMenuAction(window).status, Is.EqualTo(DropdownMenuAction.Status.Disabled), "編集中の表情がなければ使えない");

            SetWindowAvatar(window, renderer);
            InvokeWindowMethod(window, "CancelPendingThumbnailCapture");
            var model = GetWindowModel(window);
            try
            {
                Assert.That(menuButton.enabledSelf, Is.False, "1 フレームでは使えない");
                Assert.That(GetLoadStartFromRendererMenuAction(window).status, Is.EqualTo(DropdownMenuAction.Status.Disabled));

                InvokeWindowMethod(window, "SetFrameMode", ExpressionFrameMode.WeightBlend);
                Undo.FlushUndoRecordObjects();

                Assert.That(menuButton.enabledSelf, Is.True);
                Assert.That(GetLoadStartFromRendererMenuAction(window).status, Is.EqualTo(DropdownMenuAction.Status.Normal));
                AssertFrameBar(window, ExpressionEditFrame.End, 1f);

                // プレビューが記録した元の値に戻されないよう、プレビューを止めてから Renderer の値を変える。
                GetEntry(model, "Smile").endValue = 90f;
                InvokeWindowMethod(window, "StopPreview");
                renderer.SetBlendShapeWeight(0, 70f);

                GetLoadStartFromRendererMenuAction(window).Execute();
                Undo.FlushUndoRecordObjects();

                Assert.That(GetEntry(model, "Smile").value, Is.EqualTo(70f).Within(0.0001f));
                Assert.That(GetEntry(model, "Smile").endValue, Is.EqualTo(90f).Within(0.0001f), "終点は変えない");
                Assert.That(GetEntry(model, "Blink").value, Is.EqualTo(0f).Within(0.0001f));
                AssertFrameBar(window, ExpressionEditFrame.Start, 0f);
                Assert.That(_notifications.LastOrDefault(), Is.EqualTo("始点へ現在の Skinned Mesh Renderer 値を読み込みました。"));
                Assert.That(window.hasUnsavedChanges, Is.True);

                Undo.PerformUndo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");

                Assert.That(GetEntry(model, "Smile").value, Is.EqualTo(0f).Within(0.0001f), "読み込みは 1 回の Undo で戻る");
                Assert.That(GetEntry(model, "Smile").endValue, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(model.frameMode, Is.EqualTo(ExpressionFrameMode.WeightBlend));
            }
            finally
            {
                Undo.ClearUndo(model);
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_DocumentBar_HasClipNewSaveMenuAndPreview()
        {
            var instanceFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Assert.That(typeof(FacialExpressionEditorWindow).GetField("_saveAsButton", instanceFields), Is.Null, "別名保存ボタンは保存の ▾ へ移した");
            Assert.That(typeof(FacialExpressionEditorWindow).GetField("_previewToggle", instanceFields), Is.Null, "プレビューは目のアイコンのボタンにした");

            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;
            var documentBar = root.Q<VisualElement>("document-bar");
            var clipField = GetPrivateField<ObjectField>(window, "_clipField");
            var newButton = GetPrivateField<Button>(window, "_newButton");
            var saveButton = GetPrivateField<Button>(window, "_saveButton");
            var saveMenuButton = GetPrivateField<Button>(window, "_saveMenuButton");
            var saveMenuIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_saveMenuIcon");
            var previewButton = GetPrivateField<Button>(window, "_previewButton");
            var documentBarElements = documentBar.Query<VisualElement>().ToList();
            var orderedControls = new VisualElement[] { clipField, newButton, saveButton, saveMenuButton, previewButton };

            var controlIndices = orderedControls.Select(control => documentBarElements.IndexOf(control)).ToList();
            Assert.That(controlIndices, Has.All.GreaterThanOrEqualTo(0), "クリップ欄とボタンを 1 行目に置く");
            Assert.That(controlIndices, Is.Ordered, "クリップ欄・新規・保存 ▾・プレビューの順");
            Assert.That(saveMenuButton.parent, Is.SameAs(saveButton.parent), "保存と ▾ はつなげて置く");
            Assert.That(newButton.text, Is.EqualTo(FacialExpressionEditorWindow.NewButtonLabel));
            Assert.That(saveButton.text, Is.EqualTo(FacialExpressionEditorWindow.SaveButtonLabel));
            Assert.That(saveMenuButton.text, Is.Null.Or.Empty, "文字ではなくアイコンで示す");
            Assert.That(saveMenuButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.SaveMenuTooltip));
            Assert.That(saveMenuButton.Contains(saveMenuIcon), Is.True);
            Assert.That(saveMenuIcon.Icon, Is.SameAs(ExpressionEditorIcons.SaveMenu));
            Assert.That(saveMenuButton, Is.InstanceOf<IToolbarMenuElement>());
            Assert.That(root.Query<Button>().ToList().Select(button => button.text), Has.None.EqualTo("別名保存"));
            Assert.That(root.Query<Toggle>().ToList().Select(toggle => toggle.label), Has.None.EqualTo("プレビュー"));
            Assert.That(
                window.CreateSaveMenu().MenuItems().OfType<DropdownMenuAction>().Select(action => action.name),
                Is.EqualTo(new[] { FacialExpressionEditorWindow.SaveAsMenuItemLabel }));
            Assert.That(newButton.enabledSelf, Is.False);
            Assert.That(saveButton.enabledSelf, Is.False);
            Assert.That(saveMenuButton.enabledSelf, Is.False, "編集中の表情がなければ保存できない");
            Assert.That(GetSaveAsMenuAction(window).status, Is.EqualTo(DropdownMenuAction.Status.Disabled));

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                Assert.That(newButton.enabledSelf, Is.True);
                Assert.That(saveButton.enabledSelf, Is.True);
                Assert.That(saveMenuButton.enabledSelf, Is.True);
                Assert.That(GetSaveAsMenuAction(window).status, Is.EqualTo(DropdownMenuAction.Status.Normal));

                SetWindowOutputMode(window, BlendShapeOutputMode.ReferenceClipDiff);

                Assert.That(saveButton.enabledSelf, Is.False, "参照クリップが無いと保存できない");
                Assert.That(saveMenuButton.enabledSelf, Is.False);
                Assert.That(GetSaveAsMenuAction(window).status, Is.EqualTo(DropdownMenuAction.Status.Disabled));

                SetWindowOutputMode(window, BlendShapeOutputMode.AllTargets);
                EditWindowEntry(window, "Smile", 90f);
                var selections = new List<(string defaultName, string defaultDirectory)>();
                var selectedPath = "";
                window.SaveAsPathSelector = (defaultName, defaultDirectory) =>
                {
                    selections.Add((defaultName, defaultDirectory));
                    return selectedPath;
                };

                GetSaveAsMenuAction(window).Execute();

                Assert.That(selections, Is.EqualTo(new[] { ("FacialExpression_Avatar", "Assets") }), "名前を付けて保存の保存先を選ぶ");
                AssertNoSaveTarget(window);
                Assert.That(window.hasUnsavedChanges, Is.True, "キャンセルしたら未保存のまま");

                selectedPath = $"{folderPath}/SavedAs.anim";
                GetSaveAsMenuAction(window).Execute();

                var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(selectedPath);
                Assert.That(savedClip, Is.Not.Null);
                AssertSaveTarget(window, savedClip, selectedPath);
                Assert.That(GetFirstBlendShapeKeyValue(savedClip, "Smile"), Is.EqualTo(90f).Within(0.0001f));
                Assert.That(window.hasUnsavedChanges, Is.False);
                AssertUnsavedIndicator(window, false);
                Assert.That(_notifications.LastOrDefault(), Is.EqualTo("保存しました。"));

                EditWindowEntry(window, "Smile", 70f);
                ClickButton(saveButton);

                Assert.That(selections, Has.Count.EqualTo(2), "保存先のあるクリップは上書き保存する");
                Assert.That(GetFirstBlendShapeKeyValue(AssetDatabase.LoadAssetAtPath<AnimationClip>(selectedPath), "Smile"), Is.EqualTo(70f).Within(0.0001f));
                Assert.That(window.hasUnsavedChanges, Is.False);
            }
            finally
            {
                Undo.ClearUndo(model);
            }
        }

        [Test]
        public void FacialExpressionEditorWindow_PreviewButton_TogglesScenePreview()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var window = CreateEditorWindow(() => true);
            var previewButton = GetPrivateField<Button>(window, "_previewButton");
            var previewIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_previewIcon");
            Assert.That(previewButton.text, Is.Null.Or.Empty, "文字ではなくアイコンで示す");
            Assert.That(previewButton.Contains(previewIcon), Is.True);
            AssertPreviewState(window, true);

            SetWindowAvatar(window, renderer);

            Assert.That(previewButton.enabledSelf, Is.False, "サムネイルのプレビュー更新中は切り替えられない");

            InvokeWindowMethod(window, "CancelPendingThumbnailCapture");
            Assert.That(previewButton.enabledSelf, Is.True);

            ClickButton(previewButton);

            AssertPreviewState(window, false);

            InvokeWindowMethod(window, "PreviewThumbnail");

            Assert.That(previewButton.enabledSelf, Is.False);
            Assert.That(GetPrivateField<ExpressionPreviewService>(window, "_previewService").PreviewEnabled, Is.True, "撮影中だけ一時的に適用する");

            InvokeWindowMethod(window, "CancelPendingThumbnailCapture");

            Assert.That(previewButton.enabledSelf, Is.True);
            AssertPreviewState(window, false);

            InvokeWindowMethod(window, "OnDisable");
            InvokeWindowMethod(window, "OnEnable");

            var rebuiltPreviewButton = GetPrivateField<Button>(window, "_previewButton");
            Assert.That(rebuiltPreviewButton, Is.Not.SameAs(previewButton));
            AssertPreviewState(window, false);

            ClickButton(rebuiltPreviewButton);

            AssertPreviewState(window, true);
        }

        [Test]
        public void FacialExpressionEditorWindow_TargetFoldout_ShowsTargetPathAndFollowsTargetState()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var avatarRoot = renderer.transform.root.gameObject;
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var meshesObject = new GameObject("Meshes");
            meshesObject.transform.SetParent(avatarRoot.transform, false);
            var nestedRenderer = AddRenderer(meshesObject, "Face2", "Smile");
            var window = CreateEditorWindow(() => true);
            var foldout = GetPrivateField<Foldout>(window, "_targetFoldout");
            Assert.That(foldout.parent, Is.SameAs(window.rootVisualElement.Q("header-container")));
            Assert.That(foldout.Contains(GetPrivateField<ObjectField>(window, "_avatarField")), Is.True);
            Assert.That(foldout.Contains(GetPrivateField<ObjectField>(window, "_rendererField")), Is.True);
            Assert.That(foldout.Contains(GetPrivateField<ObjectField>(window, "_clipField")), Is.False, "クリップ欄はドキュメントバーに置く");
            foreach (var fieldName in new[] { "_avatarField", "_rendererField" })
            {
                // 折りたたみの中身の字下げ（15px）の分ラベルを狭くし、入力欄の開始位置をクリップ欄（ラベル 180px）と揃える。
                var labelStyle = GetPrivateField<ObjectField>(window, fieldName).labelElement.style;
                Assert.That(labelStyle.width.value.value, Is.EqualTo(165f), fieldName);
                Assert.That(labelStyle.minWidth.value.value, Is.EqualTo(145f), fieldName);
            }

            var clipLabelStyle = GetPrivateField<ObjectField>(window, "_clipField").labelElement.style;
            Assert.That(clipLabelStyle.width.value.value, Is.EqualTo(180f));
            Assert.That(clipLabelStyle.minWidth.value.value, Is.EqualTo(160f));
            AssertTargetFoldout(foldout, FacialExpressionEditorWindow.TargetFoldoutEmptyHeader, true);

            SetWindowAvatar(window, descriptor);

            AssertTargetFoldout(foldout, "Avatar/Face", false);

            // 設定の有無が変わらない間は、ユーザーが開閉した状態を保つ。
            foldout.value = true;
            InvokeWindowMethod(window, "CreateNewSession");
            InvokeWindowMethod(window, "OnHierarchyChanged");

            AssertTargetFoldout(foldout, "Avatar/Face", true);

            SetWindowRenderer(window, nestedRenderer);

            AssertTargetFoldout(foldout, "Avatar/Meshes/Face2", true);

            foldout.value = false;
            Object.DestroyImmediate(nestedRenderer.gameObject);
            InvokeWindowMethod(window, "OnHierarchyChanged");

            AssertTargetFoldout(foldout, "Avatar", true);

            SetWindowRenderer(window, renderer);

            AssertTargetFoldout(foldout, "Avatar/Face", false);

            avatarRoot.name = "Avatar_A";
            InvokeWindowMethod(window, "OnHierarchyChanged");

            AssertTargetFoldout(foldout, "Avatar_A/Face", false);

            SetWindowAvatar(window, (VRCAvatarDescriptor)null);

            AssertTargetFoldout(foldout, FacialExpressionEditorWindow.TargetFoldoutEmptyHeader, true);
        }

        [Test]
        public void FacialExpressionEditorWindow_TargetFoldout_RestoredSessionStartsCollapsed()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile");
            var window = CreateEditorWindow(() => true);
            SetWindowAvatar(window, renderer);
            GetPrivateField<Foldout>(window, "_targetFoldout").value = true;

            InvokeWindowMethod(window, "OnDisable");
            InvokeWindowMethod(window, "OnEnable");

            AssertTargetFoldout(GetPrivateField<Foldout>(window, "_targetFoldout"), "Avatar/Face", false);
        }

        [Test]
        public void FacialExpressionEditorWindow_BottomTabs_OpenOneAtATimeAndKeepListState()
        {
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink", "Angry");
            var confirmationCount = 0;
            var window = CreateEditorWindow(() =>
            {
                confirmationCount++;
                return true;
            });
            var root = window.rootVisualElement;
            var editorRoot = root.Q("editor-root");
            var splitView = GetPrivateField<TwoPaneSplitView>(window, "_bottomSplitView");
            var listPane = root.Q("list-pane");
            var bottomTabPanel = root.Q("bottom-tab-panel");
            var tabBar = root.Q("bottom-tab-bar");
            var tabButtons = new[] { "_thumbnailTabButton", "_partialImportTabButton", "_outputSettingsTabButton" }
                .Select(fieldName => GetPrivateField<Button>(window, fieldName))
                .ToArray();
            var pages = new[] { "_thumbnailTabPage", "_partialImportTabPage", "_outputSettingsTabPage" }
                .Select(fieldName => GetPrivateField<ScrollView>(window, fieldName))
                .ToArray();

            Assert.That(
                editorRoot.Children(),
                Is.EqualTo(new[] { root.Q("header-container"), splitView, tabBar }),
                "見出し部・一覧と下部タブの中身の分割ビュー・タブの見出しの順");
            Assert.That(splitView.orientation, Is.EqualTo(TwoPaneSplitViewOrientation.Vertical));
            Assert.That(splitView.fixedPaneIndex, Is.EqualTo(1), "境界のドラッグで下部タブの中身の高さを変える");
            Assert.That(splitView[0], Is.SameAs(listPane));
            Assert.That(splitView[1], Is.SameAs(bottomTabPanel));
            Assert.That(listPane.Contains(root.Q("blendshape-list-container")), Is.True);
            Assert.That(tabBar.Children(), Is.EqualTo(tabButtons));
            Assert.That(
                tabButtons.Select(button => button.Q<Label>(className: "bottom-tab__label").text),
                Is.EqualTo(new[]
                {
                    FacialExpressionEditorWindow.ThumbnailTabLabel,
                    FacialExpressionEditorWindow.PartialImportTabLabel,
                    FacialExpressionEditorWindow.OutputSettingsTabLabel,
                }));
            Assert.That(
                tabButtons.Select(button => button.tooltip),
                Is.EqualTo(new[]
                {
                    FacialExpressionEditorWindow.ThumbnailTabTooltip,
                    FacialExpressionEditorWindow.PartialImportTabTooltip,
                    FacialExpressionEditorWindow.OutputSettingsTabTooltip,
                }));
            Assert.That(pages.All(page => page.parent == bottomTabPanel), Is.True);
            Assert.That(pages[0].Contains(root.Q(className: "thumbnail-view")), Is.True);
            Assert.That(pages[1].Contains(root.Q(className: "partial-import-view")), Is.True);
            Assert.That(pages[2].Contains(GetPrivateField<VisualElement>(window, "_outputModeField")), Is.True);
            Assert.That(pages.SelectMany(page => page.Query<Foldout>().ToList()), Is.Empty, "タブの中身は折りたたみにしない");
            Assert.That(root.Q(className: "thumbnail-foldout"), Is.Null);
            Assert.That(root.Q(className: "partial-import-foldout"), Is.Null);
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.None);

            SetWindowAvatar(window, renderer);
            InvokeWindowMethod(window, "CancelPendingThumbnailCapture");
            var model = GetWindowModel(window);
            EditWindowEntry(window, "Smile", 50f);
            InvokeWindowMethod(window, "RefreshOutputDecisionUi");
            InvokeWindowMethod(window, "ToggleChangedOnlyFilter");
            var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
            var listElement = GetPrivateField<ListView>(listView, "_listView");
            Assert.That(GetFilterCountText(window), Is.EqualTo("1 / 3"));

            // 下側の高さは、高さが変わったとき（OnBottomTabPanelGeometryChanged）にだけ覚え、タブを閉じるときと OnDisable では記録しない。
            // テストではレイアウトが走らないため、ここではタブの開閉・OnDisable で覚えている高さを変えない（初期値などに戻さない）ことを確かめる。
            const float rememberedBottomPaneHeight = 400f;
            SetPrivateField(window, "_bottomPaneHeight", rememberedBottomPaneHeight);

            ClickButton(tabButtons[0]);
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.ThumbnailCapture);

            ClickButton(tabButtons[1]);
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.PartialImport);

            ClickButton(tabButtons[2]);
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.OutputSettings);

            ClickButton(tabButtons[2]);
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.None);
            Assert.That(GetPrivateField<float>(window, "_bottomPaneHeight"), Is.EqualTo(rememberedBottomPaneHeight), "タブを閉じるときは高さを記録しない");

            Assert.That(GetPrivateField<BlendShapeListView>(window, "_blendShapeListView"), Is.SameAs(listView), "タブの開閉で一覧を作り直さない");
            Assert.That(listElement.parent, Is.Not.Null);
            Assert.That(listPane.Contains(listElement), Is.True);
            Assert.That(GetWindowModel(window), Is.SameAs(model));
            Assert.That(GetEntry(model, "Smile").value, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(window.hasUnsavedChanges, Is.True);
            Assert.That(GetPrivateField<bool>(window, "_showChangedOnly"), Is.True);
            Assert.That(GetPrivateField<Button>(window, "_changedOnlyFilterChip").ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.True);
            Assert.That(GetFilterCountText(window), Is.EqualTo("1 / 3"));

            ClickButton(tabButtons[1]);
            InvokeWindowMethod(window, "CreateNewSession");

            Assert.That(confirmationCount, Is.EqualTo(1));
            Assert.That(GetPrivateField<BlendShapeListView>(window, "_blendShapeListView"), Is.Not.SameAs(listView));
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.PartialImport);

            // スクリプトのコンパイル（ドメインリロード）でも開いていたタブを戻すため、開いているタブと高さはシリアライズする。
            var instanceFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            foreach (var fieldName in new[] { "_openBottomTab", "_bottomPaneHeight" })
            {
                Assert.That(
                    typeof(FacialExpressionEditorWindow).GetField(fieldName, instanceFields).IsDefined(typeof(SerializeField), false),
                    Is.True,
                    fieldName);
            }

            InvokeWindowMethod(window, "OnDisable");
            InvokeWindowMethod(window, "OnEnable");

            Assert.That(GetPrivateField<TwoPaneSplitView>(window, "_bottomSplitView"), Is.Not.SameAs(splitView));
            AssertOpenBottomTab(window, FacialExpressionEditorWindow.BottomTab.PartialImport);
            Assert.That(GetPrivateField<float>(window, "_bottomPaneHeight"), Is.EqualTo(rememberedBottomPaneHeight), "OnDisable では高さを記録しない");
        }

        [Test]
        public void FacialExpressionEditorWindow_RootTemplateContainer_StretchesToWindowHeight()
        {
            var window = CreateEditorWindow(() => true);
            var root = window.rootVisualElement;

            // 分割ビューは一覧側を flex-basis 0 にするため、CloneTree が返す TemplateContainer が伸びないと分割ビューと一覧の高さが 0 になる。
            // -nographics ではレイアウトが走らず実際の高さを確かめられないため、TemplateContainer に flex-grow: 1 を指定していることを確かめる。
            Assert.That(root.childCount, Is.EqualTo(1));
            var templateContainer = root[0];
            Assert.That(templateContainer, Is.InstanceOf<TemplateContainer>());
            Assert.That(templateContainer.Q("editor-root"), Is.Not.Null);
            Assert.That(templateContainer.style.flexGrow.keyword, Is.EqualTo(StyleKeyword.Undefined));
            Assert.That(templateContainer.style.flexGrow.value, Is.EqualTo(1f));
        }

        [TestCase(160f, 277f, 96f, 80f, 0f, 160f)]
        [TestCase(200f, 277f, 96f, 80f, 0f, 181f)]
        [TestCase(200f, 237f, 96f, 80f, 0f, 141f)]
        [TestCase(700f, 300f, 96f, 80f, 2f, 202f)]
        [TestCase(700f, 150f, 96f, 80f, 0f, 80f)]
        [TestCase(0f, 600f, 96f, 80f, 0f, FacialExpressionEditorWindow.BottomPaneInitialHeight)]
        [TestCase(float.NaN, 600f, 96f, 80f, 0f, FacialExpressionEditorWindow.BottomPaneInitialHeight)]
        [TestCase(float.MaxValue, 277f, 96f, 80f, 0f, 181f)]
        [TestCase(float.MaxValue, 150f, 96f, 80f, 0f, 80f)]
        public void FacialExpressionEditorWindow_CalculateBottomPaneHeight_KeepsListMinimumHeight(
            float preferredHeight,
            float splitViewHeight,
            float listPaneMinHeight,
            float bottomPaneMinHeight,
            float bottomPaneMargins,
            float expectedHeight)
        {
            Assert.That(
                FacialExpressionEditorWindow.CalculateBottomPaneHeight(
                    preferredHeight,
                    splitViewHeight,
                    listPaneMinHeight,
                    bottomPaneMinHeight,
                    bottomPaneMargins),
                Is.EqualTo(expectedHeight).Within(0.0001f));
        }

        [TestCase(float.NaN)]
        [TestCase(0f)]
        public void FacialExpressionEditorWindow_CalculateBottomPaneHeight_ReturnsNaNBeforeLayout(float splitViewHeight)
        {
            Assert.That(
                FacialExpressionEditorWindow.CalculateBottomPaneHeight(160f, splitViewHeight, 96f, 80f, 0f),
                Is.NaN);
        }

        // 引数は今の高さ・覚えている高さ・今の分割ビューの高さで取れる最大の高さ・覚える高さの期待値。
        [TestCase(150f, 400f, 181f, 150f)] // ドラッグで最大より低くした高さは覚える
        [TestCase(181f, 400f, 181f, 400f)] // 最大の高さへ自動で縮めた高さでは、覚えている高さを残す
        [TestCase(180.5f, 400f, 181f, 400f)] // 最大の高さとの差が 1px 以内なら最大の高さとみなす
        [TestCase(179f, 400f, 181f, 179f)] // 最大の高さより 1px を超えて低ければ覚える
        [TestCase(181f, 160f, 181f, 181f)] // 最大の高さでも、覚えている高さより高ければ覚える
        [TestCase(400f, 160f, 600f, 400f)] // ドラッグで高くした高さは覚える
        [TestCase(float.NaN, 400f, 181f, 400f)] // 今の高さが分からなければ、覚えている高さを残す
        [TestCase(0f, 400f, 181f, 400f)]
        [TestCase(181f, float.NaN, 181f, 181f)] // 覚えている高さがなければ、今の高さを覚える
        [TestCase(181f, 0f, 181f, 181f)]
        [TestCase(181f, 400f, float.NaN, 181f)] // 最大の高さが分からなければ（レイアウト前）、今の高さを覚える
        [TestCase(181f, 400f, 0f, 181f)]
        public void FacialExpressionEditorWindow_ChooseBottomPaneHeightToRemember_KeepsDraggedHeightOverAutoShrink(
            float currentHeight,
            float rememberedHeight,
            float maxHeight,
            float expectedHeight)
        {
            Assert.That(
                FacialExpressionEditorWindow.ChooseBottomPaneHeightToRemember(currentHeight, rememberedHeight, maxHeight),
                Is.EqualTo(expectedHeight).Within(0.0001f));
        }

        // 引数は変わる前の高さ・変わった後の高さ・覚えている高さ・今の分割ビューの高さで取れる最大の高さ・覚える高さの期待値。
        [TestCase(160f, 300f, 160f, 600f, 300f)] // 境界のドラッグで変えた高さは覚える
        [TestCase(400f, 181f, 400f, 181f, 400f)] // 最大の高さへ自動で縮んだときは、覚えている高さを残す
        [TestCase(141f, 141f, 400f, 181f, 400f)] // 高さが変わらない（位置だけが変わった）ときは何もしない
        [TestCase(0f, 181f, 400f, 600f, 400f)] // 変わる前の高さが 0（最初のレイアウト・畳んだ状態から戻したとき）は何もしない
        [TestCase(float.NaN, 181f, 400f, 600f, 400f)]
        [TestCase(141f, 0f, 400f, 181f, 400f)] // 畳んで高さが 0 になったときは、覚えている高さを残す
        public void FacialExpressionEditorWindow_ChooseBottomPaneHeightOnResize_RecordsOnlyHeightChanges(
            float oldHeight,
            float newHeight,
            float rememberedHeight,
            float maxHeight,
            float expectedHeight)
        {
            Assert.That(
                FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(oldHeight, newHeight, rememberedHeight, maxHeight),
                Is.EqualTo(expectedHeight).Within(0.0001f));
        }

        /// <summary>
        /// 境界をドラッグした高さが、警告の表示で一覧の上が伸びて下側が自動で縮み、警告が消えてからタブを閉じても残ることを、
        /// 下側の高さが変わるたびの処理（<see cref="FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize"/>）の流れで確かめる。
        /// 一覧の最小高さ 96px・下側の最小高さ 80px・余白 0 とする。
        /// </summary>
        [Test]
        public void FacialExpressionEditorWindow_BottomPaneHeight_KeepsDraggedHeightAfterAutoShrinkAndClose()
        {
            var remembered = FacialExpressionEditorWindow.BottomPaneInitialHeight;

            // 広いウィンドウ（分割ビュー 696px、最大 600px）で、境界を 400px までドラッグする。
            remembered = FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(160f, 300f, remembered, 600f);
            remembered = FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(300f, 400f, remembered, 600f);
            Assert.That(remembered, Is.EqualTo(400f));

            // ウィンドウを縮める（分割ビュー 277px、最大 181px）と、分割ビューが下側を最大の高さまで自動で縮める。
            var maxHeight = FacialExpressionEditorWindow.CalculateBottomPaneHeight(float.MaxValue, 277f, 96f, 80f, 0f);
            Assert.That(maxHeight, Is.EqualTo(181f));
            remembered = FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(400f, 181f, remembered, maxHeight);
            Assert.That(remembered, Is.EqualTo(400f), "自動で縮んだ高さでは上書きしない");

            // 警告が出て一覧の上が 40px 伸びる（分割ビュー 237px、最大 141px）と、下側がさらに自動で縮む。
            maxHeight = FacialExpressionEditorWindow.CalculateBottomPaneHeight(float.MaxValue, 237f, 96f, 80f, 0f);
            Assert.That(maxHeight, Is.EqualTo(141f));
            remembered = FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(181f, 141f, remembered, maxHeight);
            Assert.That(remembered, Is.EqualTo(400f));

            // 警告が消えて最大の高さが 181px に戻っても、分割ビューは下側を広げないため、下側の高さは変わらない。
            maxHeight = FacialExpressionEditorWindow.CalculateBottomPaneHeight(float.MaxValue, 277f, 96f, 80f, 0f);
            remembered = FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(141f, 141f, remembered, maxHeight);
            Assert.That(remembered, Is.EqualTo(400f));

            // この状態でタブを閉じるときに今の高さで記録すると、141px は最大の高さではないため、ドラッグした高さを上書きしてしまう。
            // そのため閉じるときには記録しない。畳んで高さが 0 になったときの変化でも覚えている高さは変わらない。
            Assert.That(FacialExpressionEditorWindow.ChooseBottomPaneHeightToRemember(141f, remembered, maxHeight), Is.EqualTo(141f));
            remembered = FacialExpressionEditorWindow.ChooseBottomPaneHeightOnResize(141f, 0f, remembered, maxHeight);
            Assert.That(remembered, Is.EqualTo(400f));

            // 広いウィンドウでタブを開き直すと、ドラッグした高さに戻る。
            Assert.That(
                FacialExpressionEditorWindow.CalculateBottomPaneHeight(remembered, 696f, 96f, 80f, 0f),
                Is.EqualTo(400f));
        }

        [Test]
        public void FacialExpressionEditorWindow_OutputSettingsTab_ShowsDiffAndWarningsInLabel()
        {
            var folderPath = CreateTemporaryAssetFolder();
            var renderer = CreateDescriptorAvatarRenderer("Smile", "Blink");
            var plainReference = CreateClipAsset(folderPath, "PlainReference", "Smile", 40f);
            var intermediateCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 50f), new Keyframe(1f, 100f));
            var intermediateReference = CreateCurveClipAsset(folderPath, "IntermediateReference", "Smile", intermediateCurve);
            var intermediateClip = CreateCurveClipAsset(folderPath, "Intermediate", "Smile", intermediateCurve);
            var window = CreateEditorWindow(() => true);
            var tabButton = GetPrivateField<Button>(window, "_outputSettingsTabButton");
            var warningIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_outputSettingsTabWarningIcon");
            var referenceField = GetPrivateField<ObjectField>(window, "_referenceClipField");
            Assert.That(tabButton.Contains(warningIcon), Is.True);
            Assert.That(warningIcon.Icon, Is.SameAs(ExpressionEditorIcons.Warning));
            AssertOutputSettingsTab(window, "出力設定", false);
            Assert.That(IsHidden(referenceField), Is.True);

            SetWindowAvatar(window, renderer);
            var model = GetWindowModel(window);
            try
            {
                AssertOutputSettingsTab(window, "出力設定", false);
                Assert.That(IsHidden(referenceField), Is.True, "参照クリップ欄は参照クリップとの差分のときだけ出す");

                SetWindowOutputMode(window, BlendShapeOutputMode.SessionBaselineDiff);

                AssertOutputSettingsTab(window, "出力設定: 差分", false);
                Assert.That(IsHidden(referenceField), Is.True);

                SetWindowOutputMode(window, BlendShapeOutputMode.ReferenceClipDiff);

                AssertOutputSettingsTab(window, "出力設定: 差分・警告あり", true);
                Assert.That(IsHidden(referenceField), Is.False);
                Assert.That(referenceField.enabledSelf, Is.True);
                Assert.That(tabButton.tooltip, Does.Contain("参照クリップを指定してください。"), "警告の文言は tooltip でも確認できる");

                SetWindowReferenceClip(window, plainReference);

                AssertOutputSettingsTab(window, "出力設定: 差分", false);

                SetWindowReferenceClip(window, intermediateReference);

                AssertOutputSettingsTab(window, "出力設定: 差分・警告あり", true);

                // 参照クリップの中間キーの警告は、出力対象の項目だけで判定する。一括変更の Undo / Redo でも名前を合わせる。
                var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
                ExecuteHeaderMenuAction(listView, BlendShapeListView.GetBulkAllExcludedLabel(2));
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                AssertOutputSettingsTab(window, "出力設定: 差分", false);

                Undo.PerformUndo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                AssertOutputSettingsTab(window, "出力設定: 差分・警告あり", true);

                Undo.PerformRedo();
                InvokeWindowMethod(window, "OnUndoRedoPerformed");
                InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");

                AssertOutputSettingsTab(window, "出力設定: 差分", false);

                SetWindowOutputMode(window, BlendShapeOutputMode.AllTargets);

                AssertOutputSettingsTab(window, "出力設定", false);
                Assert.That(IsHidden(referenceField), Is.True);

                Undo.ClearUndo(model);
                ChangeClipField(window, intermediateClip);
                model = GetWindowModel(window);

                AssertOutputSettingsTab(window, "出力設定: 警告あり", true);

                SetWindowOutputMode(window, BlendShapeOutputMode.SessionBaselineDiff);

                AssertOutputSettingsTab(window, "出力設定: 差分・警告あり", true);

                InvokeWindowMethod(window, "CreateNewSession");
                model = GetWindowModel(window);

                AssertOutputSettingsTab(window, "出力設定", false);
                Assert.That(IsHidden(referenceField), Is.True);
            }
            finally
            {
                if (model != null)
                {
                    Undo.ClearUndo(model);
                }
            }
        }

        [TestCase(false, false, "出力設定")]
        [TestCase(true, false, "出力設定: 差分")]
        [TestCase(false, true, "出力設定: 警告あり")]
        [TestCase(true, true, "出力設定: 差分・警告あり")]
        public void FacialExpressionEditorWindow_GetOutputSettingsTabLabel_CombinesStates(
            bool isDiffOutput,
            bool hasWarning,
            string expectedLabel)
        {
            Assert.That(FacialExpressionEditorWindow.GetOutputSettingsTabLabel(isDiffOutput, hasWarning), Is.EqualTo(expectedLabel));
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

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {target.GetType().FullName}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private string CreateTemporaryAssetFolder()
        {
            var folderGuid = AssetDatabase.CreateFolder("Assets", "ExpressionClipIOTests_Temp");
            var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
            Assert.That(folderPath, Is.Not.Empty, "Temporary asset folder could not be created.");
            _assetPaths.Add(folderPath);
            return folderPath;
        }

        private static AnimationClip CreateClipAsset(string folderPath, string clipName, string blendShapeName, float value)
        {
            var clip = new AnimationClip { name = clipName };
            SetBlendShapeCurve(clip, "Face", blendShapeName, new AnimationCurve(new Keyframe(0f, value)));
            var assetPath = $"{folderPath}/{clipName}.anim";
            AssetDatabase.CreateAsset(clip, assetPath);
            var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Assert.That(savedClip, Is.Not.Null, $"Clip asset could not be created: {assetPath}");
            return savedClip;
        }

        private static AnimationClip CreateCurveClipAsset(
            string folderPath,
            string clipName,
            string blendShapeName,
            AnimationCurve curve)
        {
            var clip = new AnimationClip { name = clipName };
            SetBlendShapeCurve(clip, "Face", blendShapeName, curve);
            var assetPath = $"{folderPath}/{clipName}.anim";
            AssetDatabase.CreateAsset(clip, assetPath);
            var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Assert.That(savedClip, Is.Not.Null, $"Clip asset could not be created: {assetPath}");
            return savedClip;
        }

        private static float GetFirstBlendShapeKeyValue(AnimationClip clip, string blendShapeName)
        {
            Assert.That(clip, Is.Not.Null);
            var curve = GetBlendShapeCurve(clip, "Face", blendShapeName);
            Assert.That(curve, Is.Not.Null);
            Assert.That(curve.length, Is.GreaterThan(0));
            return curve.keys[0].value;
        }

        private SkinnedMeshRenderer CreateDescriptorAvatarRenderer(params string[] blendShapeNames)
        {
            var renderer = CreateAvatarRenderer(blendShapeNames);
            renderer.transform.root.gameObject.AddComponent<VRCAvatarDescriptor>();
            return renderer;
        }

        private SkinnedMeshRenderer AddRenderer(GameObject avatarRoot, string objectName, params string[] blendShapeNames)
        {
            var rendererObject = new GameObject(objectName);
            rendererObject.transform.SetParent(avatarRoot.transform, false);
            var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);
            _temporaryObjects.Add(renderer.sharedMesh);
            return renderer;
        }

        private FacialExpressionEditorWindow CreateEditorWindow(System.Func<bool> discardUnsavedChangesConfirmation)
        {
            var window = ScriptableObject.CreateInstance<FacialExpressionEditorWindow>();
            _temporaryObjects.Add(window);
            window.DiscardUnsavedChangesConfirmation = discardUnsavedChangesConfirmation;
            window.NotificationPresenter = _notifications.Add;
            Assert.That(GetPrivateField<ObjectField>(window, "_clipField"), Is.Not.Null, "Editor window UI was not built.");
            return window;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field was not found: {target.GetType().FullName}.{fieldName}");
            field.SetValue(target, value);
        }

        /// <summary>
        /// パネルに載っていない要素ではクリックのイベントを送れないため、ボタンに登録されたクリック時の処理を直接呼ぶ。
        /// </summary>
        private static void ClickButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, "無効なボタンはクリックできない");
            var field = typeof(Clickable).GetField(
                "clicked",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Clickable.clicked was not found.");
            var clicked = (System.Action)field.GetValue(button.clickable);
            Assert.That(clicked, Is.Not.Null, "The button has no click handler.");
            clicked();
        }

        private static bool IsHidden(VisualElement element)
        {
            return element.ClassListContains(ExpressionEditorStyles.HiddenClassName);
        }

        private static void AssertMessageBoxHidden(FacialExpressionEditorWindow window)
        {
            var messageBox = GetPrivateField<HelpBox>(window, "_messageBox");
            Assert.That(messageBox.text, Is.Empty, "成功の通知を出したら以前のメッセージは消す");
            Assert.That(IsHidden(messageBox), Is.True);
        }

        private static void AssertIntermediateKeysWarningShown(HelpBox outputSettingsMessageBox)
        {
            Assert.That(IsHidden(outputSettingsMessageBox), Is.False);
            Assert.That(outputSettingsMessageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            Assert.That(outputSettingsMessageBox.text, Does.Contain(FacialExpressionEditorWindow.IntermediateKeysWarningText));
        }

        private static void AssertIntermediateKeysWarning(HelpBox outputSettingsMessageBox, string expectedWarning)
        {
            var otherWarning = expectedWarning == FacialExpressionEditorWindow.IntermediateKeysWarningText
                ? FacialExpressionEditorWindow.IntermediateKeysSingleFrameWarningText
                : FacialExpressionEditorWindow.IntermediateKeysWarningText;
            Assert.That(IsHidden(outputSettingsMessageBox), Is.False);
            Assert.That(outputSettingsMessageBox.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            Assert.That(outputSettingsMessageBox.text, Does.Contain(expectedWarning));
            Assert.That(outputSettingsMessageBox.text, Does.Not.Contain(otherWarning));
        }

        private static string GetFilterCountText(FacialExpressionEditorWindow window)
        {
            var countLabel = GetPrivateField<Label>(window, "_filterCountLabel");
            Assert.That(IsHidden(countLabel), Is.False);
            return countLabel.text;
        }

        private static void ApplySceneInfluenceFilter(
            FacialExpressionEditorWindow window,
            params BlendShapeEntry[] entries)
        {
            var hit = new BlendShapeInfluenceHit(Vector3.zero, Vector3.up, 0, 0, 1, 2, Vector3.one / 3f);
            var results = entries
                .Select(entry => new BlendShapeInfluenceResult(entry.index, entry.name, 1f, 0.01f))
                .ToList();
            InvokeWindowMethod(window, "ApplySceneInfluenceFilter", hit, (IReadOnlyList<BlendShapeInfluenceResult>)results);
        }

        private static void ExecuteHeaderMenuAction(BlendShapeListView listView, string actionName)
        {
            var action = listView.CreateHeaderMenu()
                .MenuItems()
                .OfType<DropdownMenuAction>()
                .Single(item => item.name == actionName);
            action.Execute();
            Undo.FlushUndoRecordObjects();
        }

        private static object InvokeWindowMethod(FacialExpressionEditorWindow window, string methodName, params object[] arguments)
        {
            return GetPrivateMethod(typeof(FacialExpressionEditorWindow), methodName).Invoke(window, arguments);
        }

        private static ExpressionEditModel GetWindowModel(FacialExpressionEditorWindow window)
        {
            return GetPrivateField<ExpressionEditModel>(window, "_model");
        }

        private static void SetWindowAvatar(FacialExpressionEditorWindow window, SkinnedMeshRenderer avatarRenderer)
        {
            SetWindowAvatar(window, avatarRenderer.transform.root.GetComponent<VRCAvatarDescriptor>());
        }

        private static void SetWindowAvatar(FacialExpressionEditorWindow window, VRCAvatarDescriptor descriptor)
        {
            GetPrivateField<ObjectField>(window, "_avatarField").SetValueWithoutNotify(descriptor);
            InvokeWindowMethod(window, "SetAvatar", descriptor);
        }

        private static string GetOverwriteSaveAssetPath(FacialExpressionEditorWindow window)
        {
            return (string)InvokeWindowMethod(window, "GetOverwriteSaveAssetPath");
        }

        private static void SetWindowRenderer(FacialExpressionEditorWindow window, SkinnedMeshRenderer renderer)
        {
            GetPrivateField<ObjectField>(window, "_rendererField").SetValueWithoutNotify(renderer);
            InvokeWindowMethod(window, "SetRenderer", renderer);
        }

        /// <summary>
        /// クリップ欄を UI から変更した場合と同じく、欄の値を更新してから変更通知の処理を呼ぶ。
        /// </summary>
        private static void ChangeClipField(FacialExpressionEditorWindow window, AnimationClip clip)
        {
            GetPrivateField<ObjectField>(window, "_clipField").SetValueWithoutNotify(clip);
            InvokeWindowMethod(window, "OnClipFieldChanged", clip);
        }

        private static void EditWindowEntry(FacialExpressionEditorWindow window, string blendShapeName, float value)
        {
            var entry = GetEntry(GetWindowModel(window), blendShapeName);
            entry.value = value;
            entry.endValue = value;
            InvokeWindowMethod(window, "MarkUnsaved");
        }

        private static void AssertSaveTarget(FacialExpressionEditorWindow window, AnimationClip expectedClip, string expectedAssetPath)
        {
            Assert.That(expectedClip, Is.Not.Null);
            Assert.That(GetPrivateField<AnimationClip>(window, "_currentClip"), Is.SameAs(expectedClip));
            Assert.That(GetOverwriteSaveAssetPath(window), Is.EqualTo(expectedAssetPath));
            Assert.That(GetPrivateField<ObjectField>(window, "_clipField").value, Is.SameAs(expectedClip));
        }

        private static void AssertNoSaveTarget(FacialExpressionEditorWindow window)
        {
            Assert.That(GetPrivateField<AnimationClip>(window, "_currentClip"), Is.Null);
            Assert.That(GetOverwriteSaveAssetPath(window), Is.Empty);
            Assert.That(GetPrivateField<ObjectField>(window, "_clipField").value, Is.Null);
        }

        private static void AssertUnsavedIndicator(FacialExpressionEditorWindow window, bool expectedUnsaved)
        {
            var clipField = GetPrivateField<ObjectField>(window, "_clipField");
            var expectedLabel = expectedUnsaved
                ? FacialExpressionEditorWindow.ClipFieldLabel + FacialExpressionEditorWindow.UnsavedLabelSuffix
                : FacialExpressionEditorWindow.ClipFieldLabel;
            var expectedTooltip = expectedUnsaved ? FacialExpressionEditorWindow.UnsavedChangesTooltip : "";
            Assert.That(clipField.label, Is.EqualTo(expectedLabel));
            Assert.That(clipField.labelElement.text, Is.EqualTo(expectedLabel));
            Assert.That(clipField.labelElement.tooltip ?? "", Is.EqualTo(expectedTooltip));
        }

        private static DropdownMenuAction GetSaveAsMenuAction(FacialExpressionEditorWindow window)
        {
            var action = window.CreateSaveMenu()
                .MenuItems()
                .OfType<DropdownMenuAction>()
                .Single(item => item.name == FacialExpressionEditorWindow.SaveAsMenuItemLabel);
            action.UpdateActionStatus(null);
            return action;
        }

        private static void AssertPreviewState(FacialExpressionEditorWindow window, bool expectedEnabled)
        {
            var previewButton = GetPrivateField<Button>(window, "_previewButton");
            var previewIcon = GetPrivateField<ExpressionEditorIconElement>(window, "_previewIcon");
            Assert.That(GetPrivateField<bool>(window, "_previewEnabled"), Is.EqualTo(expectedEnabled));
            Assert.That(GetPrivateField<ExpressionPreviewService>(window, "_previewService").PreviewEnabled, Is.EqualTo(expectedEnabled));
            Assert.That(previewButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.EqualTo(expectedEnabled));
            Assert.That(
                previewButton.tooltip,
                Is.EqualTo(expectedEnabled
                    ? FacialExpressionEditorWindow.PreviewEnabledTooltip
                    : FacialExpressionEditorWindow.PreviewDisabledTooltip),
                "tooltip で今の状態とクリックしたときの操作を示す");
            Assert.That(
                previewIcon.Icon,
                Is.SameAs(expectedEnabled ? ExpressionEditorIcons.PreviewVisible : ExpressionEditorIcons.PreviewHidden));
        }

        private static void AssertTargetFoldout(Foldout foldout, string expectedHeader, bool expectedExpanded)
        {
            Assert.That(foldout.text, Is.EqualTo(expectedHeader));
            Assert.That(foldout.value, Is.EqualTo(expectedExpanded), expectedExpanded ? "開いている" : "閉じている");
        }

        /// <summary>
        /// フレームモードのセグメント切替・フレームバー・ⓘ と一覧が、<paramref name="expectedFrameMode"/> の表示になっていることを確かめる。
        /// </summary>
        private static void AssertFrameModeSegment(
            FacialExpressionEditorWindow window,
            ExpressionFrameMode expectedFrameMode,
            bool expectedEnabled)
        {
            var singleFrameButton = GetPrivateField<Button>(window, "_singleFrameModeButton");
            var weightBlendButton = GetPrivateField<Button>(window, "_weightBlendModeButton");
            var isWeightBlend = expectedFrameMode == ExpressionFrameMode.WeightBlend;
            var model = GetWindowModel(window);
            if (model != null)
            {
                Assert.That(model.frameMode, Is.EqualTo(expectedFrameMode));
            }

            Assert.That(singleFrameButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.EqualTo(!isWeightBlend), "1 フレームの選択状態");
            Assert.That(weightBlendButton.ClassListContains(ExpressionEditorStyles.SelectedClassName), Is.EqualTo(isWeightBlend), "2 フレームの選択状態");
            Assert.That(singleFrameButton.enabledSelf, Is.EqualTo(expectedEnabled), "編集中の表情が無いときは切り替えられない");
            Assert.That(weightBlendButton.enabledSelf, Is.EqualTo(expectedEnabled), "編集中の表情が無いときは切り替えられない");
            Assert.That(IsHidden(GetPrivateField<VisualElement>(window, "_frameBar")), Is.EqualTo(!isWeightBlend), "フレームバーは 2 フレームのときだけ出す");
            Assert.That(IsHidden(GetPrivateField<ExpressionEditorIconElement>(window, "_frameModeInfoIcon")), Is.EqualTo(!isWeightBlend), "ⓘ は 2 フレームのときだけ出す");

            var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
            if (listView != null)
            {
                Assert.That(GetPrivateField<ExpressionFrameMode>(listView, "_refreshedFrameMode"), Is.EqualTo(expectedFrameMode), "フレームモードを変えたら一覧を更新する");
                Assert.That(
                    GetPrivateField<ExpressionEditFrame>(listView, "_editingFrame"),
                    Is.EqualTo(GetPrivateField<ExpressionEditFrame>(window, "_editingFrame")),
                    "一覧も同じフレームを編集する");
            }
        }

        /// <summary>
        /// 2 フレームのフレームバーが表示され、編集中のフレームの◆が強調され、ウェイトプレビューが <paramref name="expectedPreviewWeight"/> であることを確かめる。
        /// </summary>
        private static void AssertFrameBar(
            FacialExpressionEditorWindow window,
            ExpressionEditFrame expectedEditingFrame,
            float expectedPreviewWeight)
        {
            var startMarker = GetPrivateField<Button>(window, "_startFrameMarker");
            var startMenuButton = GetPrivateField<Button>(window, "_startFrameMenuButton");
            var endMarker = GetPrivateField<Button>(window, "_endFrameMarker");
            var slider = GetPrivateField<Slider>(window, "_previewWeightSlider");
            Assert.That(IsHidden(GetPrivateField<VisualElement>(window, "_frameBar")), Is.False);
            Assert.That(
                new VisualElement[] { startMarker, startMenuButton, slider, endMarker }.Select(control => control.enabledSelf),
                Has.All.True,
                "2 フレームでは◆・▾・つまみを使える");
            Assert.That(GetPrivateField<ExpressionEditFrame>(window, "_editingFrame"), Is.EqualTo(expectedEditingFrame));
            Assert.That(GetPrivateField<float>(window, "_previewWeight"), Is.EqualTo(expectedPreviewWeight).Within(0.0001f));
            Assert.That(slider.value, Is.EqualTo(expectedPreviewWeight).Within(0.0001f));
            Assert.That(
                startMarker.ClassListContains(ExpressionEditorStyles.SelectedClassName),
                Is.EqualTo(expectedEditingFrame == ExpressionEditFrame.Start),
                "編集中の◆を強調する");
            Assert.That(
                endMarker.ClassListContains(ExpressionEditorStyles.SelectedClassName),
                Is.EqualTo(expectedEditingFrame == ExpressionEditFrame.End),
                "編集中の◆を強調する");
            var listView = GetPrivateField<BlendShapeListView>(window, "_blendShapeListView");
            Assert.That(GetPrivateField<ExpressionEditFrame>(listView, "_editingFrame"), Is.EqualTo(expectedEditingFrame), "一覧も同じフレームを編集する");
        }

        /// <summary>
        /// ウェイトプレビューのつまみを UI から動かした場合と同じく、値を変えてから変更時の処理を呼ぶ。
        /// パネルに載っていない要素では値の変更イベントが送られないため、登録した処理を直接呼ぶ。
        /// </summary>
        private static void ChangePreviewWeightSlider(FacialExpressionEditorWindow window, float value)
        {
            var slider = GetPrivateField<Slider>(window, "_previewWeightSlider");
            var previousValue = slider.value;
            slider.SetValueWithoutNotify(value);
            using (var evt = ChangeEvent<float>.GetPooled(previousValue, value))
            {
                InvokeWindowMethod(window, "OnPreviewWeightSliderChanged", evt);
            }
        }

        private static DropdownMenuAction GetLoadStartFromRendererMenuAction(FacialExpressionEditorWindow window)
        {
            var action = window.CreateStartFrameMenu()
                .MenuItems()
                .OfType<DropdownMenuAction>()
                .Single(item => item.name == FacialExpressionEditorWindow.LoadStartFromRendererMenuItemLabel);
            action.UpdateActionStatus(null);
            return action;
        }

        private static void AssertOpenBottomTab(
            FacialExpressionEditorWindow window,
            FacialExpressionEditorWindow.BottomTab expectedTab)
        {
            Assert.That(GetPrivateField<FacialExpressionEditorWindow.BottomTab>(window, "_openBottomTab"), Is.EqualTo(expectedTab));
            foreach (var (tab, buttonFieldName, pageFieldName) in new[]
            {
                (FacialExpressionEditorWindow.BottomTab.ThumbnailCapture, "_thumbnailTabButton", "_thumbnailTabPage"),
                (FacialExpressionEditorWindow.BottomTab.PartialImport, "_partialImportTabButton", "_partialImportTabPage"),
                (FacialExpressionEditorWindow.BottomTab.OutputSettings, "_outputSettingsTabButton", "_outputSettingsTabPage"),
            })
            {
                var isOpen = tab == expectedTab;
                Assert.That(
                    GetPrivateField<Button>(window, buttonFieldName).ClassListContains(ExpressionEditorStyles.SelectedClassName),
                    Is.EqualTo(isOpen),
                    buttonFieldName);
                Assert.That(IsHidden(GetPrivateField<ScrollView>(window, pageFieldName)), Is.EqualTo(!isOpen), pageFieldName);
            }
        }

        private static void AssertOutputSettingsTab(
            FacialExpressionEditorWindow window,
            string expectedLabel,
            bool expectedWarning)
        {
            var tabButton = GetPrivateField<Button>(window, "_outputSettingsTabButton");
            Assert.That(GetPrivateField<Label>(window, "_outputSettingsTabLabel").text, Is.EqualTo(expectedLabel));
            Assert.That(IsHidden(GetPrivateField<ExpressionEditorIconElement>(window, "_outputSettingsTabWarningIcon")), Is.EqualTo(!expectedWarning));
            Assert.That(IsHidden(GetPrivateField<HelpBox>(window, "_outputSettingsMessageBox")), Is.EqualTo(!expectedWarning), "タブ名の警告は出力設定欄の警告と一致させる");
            if (expectedWarning)
            {
                Assert.That(tabButton.tooltip, Does.StartWith(FacialExpressionEditorWindow.OutputSettingsTabTooltip + "\n\n"));
            }
            else
            {
                Assert.That(tabButton.tooltip, Is.EqualTo(FacialExpressionEditorWindow.OutputSettingsTabTooltip));
            }
        }

        /// <summary>
        /// 出力範囲の欄を UI から変更した場合と同じく、値を設定してから変更時の処理を呼び、エディターの更新時の再計算まで進める。
        /// </summary>
        private static void SetWindowOutputMode(FacialExpressionEditorWindow window, BlendShapeOutputMode outputMode)
        {
            SetPrivateField(window, "_outputMode", outputMode);
            InvokeWindowMethod(window, "OnOutputSettingsChanged");
            InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");
        }

        /// <summary>
        /// 参照クリップ欄を UI から変更した場合と同じく、値を設定してから変更時の処理を呼び、エディターの更新時の再計算まで進める。
        /// </summary>
        private static void SetWindowReferenceClip(FacialExpressionEditorWindow window, AnimationClip referenceClip)
        {
            SetPrivateField(window, "_referenceClip", referenceClip);
            InvokeWindowMethod(window, "OnOutputSettingsChanged");
            InvokeWindowMethod(window, "FlushOutputDecisionUiRefreshIfReady");
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
