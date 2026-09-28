using System.Collections.Generic;
using BelleEpoque.Core;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>
    /// Agente ou ameaça. Agentes (Combatente, Especialista, Ocultista) calculam PV, PE,
    /// Sanidade e Defesa pela ficha de Ordem Paranormal; ameaças usam os números de "Stats da Ameaça".
    /// </summary>
    [CreateAssetMenu(menuName = "Belle Époque/Unidade (Agente ou Ameaça)", fileName = "NovaUnidade")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string displayName = "Novo Agente";
        public string roleName = "";
        [TextArea] public string lore = "";
        public Sprite portrait;
        public Color themeColor = Color.white;

        [Header("Ficha de Ordem Paranormal")]
        public Trilha trilha = Trilha.Combatente;
        [Range(5, 99)] public int nex = 35;
        public Atributos atributos = new Atributos(1, 1, 1, 1, 1);
        [Tooltip("Proteção e outros bônus de Defesa.")]
        public int bonusDefesa = 0;
        public int bonusPv = 0;
        public int bonusPe = 0;
        public int bonusSanidade = 0;
        [Tooltip("Iniciativa treinada: +5.")]
        public int iniciativaBonus = 0;
        [Tooltip("Fortitude/Reflexos/Vontade treinadas: +5.")]
        public int resistenciaBonus = 0;

        [Header("Stats da Ameaça (só quando Trilha = Ameaça)")]
        public StatBlock stats = new StatBlock(40, 10, 0, 14, 5, new Atributos(1, 1, 1, 1, 1));

        [Header("Vulnerabilidades e resistências")]
        public List<Element> weaknesses = new List<Element>();
        public List<Element> resistances = new List<Element>();

        [Header("Habilidades")]
        public SkillDefinition basicAttack;
        public List<SkillDefinition> skills = new List<SkillDefinition>();

        [Header("Visual e som")]
        public GameObject modelPrefab;
        public float modelScale = 1f;
        public AudioClip hurtSfx;
        public AudioClip deathSfx;

        public bool IsAgent => trilha != Trilha.Ameaca;

        /// <summary>Números finais para a batalha.</summary>
        public StatBlock BuildStats()
        {
            if (!IsAgent) return stats;
            return OrdemRules.CalcularStats(trilha, nex, atributos, bonusDefesa, bonusPv, bonusPe, bonusSanidade,
                iniciativaBonus, resistenciaBonus);
        }

        public static string TrilhaName(Trilha t)
        {
            switch (t)
            {
                case Trilha.Combatente: return "Combatente";
                case Trilha.Especialista: return "Especialista";
                case Trilha.Ocultista: return "Ocultista";
                default: return "Ameaça";
            }
        }
    }

    [System.Serializable]
    public class ItemStack
    {
        public SkillDefinition item;
        public int quantity = 1;
    }
}
