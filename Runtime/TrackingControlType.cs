
using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    public enum TrackingControlType
    {
        [InspectorName("トラッキング値を優先する")]
        Tracking,

        [InspectorName("アニメーション値を優先する")]
        Animation,

        [InspectorName("設定値を変更しない")]
        NoChange,
    }
}