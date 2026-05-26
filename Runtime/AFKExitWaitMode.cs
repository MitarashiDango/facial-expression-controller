using UnityEngine;

namespace MitarashiDango.FacialExpressionController
{
    public enum AFKExitWaitMode
    {
        [InspectorName("待機なし")]
        None,

        [InspectorName("指定時間待機")]
        Duration,

        [InspectorName("パラメーターによる制御")]
        Parameter,
    }
}
