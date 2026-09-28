using BelleEpoque.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace BelleEpoque
{
    /// <summary>
    /// Habilidade, ritual ou item editável pelo Inspector.
    /// Create > Belle Époque > Habilidade.
    /// </summary>
    [CreateAssetMenu(menuName = "Belle Époque/Habilidade", fileName = "NovaHabilidade")]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string displayName = "Nova Habilidade";
        [TextArea] public string description = "";
        public Sprite icon;
        [Tooltip("Círculo do ritual (1 a 4). 0 = não é ritual.")]
        [Range(0, 4)] public int circulo = 0;

        [Header("Tipo")]
        public SkillKind kind = SkillKind.Damage;
        public Element element = Element.Physical;
        public TargetType target = TargetType.SingleEnemy;

        [Header("Teste (Ordem Paranormal)")]
        public SkillResolution resolution = SkillResolution.AttackTest;
        [Tooltip("Ataque: quantos d20 o atacante rola (ex.: FOR para Luta, AGI para Pontaria).")]
        public Atributo testAttribute = Atributo.For;
        [Tooltip("Bônus da perícia: treinado +5, veterano +10, expert +15.")]
        public int testBonus = 5;
        [Tooltip("Ritual: atributo do alvo na resistência (VIG = Fortitude, AGI = Reflexos, PRE = Vontade).")]
        public Atributo resistAttribute = Atributo.Vig;
        [Range(15, 20)] public int critMargin = 20;

        [Header("Dano ou cura: XdY + fixo + atributo")]
        public int diceCount = 1;
        public int diceSides = 6;
        public int power = 0;
        public Atributo damageAttribute = Atributo.None;
        [Tooltip("Dano extra de Sanidade: rola 1dX (0 = nenhum).")]
        public int sanityDamage = 0;

        [Header("Custos")]
        [FormerlySerializedAs("mpCost")] public int peCost = 0;
        public int hpCost = 0;
        public int sanityCost = 0;
        public bool isItem = false;

        [Header("Status")]
        public bool appliesStatus = false;
        public StatusType status = StatusType.Bleeding;
        [Range(0f, 1f)] public float statusChance = 1f;
        public int statusDuration = 3;

        [Header("Apresentação")]
        public string animationTrigger = "Attack";
        public bool moveToTarget = true;
        public GameObject castVfx;
        public GameObject hitVfx;
        public AudioClip castSfx;
        public AudioClip hitSfx;
        [Range(0f, 1f)] public float cameraShake = 0.15f;

        public SkillData CreateData()
        {
            return new SkillData
            {
                Id = name,
                Name = displayName,
                Description = description,
                Kind = kind,
                Element = element,
                Target = target,
                Resolution = resolution,
                TestAttribute = testAttribute,
                TestBonus = testBonus,
                ResistAttribute = resistAttribute,
                CritMargin = critMargin,
                DiceCount = diceCount,
                DiceSides = diceSides,
                Power = power,
                DamageAttribute = damageAttribute,
                SanityDamage = sanityDamage,
                PeCost = peCost,
                HpCost = hpCost,
                SanityCost = sanityCost,
                IsItem = isItem,
                AppliesStatus = appliesStatus,
                Status = status,
                StatusChance = statusChance,
                StatusDuration = statusDuration,
                Tag = this
            };
        }

        /// <summary>Ex.: "3 PE · 10 SAN".</summary>
        public string CostLabel()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (peCost > 0) parts.Add($"{peCost} PE");
            if (hpCost > 0) parts.Add($"{hpCost} PV");
            if (sanityCost > 0) parts.Add($"{sanityCost} SAN");
            return string.Join(" · ", parts);
        }

        /// <summary>Linha de regras para a UI, ex.: "Ritual de Morte · 1º círculo · 2d8+2 · Fortitude reduz à metade".</summary>
        public string RulesLabel()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (circulo > 0) parts.Add($"Ritual de {ElementName(element)} · {circulo}º círculo");
            else if (element != Element.Physical) parts.Add(ElementName(element));

            string dice = diceCount > 0 ? $"{diceCount}d{diceSides}" : "";
            if (power > 0) dice += (dice.Length > 0 ? "+" : "") + power;
            if (damageAttribute != Atributo.None) dice += (dice.Length > 0 ? "+" : "") + AttrName(damageAttribute);
            if (dice.Length > 0)
            {
                string what = kind == SkillKind.Heal ? " PV" : kind == SkillKind.RestoreSanity || element == Element.Fear ? " SAN" : "";
                parts.Add(dice + what);
            }

            if (kind == SkillKind.Damage)
            {
                if (resolution == SkillResolution.AttackTest) parts.Add($"Ataque {AttrName(testAttribute)} +{testBonus}");
                else if (resolution == SkillResolution.RitualResistance) parts.Add($"{ResistName(resistAttribute)} reduz à metade");
            }
            if (appliesStatus) parts.Add(BattleSystem.StatusName(status) + (statusChance < 1f ? $" ({Mathf.RoundToInt(statusChance * 100)}%)" : ""));
            return string.Join(" · ", parts);
        }

        public static string ElementName(Element e)
        {
            switch (e)
            {
                case Element.Blood: return "Sangue";
                case Element.Death: return "Morte";
                case Element.Knowledge: return "Conhecimento";
                case Element.Energy: return "Energia";
                case Element.Fear: return "Medo";
                default: return "Físico";
            }
        }

        public static string AttrName(Atributo a)
        {
            switch (a)
            {
                case Atributo.Agi: return "AGI";
                case Atributo.For: return "FOR";
                case Atributo.Int: return "INT";
                case Atributo.Pre: return "PRE";
                case Atributo.Vig: return "VIG";
                default: return "";
            }
        }

        public static string ResistName(Atributo a)
        {
            switch (a)
            {
                case Atributo.Agi: return "Reflexos";
                case Atributo.Pre: return "Vontade";
                default: return "Fortitude";
            }
        }
    }
}
