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
        /// 取りうる値は以下の通り<br />
        /// 0   = Facial expression control inactive<br />
        /// 1   = Neutral<br />
        /// 2   = Left hand gesture<br />
        /// 3   = Left hand gesture (Fixed)<br />
        /// 4   = Right hand gesture<br />
        /// 5   = Right hand gesture (Fixed)<br />
        /// 6   = Selected facial expression in menu<br />
        /// 7   = Built-in Facial Tracking<br />
        /// 8   = Animator Based Facial Tracking<br />
        /// 9   = Dance Mode<br />
        /// 10  = AFK Mode<br />
        /// </value>
        public static readonly Parameter FacialExpressionControlMode = new Parameter
        {
            name = "FEC/Sync/FacialExpressionControlMode",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = FacialExpressionControlModeType.FacialExpressionControlInactive,
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
        public static readonly Parameter FixedWeight = new Parameter
        {
            name = "FEC/Sync/FixedWeight",
            parameterType = UnityEngine.AnimatorControllerParameterType.Float,
            defaultFloat = 0,
            localOnly = false,
            saved = true,
        };
    }
}