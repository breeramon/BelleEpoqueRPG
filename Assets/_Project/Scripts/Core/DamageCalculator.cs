using System;

namespace BelleEpoque.Core
{
    public struct HitResult
    {
        public bool Hit;
        public int Amount;
        public bool IsCritical;
        public bool IsWeakness;
        public bool IsResisted;   // resistência elemental (dano × 0,5)
        public bool SavedHalf;    // passou no teste de resistência do ritual
        public string Roll;       // texto do teste, para o log
    }

    /// <summary>Testes e dano no estilo de Ordem Paranormal.</summary>
    public static class DamageCalculator
    {
        public const float WeaknessMultiplier = 1.5f;
        public const float ResistMultiplier = 0.5f;
        public const int CritDiceMultiplier = 2; // crítico: dados de dano × 2

        public static HitResult Resolve(BattleUnit attacker, BattleUnit target, SkillData skill, IRandom rng)
        {
            switch (skill.Resolution)
            {
                case SkillResolution.RitualResistance: return ResolveRitual(attacker, target, skill, rng);
                case SkillResolution.Automatic: return RollDamage(attacker, target, skill, rng, false, "");
                default: return ResolveAttack(attacker, target, skill, rng);
            }
        }

        //Teste de ataque: Xd20 (X = atributo − penalidade de Sanidade) + bônus contra a Defesa.
        public static HitResult ResolveAttack(BattleUnit attacker, BattleUnit target, SkillData skill, IRandom rng)
        {
            int dice = attacker.Attr(skill.TestAttribute) - attacker.SanityDicePenalty;
            int natural = OrdemRules.RolarTeste(rng, dice, out string detail);
            int total = natural + skill.TestBonus;
            int defesa = target.CurrentDefesa;
            bool crit = natural >= skill.CritMargin;
            bool hit = crit || total >= defesa;
            string roll = $"{detail}+{skill.TestBonus} = {total} vs Defesa {defesa}";
            if (!hit) return new HitResult { Roll = roll };
            return RollDamage(attacker, target, skill, rng, crit, roll);
        }

        /// <summary>Ritual: acerta sempre; o alvo resiste contra a DT e, se passar, sofre metade.</summary>
        public static HitResult ResolveRitual(BattleUnit caster, BattleUnit target, SkillData skill, IRandom rng)
        {
            int dt = OrdemRules.DtRitual(caster);
            int dice = target.Attr(skill.ResistAttribute) - target.SanityDicePenalty;
            int natural = OrdemRules.RolarTeste(rng, dice, out string detail);
            int total = natural + target.Stats.ResistenciaBonus;
            bool saved = total >= dt;
            string roll = $"resistência {detail}+{target.Stats.ResistenciaBonus} = {total} vs DT {dt}";
            var result = RollDamage(caster, target, skill, rng, false, roll);
            if (saved)
            {
                result.SavedHalf = true;
                result.Amount = Math.Max(1, result.Amount / 2);
            }
            return result;
        }

        private static HitResult RollDamage(BattleUnit attacker, BattleUnit target, SkillData skill, IRandom rng, bool crit, string roll)
        {
            int dice = skill.DiceCount * (crit ? CritDiceMultiplier : 1);
            float dmg = OrdemRules.RolarDados(rng, dice, skill.DiceSides) + skill.Power + attacker.Attr(skill.DamageAttribute);

            var result = new HitResult { Hit = true, IsCritical = crit, Roll = roll };
            if (target.Weaknesses.Contains(skill.Element)) { result.IsWeakness = true; dmg *= WeaknessMultiplier; }
            else if (target.Resistances.Contains(skill.Element)) { result.IsResisted = true; dmg *= ResistMultiplier; }

            result.Amount = Math.Max(1, (int)Math.Round(dmg, MidpointRounding.AwayFromZero));
            return result;
        }

        /// <summary>Cura de PV ou de Sanidade: XdY + Power + atributo.</summary>
        public static int ResolveRestore(BattleUnit user, SkillData skill, IRandom rng)
        {
            int amount = OrdemRules.RolarDados(rng, skill.DiceCount, skill.DiceSides) + skill.Power + user.Attr(skill.DamageAttribute);
            return Math.Max(1, amount);
        }
    }
}
