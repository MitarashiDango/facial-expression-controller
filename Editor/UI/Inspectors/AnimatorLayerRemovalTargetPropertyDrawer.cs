using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.FacialExpressionController.Editor
{
    [CustomPropertyDrawer(typeof(AnimatorLayerRemovalTarget))]
    public class AnimatorLayerRemovalTargetPropertyDrawer : PropertyDrawer
    {
        private static readonly VRCAvatarDescriptor.AnimLayerType[] SelectableLayerTypes =
        {
            VRCAvatarDescriptor.AnimLayerType.Base,
            VRCAvatarDescriptor.AnimLayerType.Additive,
            VRCAvatarDescriptor.AnimLayerType.Gesture,
            VRCAvatarDescriptor.AnimLayerType.Action,
            VRCAvatarDescriptor.AnimLayerType.FX,
            VRCAvatarDescriptor.AnimLayerType.Sitting,
            VRCAvatarDescriptor.AnimLayerType.TPose,
            VRCAvatarDescriptor.AnimLayerType.IKPose
        };

        private static readonly GUIContent[] SelectableLayerTypeLabels =
            SelectableLayerTypes.Select(layerType => new GUIContent(layerType.ToString())).ToArray();

        private struct LayerNameCandidate
        {
            public string Name;
            public string Source;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var layerTypeProperty = property.FindPropertyRelative("layerType");
                var layerNameProperty = property.FindPropertyRelative("layerName");

                var layerTypeRect = new Rect(position)
                {
                    height = EditorGUIUtility.singleLineHeight
                };
                PutLayerTypePopup(layerTypeRect, layerTypeProperty);

                var candidates = GetLayerNameCandidates(GetGameObject(property), GetLayerType(layerTypeProperty));
                var popupContents = new List<GUIContent>
                {
                    new GUIContent("候補から選択")
                };
                popupContents.AddRange(candidates.Select(candidate =>
                    new GUIContent($"{candidate.Name} ({candidate.Source})", candidate.Source)));

                var popupRect = GetNextLineRect(layerTypeRect);
                var previousValue = 0;
                var selectedCandidateIndex = candidates.FindIndex(candidate => candidate.Name == layerNameProperty.stringValue);
                if (selectedCandidateIndex >= 0)
                {
                    previousValue = selectedCandidateIndex + 1;
                }

                var changedValue = EditorGUI.Popup(popupRect, new GUIContent("レイヤー名候補"), previousValue, popupContents.ToArray());
                if (changedValue != previousValue && changedValue > 0)
                {
                    layerNameProperty.stringValue = candidates[changedValue - 1].Name;
                }

                var layerNameRect = GetNextLineRect(popupRect);
                EditorGUI.PropertyField(layerNameRect, layerNameProperty, new GUIContent("レイヤー名"));
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
        }

        private static Rect GetNextLineRect(Rect previous)
        {
            return new Rect(previous)
            {
                y = previous.y + previous.height + EditorGUIUtility.standardVerticalSpacing,
                height = EditorGUIUtility.singleLineHeight
            };
        }

        private static VRCAvatarDescriptor.AnimLayerType GetLayerType(SerializedProperty property)
        {
            return (VRCAvatarDescriptor.AnimLayerType)property.intValue;
        }

        private static void PutLayerTypePopup(Rect position, SerializedProperty property)
        {
            var layerType = GetLayerType(property);
            var selectedIndex = System.Array.IndexOf(SelectableLayerTypes, layerType);
            if (selectedIndex < 0)
            {
                selectedIndex = System.Array.IndexOf(SelectableLayerTypes, VRCAvatarDescriptor.AnimLayerType.FX);
                property.intValue = (int)VRCAvatarDescriptor.AnimLayerType.FX;
            }

            var changedIndex = EditorGUI.Popup(position, new GUIContent("レイヤー種別"), selectedIndex, SelectableLayerTypeLabels);
            if (changedIndex != selectedIndex)
            {
                property.intValue = (int)SelectableLayerTypes[changedIndex];
            }
        }

        private static GameObject GetGameObject(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is Component component)
            {
                return component.gameObject;
            }

            return null;
        }

        private static List<LayerNameCandidate> GetLayerNameCandidates(GameObject gameObject, VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var avatarRoot = gameObject != null ? MiscUtil.GetAvatarRoot(gameObject.transform) : null;
            if (avatarRoot == null)
            {
                return new List<LayerNameCandidate>();
            }

            var candidates = new List<LayerNameCandidate>();
            var avatarDescriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor != null)
            {
                candidates.AddRange(GetAvatarLayerNameCandidates(avatarDescriptor, layerType));
            }

            return candidates
                .GroupBy(candidate => new { candidate.Name, candidate.Source })
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<LayerNameCandidate> GetAvatarLayerNameCandidates(VRCAvatarDescriptor avatarDescriptor, VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var customLayer = GetCustomAnimLayer(avatarDescriptor.baseAnimationLayers, layerType)
                ?? GetCustomAnimLayer(avatarDescriptor.specialAnimationLayers, layerType);

            if (!customLayer.HasValue || customLayer.Value.isDefault || customLayer.Value.animatorController == null)
            {
                return Enumerable.Empty<LayerNameCandidate>();
            }

            return GetLayerNames(customLayer.Value.animatorController)
                .Select(layerName => new LayerNameCandidate
                {
                    Name = layerName,
                    Source = $"Avatar {layerType}"
                });
        }

        private static VRCAvatarDescriptor.CustomAnimLayer? GetCustomAnimLayer(
            VRCAvatarDescriptor.CustomAnimLayer[] customAnimLayers,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            return customAnimLayers?
                .Where(layer => layer.type == layerType)
                .Cast<VRCAvatarDescriptor.CustomAnimLayer?>()
                .FirstOrDefault();
        }

        private static IEnumerable<string> GetLayerNames(RuntimeAnimatorController animatorController)
        {
            switch (animatorController)
            {
                case AnimatorController controller:
                    return controller.layers.Select(layer => layer.name);
                case AnimatorOverrideController overrideController:
                    return overrideController.runtimeAnimatorController != null
                        ? GetLayerNames(overrideController.runtimeAnimatorController)
                        : Enumerable.Empty<string>();
                default:
                    return Enumerable.Empty<string>();
            }
        }
    }
}
