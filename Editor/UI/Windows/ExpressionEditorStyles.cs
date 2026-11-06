using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// 表情アニメーションエディターの USS クラス名と、エディタースキンに応じたクラスの付け替えをまとめる。
    /// </summary>
    internal static class ExpressionEditorStyles
    {
        /// <summary>要素を非表示にし、場所も詰める（display: none）。</summary>
        internal const string HiddenClassName = "hidden";

        /// <summary>要素を非表示にするが、場所は残す（visibility: hidden）。列幅を保ちたいときに使う。</summary>
        internal const string InvisibleClassName = "invisible";

        /// <summary>オン／オフを切り替えるボタン（チップなど）がオンであることを示すクラス。</summary>
        internal const string SelectedClassName = "selected";

        /// <summary>ダークスキンのときにルートへ付けるクラス。</summary>
        internal const string DarkSkinClassName = "dark-skin";

        /// <summary>ライトスキンのときにルートへ付けるクラス。</summary>
        internal const string LightSkinClassName = "light-skin";

        /// <summary>
        /// エディタースキンに合わせて、<paramref name="element"/> のスキンクラスを付け替える。
        /// 色の指定はこのクラスでスキンごとに切り替える。
        /// </summary>
        internal static void ApplySkinClass(VisualElement element, bool isProSkin)
        {
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(DarkSkinClassName, isProSkin);
            element.EnableInClassList(LightSkinClassName, !isProSkin);
        }
    }
}
