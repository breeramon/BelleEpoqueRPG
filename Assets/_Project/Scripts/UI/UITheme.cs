using TMPro;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>
    /// Tema visual da interface. Paleta e trio de fontes vêm do projeto Expedition33
    /// (portfolio-react/src/index.css): "luz de lampião sobre a noite".
    /// Troque cores e fontes aqui e toda a HUD acompanha.
    /// </summary>
    [CreateAssetMenu(menuName = "Belle Époque/Tema da Interface", fileName = "TemaBelleEpoque")]
    public class UITheme : ScriptableObject
    {
        [Header("Fontes (TTF/OTF)")]
        [Tooltip("Títulos e nomes — Cinzel")]
        public Font displayFont;
        [Tooltip("Botões e cabeçalhos menores — Cinzel Regular")]
        public Font displayFontRegular;
        [Tooltip("Texto corrido — EB Garamond")]
        public Font bodyFont;
        [Tooltip("Log e descrições — EB Garamond Itálico")]
        public Font bodyItalicFont;
        [Tooltip("Rótulos e números em caixa alta — Bebas Neue")]
        public Font labelFont;

        [Header("Paleta (Clair Obscur)")]
        public Color nuit = Hex("#0a0b10");      // fundo
        public Color nuit2 = Hex("#11131a");     // superfícies
        public Color nuit3 = Hex("#1c1e29");     // linhas e bordas
        public Color toile = Hex("#e9e2d3");     // texto principal
        public Color cendre = Hex("#a3a2ad");    // texto secundário
        public Color dore = Hex("#c9a24b");      // destaque dourado
        public Color doreClaro = Hex("#e4c878"); // foco
        public Color carmin = Hex("#c8323c");    // perigo

        [Header("Recursos de Ordem")]
        public Color pv = Hex("#b3262f");
        public Color pe = Hex("#c9a24b");
        public Color san = Hex("#8f6ad0");

        [Header("Elementos")]
        public Color sangue = Hex("#d0453f");
        public Color morte = Hex("#9c98a6");
        public Color conhecimento = Hex("#e4c878");
        public Color energia = Hex("#b98cff");
        public Color medo = Hex("#f2efe8");

        // Font assets criados em tempo de execução a partir dos TTF.
        private TMP_FontAsset _display, _displayRegular, _body, _italic, _label;

        public TMP_FontAsset Display => _display != null ? _display : (_display = Make(displayFont));
        public TMP_FontAsset DisplayRegular => _displayRegular != null ? _displayRegular : (_displayRegular = Make(displayFontRegular != null ? displayFontRegular : displayFont));
        public TMP_FontAsset Body => _body != null ? _body : (_body = Make(bodyFont));
        public TMP_FontAsset Italic => _italic != null ? _italic : (_italic = Make(bodyItalicFont != null ? bodyItalicFont : bodyFont));
        public TMP_FontAsset Label => _label != null ? _label : (_label = Make(labelFont));

        private static TMP_FontAsset Make(Font font)
        {
            if (font == null) return TMP_Settings.defaultFontAsset;
            var asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset == null) return TMP_Settings.defaultFontAsset;
            asset.name = font.name + " (runtime)";
            return asset;
        }

        public Color ElementColor(Core.Element e)
        {
            switch (e)
            {
                case Core.Element.Blood: return sangue;
                case Core.Element.Death: return morte;
                case Core.Element.Knowledge: return conhecimento;
                case Core.Element.Energy: return energia;
                case Core.Element.Fear: return medo;
                default: return toile;
            }
        }

        public static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // Parser próprio: APIs da Unity não podem ser chamadas em inicializadores de campo de ScriptableObject.
        private static Color Hex(string hex)
        {
            hex = hex.TrimStart('#');
            int v = System.Convert.ToInt32(hex, 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        /// <summary>Tema padrão (sem fontes) quando nenhum asset foi atribuído.</summary>
        public static UITheme CreateDefault() => CreateInstance<UITheme>();
    }
}
