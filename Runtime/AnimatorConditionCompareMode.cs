using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    public enum AnimatorConditionCompareMode
    {
        [InspectorName("trueの場合")]
        If,

        [InspectorName("falseの場合")]
        IfNot,

        [InspectorName("より大きい")]
        Greater,

        [InspectorName("より小さい")]
        Less,

        [InspectorName("等しい")]
        Equals,

        [InspectorName("等しくない")]
        NotEqual,
    }
}
