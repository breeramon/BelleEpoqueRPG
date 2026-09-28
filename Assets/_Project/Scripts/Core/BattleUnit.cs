using System;
using System.Collections.Generic;
using System.Linq;

namespace BelleEpoque.Core
{
    public sealed class StatusInstance
    {
        public StatusType Type;
        public int RemainingTurns;
        public StatusInstance(StatusType type, int turns) { Type = type; RemainingTurns = turns; }
    }

    /// <summary>Estado de uma unidade durante a batalha. Não sabe nada de Unity.</summary>
    public sealed class BattleUnit
    {
        public const int DefendBonus = 5;
        public const int ShieldBonus = 5;

        public string Id { get; }
        public string Name { get; }
        public Team Team { get; }
        public Trilha Trilha { get; }
        public int Nex { get; }
        public StatBlock Stats { get; }
        public IReadOnlyList<SkillData> Skills { get; }
        public SkillData BasicAttack { get; }
        public ISet<Element> Weaknesses { get; }
        public ISet<Element> Resistances { get; }

        public int Hp { get; private set; }
        public int Pe { get; private set; }
        public int Sanity { get; private set; }
        public int Initiative { get; internal set; }
        public bool IsDefending { get; internal set; }
        public List<StatusInstance> Statuses { get; } = new List<StatusInstance>();

        public object Tag { get; set; }

        public bool IsAlive => Hp > 0;
        public bool UsesSanity => Stats.MaxSanity > 0;
        public float HpPercent => Stats.MaxHp <= 0 ? 0f : (float)Hp / Stats.MaxHp;
        public float PePercent => Stats.MaxPe <= 0 ? 0f : (float)Pe / Stats.MaxPe;
        public float SanityPercent => Stats.MaxSanity <= 0 ? 1f : (float)Sanity / Stats.MaxSanity;

        public BattleUnit(
            string id, string name, Team team, StatBlock stats, SkillData basicAttack, IEnumerable<SkillData> skills,
            IEnumerable<Element> weaknesses = null, IEnumerable<Element> resistances = null,
            Trilha trilha = Trilha.Ameaca, int nex = 0)
        {
            Id = id;
            Name = name;
            Team = team;
            Stats = stats;
            Trilha = trilha;
            Nex = nex;
            BasicAttack = basicAttack ?? throw new ArgumentNullException(nameof(basicAttack));
            Skills = (skills ?? Enumerable.Empty<SkillData>()).ToList();
            Weaknesses = new HashSet<Element>(weaknesses ?? Enumerable.Empty<Element>());
            Resistances = new HashSet<Element>(resistances ?? Enumerable.Empty<Element>());
            Hp = stats.MaxHp;
            Pe = stats.MaxPe;
            Sanity = stats.MaxSanity;
        }

        public int Attr(Atributo a) => Stats.Atributos.Get(a);

        /// <summary>Defesa atual, contando Defender e Protegido.</summary>
        public int CurrentDefesa => Stats.Defesa + (IsDefending ? DefendBonus : 0) + (HasStatus(StatusType.Shielded) ? ShieldBonus : 0);

        public SanityState SanityState
        {
            get
            {
                if (!UsesSanity) return SanityState.Lucid;
                if (Sanity <= 0) return SanityState.Broken;
                float p = SanityPercent;
                if (p < 0.25f) return SanityState.Panicked;
                if (p < 0.5f) return SanityState.Shaken;
                return SanityState.Lucid;
            }
        }

        /// <summary>Dados perdidos nos testes por causa da Sanidade baixa.</summary>
        public int SanityDicePenalty
        {
            get
            {
                switch (SanityState)
                {
                    case SanityState.Shaken: return 1;
                    case SanityState.Panicked:
                    case SanityState.Broken: return 2;
                    default: return 0;
                }
            }
        }

        public bool HasStatus(StatusType type) => Statuses.Any(s => s.Type == type);

        /// <summary>Motivo pelo qual não pode pagar, ou null se pode.</summary>
        public string CannotPayReason(SkillData skill)
        {
            if (skill.IsItem) return null;
            if (skill.PeCost > Stats.PePorRodada) return $"passa do limite de {Stats.PePorRodada} PE por rodada";
            if (Pe < skill.PeCost) return "PE insuficiente";
            if (skill.HpCost > 0 && Hp <= skill.HpCost) return "PV insuficiente";
            if (UsesSanity && Sanity < skill.SanityCost) return "Sanidade insuficiente";
            return null;
        }

        public bool CanPay(SkillData skill) => CannotPayReason(skill) == null;

        internal void Pay(SkillData skill)
        {
            if (skill.IsItem) return;
            Pe -= skill.PeCost;
            Hp -= skill.HpCost;
            if (UsesSanity) Sanity = Math.Max(0, Sanity - skill.SanityCost);
        }

        internal int TakeDamage(int amount)
        {
            int applied = Math.Min(Hp, Math.Max(0, amount));
            Hp -= applied;
            if (Hp <= 0) Statuses.Clear();
            return applied;
        }

        internal int Heal(int amount)
        {
            if (!IsAlive) return 0;
            int applied = Math.Min(Stats.MaxHp - Hp, Math.Max(0, amount));
            Hp += applied;
            return applied;
        }

        internal int ChangeSanity(int delta)
        {
            if (!UsesSanity || !IsAlive) return 0;
            int before = Sanity;
            Sanity = Math.Max(0, Math.Min(Stats.MaxSanity, Sanity + delta));
            return Sanity - before;
        }

        internal int RestorePe(int amount)
        {
            int before = Pe;
            Pe = Math.Min(Stats.MaxPe, Pe + Math.Max(0, amount));
            return Pe - before;
        }

        internal void AddStatus(StatusType type, int turns)
        {
            var existing = Statuses.FirstOrDefault(s => s.Type == type);
            if (existing != null) existing.RemainingTurns = Math.Max(existing.RemainingTurns, turns);
            else Statuses.Add(new StatusInstance(type, turns));
        }

        public override string ToString() => $"{Name} PV {Hp}/{Stats.MaxHp} PE {Pe}/{Stats.MaxPe} SAN {Sanity}/{Stats.MaxSanity}";
    }
}
