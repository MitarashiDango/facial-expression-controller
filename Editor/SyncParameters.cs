namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// 同期パラメーター
    /// </summary>
    public class SyncParameters
    {
        /// <summary>
        /// 現在の動作モード
        /// </summary>
        /// <value>
        /// 取りうる値は <see cref="FacialExpressionModeType"/> を参照<br />
        /// 0 = Facial expression controller inactive<br />
        /// 1 = Neutral<br />
        /// 2 = Left hand gesture<br />
        /// 3 = Left hand gesture (Fixed)<br />
        /// 4 = Right hand gesture<br />
        /// 5 = Right hand gesture (Fixed)<br />
        /// 6 = Selected facial expression in menu<br />
        /// 7 = Dance Mode<br />
        /// 8 = AFK Mode<br />
        /// </value>
        public static readonly Parameter FacialExpressionMode = new Parameter
        {
            name = "FEC/Sync/FacialExpressionMode",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = FacialExpressionModeType.Inactive,
            localOnly = false,
            saved = false,
        };

        /// <summary>
        /// アバターへ反映する表情を管理するためのパラメーター (動作タイプ内の通し番号)<br />
        /// 0番はデフォルトの表情（予約済み番号扱い）
        /// VRC Expression Parametersの制約上、上限値は255
        /// </summary>
        public static readonly Parameter CurrentFacialExpressionNumber = new Parameter
        {
            name = "FEC/Sync/CurrentFacialExpressionNumber",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = false,
            saved = false,
        };

        /// <summary>
        /// 表情固定時のウェイト値
        /// </summary>
        /// <value>ウェイト値</value>
        public static readonly Parameter LockedFacialExpressionWeight = new Parameter
        {
            name = "FEC/Sync/LockedFacialExpressionWeight",
            parameterType = UnityEngine.AnimatorControllerParameterType.Float,
            defaultFloat = 0,
            localOnly = false,
            saved = true,
        };
    }
}
