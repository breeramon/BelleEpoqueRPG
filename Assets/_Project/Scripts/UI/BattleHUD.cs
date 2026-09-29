using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BelleEpoque.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BelleEpoque
{
    /// <summary>
    /// HUD no estilo Clair Obscur: Expedition 33.
    ///  - Menu de comandos em leque ao redor do personagem da vez (sempre dentro da tela).
    ///  - Ordem de turnos no topo, com a inicial de cada personagem.
    ///  - Fichas do grupo (PV, PE, SAN, Defesa, status) no canto inferior direito.
    ///  - Barras de PV flutuantes: embaixo dos agentes e em cima das ameaças, quando levam dano ou viram alvo.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class BattleHUD : MonoBehaviour
    {
        [SerializeField] private UITheme theme;

        [Header("Menu em leque (referência 1920x1080)")]
        [SerializeField] private float optionWidth = 290f;
        [SerializeField] private float optionHeight = 50f;
        [SerializeField] private float optionStep = 58f;      // distância vertical entre opções
        [SerializeField] private float optionSlant = 22f;     // deslocamento lateral por opção (diagonal)
        [SerializeField] private float optionTilt = -7f;      // inclinação das faixas, em graus
        [SerializeField] private float safeTop = 150f;        // espaço da ordem de turnos
        [SerializeField] private float safeBottom = 40f;

        private BattleController _controller;
        private BattleSystem _battle;
        private RectTransform _root;
        private Camera _camera;
        private UITheme T => theme;

        // ---------------- Menu em leque
        private RectTransform _fanLayer;
        private readonly List<RectTransform> _options = new List<RectTransform>();
        private RectTransform _leftOption;
        private TextMeshProUGUI _fanTitle;
        private RectTransform _hint;
        private TextMeshProUGUI _hintRules, _hintText;
        private BattleUnit _menuUnit;

        // ---------------- Topo
        private RectTransform _turnBar;
        private TextMeshProUGUI _roundLabel, _bannerTitle, _bannerSub, _log;
        private CanvasGroup _bannerGroup;
        private Coroutine _bannerRoutine;
        private readonly List<string> _logLines = new List<string>();
        private RectTransform _logPanel;
        private CanvasGroup _logGroup;
        private TextMeshProUGUI _logButtonCount;
        private bool _logOpen;

        // ---------------- Animação do menu
        private const float AppearTime = 0.24f, Stagger = 0.045f, CloseTime = 0.18f;
        private const float HintGap = 48f; // distância entre a última opção (inclinada) e a descrição
        private float _fanOpenedAt, _closeStart;
        private bool _closing;
        private RectTransform _chosen;
        private CanvasGroup _hintGroup;

        // ---------------- Fim
        private GameObject _endPanel;
        private TextMeshProUGUI _endTitle, _endMessage;

        // ---------------- Fichas do grupo (canto inferior direito)
        private class Card
        {
            public RectTransform Root;
            public UnityEngine.UI.Image Glow, Ring;
            public TextMeshProUGUI Name, Sub, PvText, PeText, SanText, Status;
            public RectTransform Pv, Pe, San;
            public float BaseY;
        }
        private readonly Dictionary<BattleUnit, Card> _cards = new Dictionary<BattleUnit, Card>();

        // ---------------- Barras flutuantes
        private class Floater
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Fill;
            public TextMeshProUGUI Text;
            public float VisibleUntil;
            public bool Pinned;
        }
        private readonly Dictionary<BattleUnit, Floater> _floaters = new Dictionary<BattleUnit, Floater>();

        public UITheme Theme => theme;

        // ---------------- Números exibidos (avançam evento a evento, não saltam para o resultado final)
        private readonly Dictionary<BattleUnit, UnitState> _display = new Dictionary<BattleUnit, UnitState>();

        /// <summary>Estado exibido da unidade (o do último evento já mostrado).</summary>
        public UnitState Shown(BattleUnit u)
        {
            if (!_display.TryGetValue(u, out var s)) { s = UnitState.Of(u); _display[u] = s; }
            return s;
        }

        /// <summary>Chamado pelo controlador quando um evento começa a ser mostrado.</summary>
        public void ApplyEvent(BattleEvent e)
        {
            if (e?.States == null) return;
            foreach (var pair in e.States) _display[pair.Key] = pair.Value;
        }

        /// <summary>Alinha a exibição com o estado real (ex.: quando o jogador vai escolher a ação).</summary>
        public void SyncToLive()
        {
            foreach (var u in _battle.AllUnits) _display[u] = UnitState.Of(u);
        }

        // ================================================================== Construção

        public void Build(BattleController controller, BattleSystem battle)
        {
            _controller = controller;
            _battle = battle;
            _root = (RectTransform)transform;
            _camera = Camera.main;
            if (theme == null) theme = UITheme.CreateDefault();
            DamagePopup.Font = T.Label;
            SyncToLive();

            // Ordem de desenho: flutuantes embaixo, depois painéis, menu e tela final por cima
            var floatLayer = Layer("Flutuantes");
            foreach (var u in battle.AllUnits) _floaters[u] = BuildFloater(floatLayer, u);

            BuildTop();
            BuildParty();
            _fanLayer = Layer("Menu");
            BuildHint();
            BuildEnd();

            HideMenus();
            Refresh();
        }

        private RectTransform Layer(string name)
        {
            var rt = UIFactory.CreateRect(name, transform);
            UIFactory.Stretch(rt);
            return rt;
        }

        private RectTransform Centered(string name, Transform parent, Vector2 size, Vector2 pivot)
        {
            var rt = UIFactory.CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.sizeDelta = size;
            return rt;
        }

        // ------------------------------------------------------------------ Topo: ordem de turnos, banner, registro

        private void BuildTop()
        {
            var veil = UIFactory.CreatePanel(transform, "VeuTopo", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.8f), false, UIFactory.FadeSprite(UIFactory.Fade.Down));
            UIFactory.Place(veil.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 200));

            _turnBar = UIFactory.CreateRect("OrdemDeTurnos", transform);
            UIFactory.Place(_turnBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(900, 90));
            _roundLabel = UIFactory.CreateText(transform, "Rodada", "", T.Label, 20, T.dore, TextAlignmentOptions.Center, 8);
            UIFactory.Place(_roundLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(400, 24));

            var banner = UIFactory.CreateRect("Banner", transform);
            UIFactory.Place(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(1000, 90));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            _bannerTitle = UIFactory.CreateText(banner, "Titulo", "", T.Display, 42, T.toile, TextAlignmentOptions.Center, 5);
            UIFactory.Anchor(_bannerTitle.rectTransform, 0, 0.38f, 1, 1);
            _bannerSub = UIFactory.CreateText(banner, "Sub", "", T.Label, 18, T.cendre, TextAlignmentOptions.Center, 6);
            UIFactory.Anchor(_bannerSub.rectTransform, 0, 0, 1, 0.38f);
            foreach (float side in new[] { -1f, 1f })
            {
                var rule = UIFactory.CreateRule(banner, "Filete", new Color(T.dore.r, T.dore.g, T.dore.b, 0.7f), 1.5f);
                rule.sprite = UIFactory.FadeSprite(side < 0 ? UIFactory.Fade.Left : UIFactory.Fade.Right);
                UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(side < 0 ? 1 : 0, 0.5f), new Vector2(side * 250, 0), new Vector2(180, 1.5f));
            }

            BuildLog();
        }

        /// <summary>Botão "Registro" no canto superior esquerdo que abre um painel pequeno com o histórico.</summary>
        private void BuildLog()
        {
            var btnBg = UIFactory.CreatePanel(transform, "BotaoRegistro", Color.white, true, UIFactory.FadeSprite(UIFactory.Fade.Right));
            UIFactory.Place(btnBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(190, 44));
            var colors = new UIFactory.ButtonColors
            {
                Normal = new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.85f),
                Highlighted = new Color(0.30f, 0.22f, 0.09f, 0.95f),
                Pressed = new Color(0.45f, 0.34f, 0.12f, 1f),
                Disabled = new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.5f)
            };
            UIFactory.MakeButton(btnBg.gameObject, btnBg, colors, ToggleLog);
            var rule = UIFactory.CreateRule(btnBg.transform, "Filete", new Color(T.dore.r, T.dore.g, T.dore.b, 0.8f), 1.5f);
            rule.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            UIFactory.Place(rule.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1.5f));
            var dia = UIFactory.CreateDiamond(btnBg.transform, "Losango", T.dore, 8);
            UIFactory.Place(dia.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16, 0), new Vector2(8, 8));
            var label = UIFactory.CreateText(btnBg.transform, "Label", "REGISTRO", T.Label, 20, T.toile, TextAlignmentOptions.Left, 5);
            UIFactory.Stretch(label.rectTransform, 32, 0, 10, 0);
            _logButtonCount = UIFactory.CreateText(btnBg.transform, "Contagem", "", T.Label, 17, T.dore, TextAlignmentOptions.Right, 2);
            UIFactory.Stretch(_logButtonCount.rectTransform, 10, 0, 14, 0);

            _logPanel = UIFactory.CreatePanel(transform, "PainelRegistro", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.93f), true).rectTransform;
            UIFactory.Place(_logPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -70), new Vector2(500, 300));
            _logGroup = _logPanel.gameObject.AddComponent<CanvasGroup>();
            var top = UIFactory.CreateRule(_logPanel, "Filete", T.dore, 1.5f);
            top.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            UIFactory.Place(top.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1.5f));
            var title = UIFactory.CreateText(_logPanel, "Titulo", "REGISTRO DA BATALHA", T.Label, 18, T.dore, TextAlignmentOptions.TopLeft, 5);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-32, 24));
            _log = UIFactory.CreateText(_logPanel, "Texto", "", T.Italic, 18, T.toile, TextAlignmentOptions.BottomLeft);
            _log.textWrappingMode = TextWrappingModes.Normal;
            _log.overflowMode = TextOverflowModes.Truncate;
            _log.lineSpacing = -6;
            UIFactory.Stretch(_log.rectTransform, 16, 12, 16, 42);
            _logPanel.gameObject.SetActive(false);
        }

        private void ToggleLog()
        {
            _logOpen = !_logOpen;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(_logOpen ? AudioManager.Instance.uiClick : AudioManager.Instance.uiBack, 0.6f, 0f);
            StopCoroutine(nameof(FadeLog));
            StartCoroutine(nameof(FadeLog));
        }

        private IEnumerator FadeLog()
        {
            if (_logOpen) _logPanel.gameObject.SetActive(true);
            float from = _logGroup.alpha, to = _logOpen ? 1f : 0f, t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.16f;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                _logGroup.alpha = Mathf.Lerp(from, to, e);
                _logPanel.anchoredPosition = new Vector2(20, -70 + (1f - _logGroup.alpha) * 12f);
                yield return null;
            }
            if (!_logOpen) _logPanel.gameObject.SetActive(false);
        }

        private void RebuildTurnBar()
        {
            for (int i = _turnBar.childCount - 1; i >= 0; i--) Destroy(_turnBar.GetChild(i).gameObject);
            var order = _battle.PreviewTurns(8);
            float big = 70f, small = 50f, gap = 12f;
            float total = order.Count == 0 ? 0 : big + (order.Count - 1) * (small + gap);
            float x = -total / 2f;
            for (int i = 0; i < order.Count; i++)
            {
                float size = i == 0 ? big : small;
                var slot = Portrait(_turnBar, order[i], size, i == 0);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1f);
                slot.pivot = new Vector2(0f, 1f);
                slot.anchoredPosition = new Vector2(x, i == 0 ? 0f : -(big - small) / 2f);
                x += size + gap;
            }
            _roundLabel.text = _battle.Round > 0 ? $"RODADA {_battle.Round}" : "";
        }

        /// <summary>Retrato redondo com a inicial (até existir arte dos personagens).</summary>
        private RectTransform Portrait(Transform parent, BattleUnit unit, float size, bool highlight)
        {
            bool hero = unit.Team == Team.Heroes;
            var root = UIFactory.CreateRect("Retrato " + unit.Name, parent);
            root.sizeDelta = new Vector2(size, size);

            var def = unit.Tag as UnitDefinition;
            var disc = UIFactory.CreatePanel(root, "Fundo", hero ? T.nuit2 : Color.Lerp(T.nuit2, T.carmin, 0.18f), false, UIFactory.CircleSprite(false));
            UIFactory.Stretch(disc.rectTransform);
            if (def != null && def.portrait != null)
            {
                var img = UIFactory.CreatePanel(root, "Arte", Color.white, false, def.portrait);
                UIFactory.Stretch(img.rectTransform, size * 0.08f);
            }
            else
            {
                var letter = UIFactory.CreateText(root, "Inicial", Initial(unit.Name), T.Display, size * 0.46f, hero ? T.toile : Color.Lerp(T.toile, T.carmin, 0.35f), TextAlignmentOptions.Center);
                UIFactory.Stretch(letter.rectTransform);
            }
            var ring = UIFactory.CreatePanel(root, "Aro", highlight ? T.doreClaro : (hero ? T.dore : T.carmin), false, UIFactory.CircleSprite(true));
            UIFactory.Stretch(ring.rectTransform);
            if (!Shown(unit).Alive) root.gameObject.AddComponent<CanvasGroup>().alpha = 0.35f;
            return root;
        }

        /// <summary>Descrição da classe: o "roleName" da ficha (ex.: Especialista · Médico de Campo) ou a trilha.</summary>
        private static string ClassLabel(BattleUnit u)
        {
            var def = u.Tag as UnitDefinition;
            string label = def != null && !string.IsNullOrWhiteSpace(def.roleName) ? def.roleName : UnitDefinition.TrilhaName(u.Trilha);
            return label.ToUpperInvariant();
        }

        private static string Initial(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            // "Irmã Céleste" -> "C": pula títulos curtos como Irmã, Dr., Mme.
            var parts = name.Split(' ');
            string pick = parts.Length > 1 && parts[0].Length <= 5 && parts[0].EndsWith("ã") ? parts[1] : parts[0];
            return pick.Substring(0, 1).ToUpperInvariant();
        }

        // ------------------------------------------------------------------ Fichas do grupo

        private void BuildParty()
        {
            var veil = UIFactory.CreatePanel(transform, "VeuFichas", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.85f), false, UIFactory.FadeSprite(UIFactory.Fade.Up));
            UIFactory.Place(veil.rectTransform, new Vector2(0.4f, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 235));

            // Cabeçalho: retrato à esquerda; à direita dele o nome e, logo abaixo, trilha / NEX / DEF
            const float P = 56f;            // tamanho do retrato
            const float TX = P + 10f;       // onde começa o texto ao lado do retrato
            const float Top = P + 8f;      // onde começa o bloco de PV
            float w = 230f, h = Top + 94f, gap = 12f;
            int n = _battle.Heroes.Count;
            for (int i = 0; i < n; i++)
            {
                var u = _battle.Heroes[i];
                var c = new Card();
                c.Root = UIFactory.CreateRect("Ficha " + u.Name, transform);
                c.BaseY = 18f;
                UIFactory.Place(c.Root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24 - (n - 1 - i) * (w + gap), c.BaseY), new Vector2(w, h));

                c.Glow = UIFactory.CreatePanel(c.Root, "Brilho", new Color(T.dore.r, T.dore.g, T.dore.b, 0f), false, UIFactory.FadeSprite(UIFactory.Fade.Up));
                UIFactory.Stretch(c.Glow.rectTransform, -8, -8, -8, 20);
                var line = UIFactory.CreateRule(c.Root, "Filete", new Color(T.dore.r, T.dore.g, T.dore.b, 0.45f), 1.5f);
                UIFactory.Place(line.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(0, 1.5f));

                // Retrato (inicial por enquanto; troca sozinho pela foto quando o UnitDefinition tiver "portrait")
                var portrait = Portrait(c.Root, u, P, false);
                UIFactory.Place(portrait, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(P, P));
                c.Ring = portrait.Find("Aro").GetComponent<UnityEngine.UI.Image>();

                // Nome ao lado do retrato; diminui sozinho se for comprido, nunca é cortado
                c.Name = UIFactory.CreateText(c.Root, "Nome", u.Name, T.Display, 20, T.toile, TextAlignmentOptions.BottomLeft);
                c.Name.overflowMode = TextOverflowModes.Overflow;
                c.Name.enableAutoSizing = true;
                c.Name.fontSizeMin = 12;
                c.Name.fontSizeMax = 20;
                UIFactory.Place(c.Name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(TX, -1), new Vector2(w - TX, 25));

                // Abaixo do nome: trilha na 1ª linha, NEX e Defesa na 2ª
                c.Sub = UIFactory.CreateText(c.Root, "Trilha", "", T.Label, 14, T.cendre, TextAlignmentOptions.TopLeft, 2);
                c.Sub.overflowMode = TextOverflowModes.Overflow;
                c.Sub.lineSpacing = -8;
                c.Sub.enableAutoSizing = true;
                c.Sub.fontSizeMin = 9;
                c.Sub.fontSizeMax = 14;
                UIFactory.Place(c.Sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(TX, -27), new Vector2(w - TX, 32));

                var pvLabel = UIFactory.CreateText(c.Root, "PVRotulo", "PV", T.Label, 16, T.pv, TextAlignmentOptions.Left, 3);
                UIFactory.Place(pvLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -Top - 6), new Vector2(40, 30));
                c.PvText = UIFactory.CreateText(c.Root, "PV", "", T.Label, 28, T.toile, TextAlignmentOptions.Right, 1);
                UIFactory.Place(c.PvText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -Top), new Vector2(w - 40, 32));
                c.Pv = SmallBar(c.Root, T.pv, 0, -Top - 34, w, 6);

                c.PeText = UIFactory.CreateText(c.Root, "PE", "", T.Label, 16, T.pe, TextAlignmentOptions.Left, 2);
                UIFactory.Place(c.PeText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -Top - 44), new Vector2(w / 2, 20));
                c.SanText = UIFactory.CreateText(c.Root, "SAN", "", T.Label, 16, T.san, TextAlignmentOptions.Right, 2);
                UIFactory.Place(c.SanText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(w / 2, -Top - 44), new Vector2(w / 2, 20));
                c.Pe = SmallBar(c.Root, T.pe, 0, -Top - 66, w / 2 - 6, 4);
                c.San = SmallBar(c.Root, T.san, w / 2 + 6, -Top - 66, w / 2 - 6, 4);

                c.Status = UIFactory.CreateText(c.Root, "Status", "", T.Italic, 15, T.cendre);
                UIFactory.Place(c.Status.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -Top - 72), new Vector2(w, 20));
                _cards[u] = c;
            }
        }

        private RectTransform SmallBar(RectTransform parent, Color color, float x, float y, float w, float h)
        {
            var fill = UIFactory.CreateBar(parent, "Barra", new Color(T.nuit3.r, T.nuit3.g, T.nuit3.b, 0.95f), color);
            UIFactory.Place((RectTransform)fill.parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(w, h));
            return fill;
        }

        // ------------------------------------------------------------------ Barras flutuantes

        private Floater BuildFloater(RectTransform layer, BattleUnit u)
        {
            var f = new Floater();
            f.Root = Centered("PV " + u.Name, layer, new Vector2(150, 36), new Vector2(0.5f, 0.5f));
            f.Group = f.Root.gameObject.AddComponent<CanvasGroup>();
            f.Group.alpha = 0f;
            f.Group.blocksRaycasts = false;
            var bg = UIFactory.CreatePanel(f.Root, "Fundo", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.55f));
            UIFactory.Stretch(bg.rectTransform, -8, -2, -8, -2);
            f.Text = UIFactory.CreateText(f.Root, "Valor", "", T.Label, 18, T.toile, TextAlignmentOptions.Center, 2);
            UIFactory.Place(f.Text.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 22));
            f.Fill = UIFactory.CreateBar(f.Root, "Barra", new Color(T.nuit3.r, T.nuit3.g, T.nuit3.b, 0.95f), T.pv);
            UIFactory.Place((RectTransform)f.Fill.parent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(0, 5));
            return f;
        }

        /// <summary>Mostra a barra de PV da unidade por alguns segundos (quando leva dano, é curada etc.).</summary>
        public void Flash(BattleUnit unit, float seconds = 2.5f)
        {
            if (unit != null && _floaters.TryGetValue(unit, out var f)) f.VisibleUntil = Time.time + seconds;
        }

        private void PinTargets(IEnumerable<BattleUnit> units)
        {
            var set = new HashSet<BattleUnit>(units ?? Enumerable.Empty<BattleUnit>());
            foreach (var pair in _floaters) pair.Value.Pinned = set.Contains(pair.Key);
        }

        // ------------------------------------------------------------------ Dica (descrição da opção em foco)

        private void BuildHint()
        {
            _hint = Centered("Dica", _fanLayer, new Vector2(400, 92), new Vector2(0, 1));
            _hintGroup = _hint.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            var bg = UIFactory.CreatePanel(_hint, "Fundo", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.9f), false, UIFactory.FadeSprite(UIFactory.Fade.Right));
            UIFactory.Stretch(bg.rectTransform);
            _hintRules = UIFactory.CreateText(_hint, "Regras", "", T.Label, 16, T.dore, TextAlignmentOptions.TopLeft, 2);
            UIFactory.Stretch(_hintRules.rectTransform, 14, 60, 10, 8);
            _hintText = UIFactory.CreateText(_hint, "Texto", "", T.Italic, 19, T.toile, TextAlignmentOptions.TopLeft);
            _hintText.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Stretch(_hintText.rectTransform, 14, 6, 10, 30);

            _fanTitle = UIFactory.CreateText(_fanLayer, "TituloDoMenu", "", T.Label, 18, T.dore, TextAlignmentOptions.Left, 4);
            var rt = _fanTitle.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(520, 24);
        }

        private void ShowHint((string rules, string text) info)
        {
            bool has = !string.IsNullOrEmpty(info.rules) || !string.IsNullOrEmpty(info.text);
            _hint.gameObject.SetActive(has);
            _hintRules.text = (info.rules ?? "").ToUpperInvariant();
            _hintText.text = info.text ?? "";
        }

        // ------------------------------------------------------------------ Tela final

        private void BuildEnd()
        {
            var dim = UIFactory.CreatePanel(transform, "Fim", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.88f), true);
            UIFactory.Stretch(dim.rectTransform);
            _endPanel = dim.gameObject;

            _endTitle = UIFactory.CreateText(dim.transform, "Titulo", "", T.Display, 110, T.dore, TextAlignmentOptions.Center, 24);
            UIFactory.Place(_endTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1400, 150));
            foreach (float side in new[] { -1f, 1f })
            {
                var r = UIFactory.CreateRule(dim.transform, "Filete", T.dore, 1.5f);
                r.sprite = UIFactory.FadeSprite(side < 0 ? UIFactory.Fade.Left : UIFactory.Fade.Right);
                UIFactory.Place(r.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(side < 0 ? 1 : 0, 0.5f), new Vector2(side * 16, 60), new Vector2(360, 1.5f));
            }
            UIFactory.Place(UIFactory.CreateDiamond(dim.transform, "Losango", T.dore, 12).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(12, 12));
            _endMessage = UIFactory.CreateText(dim.transform, "Mensagem", "", T.Italic, 34, T.toile, TextAlignmentOptions.Center);
            UIFactory.Place(_endMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1400, 60));

            var btn = Stroke(dim.transform, "Tentar de novo", "", Restart, true, null);
            var btnRt = (RectTransform)btn.transform;
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0, -110);
            btnRt.localRotation = Quaternion.identity;
            _endPanel.SetActive(false);
        }

        // ================================================================== Atualização por quadro

        private void LateUpdate()
        {
            if (_battle == null) return;
            if (_camera == null) _camera = Camera.main;
            UpdateFloaters();
            if (_menuUnit != null) LayoutFan();
        }

        private bool ToCanvas(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            if (_camera == null) return false;
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
        }

        private void UpdateFloaters()
        {
            foreach (var pair in _floaters)
            {
                var u = pair.Key;
                var f = pair.Value;
                var view = _controller.ViewOf(u);
                var shown = Shown(u);
                bool want = view != null && (f.Pinned || Time.time < f.VisibleUntil) && (shown.Alive || Time.time < f.VisibleUntil);
                f.Group.alpha = Mathf.MoveTowards(f.Group.alpha, want ? 1f : 0f, Time.deltaTime * 5f);
                if (f.Group.alpha <= 0f || view == null) continue;

                bool hero = u.Team == Team.Heroes;
                Vector3 anchor = hero ? view.transform.position : view.TopPosition;
                if (ToCanvas(anchor, out var p))
                    f.Root.anchoredPosition = p + (hero ? new Vector2(0, -34) : new Vector2(0, 26));

                UIFactory.SetFill(f.Fill, Pct(shown.Hp, u.Stats.MaxHp));
                f.Text.text = hero ? $"{shown.Hp}/{u.Stats.MaxHp}" : $"{u.Name.ToUpperInvariant()}  ·  {shown.Hp}/{u.Stats.MaxHp}";
                f.Root.localScale = Vector3.one * (f.Pinned ? 1.08f : 1f);
            }
        }

        public void Refresh()
        {
            RebuildTurnBar();
            foreach (var pair in _cards)
            {
                var u = pair.Key;
                var c = pair.Value;
                bool current = _battle.CurrentUnit == u;

                var sh = Shown(u);
                UIFactory.SetFill(c.Pv, Pct(sh.Hp, u.Stats.MaxHp));
                UIFactory.SetFill(c.Pe, Pct(sh.Pe, u.Stats.MaxPe));
                UIFactory.SetFill(c.San, Pct(sh.Sanity, u.Stats.MaxSanity));
                c.PvText.text = $"{sh.Hp}/{u.Stats.MaxHp}";
                c.PeText.text = $"PE {sh.Pe}/{u.Stats.MaxPe}";
                c.SanText.text = $"SAN {sh.Sanity}/{u.Stats.MaxSanity}";
                c.Sub.text = $"{ClassLabel(u)}\nNEX {u.Nex}%  ·  DEF {sh.Defesa}";
                c.Name.color = !sh.Alive ? new Color(T.cendre.r, T.cendre.g, T.cendre.b, 0.5f) : current ? T.doreClaro : T.toile;
                c.Glow.color = new Color(T.dore.r, T.dore.g, T.dore.b, current ? 0.18f : 0f);
                c.Ring.color = current ? T.doreClaro : T.dore;
                c.Root.anchoredPosition = new Vector2(c.Root.anchoredPosition.x, c.BaseY + (current ? 14f : 0f));

                var tags = new List<string>();
                if (!sh.Alive) tags.Add("caído");
                else
                {
                    if (sh.SanityState != SanityState.Lucid) tags.Add($"<color={UITheme.ToHex(T.san)}>{SanityLabel(sh.SanityState)}</color>");
                    if (sh.Defending) tags.Add("defendendo");
                    foreach (var s in sh.Statuses) tags.Add($"{BattleSystem.StatusName(s.Type).ToLowerInvariant()} ({s.RemainingTurns})");
                }
                c.Status.text = string.Join(" · ", tags);
            }
        }

        private static float Pct(int value, int max) => max <= 0 ? 0f : Mathf.Clamp01((float)value / max);

        private static string SanityLabel(SanityState s)
        {
            switch (s)
            {
                case SanityState.Shaken: return "abalado −1d";
                case SanityState.Panicked: return "em pânico −2d";
                case SanityState.Broken: return "enlouquecendo";
                default: return "";
            }
        }

        public void SetTurn(BattleUnit unit) => Refresh();

        public void Log(string message, string roll = null)
        {
            if (string.IsNullOrEmpty(message)) return;
            string line = message;
            if (!string.IsNullOrEmpty(roll)) line += $" <size=75%><color={UITheme.ToHex(Color.Lerp(T.cendre, T.nuit, 0.35f))}>{roll}</color></size>";
            _logLines.Add(line);
            while (_logLines.Count > 60) _logLines.RemoveAt(0);
            // Mostra as últimas linhas, as mais antigas mais apagadas
            int show = Mathf.Min(10, _logLines.Count);
            var sb = new System.Text.StringBuilder();
            for (int i = _logLines.Count - show; i < _logLines.Count; i++)
            {
                int k = i - (_logLines.Count - show);
                int alpha = 90 + (int)(165f * (k + 1) / show);
                sb.Append($"<alpha=#{alpha:X2}>").Append(_logLines[i]).Append('\n');
            }
            _log.text = sb.ToString();
            _logButtonCount.text = _logLines.Count.ToString();
        }

        public void ShowBanner(string title, string subtitle = "", float seconds = 1.2f)
        {
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(BannerRoutine(title, subtitle, seconds));
        }

        private IEnumerator BannerRoutine(string title, string subtitle, float seconds)
        {
            _bannerTitle.text = title;
            _bannerSub.text = subtitle.ToUpperInvariant();
            float t = 0f;
            while (t < 0.18f) { t += Time.deltaTime; _bannerGroup.alpha = t / 0.18f; yield return null; }
            _bannerGroup.alpha = 1f;
            yield return new WaitForSeconds(seconds);
            t = 0f;
            while (t < 0.35f) { t += Time.deltaTime; _bannerGroup.alpha = 1f - t / 0.35f; yield return null; }
            _bannerGroup.alpha = 0f;
        }

        public void ShowEnd(bool victory, string message)
        {
            HideMenus();
            _endTitle.text = victory ? "VITÓRIA" : "DERROTA";
            _endTitle.color = victory ? T.dore : T.carmin;
            _endMessage.text = message;
            _endPanel.SetActive(true);
        }

        private void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        // ================================================================== Menu em leque

        public void HideMenus()
        {
            ClearFan();
            _menuUnit = null;
            _hint.gameObject.SetActive(false);
            _fanTitle.text = "";
            PinTargets(null);
        }

        private void ClearFan()
        {
            foreach (var o in _options) if (o != null) { o.gameObject.SetActive(false); Destroy(o.gameObject); }
            _options.Clear();
            if (_leftOption != null) { _leftOption.gameObject.SetActive(false); Destroy(_leftOption.gameObject); }
            _leftOption = null;
        }

        private void OpenFan(BattleUnit unit, string title)
        {
            ClearFan();
            _menuUnit = unit;
            _fanOpenedAt = Time.unscaledTime;
            _closing = false;
            _chosen = null;
            _fanTitle.text = title;
            PinTargets(null);
        }

        public void ShowCommands(BattleUnit unit)
        {
            _controller.SetTargeting(unit, false, false);
            _controller.PreviewTarget(null);
            OpenFan(unit, "");
            var basic = unit.BasicAttack;
            // Leque à direita, de cima para baixo, como no Expedition 33
            AddOption("Itens", "", () => ShowItems(unit), _battle.Inventory.Any(kv => kv.Value > 0), ("", "Consumíveis do grupo."));
            AddOption(unit.Trilha == Trilha.Ocultista ? "Rituais" : "Habilidades", $"PE {unit.Pe}", () => ShowSkills(unit), unit.Skills.Count > 0,
                ("", $"Gastam PE. Limite de {unit.Stats.PePorRodada} PE por rodada."));
            AddOption("Atacar", "", () => ChooseTargets(unit, basic), true, Describe(basic));
            // "Defender" fica do lado esquerdo do personagem, como o "Aim" do jogo
            _leftOption = MakeOption(_fanLayer, "Defender", $"+{BattleUnit.DefendBonus} DEF", () => Submit(BattleAction.Defend(unit)), true,
                ("", $"+{BattleUnit.DefendBonus} na Defesa até o próximo turno e recupera {BattleSystem.DefendPeRestore} PE."), null, null, null, true, true);
            ShowHint(Describe(basic));
            LayoutFan();
        }

        private void ShowSkills(BattleUnit unit)
        {
            _controller.SetTargeting(unit, false, false);
            _controller.PreviewTarget(null);
            OpenFan(unit, $"{(unit.Trilha == Trilha.Ocultista ? "RITUAIS" : "HABILIDADES")}  ·  PE {unit.Pe}/{unit.Stats.MaxPe}  ·  LIMITE {unit.Stats.PePorRodada}");
            foreach (var skill in unit.Skills)
            {
                var def = skill.Tag as SkillDefinition;
                string cost = def != null ? def.CostLabel() : (skill.PeCost > 0 ? $"{skill.PeCost} PE" : "");
                string reason = unit.CannotPayReason(skill);
                var info = Describe(skill);
                if (reason != null) info.rules = $"<color={UITheme.ToHex(T.carmin)}>{reason}</color> · " + info.rules;
                AddOption(skill.Name, cost, () => ChooseTargets(unit, skill), _battle.CanUse(unit, skill), info, T.ElementColor(skill.Element));
            }
            AddBack(() => ShowCommands(unit));
            if (unit.Skills.Count > 0) ShowHint(Describe(unit.Skills[0]));
            LayoutFan();
        }

        private void ShowItems(BattleUnit unit)
        {
            _controller.SetTargeting(unit, false, false);
            _controller.PreviewTarget(null);
            OpenFan(unit, "ITENS DO GRUPO");
            SkillData first = null;
            foreach (var pair in _battle.Inventory)
            {
                if (pair.Value <= 0) continue;
                var item = pair.Key;
                if (first == null) first = item;
                AddOption(item.Name, $"x{pair.Value}", () => ChooseTargets(unit, item), _battle.CanUse(unit, item), Describe(item));
            }
            AddBack(() => ShowCommands(unit));
            if (first != null) ShowHint(Describe(first));
            LayoutFan();
        }

        private void ChooseTargets(BattleUnit unit, SkillData skill)
        {
            var targets = _battle.GetValidTargets(unit, skill).ToList();
            if (skill.TargetsAll || skill.Target == TargetType.Self)
            {
                Submit(BattleAction.UseSkill(unit, skill, targets.ToArray()));
                return;
            }

            OpenFan(unit, $"{skill.Name.ToUpperInvariant()}  ·  ESCOLHA O ALVO");
            PinTargets(targets);
            _controller.SetTargeting(unit, true, !skill.TargetsAllies);
            if (targets.Count > 0) _controller.PreviewTarget(targets[0]); // no celular não há "hover"
            foreach (var target in targets)
            {
                var t = target;
                string meta = skill.TargetsAllies ? $"PV {t.Hp} · SAN {t.Sanity}" : $"DEF {t.CurrentDefesa}";
                AddOption(t.Name, meta, () => Submit(BattleAction.UseSkill(unit, skill, t)), true, Describe(skill), null,
                    () => _controller.PreviewTarget(t), null); // o anel fica no último alvo em foco
            }
            AddBack(() => ShowCommands(unit));
            ShowHint(Describe(skill));
            LayoutFan();
        }

        private (string rules, string text) Describe(SkillData skill)
        {
            var def = skill.Tag as SkillDefinition;
            string rules = def != null ? def.RulesLabel() : skill.DiceLabel;
            return (rules, skill.Description ?? "");
        }

        private void Submit(BattleAction action)
        {
            _controller.PreviewTarget(null);
            HideMenus();
            _controller.SubmitPlayerAction(action);
        }

        private void AddBack(UnityEngine.Events.UnityAction onBack)
        {
            AddOption("Voltar", "", () =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioManager.Instance.uiBack, 0.7f, 0f);
                onBack();
            }, true, ("", ""), T.cendre, null, null, false);
        }

        private void AddOption(string label, string meta, UnityEngine.Events.UnityAction onClick, bool interactable,
            (string rules, string text) info, Color? accent = null, System.Action onEnter = null, System.Action onExit = null, bool click = true)
        {
            _options.Add(MakeOption(_fanLayer, label, meta, onClick, interactable, info, accent, onEnter, onExit, click));
        }

        private RectTransform MakeOption(Transform parent, string label, string meta, UnityEngine.Events.UnityAction onClick, bool interactable,
            (string rules, string text) info, Color? accent, System.Action onEnter, System.Action onExit, bool click, bool leftSide = false)
        {
            RectTransform self = null;
            var btn = Stroke(parent, label, meta, () =>
            {
                if (_closing) return;
                if (click && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioManager.Instance.uiClick, 0.7f, 0f);
                Choose(self, onClick);
            }, interactable, accent, leftSide);
            self = (RectTransform)btn.transform;
            self.gameObject.AddComponent<CanvasGroup>();
            var relay = btn.GetComponent<HoverRelay>();
            relay.OnEnter += () => { ShowHint(info); onEnter?.Invoke(); };
            relay.OnExit += () => onExit?.Invoke();
            return (RectTransform)btn.transform;
        }

        /// <summary>
        /// Opção em forma de pincelada: faixa escura que se dissolve, filete dourado embaixo,
        /// texto em Cinzel caixa alta e um losango no foco.
        /// </summary>
        private UnityEngine.UI.Button Stroke(Transform parent, string label, string meta, UnityEngine.Events.UnityAction onClick,
            bool interactable, Color? accent, bool leftSide = false)
        {
            var root = UIFactory.CreateRect("Opcao " + label, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(leftSide ? 1f : 0f, 0.5f);
            root.sizeDelta = new Vector2(optionWidth, optionHeight);
            root.localRotation = Quaternion.Euler(0, 0, leftSide ? -optionTilt : optionTilt);

            var bg = UIFactory.CreatePanel(root, "Pincelada", Color.white, true, UIFactory.FadeSprite(leftSide ? UIFactory.Fade.Left : UIFactory.Fade.Right));
            UIFactory.Stretch(bg.rectTransform);
            var colors = new UIFactory.ButtonColors
            {
                Normal = new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.88f),
                Highlighted = new Color(0.30f, 0.22f, 0.09f, 0.95f),
                Pressed = new Color(0.45f, 0.34f, 0.12f, 1f),
                Disabled = new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.55f)
            };
            var button = UIFactory.MakeButton(root.gameObject, bg, colors, onClick);
            button.interactable = interactable;

            Color gold = accent ?? T.dore;
            var stroke = UIFactory.CreateRule(root, "Filete", new Color(gold.r, gold.g, gold.b, 0.8f), 2f);
            stroke.sprite = UIFactory.FadeSprite(leftSide ? UIFactory.Fade.Left : UIFactory.Fade.Right);
            UIFactory.Place(stroke.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 1), new Vector2(0, 2));

            var marker = UIFactory.CreateDiamond(root, "Losango", gold, 9);
            UIFactory.Place(marker.rectTransform, new Vector2(leftSide ? 1 : 0, 0.5f), new Vector2(leftSide ? 1 : 0, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(leftSide ? -16 : 16, 0), new Vector2(9, 9));
            marker.color = new Color(gold.r, gold.g, gold.b, 0.35f);

            Color textColor = interactable ? T.toile : new Color(T.cendre.r, T.cendre.g, T.cendre.b, 0.5f);
            var text = UIFactory.CreateText(root, "Label", label.ToUpperInvariant(), T.DisplayRegular, 23, textColor,
                leftSide ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, 3);

            // Nome e valores (custo, Defesa...) em colunas separadas: a faixa cresce para caber os dois
            const float edge = 32f, gapBetween = 28f, farPad = 18f;
            float labelW = text.GetPreferredValues(text.text).x;
            float metaW = 0f;
            TextMeshProUGUI m = null;
            if (!string.IsNullOrEmpty(meta))
            {
                m = UIFactory.CreateText(root, "Meta", meta.ToUpperInvariant(), T.Label, 17, interactable ? gold : textColor,
                    leftSide ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, 2);
                metaW = m.GetPreferredValues(m.text).x + 4f;
            }
            float width = Mathf.Max(optionWidth, edge + labelW + (metaW > 0 ? gapBetween + metaW : 0f) + farPad);
            root.sizeDelta = new Vector2(width, optionHeight);

            float metaSpace = metaW > 0 ? metaW + gapBetween : 0f;
            UIFactory.Stretch(text.rectTransform, leftSide ? farPad + metaSpace : edge, 0, leftSide ? edge : farPad + metaSpace, 0);
            if (m != null)
            {
                var mr = m.rectTransform;
                mr.anchorMin = new Vector2(leftSide ? 0 : 1, 0);
                mr.anchorMax = new Vector2(leftSide ? 0 : 1, 1);
                mr.pivot = new Vector2(leftSide ? 0 : 1, 0.5f);
                mr.anchoredPosition = new Vector2(leftSide ? farPad : -farPad, 0);
                mr.sizeDelta = new Vector2(metaW, 0);
            }

            var relay = root.gameObject.AddComponent<HoverRelay>();
            relay.OnEnter += () =>
            {
                marker.color = gold;
                if (button.interactable) text.color = T.doreClaro;
            };
            relay.OnExit += () =>
            {
                marker.color = new Color(gold.r, gold.g, gold.b, 0.35f);
                text.color = textColor;
            };
            return button;
        }

        /// <summary>Posiciona o leque ao lado do personagem da vez e mantém tudo dentro da tela.</summary>
        private void LayoutFan()
        {
            if (_menuUnit == null) return;
            var view = _controller.ViewOf(_menuUnit);
            if (view == null || !ToCanvas(view.CenterPosition, out var anchor)) anchor = Vector2.zero;

            Vector2 half = _root.rect.size / 2f;
            int n = _options.Count;

            // Posições iniciais: diagonal descendo para a direita, centrada na altura do personagem
            var pos = new Vector2[n];
            float startY = anchor.y + (n - 1) * optionStep / 2f;
            for (int i = 0; i < n; i++)
                pos[i] = new Vector2(anchor.x + 90f + i * optionSlant, startY - i * optionStep);

            // Limites do bloco (inclui o título e a dica)
            float titleH = string.IsNullOrEmpty(_fanTitle.text) ? 0f : 34f;
            // Espaço da dica sempre reservado: se dependesse da dica estar visível, o menu "pularia"
            // ao passar o mouse em "Voltar" (que não tem dica) e ficaria piscando.
            float hintH = _hint.sizeDelta.y + HintGap;
            float blockTop = (n > 0 ? pos[0].y + optionHeight / 2f : anchor.y) + titleH;
            float blockBottom = (n > 0 ? pos[n - 1].y - optionHeight / 2f : anchor.y) - hintH;
            float widest = optionWidth;
            foreach (var o in _options) widest = Mathf.Max(widest, o.sizeDelta.x);
            float blockRight = (n > 0 ? pos[n - 1].x : anchor.x) + Mathf.Max(widest, _hint.sizeDelta.x + optionSlant);

            float dy = 0f, dx = 0f;
            if (blockRight > half.x - 20f) dx = (half.x - 20f) - blockRight;
            float maxTop = half.y - safeTop;
            // Não cobre as fichas do grupo no canto inferior direito
            float minBottom = -half.y + (blockRight + dx > half.x - 740f ? 215f : safeBottom);
            if (blockTop > maxTop) dy = maxTop - blockTop;
            if (blockBottom + dy < minBottom) dy = minBottom - blockBottom;

            for (int i = 0; i < n; i++) Animate(_options[i], i + (_leftOption != null ? 1 : 0), pos[i] + new Vector2(dx, dy), false);

            if (n > 0)
            {
                _fanTitle.rectTransform.anchoredPosition = pos[0] + new Vector2(dx, dy + optionHeight / 2f + 6f);
                _hint.anchoredPosition = pos[n - 1] + new Vector2(dx + optionSlant, dy - optionHeight / 2f - HintGap);
            }

            if (_leftOption != null)
            {
                var p = new Vector2(anchor.x - 70f, anchor.y - 10f);
                p.x = Mathf.Max(p.x, -half.x + optionWidth + 20f);
                p.y = Mathf.Clamp(p.y, minBottom + optionHeight, maxTop - optionHeight);
                Animate(_leftOption, 0, p, true);
            }

            // Título e dica acompanham a última opção a entrar
            float groupAlpha = FanAlpha(n + (_leftOption != null ? 1 : 0));
            _hintGroup.alpha = groupAlpha;
            _fanTitle.alpha = groupAlpha;
        }
        // ================================================================== Animação do menu

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private float FanAlpha(int index)
        {
            if (_closing) return 1f - EaseOutCubic((Time.unscaledTime - _closeStart) / CloseTime);
            return EaseOutCubic((Time.unscaledTime - _fanOpenedAt - index * Stagger) / AppearTime);
        }

        /// <summary>
        /// Entrada: cada faixa desliza a partir do personagem, em cascata.
        /// Escolha: a opção escolhida pulsa em dourado enquanto as outras deslizam e somem.
        /// </summary>
        private void Animate(RectTransform rt, int index, Vector2 finalPos, bool leftSide)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            float side = leftSide ? -1f : 1f;
            float alpha, offset, scale = 1f;

            if (_closing)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _closeStart) / CloseTime);
                if (rt == _chosen)
                {
                    scale = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
                    offset = 10f * EaseOutCubic(t);
                    alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                }
                else
                {
                    float e = t * t;
                    alpha = 1f - e;
                    offset = 36f * e;
                }
            }
            else
            {
                float t = (Time.unscaledTime - _fanOpenedAt - index * Stagger) / AppearTime;
                alpha = EaseOutCubic(t);
                offset = -70f * (1f - EaseOutBack(t));
            }

            if (cg != null)
            {
                cg.alpha = alpha;
                bool ready = !_closing && alpha > 0.85f;
                cg.interactable = ready;
                cg.blocksRaycasts = ready;
            }
            rt.anchoredPosition = finalPos + new Vector2(side * offset, 0f);
            rt.localScale = Vector3.one * scale;
        }

        private void Choose(RectTransform chosen, UnityEngine.Events.UnityAction action)
        {
            _closing = true;
            _closeStart = Time.unscaledTime;
            _chosen = chosen;
            if (chosen != null)
            {
                var label = chosen.Find("Label");
                if (label != null) label.GetComponent<TextMeshProUGUI>().color = T.doreClaro;
                var marker = chosen.Find("Losango");
                if (marker != null) marker.GetComponent<UnityEngine.UI.Image>().color = T.doreClaro;
            }
            StartCoroutine(AfterClose(action));
        }

        private IEnumerator AfterClose(UnityEngine.Events.UnityAction action)
        {
            yield return new WaitForSecondsRealtime(CloseTime + 0.03f);
            _closing = false;
            action?.Invoke();
        }
    }
}
