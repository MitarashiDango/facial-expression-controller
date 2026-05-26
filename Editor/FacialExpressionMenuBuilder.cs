using System;
using System.Collections.Generic;
using System.Linq;
using MitarashiDango.FacialExpressionController;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace MitarashiDango.FacialExpressionController.Editor
{
    public class FacialExpressionMenuBuilder
    {
        public void BuildMenuTree(FacialExpressionController fec, FacialExpressionMenuRoot fecMenu)
        {
            var menuRootObject = fecMenu.gameObject;

            var maMenuItem = menuRootObject.AddComponent<ModularAvatarMenuItem>();
            maMenuItem.MenuSource = SubmenuSource.Children;
            maMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;

            var facialExpressionControllerEnabledToggle = CreateFacialExpressionControllerEnabledToggle();
            facialExpressionControllerEnabledToggle.transform.SetParent(menuRootObject.transform);

            var facialLockOnToggle = CreateFacialLockOnToggle();
            facialLockOnToggle.transform.SetParent(menuRootObject.transform);

            var adjustFacialWeightRadialPuppet = CreateAdjustFacialWeightRadialPuppet();
            adjustFacialWeightRadialPuppet.transform.SetParent(menuRootObject.transform);

            var selectFacialExpressionSubMenu = CreateSelectFacialExpressionSubMenu(fec);
            selectFacialExpressionSubMenu.transform.SetParent(menuRootObject.transform);

            var configSubMenu = CreateConfigSubMenu(fec);
            configSubMenu.transform.SetParent(menuRootObject.transform);
        }

        private GameObject CreateFacialExpressionControllerEnabledToggle()
        {
            var go = new GameObject("表情制御");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.FacialExpressionControllerEnabled.name,
                },
                value = 1,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateFacialLockOnToggle()
        {
            var go = new GameObject("表情ロック");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.FacialExpressionLocked.name,
                },
                value = 1,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateAdjustFacialWeightRadialPuppet()
        {
            var go = new GameObject("表情固定時の表情ウェイト");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new VRCExpressionsMenu.Control.Parameter[]
                {
                    new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = SyncParameters.LockedFacialExpressionWeight.name,
                    },
                },
                value = 1.0f,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateConfigSubMenu(FacialExpressionController fec)
        {
            var subMenu = CreateSubMenu("設定", null);

            var selectGesturePrioritySubMenu = CreateSelectGesturePrioritySubMenu();
            selectGesturePrioritySubMenu.transform.SetParent(subMenu.transform);

            var contactLockOnToggle = CreateContactLockOnToggle();
            contactLockOnToggle.transform.SetParent(subMenu.transform);

            var switchToDanceModeOnToggle = CreateSwitchToDanceModeOnToggle();
            switchToDanceModeOnToggle.transform.SetParent(subMenu.transform);

            var switchToVehicleModeOnToggle = CreateSwitchToVehicleModeOnToggle();
            switchToVehicleModeOnToggle.transform.SetParent(subMenu.transform);

            var selectGesturePresetSubMenu = CreateSelectGesturePresetSubMenu(fec);
            selectGesturePresetSubMenu.transform.SetParent(subMenu.transform);

            return subMenu;
        }

        private GameObject CreateContactLockOnToggle()
        {
            var go = new GameObject("接触ロック");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.ContactLockEnabled.name,
                },
                value = 1,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateSwitchToDanceModeOnToggle()
        {
            var go = new GameObject("ダンスモード自動切り替え");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.DanceModeAutoSwitchEnabled.name,
                },
                value = 1,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateSwitchToVehicleModeOnToggle()
        {
            var go = new GameObject("乗り物モード自動切り替え");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.VehicleModeAutoSwitchEnabled.name,
                },
                value = 1,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateSelectGesturePresetSubMenu(FacialExpressionController fec)
        {
            var subMenu = CreateSubMenu("ジェスチャープリセット割り当て", null);

            var selectLeftHandGesturePresetSubMenu = CreateSelectLeftHandGesturePresetSubMenu(fec);
            selectLeftHandGesturePresetSubMenu.transform.SetParent(subMenu.transform);

            var selectRightHandGesturePresetSubMenu = CreateSelectRightHandGesturePresetSubMenu(fec);
            selectRightHandGesturePresetSubMenu.transform.SetParent(subMenu.transform);

            return subMenu;
        }

        private GameObject CreateSelectLeftHandGesturePresetSubMenu(FacialExpressionController fec)
        {
            var subMenu = CreateSubMenu("左手", null);

            foreach (var (gesturePreset, i) in fec.facialExpressionGesturePresets.Select((v, i) => (v, i)).Where(x => x.v != null))
            {
                var go = CreateSelectGesturePresetToggle(gesturePreset, i, InternalParameters.SelectedLeftGesturePreset.name);
                go.transform.SetParent(subMenu.transform);
            }

            return subMenu;
        }

        private GameObject CreateSelectRightHandGesturePresetSubMenu(FacialExpressionController fec)
        {
            var subMenu = CreateSubMenu("右手", null);

            foreach (var (gesturePreset, i) in fec.facialExpressionGesturePresets.Select((v, i) => (v, i)).Where(x => x.v != null))
            {
                var go = CreateSelectGesturePresetToggle(gesturePreset, i, InternalParameters.SelectedRightGesturePreset.name);
                go.transform.SetParent(subMenu.transform);
            }

            return subMenu;
        }

        private GameObject CreateSelectGesturePresetToggle(FacialExpressionGesturePreset gesturePreset, int presetNumber, string parameterName)
        {
            var presetName = string.IsNullOrEmpty(gesturePreset.presetName) ? $"プリセット {(presetNumber + 1).ToString()}" : gesturePreset.presetName;
            var go = new GameObject(presetName);

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();

            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = parameterName,
                },
                value = presetNumber,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateSelectGesturePrioritySubMenu()
        {
            var subMenu = CreateSubMenu("ジェスチャー優先設定", null);

            var priorityTypes = new List<Tuple<int, string>>()
            {
                new Tuple<int, string>(GesturePriorityType.FirstWin, "先勝ち"),
                new Tuple<int, string>(GesturePriorityType.LastWin, "後勝ち"),
                new Tuple<int, string>(GesturePriorityType.LeftHandPriority, "左手優先"),
                new Tuple<int, string>(GesturePriorityType.RightHandPriority, "右手優先"),
            };

            foreach (var priorityType in priorityTypes)
            {
                var go = new GameObject(priorityType.Item2);
                go.transform.SetParent(subMenu.transform);

                var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();

                var control = new VRCExpressionsMenu.Control()
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter()
                    {
                        name = InternalParameters.GesturePriority.name,
                    },
                    value = priorityType.Item1,
                };

                maMenuItem.Control = control;
            }

            return subMenu;
        }

        private GameObject CreateSelectFacialExpressionSubMenu(FacialExpressionController fec)
        {
            var subMenu = CreateSubMenu("表情選択", null);

            var noFacialExpressionSelectionToggle = CreateNoFacialExpressionSelectionToggle();
            noFacialExpressionSelectionToggle.transform.SetParent(subMenu.transform);

            var facialExpressionNumber = 1;
            foreach (var (facialExpressionGroup, i) in FacialExpressionBuildUtil.GetValidGroups(fec).Select((v, i) => (v, i)))
            {
                var groupName = string.IsNullOrEmpty(facialExpressionGroup.groupName) ? $"グループ {(i + 1).ToString()}" : facialExpressionGroup.groupName;

                var facialExpressionGroupSubMenu = CreateSubMenu(groupName, null);
                facialExpressionGroupSubMenu.transform.SetParent(subMenu.transform);

                foreach (var facialExpression in facialExpressionGroup.facialExpressions)
                {
                    var selectFacialExpressionToggle = CreateSelectFacialExpressionToggle(facialExpression, facialExpressionNumber++);
                    selectFacialExpressionToggle.transform.SetParent(facialExpressionGroupSubMenu.transform);
                }
            }

            return subMenu;
        }

        private GameObject CreateNoFacialExpressionSelectionToggle()
        {
            var go = new GameObject("表情選択なし (ジェスチャー優先)");

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.SelectedFacialExpressionInMenu.name,
                },
                value = 0,
            };

            maMenuItem.Control = control;

            return go;
        }

        private GameObject CreateSelectFacialExpressionToggle(FacialExpression facialExpression, int facialExpressionNumber)
        {
            var presetName = GetFacialExpressionName(facialExpression);
            if (string.IsNullOrEmpty(presetName))
            {
                presetName = $"表情 {facialExpressionNumber}";
            }

            var go = new GameObject(presetName);

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();

            var control = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = InternalParameters.SelectedFacialExpressionInMenu.name,
                },
                value = facialExpressionNumber,
            };

            if (facialExpression.menuIcon != null)
            {
                control.icon = facialExpression.menuIcon;
            }

            maMenuItem.Control = control;

            return go;
        }

        private string GetFacialExpressionName(FacialExpression facialExpression)
        {
            if (!string.IsNullOrEmpty(facialExpression.facialExpressionName))
            {
                return facialExpression.facialExpressionName;
            }

            if (facialExpression.motion != null && !string.IsNullOrEmpty(facialExpression.motion.name))
            {
                return facialExpression.motion.name;
            }

            return "";
        }

        private GameObject CreateSubMenu(string name, Texture2D icon)
        {
            var go = new GameObject(name);

            var maMenuItem = go.AddComponent<ModularAvatarMenuItem>();
            maMenuItem.MenuSource = SubmenuSource.Children;
            maMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            if (icon != null)
            {
                maMenuItem.Control.icon = icon;
            }

            return go;
        }
    }
}
