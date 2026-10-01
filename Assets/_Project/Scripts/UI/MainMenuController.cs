using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BelleEpoque.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BelleEpoque
{
    /// <summary>
    /// Menu inicial: título, INICIAR / PERSONAGENS / SAIR e a tela de fichas dos agentes.
    /// Toda a interface é montada por código com o tema do jogo (mesmas fontes e cores da batalha).
    /// Os textos dos personagens vêm das fichas (UnitDefinition): mudou a ficha, mudou o menu.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UITheme theme;
        [Tooltip("Agentes mostrados na tela de Personagens (normalmente os heróis do encontro).")]
        [SerializeField] private List<UnitDefinition> agentes = new List<UnitDefinition>();
        [Tooltip("Nome da cena aberta pelo INICIAR.")]
        [SerializeField] private string cenaDaBatalha = "Battle";

        [Header("Textos")]
        [SerializeField] private string titulo = "BELLE ÉPOQUE";
        [SerializeField] private string sobretitulo = "ORDO REALITAS · PARIS, 1900";
        [SerializeField, TextArea] private string epigrafe = "“Quando a Cidade-Luz se apaga, o Outro Lado desperta.”";
        [SerializeField] private string rodape = "PROTÓTIPO · V0.1";

        private UITheme T => theme;
        private RectTransform _root;
        private CanvasGroup _telaInicial, _telaPersonagens, _cortina;
        private Image _lampiao;
        private TextMeshProUGUI _dica;
        private readonly List<Cinza> _cinzas = new List<Cinza>();
        private readonly List<GameObject> _botoesInicio = new List<GameObject>();
        private readonly List<Aba> _abas = new List<Aba>();
        private bool _ocupado;
        private int _agenteAtual = -1;

        // Ficha (lado direito da tela de personagens)
        private RectTransform _fichaRoot;
        private CanvasGroup _fichaGroup;
        private Coroutine _fichaAnim;

        private sealed class Cinza { public RectTransform Rt; public Image Img; public float Vel, Fase, Amp, Alpha; }
        private sealed class Aba { public RectTransform Root; public Image Fundo, Aro; public TextMeshProUGUI Nome, Papel; }

        // ================================================================== Montagem

        private void Start()
        {
            if (theme == null) { Debug.LogError("[MainMenu] Falta o UITheme."); return; }
            Time.timeScale = 1f;
            _root = (RectTransform)transform;
            agentes = agentes.Where(a => a != null).ToList();

            ConstruirFundo();
            _telaInicial = ConstruirTelaInicial();
            _telaPersonagens = ConstruirTelaPersonagens();
            _telaPersonagens.gameObject.SetActive(false);

            // Cortina preta por cima de tudo (entrada e saída)
            var cortina = UIFactory.CreatePanel(_root, "Cortina", Color.black, true);
            UIFactory.Stretch(cortina.rectTransform);
            _cortina = cortina.gameObject.AddComponent<CanvasGroup>();
            _cortina.alpha = 1f;

            StartCoroutine(Entrada());
        }

        private void ConstruirFundo()
        {
            var fundo = UIFactory.CreatePanel(_root, "Fundo", T.nuit);
            UIFactory.Stretch(fundo.rectTransform);

            // Luz de lampião no alto à esquerda, tremulando
            _lampiao = UIFactory.CreatePanel(_root, "Lampiao", new Color(T.dore.r, T.dore.g, T.dore.b, 0.16f), false, RadialSprite());
            UIFactory.Place(_lampiao.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(420, -260), new Vector2(1500, 1500));

            // Brasa avermelhada no canto oposto (um toque de terror)
            var brasa = UIFactory.CreatePanel(_root, "Brasa", new Color(T.carmin.r, T.carmin.g, T.carmin.b, 0.07f), false, RadialSprite());
            UIFactory.Place(brasa.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-200, 120), new Vector2(1300, 1300));

            // Cinzas subindo devagar
            for (int i = 0; i < 46; i++)
            {
                float size = Random.Range(2.5f, 6.5f);
                var img = UIFactory.CreateDiamond(_root, "Cinza", T.toile, size);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(0f, 1080f));
                _cinzas.Add(new Cinza
                {
                    Rt = rt, Img = img, Vel = Random.Range(12f, 38f), Fase = Random.Range(0f, 10f),
                    Amp = Random.Range(10f, 40f), Alpha = Random.Range(0.05f, 0.22f),
                });
            }

            // Vinheta: bordas escurecidas
            Vinheta(UIFactory.Fade.Up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 360));
            Vinheta(UIFactory.Fade.Down, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 240));
            Vinheta(UIFactory.Fade.Left, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(520, 0));
        }

        private void Vinheta(UIFactory.Fade dir, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
        {
            var v = UIFactory.CreatePanel(_root, "Vinheta", new Color(0, 0, 0, 0.75f), false, UIFactory.FadeSprite(dir));
            UIFactory.Place(v.rectTransform, aMin, aMax, pivot, Vector2.zero, size);
        }

        private CanvasGroup ConstruirTelaInicial()
        {
            var tela = UIFactory.CreateRect("TelaInicial", _root);
            UIFactory.Stretch(tela);
            var group = tela.gameObject.AddComponent<CanvasGroup>();
            const float x = 170f;

            // Filete vertical à esquerda da coluna
            var filete = UIFactory.CreatePanel(tela, "FileteVertical", new Color(T.dore.r, T.dore.g, T.dore.b, 0.5f), false, UIFactory.FadeSprite(UIFactory.Fade.Down));
            UIFactory.Place(filete.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(x - 46, 0), new Vector2(1.5f, -260));

            var sobre = Texto(tela, "Sobretitulo", sobretitulo, T.Label, 26, T.dore, TextAlignmentOptions.Left, 10);
            UIFactory.Place(sobre.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 330), new Vector2(1200, 40));

            var tit = Texto(tela, "Titulo", titulo, T.Display, 132, T.toile, TextAlignmentOptions.Left, 6);
            tit.overflowMode = TextOverflowModes.Overflow;
            UIFactory.Place(tit.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x - 6, 235), new Vector2(1500, 150));

            var linha = UIFactory.CreateRule(tela, "Filete", T.dore, 1.5f);
            linha.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            UIFactory.Place(linha.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + 26, 152), new Vector2(620, 1.5f));
            UIFactory.Place(UIFactory.CreateDiamond(tela, "Losango", T.dore, 11).rectTransform,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x + 6, 152), new Vector2(11, 11));

            var epi = Texto(tela, "Epigrafe", epigrafe, T.Italic, 32, T.cendre, TextAlignmentOptions.Left);
            UIFactory.Place(epi.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 100), new Vector2(1300, 50));

            // Opções
            var opcoes = new (string rotulo, string dica, UnityEngine.Events.UnityAction acao)[]
            {
                ("INICIAR", "Descer à Rue des Ombres. A investigação começa ao anoitecer.", Iniciar),
                ("PERSONAGENS", "Conheça os agentes da Ordem: quem são e do que são capazes.", () => StartCoroutine(Trocar(_telaInicial, _telaPersonagens, AoAbrirPersonagens))),
                ("SAIR", "Fechar o jogo.", Sair),
            };
            for (int i = 0; i < opcoes.Length; i++)
            {
                var b = Opcao(tela, opcoes[i].rotulo, opcoes[i].dica, opcoes[i].acao, 46);
                UIFactory.Place((RectTransform)b.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(x, -20 - i * 84), new Vector2(560, 70));
                _botoesInicio.Add(b.gameObject);
            }

            _dica = Texto(tela, "Dica", "", T.Italic, 26, new Color(T.cendre.r, T.cendre.g, T.cendre.b, 0.9f), TextAlignmentOptions.Left);
            UIFactory.Place(_dica.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, -290), new Vector2(1100, 40));

            var rod = Texto(tela, "Rodape", rodape, T.Label, 20, new Color(T.cendre.r, T.cendre.g, T.cendre.b, 0.6f), TextAlignmentOptions.Left, 6);
            UIFactory.Place(rod.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 44), new Vector2(600, 30));
            return group;
        }

        /// <summary>Opção de menu: texto grande, losango e filete dourado que aparecem no foco.</summary>
        private Button Opcao(Transform parent, string rotulo, string dica, UnityEngine.Events.UnityAction acao, float tamanho)
        {
            var root = UIFactory.CreateRect("Opcao " + rotulo, parent);
            var hit = UIFactory.CreatePanel(root, "Area", new Color(1, 1, 1, 0.001f), true);
            UIFactory.Stretch(hit.rectTransform);

            var texto = Texto(root, "Texto", rotulo, T.DisplayRegular, tamanho, T.toile, TextAlignmentOptions.Left, 8);
            UIFactory.Stretch(texto.rectTransform);

            var losango = UIFactory.CreateDiamond(root, "Losango", T.doreClaro, 10);
            UIFactory.Place(losango.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24, 0), new Vector2(10, 10));
            losango.color = new Color(T.doreClaro.r, T.doreClaro.g, T.doreClaro.b, 0f);

            var filete = UIFactory.CreateRule(root, "Filete", T.dore, 1.5f);
            filete.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            UIFactory.Place(filete.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(0, 4), new Vector2(0, 1.5f));

            var cores = new UIFactory.ButtonColors { Normal = Color.white, Highlighted = Color.white, Pressed = Color.white, Disabled = Color.white };
            var botao = UIFactory.MakeButton(root.gameObject, hit, cores, () => { Som(true); acao(); });

            float foco = 0f;
            Coroutine anim = null;
            var relay = root.gameObject.AddComponent<HoverRelay>();
            IEnumerator Animar(float alvo)
            {
                while (!Mathf.Approximately(foco, alvo))
                {
                    foco = Mathf.MoveTowards(foco, alvo, Time.unscaledDeltaTime * 6f);
                    float e = Mathf.SmoothStep(0f, 1f, foco);
                    texto.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(0f, 18f, e), 0f);
                    texto.color = Color.Lerp(T.toile, T.doreClaro, e);
                    losango.color = new Color(T.doreClaro.r, T.doreClaro.g, T.doreClaro.b, e);
                    filete.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 460f, e), 1.5f);
                    yield return null;
                }
            }
            relay.OnEnter = () =>
            {
                if (anim != null) StopCoroutine(anim);
                anim = StartCoroutine(Animar(1f));
                if (_dica != null && !string.IsNullOrEmpty(dica)) _dica.text = dica;
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != root.gameObject)
                    EventSystem.current.SetSelectedGameObject(root.gameObject);
            };
            relay.OnExit = () =>
            {
                if (anim != null) StopCoroutine(anim);
                if (isActiveAndEnabled) anim = StartCoroutine(Animar(0f));
            };
            return botao;
        }

        // ================================================================== Tela de personagens

        private CanvasGroup ConstruirTelaPersonagens()
        {
            var tela = UIFactory.CreateRect("TelaPersonagens", _root);
            UIFactory.Stretch(tela);
            var group = tela.gameObject.AddComponent<CanvasGroup>();
            const float x = 170f;

            var sobre = Texto(tela, "Sobretitulo", "FICHAS DA ORDO REALITAS", T.Label, 26, T.dore, TextAlignmentOptions.Left, 10);
            UIFactory.Place(sobre.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -96), new Vector2(900, 40));
            var tit = Texto(tela, "Titulo", "OS AGENTES", T.Display, 66, T.toile, TextAlignmentOptions.Left, 6);
            UIFactory.Place(tit.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x - 4, -134), new Vector2(520, 90));
            var linha = UIFactory.CreateRule(tela, "Filete", T.dore, 1.5f);
            linha.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            UIFactory.Place(linha.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(x, -240), new Vector2(460, 1.5f));

            // Abas (um por agente)
            for (int i = 0; i < agentes.Count; i++)
            {
                var aba = CriarAba(tela, agentes[i], i);
                UIFactory.Place(aba.Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -290 - i * 120), new Vector2(470, 104));
                _abas.Add(aba);
            }

            // Ficha
            var painel = UIFactory.CreatePanel(tela, "Ficha", new Color(T.nuit2.r, T.nuit2.g, T.nuit2.b, 0.82f), true);
            UIFactory.Place(painel.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            painel.rectTransform.offsetMin = new Vector2(720, 64);
            painel.rectTransform.offsetMax = new Vector2(-140, -72);
            var topo = UIFactory.CreateRule(painel.transform, "FileteTopo", T.dore, 2f);
            topo.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            UIFactory.Place(topo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 2));
            _fichaRoot = UIFactory.CreateRect("Conteudo", painel.transform);
            UIFactory.Stretch(_fichaRoot, 52, 30, 52, 36);
            _fichaGroup = _fichaRoot.gameObject.AddComponent<CanvasGroup>();

            // Voltar
            var voltar = Opcao(tela, "VOLTAR", "", () => StartCoroutine(Trocar(_telaPersonagens, _telaInicial, AoVoltarInicio)), 30);
            UIFactory.Place((RectTransform)voltar.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 70), new Vector2(300, 50));
            var esc = Texto(tela, "Atalho", "ESC", T.Label, 18, new Color(T.cendre.r, T.cendre.g, T.cendre.b, 0.6f), TextAlignmentOptions.Left, 4);
            UIFactory.Place(esc.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x + 190, 82), new Vector2(80, 26));
            return group;
        }

        private Aba CriarAba(Transform parent, UnitDefinition def, int indice)
        {
            var aba = new Aba { Root = UIFactory.CreateRect("Agente " + def.displayName, parent) };
            aba.Fundo = UIFactory.CreatePanel(aba.Root, "Fundo", new Color(T.dore.r, T.dore.g, T.dore.b, 0f), true, UIFactory.FadeSprite(UIFactory.Fade.Right));
            UIFactory.Stretch(aba.Fundo.rectTransform);

            const float p = 76f;
            var disco = UIFactory.CreatePanel(aba.Root, "Retrato", T.nuit2, false, UIFactory.CircleSprite(false));
            UIFactory.Place(disco.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(p, p));
            if (def.portrait != null)
            {
                var arte = UIFactory.CreatePanel(disco.transform, "Arte", Color.white, false, def.portrait);
                UIFactory.Stretch(arte.rectTransform, 6f);
            }
            else
            {
                var ini = Texto(disco.transform, "Inicial", Inicial(def.displayName), T.Display, 36, T.toile, TextAlignmentOptions.Center);
                UIFactory.Stretch(ini.rectTransform);
            }
            aba.Aro = UIFactory.CreatePanel(disco.transform, "Aro", T.dore, false, UIFactory.CircleSprite(true));
            UIFactory.Stretch(aba.Aro.rectTransform);

            aba.Nome = Texto(aba.Root, "Nome", def.displayName, T.Display, 32, T.toile, TextAlignmentOptions.BottomLeft);
            UIFactory.Place(aba.Nome.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(p + 32, 2), new Vector2(-(p + 40), 42));
            aba.Papel = Texto(aba.Root, "Papel", Papel(def).ToUpperInvariant(), T.Label, 22, T.cendre, TextAlignmentOptions.TopLeft, 3);
            UIFactory.Place(aba.Papel.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(p + 32, -4), new Vector2(-(p + 40), 30));

            var cores = new UIFactory.ButtonColors { Normal = Color.white, Highlighted = Color.white, Pressed = Color.white, Disabled = Color.white };
            UIFactory.MakeButton(aba.Root.gameObject, aba.Fundo, cores, () => { Som(true); MostrarAgente(indice); });
            var relay = aba.Root.gameObject.AddComponent<HoverRelay>();
            relay.OnEnter = () =>
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != aba.Root.gameObject)
                    EventSystem.current.SetSelectedGameObject(aba.Root.gameObject);
                MostrarAgente(indice);
            };
            return aba;
        }

        private void MostrarAgente(int indice)
        {
            if (indice == _agenteAtual || indice < 0 || indice >= _abas.Count) return;
            _agenteAtual = indice;
            for (int i = 0; i < _abas.Count; i++)
            {
                bool sel = i == indice;
                _abas[i].Fundo.color = new Color(T.dore.r, T.dore.g, T.dore.b, sel ? 0.16f : 0f);
                _abas[i].Aro.color = sel ? T.doreClaro : new Color(T.dore.r, T.dore.g, T.dore.b, 0.55f);
                _abas[i].Nome.color = sel ? T.doreClaro : T.toile;
            }
            if (_fichaAnim != null) StopCoroutine(_fichaAnim);
            _fichaAnim = StartCoroutine(TrocarFicha(agentes[indice]));
        }

        private IEnumerator TrocarFicha(UnitDefinition def)
        {
            for (float t = _fichaGroup.alpha; t > 0f; t -= Time.unscaledDeltaTime * 8f) { _fichaGroup.alpha = t; yield return null; }
            _fichaGroup.alpha = 0f;
            ConstruirFicha(def);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 5f)
            {
                _fichaGroup.alpha = t;
                _fichaRoot.anchoredPosition = new Vector2(Mathf.Lerp(18f, 0f, Mathf.SmoothStep(0f, 1f, t)), 0f);
                yield return null;
            }
            _fichaGroup.alpha = 1f;
            _fichaRoot.anchoredPosition = Vector2.zero;
        }

        private void ConstruirFicha(UnitDefinition def)
        {
            for (int i = _fichaRoot.childCount - 1; i >= 0; i--) Destroy(_fichaRoot.GetChild(i).gameObject);
            var s = def.BuildStats();
            float y = 0f;

            var nome = Texto(_fichaRoot, "Nome", def.displayName, T.Display, 56, T.toile, TextAlignmentOptions.TopLeft, 3);
            Topo(nome.rectTransform, y, 70); y -= 66;
            string linhaPapel = def.IsAgent ? $"{Papel(def).ToUpperInvariant()}  ·  NEX {def.nex}%" : "AMEAÇA";
            var papel = Texto(_fichaRoot, "Papel", linhaPapel, T.Label, 28, T.dore, TextAlignmentOptions.TopLeft, 6);
            Topo(papel.rectTransform, y, 34); y -= 46;

            var lore = Texto(_fichaRoot, "Historia", string.IsNullOrWhiteSpace(def.lore) ? "Sem registros." : def.lore, T.Italic, 28, T.toile, TextAlignmentOptions.TopLeft);
            lore.textWrappingMode = TextWrappingModes.Normal;
            lore.overflowMode = TextOverflowModes.Overflow;
            Canvas.ForceUpdateCanvases();
            float largura = Mathf.Max(400f, _fichaRoot.rect.width);
            float alturaLore = Mathf.Max(36f, lore.GetPreferredValues(lore.text, largura, 0f).y);
            Topo(lore.rectTransform, y, alturaLore); y -= alturaLore + 18f;

            // Atributos
            Secao("ATRIBUTOS", ref y);
            var nomes = new[] { "AGILIDADE", "FORÇA", "INTELECTO", "PRESENÇA", "VIGOR" };
            var valores = new[] { def.atributos.Agi, def.atributos.For, def.atributos.Int, def.atributos.Pre, def.atributos.Vig };
            for (int i = 0; i < 5; i++)
            {
                var caixa = UIFactory.CreatePanel(_fichaRoot, "Atributo", new Color(T.nuit.r, T.nuit.g, T.nuit.b, 0.7f));
                var rt = caixa.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(i * 176f, y);
                rt.sizeDelta = new Vector2(164f, 92f);
                var borda = UIFactory.CreateRule(caixa.transform, "Filete", new Color(T.dore.r, T.dore.g, T.dore.b, 0.6f), 1.5f);
                UIFactory.Place(borda.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1.5f));
                var v = Texto(caixa.transform, "Valor", valores[i].ToString(), T.Label, 52, T.toile, TextAlignmentOptions.Center);
                UIFactory.Place(v.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(0, 56));
                var n = Texto(caixa.transform, "Nome", nomes[i], T.Label, 20, T.cendre, TextAlignmentOptions.Center, 3);
                UIFactory.Place(n.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(0, 26));
            }
            y -= 108;

            // Recursos
            Secao("RECURSOS", ref y);
            var recursos = new List<(string rot, string val, Color cor)>
            {
                ("PV", s.MaxHp.ToString(), T.pv), ("PE", s.MaxPe.ToString(), T.pe),
            };
            if (s.MaxSanity > 0) recursos.Add(("SAN", s.MaxSanity.ToString(), T.san));
            recursos.Add(("DEFESA", s.Defesa.ToString(), T.toile));
            if (def.IsAgent) recursos.Add(("PE / RODADA", s.PePorRodada.ToString(), T.cendre));
            float rx = 0f;
            foreach (var (rot, val, cor) in recursos)
            {
                var r = Texto(_fichaRoot, rot, $"<size=60%><color={UITheme.ToHex(cor)}>{rot}</color></size>  {val}", T.Label, 40, T.toile, TextAlignmentOptions.TopLeft, 2);
                r.overflowMode = TextOverflowModes.Overflow;
                r.rectTransform.anchorMin = r.rectTransform.anchorMax = new Vector2(0, 1);
                r.rectTransform.pivot = new Vector2(0, 1);
                r.rectTransform.anchoredPosition = new Vector2(rx, y);
                r.rectTransform.sizeDelta = new Vector2(200, 46);
                rx += Mathf.Max(150f, r.GetPreferredValues().x + 44f);
            }
            y -= 56;

            // Vulnerabilidades e resistências
            var tags = new List<string>();
            if (def.weaknesses.Count > 0) tags.Add($"<color={UITheme.ToHex(T.carmin)}>VULNERÁVEL A {string.Join(", ", def.weaknesses.Select(e => SkillDefinition.ElementName(e).ToUpperInvariant()))}</color>");
            if (def.resistances.Count > 0) tags.Add($"RESISTE A {string.Join(", ", def.resistances.Select(e => SkillDefinition.ElementName(e).ToUpperInvariant()))}");
            if (tags.Count > 0)
            {
                var t = Texto(_fichaRoot, "Afinidades", string.Join("   ·   ", tags), T.Label, 22, T.cendre, TextAlignmentOptions.TopLeft, 3);
                Topo(t.rectTransform, y, 30); y -= 38;
            }

            // Habilidades
            Secao("HABILIDADES", ref y);
            var habilidades = new List<SkillDefinition>();
            if (def.basicAttack != null) habilidades.Add(def.basicAttack);
            habilidades.AddRange(def.skills.Where(k => k != null));
            foreach (var h in habilidades.Take(5))
            {
                string custo = h.CostLabel();
                var nome2 = Texto(_fichaRoot, "Habilidade", h.displayName + (string.IsNullOrEmpty(custo) ? "" : $"  <size=70%><color={UITheme.ToHex(T.dore)}>{custo}</color></size>"),
                    T.DisplayRegular, 25, T.toile, TextAlignmentOptions.TopLeft, 1);
                Topo(nome2.rectTransform, y, 32); y -= 29;
                string desc = string.IsNullOrWhiteSpace(h.description) ? h.RulesLabel() : h.description;
                var d = Texto(_fichaRoot, "Descricao", desc, T.Italic, 22, T.cendre, TextAlignmentOptions.TopLeft);
                d.textWrappingMode = TextWrappingModes.Normal;
                float alturaDesc = Mathf.Max(26f, d.GetPreferredValues(desc, largura, 0f).y);
                Topo(d.rectTransform, y, alturaDesc); y -= alturaDesc + 8f;
            }
        }

        private void Secao(string titulo, ref float y)
        {
            var t = Texto(_fichaRoot, "Secao", titulo, T.Label, 24, T.dore, TextAlignmentOptions.TopLeft, 8);
            Topo(t.rectTransform, y, 30);
            var r = UIFactory.CreateRule(_fichaRoot, "Filete", new Color(T.dore.r, T.dore.g, T.dore.b, 0.35f), 1f);
            r.sprite = UIFactory.FadeSprite(UIFactory.Fade.Right);
            r.rectTransform.anchorMin = new Vector2(0, 1);
            r.rectTransform.anchorMax = new Vector2(1, 1);
            r.rectTransform.pivot = new Vector2(0, 1);
            r.rectTransform.anchoredPosition = new Vector2(t.GetPreferredValues().x + 16f, y - 13f);
            r.rectTransform.sizeDelta = new Vector2(-(t.GetPreferredValues().x + 16f), 1f);
            y -= 38;
        }

        /// <summary>Ancora no topo-esquerdo da ficha, ocupando a largura toda.</summary>
        private static void Topo(RectTransform rt, float y, float altura)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(0, altura);
        }

        /// <summary>
        /// Igual ao UIFactory.CreateText, mas sem o corte "…": as fontes do tema são mais altas que o tamanho nominal
        /// e, com o corte ligado, o texto inteiro some quando a caixa fica um pouco baixa.
        /// </summary>
        private static TextMeshProUGUI Texto(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, float spacing = 0f)
        {
            var t = UIFactory.CreateText(parent, name, text, font, size, color, alignment, spacing);
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static string Papel(UnitDefinition def) =>
            !string.IsNullOrWhiteSpace(def.roleName) ? def.roleName : UnitDefinition.TrilhaName(def.trilha);

        private static string Inicial(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return "?";
            var partes = nome.Split(' ');
            string p = partes.Length > 1 && partes[0].Length <= 5 && partes[0].EndsWith("ã") ? partes[1] : partes[0];
            return p.Substring(0, 1).ToUpperInvariant();
        }

        // ================================================================== Navegação e transições

        private void AoAbrirPersonagens()
        {
            if (_abas.Count == 0) return;
            _agenteAtual = -1;
            MostrarAgente(0);
            Selecionar(_abas[0].Root.gameObject);
        }

        private void AoVoltarInicio()
        {
            Selecionar(_botoesInicio.Count > 1 ? _botoesInicio[1] : null);
        }

        private IEnumerator Entrada()
        {
            _ocupado = true;
            _telaInicial.alpha = 0f;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * 1.2f) { _cortina.alpha = t; yield return null; }
            _cortina.alpha = 0f;
            _cortina.blocksRaycasts = false;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 1.1f) { _telaInicial.alpha = Mathf.SmoothStep(0f, 1f, t); yield return null; }
            _telaInicial.alpha = 1f;
            _ocupado = false;
            if (_botoesInicio.Count > 0) Selecionar(_botoesInicio[0]);
        }

        private IEnumerator Trocar(CanvasGroup de, CanvasGroup para, System.Action depois)
        {
            if (_ocupado) yield break;
            _ocupado = true;
            de.interactable = false;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime * 4f) { de.alpha = t; yield return null; }
            de.alpha = 0f;
            de.gameObject.SetActive(false);

            para.gameObject.SetActive(true);
            para.alpha = 0f;
            para.interactable = true;
            depois?.Invoke();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 4f) { para.alpha = Mathf.SmoothStep(0f, 1f, t); yield return null; }
            para.alpha = 1f;
            _ocupado = false;
        }

        private void Iniciar()
        {
            if (_ocupado) return;
            StartCoroutine(Sair(() => SceneManager.LoadScene(cenaDaBatalha)));
        }

        private void Sair()
        {
            if (_ocupado) return;
            StartCoroutine(Sair(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }));
        }

        private IEnumerator Sair(System.Action depois)
        {
            _ocupado = true;
            _cortina.blocksRaycasts = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 1.6f) { _cortina.alpha = t; yield return null; }
            _cortina.alpha = 1f;
            depois();
        }

        private static void Selecionar(GameObject go)
        {
            if (go != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
        }

        private static void Som(bool clique)
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.PlaySfx(clique ? AudioManager.Instance.uiClick : AudioManager.Instance.uiBack, 0.7f, 0f);
        }

        // ================================================================== Por quadro

        private void Update()
        {
            if (_root == null) return;
            float t = Time.unscaledTime;

            // Lampião tremulando
            if (_lampiao != null)
            {
                float f = 0.13f + 0.05f * Mathf.PerlinNoise(t * 1.7f, 0.3f) + 0.02f * Mathf.PerlinNoise(t * 9f, 4.1f);
                _lampiao.color = new Color(T.dore.r, T.dore.g, T.dore.b, f);
            }

            // Cinzas subindo com balanço
            foreach (var c in _cinzas)
            {
                var p = c.Rt.anchoredPosition;
                p.y += c.Vel * Time.unscaledDeltaTime;
                p.x += Mathf.Sin(t * 0.6f + c.Fase) * c.Amp * Time.unscaledDeltaTime;
                if (p.y > 1110f) { p.y = -20f; p.x = Random.Range(0f, 1920f); }
                c.Rt.anchoredPosition = p;
                float brilho = c.Alpha * (0.6f + 0.4f * Mathf.Sin(t * 1.3f + c.Fase * 3f));
                c.Img.color = new Color(T.toile.r, T.toile.g, T.toile.b, brilho);
            }

            // ESC / botão B volta da tela de personagens
            if (!_ocupado && _telaPersonagens != null && _telaPersonagens.gameObject.activeSelf && VoltarPressionado())
            {
                Som(false);
                StartCoroutine(Trocar(_telaPersonagens, _telaInicial, AoVoltarInicio));
            }
        }

        private static bool VoltarPressionado()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)) return true;
            var g = UnityEngine.InputSystem.Gamepad.current;
            return g != null && g.buttonEast.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
#endif
        }

        // ================================================================== Sprite radial (luz)

        private static Sprite _radial;

        private static Sprite RadialSprite()
        {
            if (_radial != null) return _radial;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIRadial" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            tex.Apply();
            _radial = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _radial;
        }
    }
}
