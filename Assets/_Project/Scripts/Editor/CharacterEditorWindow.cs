using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BelleEpoque.EditorTools
{
    /// <summary>
    /// Janela Belle Époque → Editor de Personagens.
    /// Escolha roupa, cabeça, cabelos, cores e animações de cada personagem, veja numa prévia 3D
    /// (arraste para girar, role para aproximar, ▶ para tocar uma animação) e clique em Montar.
    /// As escolhas ficam salvas em Assets/_Project/Data/Personagens_Receitas.asset.
    /// </summary>
    public class CharacterEditorWindow : EditorWindow
    {
        [MenuItem("Belle Époque/Editor de Personagens", priority = 29)]
        public static void Abrir()
        {
            var w = GetWindow<CharacterEditorWindow>("Personagens");
            w.minSize = new Vector2(940f, 600f);
        }

        private const string Nenhum = "(nenhum)";
        private const string Nenhuma = "(nenhuma)";

        private CharacterRecipeLibrary _lib;
        private int _sel;
        private Vector2 _scrollLista, _scrollForm;
        private string _status = "";

        // Catálogo (listas das opções)
        private string[] _roupas = new string[0], _corpos = new string[0], _cabelos = new string[0], _anims = new string[0];

        // Prévia
        private PreviewRenderUtility _pru;
        private GameObject _preview;
        private bool _previewSuja = true;
        private double _proximaMontagem;
        private readonly List<string> _avisosPrevia = new List<string>();
        private Vector3 _centro = new Vector3(0f, 0.9f, 0f);
        private float _altura = 1.8f;
        private float _yaw = 180f, _pitch = 6f, _zoom = 1f;

        // Animação da prévia
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private AnimationClip _clipAtual;
        private string _animTocando = "";
        private bool _animUmaVez;
        private float _animTempo;
        private double _ultimoTick;

        private CharacterRecipe Atual => _lib != null && _sel >= 0 && _sel < _lib.receitas.Count ? _lib.receitas[_sel] : null;

        // ================================================================== Ciclo de vida

        private void OnEnable()
        {
            _lib = CharacterRecipeLibrary.LoadOrCreate();
            AtualizarCatalogo();
            _previewSuja = true;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += AoDesfazer;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            Undo.undoRedoPerformed -= AoDesfazer;
            Salvar();
            DestruirPrevia();
            if (_pru != null) { _pru.Cleanup(); _pru = null; }
        }

        private void AoDesfazer()
        {
            _previewSuja = true;
            Repaint();
        }

        private void AtualizarCatalogo()
        {
            QuaterniusCharacters.LimparCache();
            _roupas = QuaterniusCharacters.Roupas().ToArray();
            _corpos = QuaterniusCharacters.Corpos().ToArray();
            _cabelos = QuaterniusCharacters.Cabelos().ToArray();
            _anims = QuaterniusCharacters.Animacoes().ToArray();
        }

        private void Salvar()
        {
            if (_lib == null) return;
            EditorUtility.SetDirty(_lib);
            AssetDatabase.SaveAssetIfDirty(_lib);
        }

        // ================================================================== Interface

        private void OnGUI()
        {
            if (_lib == null) _lib = CharacterRecipeLibrary.LoadOrCreate();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Saia do modo Play para editar os personagens.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DesenharLista();
            DesenharEditor();
            EditorGUILayout.EndHorizontal();
        }

        private void DesenharLista()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(190f));
            GUILayout.Label("PERSONAGENS", EditorStyles.boldLabel);
            _scrollLista = EditorGUILayout.BeginScrollView(_scrollLista, EditorStyles.helpBox);
            for (int i = 0; i < _lib.receitas.Count; i++)
            {
                var r = _lib.receitas[i];
                string rotulo = string.IsNullOrEmpty(r.nome) ? "(sem nome)" : r.nome.Replace('_', ' ');
                bool sel = GUILayout.Toggle(i == _sel, rotulo, "Button", GUILayout.Height(24f));
                if (sel && i != _sel)
                {
                    Salvar();
                    _sel = i;
                    _previewSuja = true;
                    _status = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Novo personagem"))
            {
                Undo.RecordObject(_lib, "Novo personagem");
                _lib.receitas.Add(new CharacterRecipe { nome = "Novo_Personagem_" + (_lib.receitas.Count + 1), cabelos = new List<string> { "Hair_SimpleParted.fbx" } });
                _sel = _lib.receitas.Count - 1;
                Mudou();
            }
            using (new EditorGUI.DisabledScope(Atual == null))
            {
                if (GUILayout.Button("Duplicar"))
                {
                    Undo.RecordObject(_lib, "Duplicar personagem");
                    var c = Atual.Clone();
                    c.nome += "_Copia";
                    c.ficha = null;
                    _lib.receitas.Insert(_sel + 1, c);
                    _sel++;
                    Mudou();
                }
                if (GUILayout.Button("Remover") &&
                    EditorUtility.DisplayDialog("Remover personagem", $"Remover \"{Atual.nome}\" da lista?\n(O prefab e a ficha não são apagados.)", "Remover", "Cancelar"))
                {
                    Undo.RecordObject(_lib, "Remover personagem");
                    _lib.receitas.RemoveAt(_sel);
                    _sel = Mathf.Clamp(_sel, 0, _lib.receitas.Count - 1);
                    Mudou();
                }
            }

            GUILayout.Space(6f);
            if (GUILayout.Button(new GUIContent("↻ Atualizar listas", "Use depois de colocar pacotes novos na pasta Art.")))
            {
                AtualizarCatalogo();
                _previewSuja = true;
            }
            EditorGUILayout.EndVertical();
        }

        private void DesenharEditor()
        {
            var r = Atual;
            EditorGUILayout.BeginVertical();
            if (r == null)
            {
                EditorGUILayout.HelpBox("Nenhum personagem. Clique em \"+ Novo personagem\".", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // --- Prévia 3D
            EditorGUILayout.BeginVertical(GUILayout.Width(330f));
            var rect = GUILayoutUtility.GetRect(330f, 440f, GUILayout.Width(330f), GUILayout.Height(440f));
            DesenharPrevia(rect);
            string dica = string.IsNullOrEmpty(_animTocando) ? "Arraste para girar · role para aproximar"
                : $"▶ {_animTocando}  {_animTempo:0.0}s / {(_clipAtual != null ? _clipAtual.length : 0f):0.0}s";
            GUILayout.Label(dica, EditorStyles.centeredGreyMiniLabel);
            foreach (var a in _avisosPrevia) EditorGUILayout.HelpBox(a, MessageType.Warning);
            EditorGUILayout.EndVertical();

            // --- Campos
            _scrollForm = EditorGUILayout.BeginScrollView(_scrollForm);
            Undo.RecordObject(_lib, "Editar personagem");
            EditorGUI.BeginChangeCheck();

            GUILayout.Label(r.nome.Replace('_', ' ').ToUpperInvariant(), EditorStyles.largeLabel);
            r.nome = EditorGUILayout.TextField("Nome", r.nome);
            r.ficha = (UnitDefinition)EditorGUILayout.ObjectField(new GUIContent("Ficha", "A ficha (stats e habilidades) que usa este modelo na batalha."), r.ficha, typeof(UnitDefinition), false);
            if (r.ficha == null)
            {
                EditorGUILayout.HelpBox("Sem ficha: o modelo é montado, mas não aparece na batalha.", MessageType.Info);
                if (GUILayout.Button("Criar ficha nova (ameaça)")) CriarFicha(r);
            }

            Titulo("Aparência");
            string roupaAntes = r.roupa;
            r.roupa = Popup("Roupa", r.roupa, _roupas, null, Bonito);
            if (r.roupa != roupaAntes) r.roupaTextura = "";

            var texturas = QuaterniusCharacters.TexturasDaRoupa(r.roupa).ToArray();
            if (texturas.Length > 0)
            {
                if (string.IsNullOrEmpty(r.roupaTextura) || !texturas.Contains(r.roupaTextura)) r.roupaTextura = texturas[0];
                r.roupaTextura = Popup("Cor da roupa", r.roupaTextura, texturas, null, NomeTextura);
                r.roupaTom = EditorGUILayout.ColorField(new GUIContent("Tom da roupa", "Tinge a roupa. Branco = cor original."), r.roupaTom);
            }
            else
            {
                r.corPrincipal = EditorGUILayout.ColorField("Cor principal", r.corPrincipal);
                r.corJuntas = EditorGUILayout.ColorField("Cor das juntas", r.corJuntas);
            }

            r.corpo = Popup("Cabeça", r.corpo, _corpos, Nenhuma, NomeCorpo);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(r.corpo)))
                r.olhosTom = EditorGUILayout.ColorField(new GUIContent("Cor dos olhos", "Tinge o olho castanho. Branco = original."), r.olhosTom);
            r.fantasma = EditorGUILayout.Toggle(new GUIContent("Fantasma", "Deixa o personagem translúcido e azulado."), r.fantasma);

            Titulo("Cabelos, barba e sobrancelhas");
            if (!string.IsNullOrEmpty(r.roupa) && r.roupa.Contains("Ranger") && r.cabelos.Count > 0)
                EditorGUILayout.HelpBox("Com capuz, o cabelo costuma atravessar o tecido.", MessageType.None);
            int remover = -1;
            for (int i = 0; i < r.cabelos.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                r.cabelos[i] = Popup("  " + (i + 1), r.cabelos[i], _cabelos, null, Bonito);
                if (GUILayout.Button("✕", GUILayout.Width(24f))) remover = i;
                EditorGUILayout.EndHorizontal();
            }
            if (remover >= 0) r.cabelos.RemoveAt(remover);
            if (GUILayout.Button("+ Cabelo", GUILayout.Width(90f)) && _cabelos.Length > 0) r.cabelos.Add(_cabelos[0]);

            Titulo("Animações");
            for (int i = 0; i < CharacterRecipe.Gatilhos.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string atual = r.GetAnim(i);
                r.SetAnim(i, Popup(CharacterRecipe.Rotulos[i], atual, _anims, Nenhuma, null));
                string anim = r.GetAnim(i);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(anim)))
                {
                    if (GUILayout.Button(new GUIContent("▶", "Tocar na prévia"), GUILayout.Width(28f)))
                        TocarAnimacao(anim, umaVez: i != 0);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField(" ", "Sem animação = o jogo usa o movimento automático.", EditorStyles.miniLabel);

            if (EditorGUI.EndChangeCheck()) Mudou();

            GUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Montar este personagem", GUILayout.Height(30f)))
            {
                if (QuaterniusCharacters.ForaDoPlay())
                {
                    Salvar();
                    _status = QuaterniusCharacters.MontarUm(r);
                    _previewSuja = true;
                }
            }
            if (GUILayout.Button("Montar todos", GUILayout.Height(30f)))
            {
                if (QuaterniusCharacters.ForaDoPlay())
                {
                    Salvar();
                    _status = QuaterniusCharacters.MontarTodos(_lib.receitas, mostrarJanela: false);
                    _previewSuja = true;
                }
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status.Trim() + "\nDê Play para ver na batalha.", _status.Contains("✗") ? MessageType.Warning : MessageType.Info);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void Mudou()
        {
            EditorUtility.SetDirty(_lib);
            _previewSuja = true;
            _proximaMontagem = EditorApplication.timeSinceStartup + 0.15; // espera parar de arrastar a cor
        }

        private static void Titulo(string texto)
        {
            GUILayout.Space(8f);
            GUILayout.Label(texto, EditorStyles.boldLabel);
        }

        /// <summary>Lista suspensa de strings (com opção "nenhum" opcional). Valor fora da lista aparece marcado.</summary>
        private static string Popup(string rotulo, string valor, string[] opcoes, string vazio, System.Func<string, string> exibir)
        {
            var lista = new List<string>();
            if (vazio != null) lista.Add("");
            lista.AddRange(opcoes);
            if (!string.IsNullOrEmpty(valor) && !lista.Contains(valor)) lista.Add(valor);
            int idx = Mathf.Max(0, lista.IndexOf(valor ?? ""));
            var nomes = lista.Select(v => string.IsNullOrEmpty(v) ? (vazio ?? Nenhum)
                : (!opcoes.Contains(v) ? "⚠ " : "") + (exibir != null ? exibir(v) : v)).ToArray();
            int novo = EditorGUILayout.Popup(rotulo, idx, nomes);
            return lista[novo];
        }

        private static string Bonito(string arquivo) => Path.GetFileNameWithoutExtension(arquivo).Replace('_', ' ');
        private static string NomeTextura(string arquivo) => Path.GetFileNameWithoutExtension(arquivo).Replace("T_", "").Replace("_BaseColor", "").Replace('_', ' ');
        private static string NomeCorpo(string arquivo) =>
            arquivo.Contains("Female") ? "Feminina" : arquivo.Contains("Male") ? "Masculina" : Bonito(arquivo);

        private void CriarFicha(CharacterRecipe r)
        {
            const string dir = "Assets/_Project/Data/Units";
            var ficha = CreateInstance<UnitDefinition>();
            ficha.displayName = r.nome.Replace('_', ' ');
            ficha.trilha = BelleEpoque.Core.Trilha.Ameaca;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/Inimigo_{QuaterniusCharacters.NomeArquivo(r.nome)}.asset");
            AssetDatabase.CreateAsset(ficha, path);
            AssetDatabase.SaveAssets();
            r.ficha = ficha;
            EditorGUIUtility.PingObject(ficha);
            EditorUtility.DisplayDialog("Ficha criada",
                $"Criei {Path.GetFileName(path)} em Data/Units.\n\nPreencha stats e habilidades no Inspector, e adicione a ficha ao Encontro (Data/Encontro_RueDesOmbres) para ela aparecer na batalha.",
                "OK");
        }

        // ================================================================== Prévia 3D

        private void GarantirPreviewUtility()
        {
            if (_pru != null) return;
            _pru = new PreviewRenderUtility();
            _pru.camera.fieldOfView = 30f;
            _pru.camera.nearClipPlane = 0.05f;
            _pru.camera.farClipPlane = 100f;
            _pru.camera.clearFlags = CameraClearFlags.SolidColor;
            _pru.camera.backgroundColor = new Color(0.13f, 0.13f, 0.16f);
            _pru.lights[0].intensity = 1.2f;
            _pru.lights[0].transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            _pru.lights[1].intensity = 0.7f;
            _pru.lights[1].transform.rotation = Quaternion.Euler(-20f, -30f, 0f);
            _pru.ambientColor = new Color(0.35f, 0.35f, 0.4f);
        }

        private void MontarPrevia()
        {
            _previewSuja = false;
            string animAntes = _animTocando;
            bool umaVezAntes = _animUmaVez;
            DestruirPrevia();
            _avisosPrevia.Clear();

            var r = Atual;
            if (r == null) return;
            GarantirPreviewUtility();

            try
            {
                QuaterniusCharacters.PrepararImportacao(new[] { r });
                var avisos = new List<string>();
                _preview = QuaterniusCharacters.MontarCorpo(r, persistir: false, avisos);
                _avisosPrevia.AddRange(avisos);
            }
            catch (System.Exception ex)
            {
                _avisosPrevia.Add("Erro na prévia: " + ex.Message);
                Debug.LogException(ex);
            }
            if (_preview == null) return;

            foreach (var t in _preview.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var smr in _preview.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;               // a prévia não tem "câmera principal": sem isso a malha não reanima
                smr.forceMatrixRecalculationPerRender = true;
            }
            _preview.transform.position = Vector3.zero;
            _preview.transform.rotation = Quaternion.identity;
            _pru.AddSingleGO(_preview);

            var renderers = _preview.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var rd in renderers) b.Encapsulate(rd.bounds);
                _centro = b.center;
                _altura = Mathf.Max(0.5f, b.size.y);
            }

            // Continua tocando a mesma animação (ex.: você mudou a cor com o ataque tocando)
            if (!string.IsNullOrEmpty(animAntes) && umaVezAntes) TocarAnimacao(animAntes, true);
            else TocarAnimacao(r.idle, false);
        }

        private void DestruirPrevia()
        {
            PararAnimacao();
            if (_preview != null) DestroyImmediate(_preview);
            _preview = null;
        }

        private void DesenharPrevia(Rect rect)
        {
            GarantirPreviewUtility();
            var e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag)
                {
                    _yaw += e.delta.x * 0.6f;
                    _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.3f, -30f, 60f);
                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.ScrollWheel)
                {
                    _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.5f, 3f);
                    e.Use();
                    Repaint();
                }
            }

            if (e.type != EventType.Repaint) return;
            if (_preview == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.16f));
                GUI.Label(rect, _previewSuja ? "Montando prévia..." : "Sem prévia", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Avaliar(); // aplica a pose do instante atual antes de desenhar
            _pru.BeginPreview(rect, GUIStyle.none);
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            float dist = _altura * 2.1f / _zoom;
            var cam = _pru.camera;
            cam.transform.rotation = rot;
            cam.transform.position = _centro - rot * Vector3.forward * dist;
            _pru.Render(true);
            var tex = _pru.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        // ================================================================== Animação da prévia

        private void TocarAnimacao(string nome, bool umaVez)
        {
            PararAnimacao();
            if (_preview == null || string.IsNullOrEmpty(nome)) return;
            var clip = QuaterniusCharacters.Clip(nome);
            var animator = _preview.GetComponent<Animator>();
            if (clip == null || animator == null || animator.avatar == null) return;

            _graph = PlayableGraph.Create("Prévia de personagem");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Animação", animator);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            output.SetSourcePlayable(_playable);
            _graph.Play();

            _clipAtual = clip;
            _animTocando = nome;
            _animUmaVez = umaVez;
            _animTempo = 0f;
            _ultimoTick = EditorApplication.timeSinceStartup;
            Avaliar();
        }

        private void PararAnimacao()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _clipAtual = null;
            _animTocando = "";
        }

        private void Avaliar()
        {
            if (!_graph.IsValid() || _clipAtual == null) return;
            float dur = Mathf.Max(0.01f, _clipAtual.length);
            float t = _animUmaVez ? Mathf.Min(_animTempo, dur) : _animTempo % dur;
            _playable.SetTime(t);
            _graph.Evaluate();
        }

        private void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (_previewSuja && EditorApplication.timeSinceStartup >= _proximaMontagem)
            {
                MontarPrevia();
                Repaint();
            }

            if (!_graph.IsValid() || _clipAtual == null) return;
            double agora = EditorApplication.timeSinceStartup;
            float dt = (float)(agora - _ultimoTick);
            _ultimoTick = agora;
            _animTempo += dt;

            float dur = Mathf.Max(0.01f, _clipAtual.length);
            // Toca uma vez, segura a pose final um pouco e volta para o "parado"
            if (_animUmaVez && _animTempo > dur + 0.8f) { var r = Atual; TocarAnimacao(r != null ? r.idle : "", false); }
            Repaint();
        }
    }
}
