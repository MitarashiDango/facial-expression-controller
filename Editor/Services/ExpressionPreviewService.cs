using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// 表情プレビューの反映・復元を管理する。
    /// </summary>
    public sealed class ExpressionPreviewService : IDisposable
    {
        private const float PreviewValueTolerance = 0.0001f;
        private bool _previewEnabled = true;
        private bool _disposed;
        private bool _externalAnimationModeWarningShown;
        private GameObject _currentAvatarRootObject;
        private SkinnedMeshRenderer _currentTargetRenderer;
        private readonly Dictionary<int, float> _originalWeights = new Dictionary<int, float>();

        public ExpressionPreviewService()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += GuardDestroyedTargets;
        }

        public bool PreviewEnabled
        {
            get => _previewEnabled;
            set
            {
                _previewEnabled = value;
                if (!_previewEnabled)
                {
                    Stop();
                }
            }
        }

        /// <summary>
        /// 編集モデルの出力対象ブレンドシェイプをシーン上のアバターへ反映する。
        /// </summary>
        public void Sample(ExpressionEditModel model)
        {
            if (_disposed || !_previewEnabled || model == null)
            {
                return;
            }

            if (model.avatarRootObject == null || model.targetRenderer == null)
            {
                Stop();
                return;
            }

            if (ShouldSkipForExternalAnimationMode())
            {
                return;
            }

            if (!EnsurePreviewTarget(model))
            {
                return;
            }

            foreach (var entry in model.entries)
            {
                if (entry.ShouldOutput && HasPreviewValue(entry))
                {
                    ApplyEntryWeight(entry);
                }
                else
                {
                    RestoreEntryWeight(entry);
                }
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// 変更されたブレンドシェイプだけを即時反映する。
        /// </summary>
        public void SampleEntry(ExpressionEditModel model, BlendShapeEntry entry)
        {
            if (_disposed || !_previewEnabled || model == null || entry == null)
            {
                return;
            }

            if (model.avatarRootObject == null || model.targetRenderer == null)
            {
                Stop();
                return;
            }

            if (ShouldSkipForExternalAnimationMode())
            {
                return;
            }

            if (!EnsurePreviewTarget(model))
            {
                return;
            }

            if (entry.ShouldOutput && HasPreviewValue(entry))
            {
                ApplyEntryWeight(entry);
            }
            else
            {
                RestoreEntryWeight(entry);
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// プレビュー前のブレンドシェイプ値へ復元する。
        /// </summary>
        public void Stop()
        {
            RestoreAllWeights();
            _currentAvatarRootObject = null;
            _currentTargetRenderer = null;
            _externalAnimationModeWarningShown = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= GuardDestroyedTargets;
            Stop();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                Stop();
            }
        }

        private void GuardDestroyedTargets()
        {
            if (_currentAvatarRootObject != null && _currentTargetRenderer != null)
            {
                return;
            }

            if (_originalWeights.Count > 0)
            {
                Stop();
            }
        }

        private bool EnsurePreviewTarget(ExpressionEditModel model)
        {
            if (_currentAvatarRootObject == model.avatarRootObject
                && _currentTargetRenderer == model.targetRenderer)
            {
                return true;
            }

            Stop();
            _currentAvatarRootObject = model.avatarRootObject;
            _currentTargetRenderer = model.targetRenderer;
            return _currentTargetRenderer != null;
        }

        private bool ShouldSkipForExternalAnimationMode()
        {
            if (!AnimationMode.InAnimationMode())
            {
                _externalAnimationModeWarningShown = false;
                return false;
            }

            if (_originalWeights.Count > 0)
            {
                RestoreAllWeights();
            }

            if (!_externalAnimationModeWarningShown)
            {
                Debug.LogWarning("[FacialExpressionController] Facial expression preview was skipped because another editor tool is using AnimationMode.");
                _externalAnimationModeWarningShown = true;
            }

            return true;
        }

        private void ApplyEntryWeight(BlendShapeEntry entry)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }

            if (!_originalWeights.ContainsKey(entry.index))
            {
                _originalWeights.Add(entry.index, _currentTargetRenderer.GetBlendShapeWeight(entry.index));
            }

            _currentTargetRenderer.SetBlendShapeWeight(entry.index, entry.value);
        }

        private void RestoreEntryWeight(BlendShapeEntry entry)
        {
            if (!IsValidEntry(entry) || !_originalWeights.TryGetValue(entry.index, out var originalWeight))
            {
                return;
            }

            _currentTargetRenderer.SetBlendShapeWeight(entry.index, originalWeight);
            _originalWeights.Remove(entry.index);
        }

        private void RestoreAllWeights()
        {
            if (_currentTargetRenderer != null && _currentTargetRenderer.sharedMesh != null)
            {
                foreach (var originalWeight in _originalWeights)
                {
                    if (originalWeight.Key >= 0 && originalWeight.Key < _currentTargetRenderer.sharedMesh.blendShapeCount)
                    {
                        _currentTargetRenderer.SetBlendShapeWeight(originalWeight.Key, originalWeight.Value);
                    }
                }

                SceneView.RepaintAll();
            }

            _originalWeights.Clear();
        }

        private bool IsValidEntry(BlendShapeEntry entry)
        {
            return _currentTargetRenderer != null
                && _currentTargetRenderer.sharedMesh != null
                && entry.index >= 0
                && entry.index < _currentTargetRenderer.sharedMesh.blendShapeCount;
        }

        private bool HasPreviewValue(BlendShapeEntry entry)
        {
            return _originalWeights.ContainsKey(entry.index)
                || Mathf.Abs(entry.value - entry.initialValue) > PreviewValueTolerance;
        }
    }
}
