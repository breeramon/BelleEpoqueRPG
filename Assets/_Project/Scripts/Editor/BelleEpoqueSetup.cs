using System;
using System.Collections.Generic;
using System.Linq;
using BelleEpoque.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BelleEpoque.EditorTools
{
    /// <summary>
    /// Menu "Belle Époque" na barra superior da Unity.
    /// Gera dados de exemplo, modelos provisórios, a cena de batalha com clima de terror
    /// e um Animator Controller pronto para receber animações do Mixamo.
    ///
    /// Pode rodar de novo sem medo: dados que já existem NÃO são sobrescritos.
    /// </summary>
    public static class BelleEpoqueSetup
    {
        private const string Root = "Assets/_Project";
        private const string DataDir = Root + "/Data";
        private const string SkillsDir = DataDir + "/Skills";
        private const string UnitsDir = DataDir + "/Units";
        private const string MaterialsDir = Root + "/Art/Materials";
        private const string AnimDir = Root + "/Art/Animations";
        private const string PrefabsDir = Root + "/Prefabs/Placeholders";
        private const string SettingsDir = Root + "/Settings";
        private const string ScenePath = Root + "/Scenes/Battle.unity";
        private const string MenuScenePath = Root + "/Scenes/MainMenu.unity";

        // ================================================================== Menus

        [MenuItem("Belle Époque/1. Montar projeto de exemplo (dados + cena)", priority = 1)]
        public static void BuildEverything()
        {
            if (!NotInPlayMode()) return;
            if (!EnsureTextMeshPro()) return;
            if (AssetDatabase.LoadAssetAtPath<EncounterDefinition>(DataDir + "/Encontro_RueDesOmbres.asset") != null &&
                !EditorUtility.DisplayDialog("Belle Époque",
                    "Isto reaplica os dados de exemplo (agentes, ameaças, habilidades e tema) com as regras de Ordem Paranormal " +
                    "e recria a cena Battle.\n\nMudanças que você fez nesses assets de exemplo serão substituídas. Continuar?",
                    "Aplicar", "Cancelar"))
                return;

            EnsureFolders();
            var mats = CreateMaterials();
            var prefabs = CreatePlaceholderPrefabs(mats);
            var encounter = CreateSampleData(prefabs);
            CreateAnimatorTemplate();
            var profile = CreateHorrorVolumeProfile();
            var theme = CreateTheme();
            AssetDatabase.SaveAssets();

            if (!CreateBattleScene(encounter, profile, theme, mats)) return;

            EditorUtility.DisplayDialog("Belle Époque",
                "Pronto!\n\nA cena Battle foi criada em Assets/_Project/Scenes.\n" +
                "Aperte Play para testar a batalha.\n\n" +
                "Dica: veja o resultado na aba Game (o pós-processamento não aparece igual na aba Scene).",
                "Vamos lá");
        }

        [MenuItem("Belle Époque/2. Recriar somente a cena de batalha", priority = 2)]
        public static void RebuildSceneOnly()
        {
            if (!NotInPlayMode()) return;
            if (!EnsureTextMeshPro()) return;
            EnsureFolders();
            var mats = CreateMaterials();
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(DataDir + "/Encontro_RueDesOmbres.asset");
            if (encounter == null)
            {
                EditorUtility.DisplayDialog("Belle Époque", "Rode primeiro o item 1 do menu para criar os dados.", "OK");
                return;
            }
            var profile = CreateHorrorVolumeProfile();
            var theme = CreateTheme();
            AssetDatabase.SaveAssets();
            CreateBattleScene(encounter, profile, theme, mats);
        }

        [MenuItem("Belle Époque/3. Criar Animator Controller base (Mixamo)", priority = 20)]
        public static void CreateAnimatorTemplateMenu()
        {
            EnsureFolders();
            var ctrl = CreateAnimatorTemplate();
            Selection.activeObject = ctrl;
            EditorGUIUtility.PingObject(ctrl);
        }

        [MenuItem("Belle Époque/5. Criar menu inicial", priority = 3)]
        public static void CreateMainMenu()
        {
            if (!NotInPlayMode()) return;
            if (!EnsureTextMeshPro()) return;
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(SettingsDir + "/Tema_BelleEpoque.asset");
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(DataDir + "/Encontro_RueDesOmbres.asset");
            if (theme == null || encounter == null)
            {
                EditorUtility.DisplayDialog("Belle Époque", "Rode primeiro o menu \"1. Montar projeto de exemplo\" (o menu inicial usa o tema e os agentes dele).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolders();
            AtualizarHistorias(encounter.heroes);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene descarrega os assets carregados antes: recarrega pelo caminho
            theme = AssetDatabase.LoadAssetAtPath<UITheme>(SettingsDir + "/Tema_BelleEpoque.asset");
            encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(DataDir + "/Encontro_RueDesOmbres.asset");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.nuit;

            var canvasGo = new GameObject("MenuInicial");
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var menu = canvasGo.AddComponent<MainMenuController>();
            var so = new SerializedObject(menu);
            so.FindProperty("theme").objectReferenceValue = theme;
            var lista = so.FindProperty("agentes");
            lista.arraySize = encounter.heroes.Count;
            for (int i = 0; i < encounter.heroes.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = encounter.heroes[i];
            so.FindProperty("cenaDaBatalha").stringValue = System.IO.Path.GetFileNameWithoutExtension(ScenePath);
            so.ApplyModifiedPropertiesWithoutUndo();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            OrdenarCenasNoBuild();
            Selection.activeGameObject = canvasGo;

            EditorUtility.DisplayDialog("Belle Époque",
                "Menu inicial criado em Assets/_Project/Scenes/MainMenu.\n\n" +
                "Ele é a primeira cena do jogo (Build Settings) e o INICIAR abre a batalha.\nAperte Play para ver.", "OK");
        }

        /// <summary>Menu inicial primeiro, batalha em seguida, e as demais cenas depois.</summary>
        private static void OrdenarCenasNoBuild()
        {
            var cenas = EditorBuildSettings.scenes.Where(c => c.path != MenuScenePath && c.path != ScenePath).ToList();
            int i = 0;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null) cenas.Insert(i++, new EditorBuildSettingsScene(MenuScenePath, true));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) cenas.Insert(i, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = cenas.ToArray();
        }

        /// <summary>Troca as histórias curtas antigas dos agentes pelas novas (não mexe em textos que você mesmo escreveu).</summary>
        private static void AtualizarHistorias(IEnumerable<UnitDefinition> agentes)
        {
            var trocas = new Dictionary<string, string>
            {
                { "Ex-soldado da Legião. Voltou da guerra ouvindo vozes que ninguém mais ouve.", "Ex-soldado da Legião Estrangeira. Voltou do front com uma cicatriz no peito e vozes que ninguém mais ouve. Hoje empunha o sabre pela Ordo Realitas, certo de que algumas coisas só param quando sangram." },
                { "Acadêmica da Sorbonne expulsa por estudar livros que não deveriam existir.", "Acadêmica da Sorbonne, expulsa por estudar livros que não deveriam existir. Lê sigilos como quem lê poesia — e sabe que cada ritual cobra um pedaço de quem o conjura." },
                { "Freira do Hôtel-Dieu. Suas preces funcionam — e ela tem medo de descobrir por quê.", "Freira do Hôtel-Dieu, acostumada a costurar feridas à luz de lampião. Suas preces funcionam — e ela tem medo de descobrir por quê." },
            };
            foreach (var a in agentes)
            {
                if (a == null || !trocas.TryGetValue(a.lore ?? "", out var nova)) continue;
                a.lore = nova;
                EditorUtility.SetDirty(a);
            }
            AssetDatabase.SaveAssets();
        }

        private static bool NotInPlayMode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("Belle Époque", "Saia do modo Play (botão ▶) antes de usar este menu.", "OK");
            return false;
        }

        // ================================================================== TextMeshPro

        private static bool EnsureTextMeshPro()
        {
            if (Resources.Load<TMPro.TMP_Settings>("TMP Settings") != null) return true;

            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            EditorUtility.DisplayDialog("Belle Époque",
                "Importei os recursos essenciais do TextMeshPro (fontes da interface).\n\n" +
                "Espere a importação terminar e rode o menu Belle Époque > 1 de novo.",
                "OK");
            return false;
        }

        // ================================================================== Pastas

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { DataDir, SkillsDir, UnitsDir, MaterialsDir, AnimDir, PrefabsDir, SettingsDir, Root + "/Scenes",
                                        Root + "/Audio/Music", Root + "/Audio/SFX", Root + "/Audio/Ambience", Root + "/VFX", Root + "/Art/Models" })
                EnsureFolder(dir);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static T LoadOrCreate<T>(string path, Action<T> configure, out bool created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) { created = false; return existing; }
            var asset = ScriptableObject.CreateInstance<T>();
            configure(asset);
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        // ================================================================== Materiais

        private class Mats
        {
            public Material Ground, Facade, Stone, Iron, Brass, WindowGlow, LampGlow, RitualGlow, TurnRing;
            public Material Crimson, Violet, Ivory, Steel, SigilGreen, LanternWarm;
            public Material CultRobe, Shadow, EyeRed, Porcelain, GhostGlow;
        }

        private static Mats CreateMaterials()
        {
            return new Mats
            {
                Ground = Mat("Chao_Paralelepipedo", new Color(0.36f, 0.35f, 0.36f), smoothness: 0.25f),
                Facade = Mat("Fachada_Haussmann", new Color(0.66f, 0.61f, 0.53f)), // calcário de Paris
                Stone = Mat("Pedra_Escura", new Color(0.3f, 0.29f, 0.3f)),
                Iron = Mat("Ferro_Fundido", new Color(0.05f, 0.05f, 0.06f), metallic: 0.8f, smoothness: 0.5f),
                Brass = Mat("Latao", new Color(0.55f, 0.42f, 0.2f), metallic: 0.9f, smoothness: 0.6f),
                WindowGlow = Mat("Janela_Acesa", new Color(0.2f, 0.12f, 0.05f), emission: new Color(1.6f, 0.9f, 0.35f)),
                LampGlow = Mat("Lampiao_Gas", new Color(0.3f, 0.2f, 0.1f), emission: new Color(4f, 2.4f, 1f)),
                RitualGlow = Mat("Circulo_Ritual", new Color(0.1f, 0f, 0.02f), emission: new Color(1.4f, 0.05f, 0.12f)),
                TurnRing = Mat("Indicador_Turno", new Color(0.3f, 0.22f, 0.08f), emission: new Color(2.2f, 1.6f, 0.6f)),

                Crimson = Mat("Heroi_Combatente", new Color(0.36f, 0.07f, 0.09f), smoothness: 0.3f),
                Violet = Mat("Heroi_Ocultista", new Color(0.22f, 0.12f, 0.32f), smoothness: 0.3f),
                Ivory = Mat("Heroi_Curandeira", new Color(0.82f, 0.79f, 0.7f), smoothness: 0.2f),
                Steel = Mat("Aco_Sabre", new Color(0.7f, 0.72f, 0.75f), metallic: 1f, smoothness: 0.85f),
                SigilGreen = Mat("Sigilo_Verde", new Color(0.05f, 0.2f, 0.1f), emission: new Color(0.3f, 1.6f, 0.6f)),
                LanternWarm = Mat("Lanterna_Quente", new Color(0.3f, 0.2f, 0.1f), emission: new Color(2.5f, 1.6f, 0.6f)),

                CultRobe = Mat("Inimigo_Manto_Cultista", new Color(0.2f, 0.03f, 0.04f)),
                Shadow = Mat("Sombra_Capuz", new Color(0.01f, 0.01f, 0.01f)),
                EyeRed = Mat("Olhos_Vermelhos", new Color(0.2f, 0f, 0f), emission: new Color(3f, 0.1f, 0.1f)),
                Porcelain = Mat("Inimigo_Porcelana", new Color(0.8f, 0.75f, 0.68f), smoothness: 0.7f),
                GhostGlow = Mat("Inimigo_Aparicao", new Color(0.1f, 0.18f, 0.2f), emission: new Color(0.35f, 1.1f, 1.3f)),
            };
        }

        private static Material Mat(string matName, Color color, float metallic = 0f, float smoothness = 0.2f, Color? emission = null)
        {
            string path = $"{MaterialsDir}/{matName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var mat = existing != null ? existing : new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = matName };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (existing == null) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================== Modelos provisórios

        private class Prefabs
        {
            public GameObject Combatente, Ocultista, Curandeira, Cultista, Manequim, Aparicao;
        }

        private static Prefabs CreatePlaceholderPrefabs(Mats m)
        {
            return new Prefabs
            {
                Combatente = Placeholder("Placeholder_Combatente", root =>
                {
                    Part(root, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.75f, 1f, 0.75f), m.Crimson);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.45f, m.Ivory);
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 2.4f, 0f), new Vector3(0.42f, 0.05f, 0.42f), m.Shadow); // aba do quepe
                    Part(root, PrimitiveType.Cube, new Vector3(0.5f, 1.1f, 0.2f), new Vector3(0.05f, 1.1f, 0.08f), m.Steel);   // sabre
                    Part(root, PrimitiveType.Cube, new Vector3(0.5f, 0.55f, 0.2f), new Vector3(0.18f, 0.05f, 0.12f), m.Brass); // guarda
                }),
                Ocultista = Placeholder("Placeholder_Ocultista", root =>
                {
                    Part(root, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.65f, 1f, 0.65f), m.Violet);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.42f, m.Ivory);
                    Part(root, PrimitiveType.Cube, new Vector3(-0.45f, 1.1f, 0.25f), new Vector3(0.32f, 0.42f, 0.08f), m.Stone);    // grimório
                    Part(root, PrimitiveType.Sphere, new Vector3(0.45f, 1.5f, 0.35f), Vector3.one * 0.18f, m.SigilGreen);          // sigilo flutuante
                }),
                Curandeira = Placeholder("Placeholder_Curandeira", root =>
                {
                    Part(root, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.7f, 1f, 0.7f), m.Ivory);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.05f, 0f), new Vector3(0.5f, 0.5f, 0.5f), m.Shadow);       // véu escuro
                    Part(root, PrimitiveType.Cube, new Vector3(0.45f, 0.9f, 0.25f), new Vector3(0.18f, 0.25f, 0.18f), m.LanternWarm); // lanterna
                    Part(root, PrimitiveType.Cube, new Vector3(0f, 1.3f, 0.34f), new Vector3(0.06f, 0.35f, 0.02f), m.Brass);      // cruz
                    Part(root, PrimitiveType.Cube, new Vector3(0f, 1.38f, 0.34f), new Vector3(0.2f, 0.06f, 0.02f), m.Brass);
                }),
                Cultista = Placeholder("Placeholder_Cultista", root =>
                {
                    Part(root, PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f), new Vector3(0.8f, 1.05f, 0.8f), m.CultRobe);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.1f, 0.02f), new Vector3(0.55f, 0.6f, 0.55f), m.CultRobe); // capuz
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.08f, 0.14f), new Vector3(0.38f, 0.4f, 0.3f), m.Shadow);   // rosto na sombra
                    Part(root, PrimitiveType.Sphere, new Vector3(-0.08f, 2.12f, 0.27f), Vector3.one * 0.06f, m.EyeRed);
                    Part(root, PrimitiveType.Sphere, new Vector3(0.08f, 2.12f, 0.27f), Vector3.one * 0.06f, m.EyeRed);
                }),
                Manequim = Placeholder("Placeholder_Manequim", root =>
                {
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f), new Vector3(0.08f, 0.35f, 0.08f), m.Iron);      // haste
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.6f, 0.02f, 0.6f), m.Iron);        // base
                    Part(root, PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(0.7f, 1.1f, 0.4f), m.Porcelain);        // torso
                    Part(root, PrimitiveType.Sphere, new Vector3(0.05f, 2.05f, 0f), new Vector3(0.42f, 0.5f, 0.42f), m.Porcelain).transform.localRotation = Quaternion.Euler(0f, 0f, 18f); // cabeça torta
                    Part(root, PrimitiveType.Cube, new Vector3(-0.55f, 1.2f, 0.3f), new Vector3(0.12f, 0.9f, 0.12f), m.Porcelain).transform.localRotation = Quaternion.Euler(-60f, 0f, 20f); // braço estendido
                }),
                Aparicao = Placeholder("Placeholder_Aparicao", root =>
                {
                    Part(root, PrimitiveType.Capsule, new Vector3(0f, 1.6f, 0f), new Vector3(0.8f, 1.2f, 0.8f), m.GhostGlow);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.9f, 0f), Vector3.one * 0.45f, m.GhostGlow);
                    Part(root, PrimitiveType.Sphere, new Vector3(0f, 2.9f, 0.18f), new Vector3(0.3f, 0.12f, 0.15f), m.Shadow); // boca escura
                }),
            };
        }

        private static GameObject Placeholder(string prefabName, Action<Transform> build)
        {
            string path = $"{PrefabsDir}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(prefabName);
            build(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ================================================================== Dados (habilidades, agentes, ameaças)

        /// <summary>
        /// Cria o asset ou, se já existir, sobrescreve com os valores de exemplo
        /// (mantendo o arquivo e as referências das outras cenas/assets).
        /// </summary>
        private static T Upsert<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var fresh = ScriptableObject.CreateInstance<T>();
            configure(fresh);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            fresh.name = existing.name;
            EditorUtility.CopySerialized(fresh, existing);
            UnityEngine.Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static SkillDefinition Skill(string file, Action<SkillDefinition> configure) => Upsert($"{SkillsDir}/{file}.asset", configure);
        /// <summary>
        /// Recria a ficha, mas mantém o modelo 3D e o retrato que você (ou o menu 4) já colocou:
        /// só os bonecos provisórios são trocados de volta.
        /// </summary>
        private static UnitDefinition Unit(string file, Action<UnitDefinition> configure)
        {
            string path = $"{UnitsDir}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            GameObject keepModel = null;
            float keepScale = 1f;
            Sprite keepPortrait = existing != null ? existing.portrait : null;
            if (existing != null && existing.modelPrefab != null && !AssetDatabase.GetAssetPath(existing.modelPrefab).StartsWith(PrefabsDir))
            {
                keepModel = existing.modelPrefab;
                keepScale = existing.modelScale;
            }
            return Upsert(path, (UnitDefinition u) =>
            {
                configure(u);
                if (keepModel != null) { u.modelPrefab = keepModel; u.modelScale = keepScale; }
                if (keepPortrait != null) u.portrait = keepPortrait;
            });
        }

        // Atalhos para montar habilidades no padrão de Ordem
        private static void Melee(SkillDefinition s, string name, int dice, int sides, int bonus, float shake = 0.15f)
        {
            s.displayName = name; s.resolution = SkillResolution.AttackTest; s.testAttribute = Atributo.For; s.testBonus = bonus;
            s.diceCount = dice; s.diceSides = sides; s.damageAttribute = Atributo.For; s.animationTrigger = "Attack"; s.moveToTarget = true; s.cameraShake = shake;
        }

        private static void Ritual(SkillDefinition s, string name, Element e, int circulo, int pe, int dice, int sides, Atributo resist, float shake = 0.25f)
        {
            s.displayName = name; s.element = e; s.circulo = circulo; s.peCost = pe; s.resolution = SkillResolution.RitualResistance;
            s.resistAttribute = resist; s.diceCount = dice; s.diceSides = sides; s.animationTrigger = "Cast"; s.moveToTarget = false; s.cameraShake = shake;
        }

        private static void Support(SkillDefinition s, string name, SkillKind kind, TargetType target, int pe, int dice, int sides)
        {
            s.displayName = name; s.kind = kind; s.target = target; s.peCost = pe; s.resolution = SkillResolution.Automatic;
            s.diceCount = dice; s.diceSides = sides; s.animationTrigger = "Heal"; s.moveToTarget = false; s.cameraShake = 0f;
        }

        private static EncounterDefinition CreateSampleData(Prefabs p)
        {
            // ---------------- Lucien Duval — Combatente (Luta veterano +10)
            var sabre = Skill("Combatente_GolpeDeSabre", s => { Melee(s, "Golpe de Sabre", 1, 8, 10); s.description = "Sabre de cavalaria, herança da Legião."; });
            var especial = Skill("Combatente_AtaqueEspecial", s =>
            {
                Melee(s, "Ataque Especial", 1, 8, 15, 0.3f); s.peCost = 3; s.power = 5;
                s.description = "Poder de Combatente (NEX 25%): gasta 3 PE para +5 no ataque e +5 no dano.";
            });
            var varredura = Skill("Combatente_Varredura", s =>
            {
                Melee(s, "Varredura", 1, 8, 10, 0.3f); s.peCost = 4; s.target = TargetType.AllEnemies;
                s.description = "Um arco largo do sabre que atinge todas as ameaças.";
            });
            var lamina = Skill("Combatente_LaminaDeSangue", s =>
            {
                Melee(s, "Lâmina de Sangue", 2, 8, 10, 0.4f); s.element = Element.Blood; s.peCost = 3; s.hpCost = 6;
                s.appliesStatus = true; s.status = StatusType.Bleeding; s.statusChance = 1f; s.statusDuration = 3;
                s.description = "Como Arma Atroz: o sabre bebe 6 PV de Lucien e rasga com Sangue. O alvo sangra.";
            });

            // ---------------- Margot Verlaine — Ocultista (DT = 10 + limite de PE + PRE)
            var bengala = Skill("Ocultista_BengalaDePrata", s => { Melee(s, "Bengala de Prata", 1, 4, 0); s.description = "Margot não é treinada em Luta."; });
            var sigilo = Skill("Ocultista_SigiloDoConhecimento", s =>
            {
                Ritual(s, "Sigilo do Conhecimento", Element.Knowledge, 1, 1, 2, 8, Atributo.Pre);
                s.description = "Um símbolo dourado grava verdades proibidas na mente do alvo.";
            });
            var decadencia = Skill("Ocultista_Decadencia", s =>
            {
                Ritual(s, "Decadência", Element.Death, 1, 1, 2, 8, Atributo.Vig); s.power = 2;
                s.description = "Acelera o envelhecimento dos órgãos internos do alvo (2d8+2 de Morte).";
            });
            var descarga = Skill("Ocultista_DescargaParanormal", s =>
            {
                Ritual(s, "Descarga Paranormal", Element.Energy, 2, 3, 3, 6, Atributo.Agi, 0.35f); s.target = TargetType.AllEnemies;
                s.description = "Energia do Outro Lado salta de lampião em lampião e atinge todas as ameaças.";
            });
            var proibido = Skill("Ocultista_RitualProibido", s =>
            {
                Ritual(s, "Ritual Proibido", Element.Death, 2, 3, 4, 8, Atributo.Vig, 0.55f); s.sanityCost = 10; s.damageAttribute = Atributo.Int;
                s.appliesStatus = true; s.status = StatusType.Stunned; s.statusChance = 0.5f; s.statusDuration = 1;
                s.description = "Poder imenso ao custo de 10 de Sanidade. Pode atordoar.";
            });

            // ---------------- Irmã Céleste — Especialista, trilha Médico de Campo
            var lanterna = Skill("Curandeira_LanternaSagrada", s => { Melee(s, "Lanterna Sagrada", 1, 6, 0); s.description = "Uma lanterna de ferro, mais útil para iluminar do que para bater."; });
            var paramedico = Skill("Curandeira_Paramedico", s =>
            {
                Support(s, "Paramédico", SkillKind.Heal, TargetType.SingleAlly, 2, 2, 10);
                s.description = "Médico de Campo (NEX 10%): 2 PE para curar 2d10 PV.";
            });
            var vigilia = Skill("Curandeira_Vigilia", s =>
            {
                Support(s, "Vigília", SkillKind.Heal, TargetType.AllAllies, 4, 1, 10); s.damageAttribute = Atributo.Int;
                s.description = "Uma prece longa em latim. Cura 1d10+INT em todo o grupo.";
            });
            var lucidez = Skill("Curandeira_PalavrasDeLucidez", s =>
            {
                Support(s, "Palavras de Lucidez", SkillKind.RestoreSanity, TargetType.SingleAlly, 2, 2, 6); s.damageAttribute = Atributo.Pre;
                s.description = "Ancora a mente de um aliado: recupera 2d6+PRE de Sanidade.";
            });
            var egide = Skill("Curandeira_Egide", s =>
            {
                Support(s, "Égide", SkillKind.Buff, TargetType.SingleAlly, 2, 0, 6);
                s.appliesStatus = true; s.status = StatusType.Shielded; s.statusDuration = 3; s.animationTrigger = "Cast";
                s.description = "Como Armadura de Sangue, mas de luz: +5 na Defesa de um aliado por 3 turnos.";
            });

            // ---------------- Ameaças
            var adaga = Skill("Ameaca_AdagaRitual", s => { Melee(s, "Adaga Ritual", 1, 4, 5); });
            var corte = Skill("Ameaca_CorteCarmesim", s =>
            {
                Melee(s, "Corte Carmesim", 2, 6, 5); s.element = Element.Blood; s.peCost = 2;
                s.appliesStatus = true; s.status = StatusType.Bleeding; s.statusChance = 0.6f; s.statusDuration = 2;
            });
            var cantico = Skill("Ameaca_CanticoProfano", s => { Support(s, "Cântico Profano", SkillKind.Heal, TargetType.SingleAlly, 2, 2, 8); s.animationTrigger = "Cast"; });
            var garras = Skill("Ameaca_GarrasDePorcelana", s => { Melee(s, "Garras de Porcelana", 1, 6, 5, 0.2f); });
            var abraco = Skill("Ameaca_AbracoFrio", s =>
            {
                Melee(s, "Abraço Frio", 2, 6, 5, 0.3f); s.sanityDamage = 3;
                s.appliesStatus = true; s.status = StatusType.Stunned; s.statusChance = 0.3f; s.statusDuration = 1;
            });
            var toque = Skill("Ameaca_ToqueGelido", s =>
            {
                Melee(s, "Toque Gélido", 1, 8, 5); s.element = Element.Death; s.testAttribute = Atributo.Pre; s.damageAttribute = Atributo.Pre;
                s.animationTrigger = "Cast"; s.moveToTarget = false;
            });
            var sussurro = Skill("Ameaca_SussurroDoOutroLado", s => { Ritual(s, "Sussurro do Outro Lado", Element.Fear, 0, 2, 2, 6, Atributo.Pre, 0.1f); s.power = 2; });
            var lamento = Skill("Ameaca_Lamento", s =>
            {
                Ritual(s, "Lamento", Element.Fear, 0, 4, 1, 8, Atributo.Pre, 0.2f); s.target = TargetType.AllEnemies;
                s.appliesStatus = true; s.status = StatusType.Terrified; s.statusChance = 0.5f; s.statusDuration = 2;
            });

            // ---------------- Itens
            var tonico = Skill("Item_TonicoMedicinal", s => { Support(s, "Tônico Medicinal", SkillKind.Heal, TargetType.SingleAlly, 0, 2, 8); s.power = 4; s.isItem = true; s.animationTrigger = "Item"; s.description = "Frasco âmbar de farmácia. Cura 2d8+4 PV."; });
            var sais = Skill("Item_SaisAromaticos", s => { Support(s, "Sais Aromáticos", SkillKind.RestoreSanity, TargetType.SingleAlly, 0, 2, 6); s.power = 4; s.isItem = true; s.animationTrigger = "Item"; s.description = "Desperta os sentidos. Recupera 2d6+4 de Sanidade."; });

            // ---------------- Agentes (NEX 35%: limite de 7 PE por rodada)
            var lucien = Unit("Heroi_LucienDuval", u =>
            {
                u.displayName = "Lucien Duval"; u.roleName = "Combatente";
                u.lore = "Ex-soldado da Legião Estrangeira. Voltou do front com uma cicatriz no peito e vozes que ninguém mais ouve. Hoje empunha o sabre pela Ordo Realitas, certo de que algumas coisas só param quando sangram.";
                u.trilha = Trilha.Combatente; u.nex = 35; u.atributos = new Atributos(2, 3, 1, 1, 3);
                u.bonusDefesa = 5; u.resistenciaBonus = 5;
                u.basicAttack = sabre; u.skills = new List<SkillDefinition> { especial, varredura, lamina };
                u.themeColor = new Color(0.6f, 0.1f, 0.12f); u.modelPrefab = p.Combatente;
            });
            var margot = Unit("Heroi_MargotVerlaine", u =>
            {
                u.displayName = "Margot Verlaine"; u.roleName = "Ocultista";
                u.lore = "Acadêmica da Sorbonne, expulsa por estudar livros que não deveriam existir. Lê sigilos como quem lê poesia — e sabe que cada ritual cobra um pedaço de quem o conjura.";
                u.trilha = Trilha.Ocultista; u.nex = 35; u.atributos = new Atributos(2, 0, 4, 3, 1);
                u.resistenciaBonus = 5; u.weaknesses = new List<Element> { Element.Blood };
                u.basicAttack = bengala; u.skills = new List<SkillDefinition> { sigilo, decadencia, descarga, proibido };
                u.themeColor = new Color(0.45f, 0.25f, 0.65f); u.modelPrefab = p.Ocultista;
            });
            var celeste = Unit("Heroi_IrmaCeleste", u =>
            {
                u.displayName = "Irmã Céleste"; u.roleName = "Especialista · Médico de Campo";
                u.lore = "Freira do Hôtel-Dieu, acostumada a costurar feridas à luz de lampião. Suas preces funcionam — e ela tem medo de descobrir por quê.";
                u.trilha = Trilha.Especialista; u.nex = 35; u.atributos = new Atributos(2, 1, 3, 2, 2);
                u.bonusDefesa = 2; u.resistenciaBonus = 5; u.weaknesses = new List<Element> { Element.Death };
                u.basicAttack = lanterna; u.skills = new List<SkillDefinition> { paramedico, vigilia, lucidez, egide };
                u.themeColor = new Color(0.9f, 0.85f, 0.7f); u.modelPrefab = p.Curandeira;
            });

            var cultista = Unit("Inimigo_CultistaEncapuzado", u =>
            {
                u.displayName = "Cultista Encapuzado"; u.trilha = Trilha.Ameaca;
                u.stats = new StatBlock(35, 12, 0, 15, 4, new Atributos(2, 2, 1, 2, 2), 0, 5);
                u.weaknesses = new List<Element> { Element.Energy };
                u.basicAttack = adaga; u.skills = new List<SkillDefinition> { corte, cantico }; u.modelPrefab = p.Cultista;
            });
            var manequim = Unit("Inimigo_ManequimRastejante", u =>
            {
                u.displayName = "Manequim Rastejante"; u.trilha = Trilha.Ameaca;
                u.stats = new StatBlock(55, 0, 0, 17, 4, new Atributos(1, 3, 0, 0, 3), 0, 5);
                u.weaknesses = new List<Element> { Element.Knowledge }; u.resistances = new List<Element> { Element.Physical };
                u.basicAttack = garras; u.skills = new List<SkillDefinition> { abraco }; u.modelPrefab = p.Manequim;
            });
            var aparicao = Unit("Inimigo_AparicaoDoTeatro", u =>
            {
                u.displayName = "Aparição do Teatro"; u.trilha = Trilha.Ameaca;
                u.stats = new StatBlock(32, 20, 0, 18, 5, new Atributos(3, 0, 2, 3, 1), 0, 5);
                u.weaknesses = new List<Element> { Element.Blood }; u.resistances = new List<Element> { Element.Death, Element.Physical };
                u.basicAttack = toque; u.skills = new List<SkillDefinition> { sussurro, lamento }; u.modelPrefab = p.Aparicao;
            });

            // Habilidades antigas que foram renomeadas nesta versão
            foreach (var old in new[] { "Combatente_EstocadaDaLegiao", "Curandeira_PreceDeCura", "Curandeira_SaisDeLucidez",
                                        "Inimigo_AdagaRitual", "Inimigo_CorteCarmesim", "Inimigo_CanticoProfano", "Inimigo_GarrasDePorcelana",
                                        "Inimigo_AbracoFrio", "Inimigo_ToqueGelido", "Inimigo_SussurroDoOutroLado", "Inimigo_Lamento" })
            {
                string path = $"{SkillsDir}/{old}.asset";
                if (AssetDatabase.LoadAssetAtPath<SkillDefinition>(path) != null) AssetDatabase.MoveAssetToTrash(path);
            }

            var encounter = Upsert($"{DataDir}/Encontro_RueDesOmbres.asset", (EncounterDefinition e) =>
            {
                e.title = "Rue des Ombres";
                e.heroes = new List<UnitDefinition> { lucien, margot, celeste };
                e.enemies = new List<UnitDefinition> { cultista, manequim, aparicao };
                e.items = new List<ItemStack>
                {
                    new ItemStack { item = tonico, quantity = 3 },
                    new ItemStack { item = sais, quantity = 2 }
                };
            });
            AssetDatabase.SaveAssets();
            return encounter;
        }

        // ================================================================== Tema da interface

        private static UITheme CreateTheme()
        {
            string fonts = Root + "/Art/Fonts";
            return Upsert($"{SettingsDir}/Tema_BelleEpoque.asset", (UITheme t) =>
            {
                t.displayFont = AssetDatabase.LoadAssetAtPath<Font>($"{fonts}/Cinzel-SemiBold.ttf");
                t.displayFontRegular = AssetDatabase.LoadAssetAtPath<Font>($"{fonts}/Cinzel-Regular.ttf");
                t.bodyFont = AssetDatabase.LoadAssetAtPath<Font>($"{fonts}/EBGaramond-Medium.ttf");
                t.bodyItalicFont = AssetDatabase.LoadAssetAtPath<Font>($"{fonts}/EBGaramond-Italic.ttf");
                t.labelFont = AssetDatabase.LoadAssetAtPath<Font>($"{fonts}/BebasNeue-Regular.ttf");
                if (t.displayFont == null) Debug.LogWarning("[Belle Époque] Fontes não encontradas em " + fonts + ". A HUD usará a fonte padrão.");
            });
        }

        // ================================================================== Animator base

        private static AnimatorController CreateAnimatorTemplate()
        {
            string path = AnimDir + "/Personagem_Batalha.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            string[] triggers = { "Attack", "Cast", "Heal", "Hit", "Defend", "Item", "Die" };
            foreach (var t in triggers) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(300f, 0f, 0f));
            sm.defaultState = idle;

            float y = 100f;
            foreach (var t in triggers)
            {
                var state = sm.AddState(t, new Vector3(600f, y, 0f));
                y += 70f;
                var enter = sm.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, t);
                enter.duration = 0.1f;
                enter.canTransitionToSelf = false;

                if (t == "Die") continue; // morto fica morto

                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.15f;
            }
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        // ================================================================== Pós-processamento

        private static VolumeProfile CreateHorrorVolumeProfile()
        {
            string path = SettingsDir + "/Terror_BelleEpoque_Profile.asset";
            // Sempre recria, para aplicar mudanças de visual (o asset é só do exemplo)
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var urp = UniversalRenderPipeline.asset;
            bool hdr = urp != null && urp.supportsHDR;

            var tonemap = AddOverride<Tonemapping>(profile);
            tonemap.mode.value = TonemappingMode.ACES;

            var color = AddOverride<ColorAdjustments>(profile);
            // Dia nublado em Paris: claro e legível, ainda com a cor dessaturada de época
            color.postExposure.value = 0.25f;
            color.contrast.value = 12f;
            color.saturation.value = -12f;
            color.colorFilter.value = new Color(1f, 0.97f, 0.91f); // sépia bem leve

            var split = AddOverride<SplitToning>(profile);
            split.shadows.value = new Color(0.42f, 0.43f, 0.52f);    // sombras levemente frias
            split.highlights.value = new Color(0.6f, 0.54f, 0.42f);  // luz quente de fim de tarde
            split.balance.value = 0f;

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.value = hdr ? 1.1f : 0.9f;
            bloom.intensity.value = 0.35f;
            bloom.scatter.value = 0.7f;

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.5f;
            vignette.color.value = new Color(0.05f, 0.02f, 0.03f);

            var grain = AddOverride<FilmGrain>(profile);
            grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.value = 0.12f;

            var chroma = AddOverride<ChromaticAberration>(profile);
            chroma.intensity.value = 0.02f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Adiciona o efeito, liga todos os overrides e salva como sub-asset do profile.</summary>
        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ================================================================== Cena

        private static bool CreateBattleScene(EncounterDefinition encounter, VolumeProfile profile, UITheme theme, Mats m)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Belle Époque", "A cena Battle já existe. Recriar do zero?", "Recriar", "Cancelar"))
                return false;

            // Guardamos os caminhos: ao abrir uma cena nova a Unity pode descarregar assets
            // da memória, e a referência antiga viraria null (bug do "Nenhum EncounterDefinition").
            string encounterPath = AssetDatabase.GetAssetPath(encounter);
            string profilePath = AssetDatabase.GetAssetPath(profile);
            string themePath = AssetDatabase.GetAssetPath(theme);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(encounterPath);
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            theme = AssetDatabase.LoadAssetAtPath<UITheme>(themePath);
            if (encounter == null)
            {
                Debug.LogError("[Belle Époque] Não encontrei o encontro em " + encounterPath);
                return false;
            }

            // ---------- Iluminação e névoa
            // Dia nublado: luz ambiente do céu, névoa clara e sol baixo e quente
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.67f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.49f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.7f, 0.72f, 0.76f);
            RenderSettings.fogDensity = 0.014f;

            var sun = new GameObject("Sol (Directional)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -40f, 0f);

            // ---------- Cenário
            var env = new GameObject("Cenario").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao";
            ground.transform.SetParent(env);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = m.Ground;

            // Fachadas haussmannianas ao fundo
            float[] heights = { 7f, 9f, 8f, 10f, 7.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                float x = -10f + i * 5f;
                EnvPart(env, PrimitiveType.Cube, new Vector3(x, heights[i] / 2f, 13f), new Vector3(4.6f, heights[i], 3f), m.Facade, "Predio");
                EnvPart(env, PrimitiveType.Cube, new Vector3(x, heights[i] + 0.15f, 13f), new Vector3(4.9f, 0.3f, 3.3f), m.Stone, "Cornija");
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        bool lit = (i + row * 2 + col) % 4 == 0;
                        EnvPart(env, PrimitiveType.Cube,
                            new Vector3(x - 1.4f + col * 1.4f, 1.8f + row * 2.1f, 11.45f),
                            new Vector3(0.6f, 1.1f, 0.05f), lit ? m.WindowGlow : m.Stone, "Janela");
                    }
            }

            // Lampiões a gás
            foreach (var x in new[] { -6f, 6f })
            {
                var lamp = new GameObject("Lampiao").transform;
                lamp.SetParent(env);
                lamp.position = new Vector3(x, 0f, 1.5f);
                EnvPart(lamp, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.14f, 1.6f, 0.14f), m.Iron, "Poste");
                EnvPart(lamp, PrimitiveType.Cube, new Vector3(0f, 3.35f, 0f), new Vector3(0.4f, 0.45f, 0.4f), m.LampGlow, "Lanterna");
                EnvPart(lamp, PrimitiveType.Cube, new Vector3(0f, 3.62f, 0f), new Vector3(0.55f, 0.08f, 0.55f), m.Iron, "Tampa");
                var light = new GameObject("Luz do Lampiao").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.transform.localPosition = new Vector3(0f, 3.3f, 0f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.68f, 0.38f);
                light.intensity = 1.2f; // de dia os lampiões são só decoração
                light.range = 9f;
                light.shadows = LightShadows.None; // sombras de luz pontual pesam no celular
            }

            // Círculo ritual sob os inimigos + luz vermelha
            EnvPart(env, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 5f), new Vector3(9f, 0.005f, 3.4f), m.RitualGlow, "Circulo Ritual");
            var red = new GameObject("Luz do Outro Lado").AddComponent<Light>();
            red.transform.SetParent(env);
            red.transform.position = new Vector3(0f, 2.5f, 8f);
            red.type = LightType.Point;
            red.color = new Color(0.9f, 0.08f, 0.15f);
            red.intensity = 2.5f;
            red.range = 9f;

            // ---------- Posições das unidades
            var spawns = new GameObject("Posicoes").transform;
            var heroSpawns = new Transform[3];
            var enemySpawns = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                heroSpawns[i] = new GameObject($"Heroi {i + 1}").transform;
                heroSpawns[i].SetParent(spawns);
                heroSpawns[i].position = new Vector3(-2.6f + i * 2.6f, 0f, -2f - (i == 1 ? 0.6f : 0f));
                heroSpawns[i].rotation = Quaternion.identity;

                enemySpawns[i] = new GameObject($"Inimigo {i + 1}").transform;
                enemySpawns[i].SetParent(spawns);
                enemySpawns[i].position = new Vector3(-3f + i * 3f, 0f, 5f + (i == 1 ? 0.8f : 0f));
                enemySpawns[i].rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "Indicador de Turno";
            UnityEngine.Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.transform.position = new Vector3(0f, 0.03f, 0f);
            indicator.transform.localScale = new Vector3(1.3f, 0.01f, 1.3f);
            indicator.GetComponent<Renderer>().sharedMaterial = m.TurnRing;

            // ---------- Câmera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.fieldOfView = 45f;
            cam.farClipPlane = 80f;
            camGo.transform.position = new Vector3(5.5f, 3.6f, -10f);
            camGo.transform.LookAt(new Vector3(0f, 1.2f, 1.8f));
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            var shake = camGo.AddComponent<CameraShake>();

            // ---------- Volume global
            var volumeGo = new GameObject("Global Volume (Terror)");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var sanityFx = volumeGo.AddComponent<SanityPostFx>();
            SetRef(sanityFx, "volume", volume);

            // ---------- Áudio
            var audioGo = new GameObject("AudioManager");
            var audio = audioGo.AddComponent<AudioManager>();

            // ---------- UI
            var canvasGo = new GameObject("BattleHUD");
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var hud = canvasGo.AddComponent<BattleHUD>();
            SetRef(hud, "theme", theme);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // ---------- Controlador
            var controllerGo = new GameObject("BattleController");
            var controller = controllerGo.AddComponent<BattleController>();
            var so = new SerializedObject(controller);
            so.FindProperty("encounter").objectReferenceValue = encounter;
            SetArray(so.FindProperty("heroSpawnPoints"), heroSpawns);
            SetArray(so.FindProperty("enemySpawnPoints"), enemySpawns);
            so.FindProperty("turnIndicator").objectReferenceValue = indicator;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("audioManager").objectReferenceValue = audio;
            so.FindProperty("sanityFx").objectReferenceValue = sanityFx;
            so.FindProperty("cameraShake").objectReferenceValue = shake;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Salvar e registrar no Build
            EditorSceneManager.SaveScene(scene, ScenePath);
            OrdenarCenasNoBuild();

            Selection.activeGameObject = controllerGo;
            return true;
        }

        private static GameObject EnvPart(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material mat, string partName)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = partName;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedProperty prop, IList<Transform> values)
        {
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
