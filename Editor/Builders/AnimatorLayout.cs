using UnityEngine;

namespace MitarashiDango.FacialExpressionController.Editor.Builders
{
    /// <summary>
    /// Animator ステートマシン上でのステート配置に関する定数
    /// </summary>
    public static class AnimatorLayout
    {
        /// <summary>
        /// ステート 1 行あたりの Y 座標間隔
        /// </summary>
        public const float RowSpacing = 80f;

        /// <summary>
        /// ステート 1 列あたりの X 座標間隔
        /// </summary>
        public const float ColumnSpacing = 200f;

        /// <summary>
        /// ステートマシンの Entry ノードの既定位置
        /// </summary>
        public static readonly Vector3 DefaultEntryPosition = new Vector3(0, 0, 0);

        /// <summary>
        /// ステートマシンの Exit ノードの既定位置
        /// </summary>
        public static readonly Vector3 DefaultExitPosition = new Vector3(0, -40, 0);

        /// <summary>
        /// ステートマシンの Any State ノードの既定位置
        /// </summary>
        public static readonly Vector3 DefaultAnyStatePosition = new Vector3(0, -RowSpacing, 0);
    }
}
