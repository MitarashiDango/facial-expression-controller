using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Dynamics.Contact.Components;
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

        public List<FacialExpressionGesturePreset> facialExpressionGesturePresets = new List<FacialExpressionGesturePreset>();

        public List<FacialExpressionGroup> facialExpressionGroups = new List<FacialExpressionGroup>();

        /// <summary>
        /// 表情ロック用のContact Receiver
        /// </summary>
        public VRCContactReceiver facialExpressionLockContactReceiver;

        /// <summary>
        /// AFKモードを使用するかどうか (true: する, false: しない)
        /// </summary>
        public bool useAFKMode = true;

        /// <summary>
        /// AFK終了モーション待機方法 (AFKモードを使用する場合のみ有効)
        /// </summary>
        public AFKExitMotionWaitMode afkExitMotionWaitMode = AFKExitMotionWaitMode.None;

        /// <summary>
        /// AFK終了モーション待機時間 (待機方法が指定時間待機の場合のみ有効)
        /// </summary>
        public float afkExitMotionWaitDuration = 0;

        /// <summary>
        /// AFK終了モーション待機パラメーター条件 (待機方法がパラメーターによる制御の場合のみ有効)
        /// </summary>
        public List<AFKExitMotionWaitParameterCondition> afkExitMotionWaitParameterConditions = new List<AFKExitMotionWaitParameterCondition>();
    }
}
