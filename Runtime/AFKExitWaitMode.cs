using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    public enum AFKExitWaitMode
    {
        [InspectorName("待機なし")]
        None,

        [InspectorName("指定時間待機")]
        Duration,

        // TODO そのうち実装する
        //[InspectorName("パラメーターによる制御")]
        //Parameter,
    }
}