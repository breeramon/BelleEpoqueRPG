using System.Collections.Generic;
using System.Linq;

namespace BelleEpoque.Core
{
    public interface IEnemyAI
    {
        BattleAction ChooseAction(BattleUnit actor, BattleSystem battle, IRandom rng);
    }

    /// <summary>
    /// IA simples: cura aliados feridos, usa habilidades às vezes
    /// e prefere atacar quem está com pouca vida.
    /// </summary>
    public sealed class SimpleEnemyAI : IEnemyAI
    {
        public float SkillUseChance = 0.6f;
        public float FocusWeakestChance = 0.5f;
        public float HealThreshold = 0.4f;

        public BattleAction ChooseAction(BattleUnit actor, BattleSystem battle, IRandom rng)
        {
            var usable = actor.Skills.Where(s => !s.IsItem && actor.CanPay(s)).ToList();

            // 1) Curar aliado ferido
            var heal = usable.FirstOrDefault(s => s.Kind == SkillKind.Heal);
            if (heal != null)
            {
                var hurt = battle.AlliesOf(actor).Where(u => u.IsAlive && u.HpPercent < HealThreshold)
                                 .OrderBy(u => u.HpPercent).FirstOrDefault();
                if (hurt != null)
                    return BuildAction(actor, heal, battle, rng, hurt);
            }

            // 2) Habilidade ofensiva ou ataque básico
            var offensive = usable.Where(s => s.Kind == SkillKind.Damage).ToList();
            SkillData chosen = actor.BasicAttack;
            if (offensive.Count > 0 && rng.NextDouble() < SkillUseChance)
                chosen = offensive[rng.Next(0, offensive.Count)];

            return BuildAction(actor, chosen, battle, rng, null);
        }

        private BattleAction BuildAction(BattleUnit actor, SkillData skill, BattleSystem battle, IRandom rng, BattleUnit preferred)
        {
            var targets = battle.GetValidTargets(actor, skill).ToList();
            if (skill.TargetsAll || targets.Count == 0)
                return BattleAction.UseSkill(actor, skill, targets.ToArray());

            BattleUnit target = preferred;
            if (target == null || !targets.Contains(target))
            {
                if (rng.NextDouble() < FocusWeakestChance)
                    target = targets.OrderBy(t => t.HpPercent).First();
                else
                    target = targets[rng.Next(0, targets.Count)];
            }
            return BattleAction.UseSkill(actor, skill, target);
        }
    }
}
