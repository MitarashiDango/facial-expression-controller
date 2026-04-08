using System;

namespace MitarashiDango.FacialExpressionController.Runtime
{
    [Serializable]
    public class AFKExitMotionWaitParameterCondition
    {
        /// <summary>
        /// 条件判定対象のパラメーター名
        /// </summary>
        public string parameterName = "";

        /// <summary>
        /// 条件の比較方法
        /// </summary>
        public AnimatorConditionCompareMode compareMode = AnimatorConditionCompareMode.If;

        /// <summary>
        /// 閾値 (Greater/Less/Equals/NotEqualの場合のみ使用)
        /// </summary>
        public float threshold = 0;
    }
}
