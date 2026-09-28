namespace BelleEpoque.Core
{
    /// <summary>
    /// Habilidade, ritual ou item em C# puro. Na Unity é gerado a partir de um SkillDefinition.
    /// Dano/cura = XdY + Power + atributo (ex.: 1d8 + FOR para um sabre).
    /// </summary>
    public sealed class SkillData
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public SkillKind Kind = SkillKind.Damage;
        public Element Element = Element.Physical;
        public TargetType Target = TargetType.SingleEnemy;

        // Como resolver
        public SkillResolution Resolution = SkillResolution.AttackTest;
        /// <summary>Atributo que define quantos d20 o atacante rola.</summary>
        public Atributo TestAttribute = Atributo.For;
        /// <summary>Bônus da perícia (treinado +5, veterano +10, expert +15) e outros.</summary>
        public int TestBonus = 5;
        /// <summary>Atributo que o alvo usa no teste de resistência (Vig = Fortitude, Agi = Reflexos, Pre = Vontade).</summary>
        public Atributo ResistAttribute = Atributo.Vig;
        /// <summary>Resultado natural mínimo no d20 para crítico.</summary>
        public int CritMargin = 20;

        // Efeito
        public int DiceCount = 1;
        public int DiceSides = 6;
        public int Power = 0;
        /// <summary>Atributo somado ao dano ou cura (ex.: FOR em corpo a corpo).</summary>
        public Atributo DamageAttribute = Atributo.None;
        /// <summary>Dano extra de Sanidade além do dano normal (rolado como 1dX, X = este valor).</summary>
        public int SanityDamage = 0;

        // Custos
        public int PeCost = 0;
        public int HpCost = 0;
        public int SanityCost = 0;

        // Status
        public bool AppliesStatus = false;
        public StatusType Status = StatusType.Bleeding;
        public float StatusChance = 1f;
        public int StatusDuration = 3;

        public bool IsItem = false;

        /// <summary>Referência livre para a apresentação (o SkillDefinition com VFX e sons).</summary>
        public object Tag;

        public bool TargetsAllies => Target == TargetType.SingleAlly || Target == TargetType.AllAllies || Target == TargetType.Self;
        public bool TargetsAll => Target == TargetType.AllAllies || Target == TargetType.AllEnemies;

        public string DiceLabel
        {
            get
            {
                if (DiceCount <= 0) return Power > 0 ? Power.ToString() : "";
                string s = $"{DiceCount}d{DiceSides}";
                if (Power > 0) s += $"+{Power}";
                return s;
            }
        }
    }
}
