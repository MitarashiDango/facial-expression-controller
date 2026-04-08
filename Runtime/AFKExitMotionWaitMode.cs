using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    public enum AFKExitMotionWaitMode
    {
        [InspectorName("待機なし")]
        None,

        [InspectorName("指定時間待機")]
        Duration,

        [InspectorName("パラメーターによる制御")]
        Parameter,
    }
}
