using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Facial Expression Controller/Facial Expression Control")]
    public class FacialExpressionControl : MonoBehaviour, IEditorOnly
    {
        /// <summary>
        /// 既定の表情をブレンドシェイプから自動生成するかどうか (true: する, false: しない)
        /// </summary>
        public bool generateDefaultFacialAnimation;

        /// <summary>
        /// 既定の表情<br />
        /// 既定の表情をブレンドシェイプから自動生成する設定の場合、無視される
        /// </summary>
        public Motion defaultFace;

        /// <summary>
        /// 表情切り替え時間
        /// </summary>
        public float transitionTime = 0.1f;

        public List<FacialExpressionGesturePreset> facialExpressionGesturePresets;

        public List<FacialExpressionGroup> facialExpressionGroups;

        /// <summary>
        /// AFKモードを使用するかどうか (true: する, false: しない)
        /// </summary>
        public bool useAFKMode = true;

        /// <summary>
        /// AFK終了後の待機モード
        /// </summary>
        public AFKExitWaitMode afkExitWaitMode = AFKExitWaitMode.None;

        /// <summary>
        /// 待機時間
        /// </summary>
        public float waitAFKExitDurationTime = 0;
    }
}
