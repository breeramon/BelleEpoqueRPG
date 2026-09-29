using TMPro;
using UnityEngine;

/// <summary>
/// Mostra o FPS (quadros por segundo) e o tempo de cada quadro no canto da tela.
///
/// Como usar:
///  - F3 (no PC) ou toque com 3 dedos (no celular) mostra/esconde o contador.
/// </summary>
public class ContadorDeFPS : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Texto onde o FPS aparece. Pode ficar vazio: o script cria um.")]
    [SerializeField] private TextMeshProUGUI textoDoContador;
    [Tooltip("Canvas onde o texto fica. Se vazio, procura um objeto chamado 'TextoDoContadorDeFPS' ou 'Canvas'.")]
    [SerializeField] private Canvas canvasDoContador;

    [Header("Medição")]
    [Tooltip("De quanto em quanto tempo (segundos) o número é atualizado. Média do período: evita número pulando.")]
    [SerializeField] private float intervaloDeAtualizacao = 0.5f;

    [Header("Exibição")]
    [SerializeField] private bool comecarVisivel = true;
    [SerializeField] private bool mostrarMilissegundos = true;
    [Tooltip("Acima disso fica verde.")]
    [SerializeField] private int fpsBom = 55;
    [Tooltip("Acima disso fica amarelo; abaixo, vermelho.")]
    [SerializeField] private int fpsAceitavel = 30;
    [SerializeField] private Color corBoa = new Color(0.55f, 0.9f, 0.55f);
    [SerializeField] private Color corAceitavel = new Color(0.95f, 0.8f, 0.35f);
    [SerializeField] private Color corRuim = new Color(0.95f, 0.35f, 0.3f);
    [Tooltip("Opcional: fonte do contador. Vazio = usa a Bebas Neue da HUD, se já existir.")]
    [SerializeField] private TMP_FontAsset fonte;

    private float quantidadeDeFPS;
    private float tempoAcumulado;
    private int quadrosContados;
    private bool visivel;
    private bool tocouComTresDedos;
    private bool fonteAplicada;

    public float FpsAtual => quantidadeDeFPS;

    private void Start()
    {
        PrepararTexto();
        visivel = comecarVisivel;
        AplicarVisibilidade();
        if (textoDoContador != null) textoDoContador.text = "-- FPS";
    }

    private void Update()
    {
        VerificarAtalho();
        ContarFpsDoJogo();
    }

    /// <summary>
    /// Soma o tempo real de cada quadro e, a cada intervalo, calcula a média:
    /// FPS = quadros / tempo. Usa unscaledDeltaTime para não ser afetado por pausa ou câmera lenta.
    /// </summary>
    private void ContarFpsDoJogo()
    {
        tempoAcumulado += Time.unscaledDeltaTime;
        quadrosContados++;

        if (tempoAcumulado < intervaloDeAtualizacao) return;

        quantidadeDeFPS = quadrosContados / tempoAcumulado;
        float milissegundos = tempoAcumulado / quadrosContados * 1000f;
        tempoAcumulado = 0f;
        quadrosContados = 0;

        if (textoDoContador == null || !visivel) return;
        AplicarFonte();

        int fps = Mathf.RoundToInt(quantidadeDeFPS);
        textoDoContador.text = mostrarMilissegundos
            ? $"{fps} FPS  <size=70%>{milissegundos:0.0} ms</size>"
            : $"{fps} FPS";
        textoDoContador.color = fps >= fpsBom ? corBoa : fps >= fpsAceitavel ? corAceitavel : corRuim;
    }

    private void VerificarAtalho()
    {
        bool alternar = false;

#if ENABLE_INPUT_SYSTEM
        var teclado = UnityEngine.InputSystem.Keyboard.current;
        if (teclado != null && teclado.f3Key.wasPressedThisFrame) alternar = true;

        var toque = UnityEngine.InputSystem.Touchscreen.current;
        if (toque != null)
        {
            int dedos = 0;
            foreach (var t in toque.touches) if (t.isInProgress) dedos++;
            if (dedos >= 3 && !tocouComTresDedos) alternar = true;
            tocouComTresDedos = dedos >= 3;
        }
#else
        if (Input.GetKeyDown(KeyCode.F3)) alternar = true;
        if (Input.touchCount >= 3 && !tocouComTresDedos) alternar = true;
        tocouComTresDedos = Input.touchCount >= 3;
#endif

        if (alternar) Alternar();
    }

    /// <summary>Mostra ou esconde o contador (pode ser ligado a um botão de opções).</summary>
    public void Alternar()
    {
        visivel = !visivel;
        AplicarVisibilidade();
    }

    private void AplicarVisibilidade()
    {
        if (textoDoContador != null) textoDoContador.gameObject.SetActive(visivel);
    }

    // ------------------------------------------------------------------ Montagem do texto

    private void PrepararTexto()
    {
        if (canvasDoContador == null) canvasDoContador = AcharCanvas();

        if (textoDoContador == null && canvasDoContador != null)
            textoDoContador = canvasDoContador.GetComponentInChildren<TextMeshProUGUI>(true);

        if (canvasDoContador == null)
        {
            var go = new GameObject("TextoDoContadorDeFPS", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvasDoContador = go.GetComponent<Canvas>();
            canvasDoContador.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Sempre por cima da HUD da batalha, e sem bloquear cliques
        canvasDoContador.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasDoContador.overrideSorting = true;
        canvasDoContador.sortingOrder = 100;
        var raycaster = canvasDoContador.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
        if (canvasDoContador.transform.localScale == Vector3.zero) canvasDoContador.transform.localScale = Vector3.one;

        if (textoDoContador == null) textoDoContador = CriarTexto(canvasDoContador.transform);

        textoDoContador.raycastTarget = false;
        textoDoContador.textWrappingMode = TextWrappingModes.NoWrap;
        AplicarFonte();
    }

    /// <summary>A fonte da HUD só existe depois que a batalha monta a interface; tenta até conseguir.</summary>
    private void AplicarFonte()
    {
        if (fonteAplicada || textoDoContador == null) return;
        var fonteUsada = fonte != null ? fonte : BelleEpoque.DamagePopup.Font;
        if (fonteUsada == null) return;
        textoDoContador.font = fonteUsada;
        fonteAplicada = true;
    }

    private Canvas AcharCanvas()
    {
        foreach (var nome in new[] { "TextoDoContadorDeFPS", "Canvas" })
        {
            var go = GameObject.Find(nome);
            if (go != null && go.TryGetComponent(out Canvas c)) return c;
        }
        return null;
    }

    private static TextMeshProUGUI CriarTexto(Transform canvas)
    {
        var go = new GameObject("TextoFPS", typeof(RectTransform));
        go.layer = 5; // UI
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        // Canto superior direito (o topo central é da ordem de turnos; o esquerdo, do Registro)
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -20f);
        rt.sizeDelta = new Vector2(260f, 40f);

        var texto = go.AddComponent<TextMeshProUGUI>();
        texto.fontSize = 24;
        texto.characterSpacing = 3;
        texto.alignment = TextAlignmentOptions.Right;
        return texto;
    }
}
