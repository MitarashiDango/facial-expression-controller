namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// VRChatのハンドジェスチャーに関する定数定義
    /// </summary>
    public static class GestureConstants
    {
        public static readonly (string Name, int Value)[] Gestures = new[]
        {
            ("Fist", 1),
            ("HandOpen", 2),
            ("FingerPoint", 3),
            ("Victory", 4),
            ("RockNRoll", 5),
            ("HandGun", 6),
            ("ThumbsUp", 7)
        };
    }
}