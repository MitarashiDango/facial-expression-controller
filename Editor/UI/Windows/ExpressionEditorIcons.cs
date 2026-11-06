using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.FacialExpressionController.Editor
{
    /// <summary>
    /// 表示に使う Unity 組み込みエディターアイコン 1 つ分の定義。スキンごとのアイコン名と、
    /// アイコンを読み込めない場合に代わりに表示する短い日本語ラベルを持つ。
    /// </summary>
    internal sealed class ExpressionEditorIcon
    {
        public ExpressionEditorIcon(string darkSkinName, string lightSkinName, string fallbackText)
        {
            DarkSkinName = darkSkinName;
            LightSkinName = lightSkinName;
            FallbackText = fallbackText;
        }

        /// <summary>ダークスキンで使うアイコン名（<see cref="EditorGUIUtility.FindTexture"/> に渡す名前）。</summary>
        public string DarkSkinName { get; }

        /// <summary>ライトスキンで使うアイコン名（<see cref="EditorGUIUtility.FindTexture"/> に渡す名前）。</summary>
        public string LightSkinName { get; }

        /// <summary>アイコンを読み込めない場合に表示する短い日本語ラベル。</summary>
        public string FallbackText { get; }

        /// <summary>
        /// 現在のエディタースキンのアイコンを読み込む。高 DPI の画面では @2x の画像を優先する。
        /// 読み込めない場合は null を返す。
        /// </summary>
        public Texture2D LoadTexture()
        {
            return ExpressionEditorIcons.LoadTexture(
                EditorGUIUtility.isProSkin ? DarkSkinName : LightSkinName,
                EditorGUIUtility.pixelsPerPoint > 1f);
        }
    }

    /// <summary>
    /// 表情アニメーションエディターで使う組み込みアイコンの一覧。アイコン名はここだけに書く。
    /// いずれも Unity 2022.3 のエディターリソースにあることを EditMode テストで確認している。
    /// </summary>
    internal static class ExpressionEditorIcons
    {
        /// <summary>システム除外でロック中の行の鍵アイコン。</summary>
        internal static readonly ExpressionEditorIcon Locked = new ExpressionEditorIcon(
            "PackageManager/Dark/Locked",
            "PackageManager/Light/Locked",
            "ロック");

        /// <summary>システム除外をユーザーが解除した行の解錠アイコン。</summary>
        internal static readonly ExpressionEditorIcon Unlocked = new ExpressionEditorIcon(
            "PackageManager/Dark/Unlocked",
            "PackageManager/Light/Unlocked",
            "解錠");

        /// <summary>行の値を初期値に戻すボタンのアイコン。</summary>
        internal static readonly ExpressionEditorIcon Reset = new ExpressionEditorIcon(
            "d_Refresh",
            "Refresh",
            "戻す");

        /// <summary>メニューを開く操作に添える ▾ アイコン。</summary>
        internal static readonly ExpressionEditorIcon Dropdown = new ExpressionEditorIcon(
            "d_icon dropdown",
            "icon dropdown",
            "一括");

        /// <summary>
        /// Scene View のクリック位置で絞り込むモードの切り替えボタンのアイコン。
        /// Hierarchy で「シーンでクリックして選択できるか」を示す指のアイコンを使い、Scene View をクリックする操作を表す。
        /// </summary>
        internal static readonly ExpressionEditorIcon ScenePick = new ExpressionEditorIcon(
            "d_scenepicking_pickable",
            "scenepicking_pickable",
            "シーン");

        /// <summary>チップの絞り込みを解除するボタンの × アイコン。</summary>
        internal static readonly ExpressionEditorIcon Clear = new ExpressionEditorIcon(
            "d_winbtn_win_close",
            "winbtn_win_close",
            "解除");

        /// <summary>補足説明を tooltip で示す ⓘ アイコン。</summary>
        internal static readonly ExpressionEditorIcon Info = new ExpressionEditorIcon(
            "d_console.infoicon.sml",
            "console.infoicon.sml",
            "説明");

        /// <summary>このクラスで定義しているすべてのアイコン。</summary>
        internal static IReadOnlyList<ExpressionEditorIcon> All { get; } = new[]
        {
            Locked,
            Unlocked,
            Reset,
            Dropdown,
            ScenePick,
            Clear,
            Info,
        };

        /// <summary>高解像度版のアイコン名に付く接尾辞。</summary>
        internal const string HighResolutionSuffix = "@2x";

        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// アイコン名からテクスチャを読み込む。<paramref name="preferHighResolution"/> が true の場合は
        /// 先に @2x の画像を探し、無ければ通常の画像を読み込む。読み込めない場合は null を返す。
        /// </summary>
        internal static Texture2D LoadTexture(string iconName, bool preferHighResolution)
        {
            if (preferHighResolution && !string.IsNullOrEmpty(iconName))
            {
                var highResolutionTexture = LoadTexture(iconName + HighResolutionSuffix);
                if (highResolutionTexture != null)
                {
                    return highResolutionTexture;
                }
            }

            return LoadTexture(iconName);
        }

        /// <summary>
        /// アイコン名からテクスチャを読み込む。読み込めない場合は null を返す。
        /// 読み込めなかった名前も覚えておき、行の再バインドのたびに探し直さない。
        /// </summary>
        internal static Texture2D LoadTexture(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            if (TextureCache.TryGetValue(iconName, out var cachedTexture)
                && (ReferenceEquals(cachedTexture, null) || cachedTexture != null))
            {
                return cachedTexture;
            }

            var texture = EditorGUIUtility.FindTexture(iconName);
            TextureCache[iconName] = texture;
            return texture;
        }
    }

    /// <summary>
    /// 組み込みアイコンを表示する要素。アイコンを読み込めない場合は、代わりに短い日本語ラベルを表示する。
    /// アイコンだけの操作には、この要素か親要素に必ず tooltip を付ける。
    /// </summary>
    internal sealed class ExpressionEditorIconElement : VisualElement
    {
        internal const string UssClassName = "expression-editor-icon";
        internal const string ImageUssClassName = UssClassName + "__image";
        internal const string FallbackLabelUssClassName = UssClassName + "__fallback-label";
        internal const string FallbackUssClassName = UssClassName + "--fallback";

        private readonly Image _image;
        private readonly Label _fallbackLabel;

        public ExpressionEditorIconElement()
        {
            AddToClassList(UssClassName);

            _image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
            };
            _image.AddToClassList(ImageUssClassName);
            Add(_image);

            _fallbackLabel = new Label
            {
                pickingMode = PickingMode.Ignore,
            };
            _fallbackLabel.AddToClassList(FallbackLabelUssClassName);
            _fallbackLabel.AddToClassList(ExpressionEditorStyles.HiddenClassName);
            Add(_fallbackLabel);
        }

        /// <summary>表示中のアイコン定義。</summary>
        public ExpressionEditorIcon Icon { get; private set; }

        /// <summary>表示中のテクスチャ。代わりのラベルを表示している場合は null。</summary>
        public Texture Texture => _image.image;

        /// <summary>アイコンの代わりに表示しているラベル。アイコンを表示している場合は空文字。</summary>
        public string FallbackText => _fallbackLabel.text;

        /// <summary>
        /// 表示するアイコンを設定する。テクスチャを読み込めない場合は代わりのラベルを表示する。
        /// </summary>
        public void SetIcon(ExpressionEditorIcon icon)
        {
            Icon = icon;
            var texture = icon?.LoadTexture();
            var useFallback = icon != null && texture == null;
            _image.image = texture;
            _image.EnableInClassList(ExpressionEditorStyles.HiddenClassName, texture == null);
            _fallbackLabel.text = useFallback ? icon.FallbackText : "";
            _fallbackLabel.EnableInClassList(ExpressionEditorStyles.HiddenClassName, !useFallback);
            EnableInClassList(FallbackUssClassName, useFallback);
        }

        /// <summary>
        /// この要素自身もクリックを受け取らないようにする。親要素でクリックや tooltip を扱う場合に使う。
        /// </summary>
        public void SetPickingModeIgnore()
        {
            pickingMode = PickingMode.Ignore;
        }
    }
}
