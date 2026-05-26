namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// 動作モード定義
    /// </summary>
    public class FacialExpressionModeType
    {
        public static readonly int Inactive = 0;
        public static readonly int Neutral = 1;
        public static readonly int LeftHandGesture = 2;
        public static readonly int LeftHandGestureFixed = 3;
        public static readonly int RightHandGesture = 4;
        public static readonly int RightHandGestureFixed = 5;
        public static readonly int SelectedFacialExpressionInMenu = 6;
        public static readonly int DanceMode = 7;
        public static readonly int AFKMode = 8;
    }
}