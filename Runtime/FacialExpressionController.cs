using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace MitarashiDango.FacialExpressionController
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Facial Expression Controller/Facial Expression Controller")]
    public class FacialExpressionController : MonoBehaviour, IEditorOnly
    {
        /// <summary>
        /// 既定の表情をブレンドシェイプから自動生成するかどうか (true: する, false: しない)
        /// </summary>
        public bool generateDefaultFacialExpressionAnimation;

        /// <summary>
        /// 既定の表情<br />
        /// 既定の表情をブレンドシェイプから自動生成する設定の場合、無視される
        /// </summary>
        public Motion defaultFacialExpressionMotion;

        /// <summary>
        /// 表情切り替え時間
        /// </summary>
        public float transitionDuration = 0.1f;

        public List<FacialExpressionGesturePreset> facialExpressionGesturePresets = new List<FacialExpressionGesturePreset>();

        /// <summary>
        /// 左手に割り当てる初期ジェスチャープリセット番号 (1 = 先頭のプリセット)
        /// </summary>
        [Min(1)]
        public int defaultLeftGesturePresetNumber = 1;

        /// <summary>
        /// 右手に割り当てる初期ジェスチャープリセット番号 (1 = 先頭のプリセット)
        /// </summary>
        [Min(1)]
        public int defaultRightGesturePresetNumber = 1;

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
        public AFKExitWaitMode afkExitWaitMode = AFKExitWaitMode.None;

        /// <summary>
        /// AFK終了モーション待機時間 (待機方法が指定時間待機の場合のみ有効)
        /// </summary>
        public float afkExitWaitDuration = 0;

        /// <summary>
        /// AFK終了モーション待機パラメーター条件 (待機方法がパラメーターによる制御の場合のみ有効)
        /// </summary>
        public List<AFKExitWaitParameterCondition> afkExitWaitConditions = new List<AFKExitWaitParameterCondition>();

        /// <summary>
        /// アニメーションレイヤー削除機能を利用するかどうか (true: する, false: しない)
        /// </summary>
        public bool removeExistingFacialExpressionLayers = false;

        /// <summary>
        /// 削除対象レイヤー情報
        /// </summary>
        public List<AnimatorLayerRemovalTarget> layerRemovalTargets = new List<AnimatorLayerRemovalTarget>();
    }
}
