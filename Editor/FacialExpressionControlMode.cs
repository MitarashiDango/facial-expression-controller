namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// 動作モード定義
    /// </summary>
    public class FacialExpressionControlModeType
    {
        public static readonly int FacialExpressionControlInactive = 0;
        public static readonly int Neutral = 1;
        public static readonly int LeftHandGesture = 2;
        public static readonly int LeftHandGestureFixed = 3;
        public static readonly int RightHandGesture = 4;
        public static readonly int RightHandGestureFixed = 5;
        public static readonly int SelectedFacialExpressionInMenu = 6;
        public static readonly int BuiltInFacialTracking = 7;
        public static readonly int AnimatorBasedFacialTracking = 8;
        public static readonly int DanceMode = 9;
        public static readonly int AFKMode = 10;
    }
}