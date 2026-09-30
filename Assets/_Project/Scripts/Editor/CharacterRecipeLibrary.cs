using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BelleEpoque.EditorTools
{
    /// <summary>
    /// "Receita" de um personagem montado com os pacotes do Quaternius:
    /// roupa, cabeça, cabelos, cores e qual animação toca em cada ação da batalha.
    /// Editada pela janela Belle Époque → Editor de Personagens.
    /// </summary>
    [Serializable]
    public class CharacterRecipe
    {
        public string nome = "Novo_Personagem";
        public UnitDefinition ficha;

        [Header("Aparência")]
        public string roupa = "Male_Peasant.fbx";
        [Tooltip("FBX de onde vem a cabeça (rosto, olhos e sobrancelhas). Vazio = sem cabeça (ex.: manequim).")]
        public string corpo = "Superhero_Male_FullBody.fbx";
        public List<string> cabelos = new List<string>();
        public string roupaTextura = "";
        public Color roupaTom = Color.white;
        public Color olhosTom = Color.white;
        public bool fantasma;
        [Tooltip("Só para o manequim (modelo sem textura).")]
        public Color corPrincipal = new Color(0.86f, 0.8f, 0.72f);
        public Color corJuntas = new Color(0.22f, 0.17f, 0.15f);

        [Header("Animações (nomes da Universal Animation Library)")]
        public string idle = "Idle_Loop";
        public string attack = "Sword_Attack";
        public string cast = "Spell_Simple_Shoot";
        public string heal = "Spell_Simple_Enter";
        public string hit = "Hit_Chest";
        public string defend = "Sword_Block";
        public string item = "Consume";
        public string die = "Death01";

        /// <summary>Gatilhos do Animator que o jogo dispara (mesma ordem de <see cref="Rotulos"/>).</summary>
        public static readonly string[] Gatilhos = { "Idle", "Attack", "Cast", "Heal", "Hit", "Defend", "Item", "Die" };
        public static readonly string[] Rotulos = { "Parado", "Ataque", "Ritual", "Cura", "Levar golpe", "Defender", "Usar item", "Morte" };

        public string GetAnim(int i)
        {
            switch (i)
            {
                case 0: return idle;
                case 1: return attack;
                case 2: return cast;
                case 3: return heal;
                case 4: return hit;
                case 5: return defend;
                case 6: return item;
                default: return die;
            }
        }

        public void SetAnim(int i, string value)
        {
            switch (i)
            {
                case 0: idle = value; break;
                case 1: attack = value; break;
                case 2: cast = value; break;
                case 3: heal = value; break;
                case 4: hit = value; break;
                case 5: defend = value; break;
                case 6: item = value; break;
                default: die = value; break;
            }
        }

        public CharacterRecipe Clone()
        {
            var c = (CharacterRecipe)MemberwiseClone();
            c.cabelos = new List<string>(cabelos);
            return c;
        }
    }

    /// <summary>Arquivo com as receitas de todos os personagens (Assets/_Project/Data/Personagens_Receitas.asset).</summary>
    public class CharacterRecipeLibrary : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/Personagens_Receitas.asset";

        public List<CharacterRecipe> receitas = new List<CharacterRecipe>();

        /// <summary>Abre o arquivo de receitas; na primeira vez cria com os 6 personagens do exemplo.</summary>
        public static CharacterRecipeLibrary LoadOrCreate()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CharacterRecipeLibrary>(AssetPath);
            if (lib != null) return lib;

            lib = CreateInstance<CharacterRecipeLibrary>();
            lib.receitas = QuaterniusCharacters.ReceitasPadrao();
            AssetDatabase.CreateAsset(lib, AssetPath);
            AssetDatabase.SaveAssets();
            return lib;
        }
    }
}
