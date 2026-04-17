namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// <see cref="SyncParameters.CurrentFacialExpressionNumber"/> の採番ルールに関する定数
    /// </summary>
    public static class FacialExpressionNumbering
    {
        /// <summary>
        /// 1 つのサブステートマシンにまとめる表情ステートの最大数 (視覚グルーピング用)
        /// </summary>
        public const int StateGroupSize = 10;

        /// <summary>
        /// CurrentFacialExpressionNumber の最大値 (VRC Expression Parameters の Int 上限)
        /// </summary>
        public const int MaxNumber = 255;

        /// <summary>
        /// ジェスチャープリセット 1 つあたりに割り当てられる表情数
        /// </summary>
        public static int GestureCountPerPreset => GestureConstants.Gestures.Length;
    }
}
