namespace MitarashiDango.FacialExpressionController.Editor
{
    public class InternalParameters
    {
        /// <summary>
        /// 表情制御を有効にするかどうか (true: 有効, false: 無効)
        /// </summary>
        public static readonly Parameter FacialExpressionControllerEnabled = new Parameter
        {
            name = "FEC/Internal/FacialExpressionControllerEnabled",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = true,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// ダンスモードへの切り替えを有効化するかどうか (true: する, false: しない)
        /// ダンスモードがアクティブになった場合、以下の状態となる<br />
        /// - 表情制御が一時的に非アクティブとなる<br />
        /// - 表情ロック用のContact Receiverが一時的に無効化される
        /// </summary>
        public static readonly Parameter DanceModeAutoSwitchEnabled = new Parameter
        {
            name = "FEC/Internal/DanceModeAutoSwitchEnabled",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = true,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// 乗り物着座時に表情ロックの切り替えを一時停止するかどうか (true: 停止する, false: 停止しない)<br />
        /// true の場合、InStation かつ Seated の状態のときに Contact Receiver による<br />
        /// 表情ロックの自動切り替え（有効化／無効化のいずれも）を抑止する。<br />
        /// コントローラ操作（トリガー等）による意図しないロック発火を防止する用途。<br />
        /// ※既存の表情ロック状態自体は保持され、変更されない。
        /// </summary>
        public static readonly Parameter SuspendFacialExpressionLockSwitchInVehicleEnabled = new Parameter
        {
            name = "FEC/Internal/SuspendFacialExpressionLockSwitchInVehicleEnabled",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = false,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// 表情ロックが有効かどうか (true: ロック有効, false: ロック無効)<br />
        /// </summary>
        public static readonly Parameter FacialExpressionLocked = new Parameter
        {
            name = "FEC/Internal/FacialExpressionLocked",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = false,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// 左手に割り当てられているジェスチャープリセットを管理するためのパラメーター
        /// </summary>
        public static readonly Parameter SelectedLeftGesturePreset = new Parameter
        {
            name = "FEC/Internal/SelectedLeftGesturePreset",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// 右手に割り当てられているジェスチャープリセットを管理するためのパラメーター
        /// </summary>
        public static readonly Parameter SelectedRightGesturePreset = new Parameter
        {
            name = "FEC/Internal/SelectedRightGesturePreset",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// メニュー上で選択している表情を管理するためのパラメーター
        /// </summary>
        public static readonly Parameter SelectedFacialExpressionInMenu = new Parameter
        {
            name = "FEC/Internal/SelectedFacialExpressionInMenu",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// Contact Receiverによる表情ロックを有効にするためのパラメーター
        /// </summary>
        public static readonly Parameter ContactLockEnabled = new Parameter
        {
            name = "FEC/Internal/ContactLockEnabled",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = true,
            localOnly = true,
            saved = true,
        };

        /// <summary>
        /// 表情ロックを行うためのContact Receiverが接触中か判定を行うためのパラメーター
        /// </summary>
        public static readonly Parameter FacialExpressionLockReceiverInContact = new Parameter
        {
            name = "FEC/Internal/FacialExpressionLockReceiverInContact",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = false,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// ジェスチャーの優先設定
        /// </summary>
        /// <value>
        /// 以下の値を取る
        /// 0 = 先勝ち
        /// 1 = 後勝ち
        /// 2 = 左手優先
        /// 3 = 右手優先
        /// </value>
        public static readonly Parameter GesturePriority = new Parameter
        {
            name = "FEC/Internal/GesturePriority",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = GesturePriorityType.FirstWin,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// 左手のジェスチャー番号
        /// </summary>
        public static readonly Parameter State_CurrentGestureLeft = new Parameter
        {
            name = "FEC/Internal/State/CurrentGestureLeft",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// 右手のジェスチャー番号
        /// </summary>
        public static readonly Parameter State_CurrentGestureRight = new Parameter
        {
            name = "FEC/Internal/State/CurrentGestureRight",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// 現在のジェスチャー判定対象の手 (0: Neutral, 1: 左手, 2: 右手)
        /// </summary>
        public static readonly Parameter State_CurrentGestureHand = new Parameter
        {
            name = "FEC/Internal/State/CurrentGestureHand",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// 最後にジェスチャーが変化した手 (0: 初期値, 1: 左手, 2: 右手)
        /// </summary>
        public static readonly Parameter State_LastGestureChangedHand = new Parameter
        {
            name = "FEC/Internal/State/LastGestureChangedHand",
            parameterType = UnityEngine.AnimatorControllerParameterType.Int,
            defaultInt = 0,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// AFKモードがアクティブかどうか<br />
        /// ※AFK = falseになった後の待機時間中もtrueとなる。
        /// </summary>
        public static readonly Parameter State_AFKModeActive = new Parameter
        {
            name = "FEC/Internal/State/AFKModeActive",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = false,
            localOnly = true,
            saved = false,
        };

        /// <summary>
        /// ダンスモードがアクティブかどうか
        /// </summary>
        public static readonly Parameter State_DanceModeActive = new Parameter
        {
            name = "FEC/Internal/State/DanceModeActive",
            parameterType = UnityEngine.AnimatorControllerParameterType.Bool,
            defaultBool = false,
            localOnly = true,
            saved = false,
        };
    }
}
