using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BelleEpoque.EditorTools
{
    /// <summary>
    /// Monta personagens a partir dos pacotes grátis do Quaternius:
    ///  - Roupas: "Modular Character Outfits - Fantasy" (vêm SEM cabeça);
    ///  - Cabeça, olhos e sobrancelhas: "Universal Base Characters" (só a cabeça é aproveitada do corpo);
    ///  - Cabelos: "Hairstyles / Rigged to Head Bone";
    ///  - Animações: "Universal Animation Library" 1 e 2.
    ///
    /// As escolhas de cada personagem ficam em Data/Personagens_Receitas.asset e são editadas pela janela
    /// Belle Époque → Editor de Personagens. Para cada personagem esta classe cria materiais URP,
    /// um Animator Controller com os gatilhos que o jogo usa (Attack, Cast, Heal, Hit, Defend, Item, Die),
    /// salva um prefab e liga na ficha (UnitDefinition).
    /// </summary>
    public static class QuaterniusCharacters
    {
        private const string ArtDir = "Assets/_Project/Art";
        private const string OutMaterials = ArtDir + "/Materials/Personagens";
        private const string OutMeshes = ArtDir + "/Models/_Gerados";
        private const string OutControllers = ArtDir + "/Animations/Controllers";
        private const string OutPrefabs = "Assets/_Project/Prefabs/Personagens";
        private const string UnitsDir = "Assets/_Project/Data/Units";
        private static readonly string[] BibliotecasDeAnimacao = { "UAL1_Standard.fbx", "UAL2_Standard.fbx" };

        // Ossos cuja malha é aproveitada do corpo base (o resto fica escondido pela roupa)
        private static readonly HashSet<string> HeadBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Head", "neck_01" };

        // ================================================================== Receitas iniciais

        /// <summary>Os 6 personagens do exemplo (usados só para criar o arquivo de receitas na primeira vez).</summary>
        public static List<CharacterRecipe> ReceitasPadrao()
        {
            UnitDefinition Ficha(string f) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"{UnitsDir}/{f}.asset");
            return new List<CharacterRecipe>
            {
                new CharacterRecipe
                {
                    nome = "Lucien_Duval", ficha = Ficha("Heroi_LucienDuval"),
                    roupa = "Male_Peasant.fbx", corpo = "Superhero_Male_FullBody.fbx",
                    cabelos = new List<string> { "Hair_SimpleParted.fbx", "Hair_Beard.fbx" },
                    roupaTextura = "T_Peasant_2_BaseColor.png", roupaTom = new Color(0.82f, 0.78f, 0.78f),
                    idle = "Sword_Idle", attack = "Sword_Attack", cast = "Spell_Simple_Shoot", heal = "Spell_Simple_Enter",
                    hit = "Hit_Chest", defend = "Sword_Block", item = "Consume", die = "Death01",
                },
                new CharacterRecipe
                {
                    nome = "Margot_Verlaine", ficha = Ficha("Heroi_MargotVerlaine"),
                    roupa = "Female_Peasant.fbx", corpo = "Superhero_Female_FullBody.fbx",
                    cabelos = new List<string> { "Hair_Long.fbx" },
                    roupaTextura = "T_Peasant_BaseColor.png", roupaTom = new Color(0.78f, 0.7f, 0.88f),
                    idle = "Spell_Simple_Idle_Loop", attack = "Sword_Regular_B", cast = "Spell_Simple_Shoot", heal = "Spell_Simple_Enter",
                    hit = "Hit_Head", defend = "Sword_Block", item = "Consume", die = "Death01",
                },
                new CharacterRecipe
                {
                    // O capuz do Ranger vira o hábito da freira
                    nome = "Irma_Celeste", ficha = Ficha("Heroi_IrmaCeleste"),
                    roupa = "Female_Ranger.fbx", corpo = "Superhero_Female_FullBody.fbx",
                    roupaTextura = "T_Ranger_BaseColor.png", roupaTom = new Color(0.55f, 0.55f, 0.62f),
                    idle = "Idle_Lantern_Loop", attack = "Melee_Hook", cast = "Spell_Simple_Shoot", heal = "Spell_Simple_Enter",
                    hit = "Hit_Chest", defend = "Sword_Block", item = "Consume", die = "Death01",
                },
                new CharacterRecipe
                {
                    nome = "Cultista_Encapuzado", ficha = Ficha("Inimigo_CultistaEncapuzado"),
                    roupa = "Male_Ranger.fbx", corpo = "Superhero_Male_FullBody.fbx",
                    roupaTextura = "T_Ranger_3_BaseColor.png", roupaTom = new Color(0.8f, 0.4f, 0.4f),
                    idle = "Idle_Loop", attack = "Sword_Regular_A", cast = "Spell_Simple_Shoot", heal = "Spell_Simple_Enter",
                    hit = "Hit_Chest", defend = "Sword_Block", item = "", die = "Death01",
                },
                new CharacterRecipe
                {
                    // O manequim que vem com a biblioteca de animações 2, pintado de porcelana
                    nome = "Manequim_Rastejante", ficha = Ficha("Inimigo_ManequimRastejante"),
                    roupa = "Mannequin_F.fbx", corpo = "",
                    idle = "Zombie_Idle_Loop", attack = "Zombie_Scratch", cast = "Zombie_Scratch", heal = "",
                    hit = "Hit_Chest", defend = "", item = "", die = "Death01",
                },
                new CharacterRecipe
                {
                    nome = "Aparicao_do_Teatro", ficha = Ficha("Inimigo_AparicaoDoTeatro"),
                    roupa = "Female_Ranger.fbx", corpo = "Superhero_Female_FullBody.fbx",
                    cabelos = new List<string> { "Hair_Long.fbx" },
                    roupaTextura = "T_Ranger_BaseColor.png", fantasma = true,
                    idle = "Zombie_Idle_Loop", attack = "Zombie_Scratch", cast = "Spell_Simple_Shoot", heal = "",
                    hit = "Hit_Head", defend = "", item = "", die = "Death01",
                },
            };
        }

        // ================================================================== Menu e montagem completa

        [MenuItem("Belle Époque/4. Montar personagens (Quaternius)", priority = 30)]
        public static void Montar()
        {
            if (!ForaDoPlay()) return;
            var lib = CharacterRecipeLibrary.LoadOrCreate();
            MontarTodos(lib.receitas, mostrarJanela: true);
        }

        public static string MontarTodos(IList<CharacterRecipe> receitas, bool mostrarJanela)
        {
            var log = new StringBuilder();
            try
            {
                EditorUtility.DisplayProgressBar("Belle Époque", "Configurando importação dos modelos...", 0.1f);
                PrepararPastas();
                PrepararImportacao(receitas, log, incluirNormais: true);
                AssetDatabase.Refresh();
                _clips = null;

                for (int i = 0; i < receitas.Count; i++)
                {
                    var r = receitas[i];
                    EditorUtility.DisplayProgressBar("Belle Époque", "Montando " + r.nome + "...", 0.3f + 0.7f * i / receitas.Count);
                    log.AppendLine(MontarPrefab(r));
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[Belle Époque] Personagens:\n" + log);
            if (mostrarJanela) EditorUtility.DisplayDialog("Belle Époque — Personagens", log + "\nDê Play para ver na batalha.", "OK");
            return log.ToString();
        }

        /// <summary>Monta um personagem só (usado pelo botão da janela). Retorna a linha de resultado.</summary>
        public static string MontarUm(CharacterRecipe r)
        {
            string resultado;
            try
            {
                EditorUtility.DisplayProgressBar("Belle Époque", "Montando " + r.nome + "...", 0.5f);
                PrepararPastas();
                var log = new StringBuilder();
                PrepararImportacao(new[] { r }, log, incluirNormais: true);
                resultado = log + MontarPrefab(r);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[Belle Époque] " + resultado);
            return resultado;
        }

        public static bool ForaDoPlay()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("Belle Époque", "Saia do modo Play (botão ▶) antes de montar personagens.", "OK");
            return false;
        }

        private static void PrepararPastas()
        {
            foreach (var dir in new[] { OutMaterials, OutMeshes, OutControllers, OutPrefabs }) EnsureFolder(dir);
        }

        /// <summary>Nome seguro para arquivo.</summary>
        public static string NomeArquivo(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "Personagem";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(nome.Trim().Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        }

        // ================================================================== Importação

        /// <summary>Deixa os FBX usados pelas receitas (e as bibliotecas de animação) com a configuração certa.</summary>
        public static void PrepararImportacao(IEnumerable<CharacterRecipe> receitas, StringBuilder log = null, bool incluirNormais = false)
        {
            var corpos = new HashSet<string>();   // roupas e corpos: esqueleto Humanoid
            var cabelos = new HashSet<string>();  // cabelos: só a malha presa nos ossos (Generic)
            foreach (var r in receitas)
            {
                if (!string.IsNullOrEmpty(r.roupa)) corpos.Add(r.roupa);
                if (!string.IsNullOrEmpty(r.corpo)) corpos.Add(r.corpo);
                foreach (var c in r.cabelos) if (!string.IsNullOrEmpty(c)) cabelos.Add(c);
            }

            foreach (var file in corpos.Concat(cabelos.Except(corpos)))
            {
                var path = FindModel(file);
                if (path == null) { log?.AppendLine("! Não encontrei " + file); continue; }
                SetupModel(path, animacoes: false, legivel: file.Contains("FullBody"), humanoide: !cabelos.Contains(file));
            }

            foreach (var file in BibliotecasDeAnimacao)
            {
                var path = Find(file, "/Unity/");
                if (path == null) log?.AppendLine("! Não encontrei a biblioteca de animações " + file);
                else if (SetupModel(path, animacoes: true, legivel: false, humanoide: true)) _clips = null;
            }

            if (!incluirNormais) return;
            // Mapas de normal precisam ser marcados como "Normal map"
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D _Normal", new[] { ArtDir + "/Models" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(p).EndsWith("_Normal")) continue;
                if (AssetImporter.GetAtPath(p) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
        }

        /// <summary>Configuração recomendada pelo Quaternius: Bake Axis Conversion + rig Humanoid. Retorna true se reimportou.</summary>
        private static bool SetupModel(string path, bool animacoes, bool legivel, bool humanoide)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) return false;
            bool reimportou = false;
            bool changed = false;

            // 1ª etapa: eixos, esqueleto e tipo de rig. Precisa reimportar antes de escrever o mapa Humanoid,
            // senão a pose de repouso gravada no mapa fica diferente do arquivo ("Bone length ... does not match").
            if (!imp.bakeAxisConversion) { imp.bakeAxisConversion = true; changed = true; }
            // "Strip Bones" desligado: sem isso o Unity apaga ossos sem malha (ex.: a cabeça numa roupa sem cabeça),
            // e cada FBX fica com um esqueleto diferente.
            if (imp.optimizeBones) { imp.optimizeBones = false; changed = true; }
            if (humanoide && imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (!humanoide && imp.animationType != ModelImporterAnimationType.Generic)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (changed)
            {
                imp.SaveAndReimport();
                reimportou = true;
                imp = (ModelImporter)AssetImporter.GetAtPath(path);
                changed = false;
            }

            // 2ª etapa: mapa dos ossos e demais opções
            if (humanoide && MapearOssos(imp, path)) changed = true;
            if (imp.importAnimation != animacoes) { imp.importAnimation = animacoes; changed = true; }
            if (legivel && !imp.isReadable) { imp.isReadable = true; changed = true; }

            if (animacoes)
            {
                // Liga o "Loop Time" nas animações que repetem (as que terminam em _Loop e as de espera)
                var clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    bool loop = IsLoop(c.name);
                    if (c.loopTime != loop) { c.loopTime = loop; changed = true; }

                    // "Bake Into Pose" (Based Upon: Original) em rotação, altura e posição: o movimento do corpo fica
                    // dentro da animação. Sem isso, a descida da morte vira "root motion", é descartada
                    // (o jogo não usa root motion) e o personagem morre flutuando.
                    if (!c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ ||
                        !c.keepOriginalOrientation || !c.keepOriginalPositionY || !c.keepOriginalPositionXZ)
                    {
                        c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                        c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                        changed = true;
                    }
                }
                if (changed || imp.clipAnimations == null || imp.clipAnimations.Length == 0)
                {
                    imp.clipAnimations = clips;
                    changed = true;
                }
            }

            if (changed) imp.SaveAndReimport();
            return changed || reimportou;
        }

        // ------------------------------------------------------------------ Mapa Humanoid
        // A detecção automática do Unity erra neste esqueleto (não achou a cabeça numa roupa e trocou os dedos do pé).
        // Todos os FBX do Quaternius usam os mesmos nomes de ossos, então o mapa é escrito à mão.

        private static readonly (string humano, string osso)[] MapaOssos = BuildMapa();

        private static (string, string)[] BuildMapa()
        {
            var m = new List<(string, string)>
            {
                ("Hips", "pelvis"), ("Spine", "spine_01"), ("Chest", "spine_02"), ("UpperChest", "spine_03"),
                ("Neck", "neck_01"), ("Head", "Head"),
            };
            foreach (var (lado, s) in new[] { ("Left", "l"), ("Right", "r") })
            {
                m.Add((lado + "Shoulder", "clavicle_" + s));
                m.Add((lado + "UpperArm", "upperarm_" + s));
                m.Add((lado + "LowerArm", "lowerarm_" + s));
                m.Add((lado + "Hand", "hand_" + s));
                m.Add((lado + "UpperLeg", "thigh_" + s));
                m.Add((lado + "LowerLeg", "calf_" + s));
                m.Add((lado + "Foot", "foot_" + s));
                m.Add((lado + "Toes", "ball_" + s));
                foreach (var (dedo, osso) in new[] { ("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "pinky") })
                {
                    m.Add(($"{lado} {dedo} Proximal", $"{osso}_01_{s}"));
                    m.Add(($"{lado} {dedo} Intermediate", $"{osso}_02_{s}"));
                    m.Add(($"{lado} {dedo} Distal", $"{osso}_03_{s}"));
                }
            }
            return m.ToArray();
        }

        /// <summary>Escreve o mapa Humanoid no importador. Retorna true se mudou algo.</summary>
        private static bool MapearOssos(ModelImporter imp, string path)
        {
            var hd = imp.humanDescription;
            var atual = hd.human ?? new HumanBone[0];
            bool igual = atual.Length == MapaOssos.Length &&
                         MapaOssos.All(p => atual.Any(h => h.humanName == p.humano && h.boneName == p.osso));
            if (igual && hd.skeleton != null && hd.skeleton.Length > 0) return false;

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (modelo == null) return false;
            var transforms = modelo.GetComponentsInChildren<Transform>(true);
            var nomes = new HashSet<string>(transforms.Select(t => t.name));

            hd.human = MapaOssos
                .Where(p => nomes.Contains(p.osso))
                .Select(p => new HumanBone { humanName = p.humano, boneName = p.osso, limit = new HumanLimit { useDefaultValues = true } })
                .ToArray();
            // Mantém a pose de repouso que o Unity já gerou; só cria se ainda não existir
            if (hd.skeleton == null || hd.skeleton.Length == 0)
            {
                hd.skeleton = transforms
                    .Select(t => new SkeletonBone
                    {
                        name = t == modelo.transform ? t.name + "(Clone)" : t.name, // o Unity nomeia a raiz assim
                        position = t.localPosition, rotation = t.localRotation, scale = t.localScale,
                    })
                    .ToArray();
            }
            imp.humanDescription = hd;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            return true;
        }

        private static bool IsLoop(string clipName)
        {
            string n = Suffix(clipName);
            return n.EndsWith("_Loop") || n == "Sword_Idle";
        }

        // ================================================================== Montagem

        /// <summary>Monta o personagem, salva o prefab e liga na ficha. Retorna a linha de resultado.</summary>
        private static string MontarPrefab(CharacterRecipe r)
        {
            string nome = NomeArquivo(r.nome);
            var avisos = new List<string>();
            GameObject root = null;
            try
            {
                // Apaga o prefab e o controller antigos antes de recriar: o Unity às vezes não regrava um prefab existente
                // e ele continua apontando para os arquivos da montagem anterior.
                var prefabPath = $"{OutPrefabs}/{nome}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);

                root = MontarCorpo(r, persistir: true, avisos);
                if (root == null) return $"✗ {r.nome}: {string.Join("; ", avisos)}";

                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = BuildController(r, nome, avisos);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                if (r.ficha != null)
                {
                    r.ficha.modelPrefab = prefab;
                    r.ficha.modelScale = 1f;
                    EditorUtility.SetDirty(r.ficha);
                }
                else avisos.Add("sem ficha ligada (o prefab foi salvo, mas não aparece na batalha)");

                return avisos.Count == 0 ? $"✓ {r.nome}" : $"✓ {r.nome} — {string.Join("; ", avisos)}";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return $"✗ {r.nome}: erro ({ex.Message}). Detalhes no Console.";
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Monta o corpo do personagem (roupa + cabeça + cabelos + materiais + Animator com avatar) numa cena.
        /// persistir = false é usado pela prévia da janela: materiais e malhas ficam só na memória.
        /// Quem chama é dono do objeto e deve destruí-lo.
        /// </summary>
        public static GameObject MontarCorpo(CharacterRecipe r, bool persistir, List<string> avisos)
        {
            var roupaPath = string.IsNullOrEmpty(r.roupa) ? null : FindModel(r.roupa);
            if (roupaPath == null) { avisos.Add("falta o arquivo da roupa " + r.roupa); return null; }
            var roupaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(roupaPath);
            if (roupaAsset == null) { avisos.Add("não consegui abrir " + r.roupa); return null; }

            var root = UnityEngine.Object.Instantiate(roupaAsset);
            root.name = NomeArquivo(r.nome);

            var bones = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            // Materiais da roupa
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.sharedMaterials = smr.sharedMaterials.Select(m => MaterialFor(m, r, persistir)).ToArray();

            // Cabeça, olhos e sobrancelhas do corpo base
            if (!string.IsNullOrEmpty(r.corpo))
            {
                var corpoPath = FindModel(r.corpo);
                var corpo = corpoPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(corpoPath) : null;
                if (corpo == null) avisos.Add("sem cabeça (falta " + r.corpo + ")");
                else
                {
                    foreach (var smr in corpo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        bool isBody = smr.sharedMaterials.Any(m => m != null && m.name.IndexOf("Superhero", StringComparison.OrdinalIgnoreCase) >= 0);
                        Mesh mesh = isBody ? HeadOnly(smr, Path.GetFileNameWithoutExtension(corpoPath), persistir) : null;
                        Stitch(root, bones, smr, mesh, smr.sharedMaterials.Select(m => MaterialFor(m, r, persistir)).ToArray(), isBody ? "Cabeca" : null);
                    }
                }
            }

            // Cabelos
            foreach (var cabelo in r.cabelos)
            {
                if (string.IsNullOrEmpty(cabelo)) continue;
                var path = FindModel(cabelo);
                var asset = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                if (asset == null) { avisos.Add("sem " + cabelo); continue; }
                foreach (var smr in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Stitch(root, bones, smr, null, smr.sharedMaterials.Select(m => MaterialFor(m, r, persistir)).ToArray(), null);
            }

            // Animator com o esqueleto Humanoid
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            if (animator.avatar == null)
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(roupaPath).OfType<Avatar>().FirstOrDefault();
            if (animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
            {
                // O Unity às vezes não reconhece o esqueleto de um FBX. As roupas do Quaternius usam
                // exatamente os mesmos ossos, então dá para emprestar o "Humanoid" de outra.
                var emprestado = AvatarEmprestado(r.roupa);
                if (emprestado != null) animator.avatar = emprestado;
                else avisos.Add("o esqueleto não virou Humanoid (veja o FBX em Rig)");
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return root;
        }

        /// <summary>Procura um Avatar Humanoid válido num FBX com o mesmo esqueleto (mesmo gênero primeiro).</summary>
        private static Avatar AvatarEmprestado(string roupa)
        {
            bool feminino = roupa.StartsWith("Female", StringComparison.OrdinalIgnoreCase);
            var candidatos = feminino
                ? new[] { "Female_Ranger.fbx", "Female_Peasant.fbx", "Superhero_Female_FullBody.fbx", "Male_Peasant.fbx", "Male_Ranger.fbx" }
                : new[] { "Male_Peasant.fbx", "Male_Ranger.fbx", "Superhero_Male_FullBody.fbx", "Female_Ranger.fbx", "Female_Peasant.fbx" };
            foreach (var file in candidatos)
            {
                if (string.Equals(file, roupa, StringComparison.OrdinalIgnoreCase)) continue;
                var path = FindModel(file);
                if (path == null) continue;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
                if (avatar != null) return avatar;
            }
            return null;
        }

        /// <summary>
        /// Copia uma malha com esqueleto (cabelo, cabeça...) para dentro do personagem,
        /// religando cada osso pelo nome ao esqueleto da roupa.
        /// </summary>
        private static void Stitch(GameObject root, Dictionary<string, Transform> bones, SkinnedMeshRenderer src, Mesh meshOverride, Material[] mats, string name)
        {
            var go = new GameObject(name ?? src.name);
            go.transform.SetParent(root.transform, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = meshOverride != null ? meshOverride : src.sharedMesh;
            r.bones = src.bones.Select(b => b != null && bones.TryGetValue(b.name, out var t) ? t : root.transform).ToArray();
            r.rootBone = src.rootBone != null && bones.TryGetValue(src.rootBone.name, out var rb) ? rb : null;
            r.localBounds = src.localBounds;
            r.sharedMaterials = mats;
        }

        private static readonly Dictionary<string, Mesh> _headsTemp = new Dictionary<string, Mesh>();

        /// <summary>
        /// Cópia da malha do corpo só com a cabeça e o pescoço
        /// (o fabricante recomenda não usar o corpo inteiro por baixo da roupa: ele atravessa o tecido).
        /// Salva em Models/_Gerados e reaproveita, porque vários personagens usam a mesma cabeça.
        /// </summary>
        private static Mesh HeadOnly(SkinnedMeshRenderer smr, string corpoNome, bool persistir)
        {
            string nome = corpoNome + "_SoCabeca";
            string path = $"{OutMeshes}/{nome}.asset";
            var salva = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (salva != null) return salva;
            if (!persistir && _headsTemp.TryGetValue(corpoNome, out var temp) && temp != null) return temp;

            var src = smr.sharedMesh;
            var keep = new HashSet<int>();
            for (int i = 0; i < smr.bones.Length; i++)
                if (smr.bones[i] != null && HeadBones.Contains(smr.bones[i].name)) keep.Add(i);

            var weights = src.boneWeights;
            float W(int v)
            {
                var w = weights[v];
                float s = 0f;
                if (keep.Contains(w.boneIndex0)) s += w.weight0;
                if (keep.Contains(w.boneIndex1)) s += w.weight1;
                if (keep.Contains(w.boneIndex2)) s += w.weight2;
                if (keep.Contains(w.boneIndex3)) s += w.weight3;
                return s;
            }

            var mesh = UnityEngine.Object.Instantiate(src);
            mesh.name = nome;
            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                var tris = src.GetTriangles(sub);
                var kept = new List<int>(tris.Length / 4);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    if (W(tris[t]) >= 0.5f && W(tris[t + 1]) >= 0.5f && W(tris[t + 2]) >= 0.5f)
                    {
                        kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                    }
                }
                mesh.SetTriangles(kept, sub);
            }
            mesh.RecalculateBounds();

            if (persistir)
            {
                EnsureFolder(OutMeshes);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                mesh.hideFlags = HideFlags.HideAndDontSave;
                _headsTemp[corpoNome] = mesh;
            }
            return mesh;
        }

        // ================================================================== Materiais

        /// <summary>"Male_Peasant.fbx" → "Peasant" (nome usado nas texturas e materiais da roupa).</summary>
        public static string ChaveDaRoupa(string roupa)
        {
            var n = Path.GetFileNameWithoutExtension(roupa ?? "");
            foreach (var prefixo in new[] { "Male_", "Female_" })
                if (n.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) return n.Substring(prefixo.Length);
            return n;
        }

        private static Material MaterialFor(Material src, CharacterRecipe r, bool persistir)
        {
            if (src == null) return null;
            string n = src.name;
            bool Has(string s) => n.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
            string nome = NomeArquivo(r.nome);

            if (r.fantasma) return Fantasma(persistir);

            string chave = ChaveDaRoupa(r.roupa);
            if ((chave.Length > 0 && Has(chave)) || Has("Peasant") || Has("Ranger"))
            {
                string tipo = Has("Peasant") ? "Peasant" : Has("Ranger") ? "Ranger" : chave;
                string textura = string.IsNullOrEmpty(r.roupaTextura) ? TexturasDaRoupa(r.roupa).FirstOrDefault() : r.roupaTextura;
                return Lit($"M_{nome}_Roupa", textura, $"T_{tipo}_Normal.png", r.roupaTom, 0.15f, persistir);
            }
            if (Has("Regular_Male")) return Lit("M_Pele_Braco_M", "T_Regular_Male_Dark_BaseColor.png", "T_Regular_Male_Normal.png", Color.white, 0.3f, persistir);
            if (Has("Regular_Female")) return Lit("M_Pele_Braco_F", "T_Regular_Female_Dark_BaseColor.png", "T_Regular_Female_Normal.png", Color.white, 0.3f, persistir);
            if (Has("Superhero_Male")) return Lit("M_Pele_Cabeca_M", "T_Superhero_Male_Dark.png", "T_Superhero_Male_Normal.png", Color.white, 0.3f, persistir);
            if (Has("Superhero_Female")) return Lit("M_Pele_Cabeca_F", "T_Superhero_Female_Dark_BaseColor.png", "T_Superhero_Female_Normal.png", Color.white, 0.3f, persistir);
            if (Has("Eye")) return Lit($"M_{nome}_Olhos", "T_Eye_Brown.png", "T_Eye_Normal.png", r.olhosTom, 0.8f, persistir);
            if (Has("Hair_1")) return Lit("M_Cabelo_1", "T_Hair_1_BaseColor.png", "T_Hair_1_Normal.png", Color.white, 0.25f, persistir, cabelo: true);
            if (Has("Hair_2")) return Lit("M_Cabelo_2", "T_Hair_2_BaseColor.png", "T_Hair_2_Normal.png", Color.white, 0.25f, persistir, cabelo: true);
            if (Has("M_Main")) return Lit($"M_{nome}_Principal", null, null, r.corPrincipal, 0.7f, persistir);
            if (Has("M_Joints")) return Lit($"M_{nome}_Juntas", null, null, r.corJuntas, 0.3f, persistir);
            return src;
        }

        private static Material Lit(string name, string baseTex, string normalTex, Color tint, float smoothness, bool persistir, bool cabelo = false)
        {
            var m = LoadOrCreateMaterial(name, persistir);
            var baseMap = baseTex != null ? FindTexture(baseTex) : null;
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smoothness);

            var normal = normalTex != null ? FindTexture(normalTex) : null;
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }

            bool clip = cabelo && baseMap != null && TextureHasAlpha(baseMap);
            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            m.SetFloat("_Cutoff", 0.5f);
            if (clip) { m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetOverrideTag("RenderType", "TransparentCutout"); }
            else { m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; m.SetOverrideTag("RenderType", "Opaque"); }
            m.SetFloat("_Cull", cabelo ? 0f : 2f); // cabelo: as duas faces
            if (persistir) EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Material translúcido e levemente brilhante para a Aparição.</summary>
        private static Material Fantasma(bool persistir)
        {
            var m = LoadOrCreateMaterial("M_Fantasma", persistir);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", new Color(0.72f, 0.82f, 1f, 0.38f));
            m.SetFloat("_Smoothness", 0.6f);
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", 5f);  // SrcAlpha
            m.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
            m.SetFloat("_SrcBlendAlpha", 1f);
            m.SetFloat("_DstBlendAlpha", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.22f, 0.32f, 0.55f));
            if (persistir) EditorUtility.SetDirty(m);
            return m;
        }

        private static readonly Dictionary<string, Material> _materiaisTemp = new Dictionary<string, Material>();

        private static Material LoadOrCreateMaterial(string name, bool persistir)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!persistir)
            {
                // Prévia: material só na memória (não mexe nos arquivos até você clicar em Montar)
                if (_materiaisTemp.TryGetValue(name, out var t) && t != null) return t;
                t = new Material(shader) { name = name + " (prévia)", hideFlags = HideFlags.HideAndDontSave };
                _materiaisTemp[name] = t;
                return t;
            }

            var path = $"{OutMaterials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            EnsureFolder(OutMaterials);
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static bool TextureHasAlpha(Texture2D tex)
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            return ti != null && ti.DoesSourceTextureHaveAlpha();
        }

        // ================================================================== Animator

        private static AnimatorController BuildController(CharacterRecipe r, string nome, List<string> avisos)
        {
            var path = $"{OutControllers}/{nome}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;

            AnimatorState idle = null;
            float y = 0f;
            for (int i = 0; i < CharacterRecipe.Gatilhos.Length; i++)
            {
                string gatilho = CharacterRecipe.Gatilhos[i];
                string clipe = r.GetAnim(i);
                if (string.IsNullOrEmpty(clipe)) continue; // sem animação: o jogo usa o movimento automático
                var clip = Clip(clipe);
                if (clip == null) { avisos.Add("sem animação " + clipe); continue; }

                if (gatilho == "Idle")
                {
                    idle = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
                    idle.motion = clip;
                    sm.defaultState = idle;
                    continue;
                }

                ctrl.AddParameter(gatilho, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(gatilho, new Vector3(600f, y += 70f, 0f));
                state.motion = clip;

                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, gatilho);
                enter.duration = 0.08f;
                enter.canTransitionToSelf = gatilho == "Hit"; // golpes seguidos reiniciam o "levou dano"

                if (gatilho == "Die" || idle == null) continue; // morto fica caído

                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.2f;
            }
            return ctrl;
        }

        private static List<AnimationClip> _clips;

        /// <summary>Procura uma animação pelo nome (ex.: "Sword_Attack") nas bibliotecas do Quaternius.</summary>
        public static AnimationClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return TodosOsClipes().FirstOrDefault(c => Suffix(c.name) == name);
        }

        private static List<AnimationClip> TodosOsClipes()
        {
            if (_clips != null && _clips.All(c => c != null)) return _clips;
            _clips = new List<AnimationClip>();
            foreach (var file in BibliotecasDeAnimacao)
            {
                var path = Find(file, "/Unity/");
                if (path == null) continue;
                _clips.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")));
            }
            return _clips;
        }

        /// <summary>"Armature|Idle_Loop" → "Idle_Loop".</summary>
        private static string Suffix(string clipName)
        {
            int i = clipName.LastIndexOf('|');
            return i >= 0 ? clipName.Substring(i + 1) : clipName;
        }

        // ================================================================== Catálogo (listas da janela)

        public static void LimparCache()
        {
            _clips = null;
        }

        private static IEnumerable<string> ModelosFbx() =>
            AssetDatabase.FindAssets("t:Model", new[] { ArtDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Where(p => p.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) < 0);

        /// <summary>Roupas (Outfits) + modelos completos que servem de corpo (ex.: manequim).</summary>
        public static List<string> Roupas() =>
            ModelosFbx()
                .Where(p => p.Replace('\\', '/').Contains("/Outfits/") || Path.GetFileName(p).StartsWith("Mannequin", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName).Distinct().OrderBy(n => n).ToList();

        /// <summary>Corpos base de onde vem a cabeça.</summary>
        public static List<string> Corpos() =>
            ModelosFbx().Where(p => Path.GetFileName(p).IndexOf("FullBody", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(Path.GetFileName).Distinct().OrderBy(n => n).ToList();

        /// <summary>Cabelos, barbas e sobrancelhas presos ao osso da cabeça.</summary>
        public static List<string> Cabelos() =>
            ModelosFbx().Where(p => p.Replace('\\', '/').Contains("Rigged to Head Bone/"))
                .Select(Path.GetFileName).Distinct().OrderBy(n => n).ToList();

        /// <summary>Variações de cor de uma roupa (ex.: T_Peasant_BaseColor.png, T_Peasant_2_BaseColor.png).</summary>
        public static List<string> TexturasDaRoupa(string roupa)
        {
            string chave = ChaveDaRoupa(roupa);
            if (string.IsNullOrEmpty(chave)) return new List<string>();
            return AssetDatabase.FindAssets("t:Texture2D T_" + chave, new[] { ArtDir + "/Models" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) < 0)
                .Select(Path.GetFileName)
                .Where(f => f.StartsWith("T_" + chave, StringComparison.OrdinalIgnoreCase) && f.IndexOf("BaseColor", StringComparison.OrdinalIgnoreCase) >= 0)
                .Distinct().OrderBy(n => n).ToList();
        }

        /// <summary>Nomes de todas as animações das bibliotecas (sem a pose T).</summary>
        public static List<string> Animacoes() =>
            TodosOsClipes().Select(c => Suffix(c.name)).Where(n => n != "A_TPose").Distinct().OrderBy(n => n).ToList();

        // ================================================================== Busca de arquivos

        /// <summary>Procura um FBX pelo nome, preferindo as versões feitas para Unity.</summary>
        private static string FindModel(string file) => Find(file, "Rigged to Head Bone", "FBX (Unity)", "/Unity/");

        private static Texture2D FindTexture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            var path = Find(file, "Normals Unity", "/Textures/");
            return path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
        }

        private static string Find(string file, params string[] prefer)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var paths = AssetDatabase.FindAssets(name, new[] { ArtDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileName(p), file, StringComparison.OrdinalIgnoreCase))
                .Where(p => p.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) < 0)
                .Distinct()
                .ToList();
            foreach (var pref in prefer)
            {
                var hit = paths.FirstOrDefault(p => p.IndexOf(pref, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit != null) return hit;
            }
            return paths.FirstOrDefault();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
