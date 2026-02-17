using MitarashiDango.FacialExpressionController.Runtime;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    [CustomEditor(typeof(FacialExpressionControl))]
    public class FacialExpressionControlEditor : UnityEditor.Editor
    {
        private static string _mainUxmlGuid = "276a4def4ec44c640b707bc26454c4c5";

        public override VisualElement CreateInspectorGUI()
        {
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"Cannot load UXML file: {_mainUxmlGuid}");
                return null;
            }

            var root = mainUxmlAsset.CloneTree();

            LanguagePrefs.ApplyFontPreferences(root);

            return root;
        }
    }
}