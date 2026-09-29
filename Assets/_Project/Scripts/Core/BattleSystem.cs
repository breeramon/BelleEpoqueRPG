using System;
using System.Collections.Generic;
using System.Linq;

namespace BelleEpoque.Core
{
    public enum BattleState
    {
        NotStarted,
        WaitingForPlayer, // CurrentUnit é um herói esperando comando
        Victory,
        Defeat
    }

    /// <summary>
    /// Máquina de estados da batalha por turnos, em C# puro.
    ///
    /// Fluxo:
    ///   Start() -> (turnos de inimigos resolvidos automaticamente) -> WaitingForPlayer
    ///   SubmitPlayerAction(acao) -> ... -> WaitingForPlayer | Victory | Defeat
    ///
    /// Cada chamada devolve a lista de eventos que aconteceram, em ordem,
    /// para a camada visual reproduzir.
    /// </summary>
    public sealed class BattleSystem
    {
        public const int HorrorOnAllyDeath = 10;   // Sanidade perdida ao ver um aliado cair
        public const int DefendPeRestore = 2;       // Defender: +5 Defesa e recupera o fôlego (2 PE)
        public const float PanicSkipChance = 0.15f;
        public const float BrokenSkipChance = 0.35f;
        public const float BleedPercent = 0.08f;
        public const float RegenPercent = 0.08f;
        public const int TerrorPerTurn = 6;

        private readonly List<BattleUnit> _heroes;
        private readonly List<BattleUnit> _enemies;
        private readonly Queue<BattleUnit> _turnQueue = new Queue<BattleUnit>();
        private readonly Dictionary<SkillData, int> _inventory;
        private readonly IRandom _rng;
        private readonly IEnemyAI _ai;

        public BattleState State { get; private set; } = BattleState.NotStarted;
        public BattleUnit CurrentUnit { get; private set; }
        public int Round { get; private set; }

        public IReadOnlyList<BattleUnit> Heroes => _heroes;
        public IReadOnlyList<BattleUnit> Enemies => _enemies;
        public IEnumerable<BattleUnit> AllUnits => _heroes.Concat(_enemies);
        public IReadOnlyDictionary<SkillData, int> Inventory => _inventory;
        public bool IsOver => State == BattleState.Victory || State == BattleState.Defeat;

        public BattleSystem(
            IEnumerable<BattleUnit> heroes,
            IEnumerable<BattleUnit> enemies,
            IRandom rng = null,
            IEnemyAI enemyAI = null,
            IDictionary<SkillData, int> inventory = null)
        {
            _heroes = heroes.ToList();
            _enemies = enemies.ToList();
            if (_heroes.Count == 0) throw new ArgumentException("A batalha precisa de pelo menos um herói.");
            if (_enemies.Count == 0) throw new ArgumentException("A batalha precisa de pelo menos um inimigo.");
            _rng = rng ?? new SystemRandom();
            _ai = enemyAI ?? new SimpleEnemyAI();
            _inventory = inventory != null ? new Dictionary<SkillData, int>(inventory) : new Dictionary<SkillData, int>();
        }

        // ------------------------------------------------------------------ API pública

        public List<BattleEvent> Start()
        {
            if (State != BattleState.NotStarted) throw new InvalidOperationException("A batalha já começou.");
            var events = new List<BattleEvent>();
            Emit(events, new BattleEvent { Type = BattleEventType.BattleStarted, Message = "A batalha começou!" });
            RollInitiative(events);
            Advance(events);
            return events;
        }

        public List<BattleEvent> SubmitPlayerAction(BattleAction action)
        {
            if (State != BattleState.WaitingForPlayer) throw new InvalidOperationException("Não é o turno do jogador.");
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Actor != CurrentUnit) throw new InvalidOperationException("Esta unidade não está no turno.");
            if (!IsActionValid(action, out string error)) throw new InvalidOperationException(error);

            var events = new List<BattleEvent>();
            Execute(action, events);
            Advance(events);
            return events;
        }

        /// <summary>Adiciona o evento junto com o retrato de todas as unidades naquele instante.</summary>
        private void Emit(List<BattleEvent> events, BattleEvent e)
        {
            e.States = new Dictionary<BattleUnit, UnitState>();
            foreach (var u in AllUnits) e.States[u] = UnitState.Of(u);
            events.Add(e);
        }

        public IEnumerable<BattleUnit> AlliesOf(BattleUnit unit) => unit.Team == Team.Heroes ? _heroes : _enemies;
        public IEnumerable<BattleUnit> OpponentsOf(BattleUnit unit) => unit.Team == Team.Heroes ? _enemies : _heroes;

        public IEnumerable<BattleUnit> GetValidTargets(BattleUnit actor, SkillData skill)
        {
            switch (skill.Target)
            {
                case TargetType.Self: return new[] { actor };
                case TargetType.SingleAlly:
                case TargetType.AllAllies: return AlliesOf(actor).Where(u => u.IsAlive);
                default: return OpponentsOf(actor).Where(u => u.IsAlive);
            }
        }

        public int GetItemCount(SkillData item) => _inventory.TryGetValue(item, out int n) ? n : 0;

        public bool CanUse(BattleUnit actor, SkillData skill)
        {
            if (skill.IsItem && GetItemCount(skill) <= 0) return false;
            return actor.CanPay(skill) && GetValidTargets(actor, skill).Any();
        }

        public bool IsActionValid(BattleAction action, out string error)
        {
            error = "";
            if (action.Type == ActionType.Defend) return true;
            if (action.Skill == null) { error = "Ação sem habilidade."; return false; }
            if (!CanUse(action.Actor, action.Skill))
            {
                string reason = action.Actor.CannotPayReason(action.Skill) ?? "sem alvos ou sem itens";
                error = $"{action.Skill.Name}: {reason}.";
                return false;
            }
            if (!action.Skill.TargetsAll)
            {
                if (action.Targets.Count != 1) { error = "Escolha exatamente um alvo."; return false; }
                if (!GetValidTargets(action.Actor, action.Skill).Contains(action.Targets[0])) { error = "Alvo inválido."; return false; }
            }
            return true;
        }

        // ------------------------------------------------------------------ Loop de turnos

        private void Advance(List<BattleEvent> events)
        {
            while (true)
            {
                if (CheckEnd(events)) return;

                if (_turnQueue.Count == 0) BeginRound(events);
                var unit = _turnQueue.Dequeue();
                if (!unit.IsAlive) continue;

                CurrentUnit = unit;
                Emit(events, new BattleEvent { Type = BattleEventType.TurnStarted, Actor = unit, Message = $"Turno de {unit.Name}." });

                if (!StartTurn(unit, events)) continue; // perdeu o turno ou morreu
                if (CheckEnd(events)) return;

                if (unit.Team == Team.Heroes)
                {
                    State = BattleState.WaitingForPlayer;
                    return;
                }

                var action = _ai.ChooseAction(unit, this, _rng);
                if (action == null || !IsActionValid(action, out _)) action = BattleAction.Defend(unit);
                Execute(action, events);
            }
        }

        private List<BattleUnit> _initiativeOrder = new List<BattleUnit>();
        public IReadOnlyList<BattleUnit> InitiativeOrder => _initiativeOrder;

        /// <summary>Próximos turnos (a unidade atual primeiro), para a barra de ordem de turnos.</summary>
        public List<BattleUnit> PreviewTurns(int count)
        {
            var list = new List<BattleUnit>();
            if (CurrentUnit != null && CurrentUnit.IsAlive) list.Add(CurrentUnit);
            foreach (var u in _turnQueue)
            {
                if (list.Count >= count) return list;
                if (u.IsAlive) list.Add(u);
            }
            if (!_initiativeOrder.Any(u => u.IsAlive)) return list;
            while (list.Count < count)
                foreach (var u in _initiativeOrder)
                {
                    if (list.Count >= count) break;
                    if (u.IsAlive) list.Add(u);
                }
            return list;
        }

        /// <summary>Iniciativa de Ordem: AGI d20 + bônus, rolada uma vez no início da batalha.</summary>
        private void RollInitiative(List<BattleEvent> events)
        {
            var rolls = new List<string>();
            foreach (var u in AllUnits)
            {
                u.Initiative = OrdemRules.RolarTeste(_rng, u.Attr(Atributo.Agi), out _) + u.Stats.IniciativaBonus;
            }
            _initiativeOrder = AllUnits
                .Select(u => new { Unit = u, Tie = _rng.NextDouble() })
                .OrderByDescending(x => x.Unit.Initiative)
                .ThenByDescending(x => x.Unit.Attr(Atributo.Agi))
                .ThenBy(x => x.Tie)
                .Select(x => x.Unit).ToList();
            string order = string.Join(" · ", _initiativeOrder.Select(u => $"{u.Name} {u.Initiative}"));
            Emit(events, new BattleEvent { Type = BattleEventType.InitiativeRolled, Message = "Iniciativa: " + order, Roll = order });
        }

        private void BeginRound(List<BattleEvent> events)
        {
            Round++;
            foreach (var u in _initiativeOrder.Where(u => u.IsAlive)) _turnQueue.Enqueue(u);
            Emit(events, new BattleEvent { Type = BattleEventType.RoundStarted, Amount = Round, Message = $"Rodada {Round}" });
        }

        /// <returns>true se a unidade pode agir neste turno.</returns>
        private bool StartTurn(BattleUnit unit, List<BattleEvent> events)
        {
            unit.IsDefending = false;

            // Efeitos contínuos
            foreach (var status in unit.Statuses.ToList())
            {
                switch (status.Type)
                {
                    case StatusType.Bleeding:
                    {
                        int dmg = unit.TakeDamage(Math.Max(1, (int)(unit.Stats.MaxHp * BleedPercent)));
                        Emit(events, new BattleEvent { Type = BattleEventType.StatusTick, Actor = unit, Target = unit, Status = StatusType.Bleeding, Amount = -dmg, Message = $"{unit.Name} sangra ({dmg})." });
                        break;
                    }
                    case StatusType.Regenerating:
                    {
                        int heal = unit.Heal(Math.Max(1, (int)(unit.Stats.MaxHp * RegenPercent)));
                        Emit(events, new BattleEvent { Type = BattleEventType.StatusTick, Actor = unit, Target = unit, Status = StatusType.Regenerating, Amount = heal, Message = $"{unit.Name} regenera ({heal})." });
                        break;
                    }
                    case StatusType.Terrified:
                    {
                        int delta = unit.ChangeSanity(-TerrorPerTurn);
                        if (delta != 0)
                            Emit(events, new BattleEvent { Type = BattleEventType.SanityChanged, Actor = unit, Target = unit, Status = StatusType.Terrified, Amount = delta, Message = $"{unit.Name} é consumido(a) pelo terror ({delta} SAN)." });
                        break;
                    }
                }
                if (!unit.IsAlive) break;
            }

            if (!unit.IsAlive)
            {
                OnUnitDied(unit, events);
                return false;
            }

            bool stunned = unit.HasStatus(StatusType.Stunned);

            // Duração dos status diminui no início de cada turno da unidade.
            foreach (var status in unit.Statuses.ToList())
            {
                status.RemainingTurns--;
                if (status.RemainingTurns <= 0)
                {
                    unit.Statuses.Remove(status);
                    Emit(events, new BattleEvent { Type = BattleEventType.StatusExpired, Actor = unit, Target = unit, Status = status.Type, Message = $"{StatusName(status.Type)} de {unit.Name} acabou." });
                }
            }

            if (stunned)
            {
                Emit(events, new BattleEvent { Type = BattleEventType.TurnSkipped, Actor = unit, Status = StatusType.Stunned, Message = $"{unit.Name} está atordoado(a)!" });
                return false;
            }

            float skip = unit.SanityState == SanityState.Broken ? BrokenSkipChance
                       : unit.SanityState == SanityState.Panicked ? PanicSkipChance : 0f;
            if (skip > 0f && _rng.NextDouble() < skip)
            {
                Emit(events, new BattleEvent { Type = BattleEventType.TurnSkipped, Actor = unit, Message = $"{unit.Name} está paralisado(a) de medo!" });
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ Execução de ações

        private void Execute(BattleAction action, List<BattleEvent> events)
        {
            var actor = action.Actor;

            if (action.Type == ActionType.Defend)
            {
                actor.IsDefending = true;
                int pe = actor.RestorePe(DefendPeRestore);
                Emit(events, new BattleEvent { Type = BattleEventType.Defending, Actor = actor, Amount = pe, Message = $"{actor.Name} se defende (+{BattleUnit.DefendBonus} Defesa{(pe > 0 ? $", +{pe} PE" : "")})." });
                return;
            }

            var skill = action.Skill;
            if (skill.IsItem)
            {
                _inventory[skill] = GetItemCount(skill) - 1;
                Emit(events, new BattleEvent { Type = BattleEventType.ItemUsed, Actor = actor, Skill = skill, Amount = _inventory[skill], Message = $"{actor.Name} usa {skill.Name}." });
            }

            actor.Pay(skill);
            Emit(events, new BattleEvent { Type = BattleEventType.ActionStarted, Actor = actor, Skill = skill, Message = $"{actor.Name} usa {skill.Name}!" });

            if (skill.HpCost > 0)
                Emit(events, new BattleEvent { Type = BattleEventType.Damage, Actor = actor, Target = actor, Skill = skill, Amount = skill.HpCost, Message = $"{actor.Name} sacrifica {skill.HpCost} de vida." });
            if (skill.SanityCost > 0 && actor.Stats.MaxSanity > 0)
                Emit(events, new BattleEvent { Type = BattleEventType.SanityChanged, Actor = actor, Target = actor, Skill = skill, Amount = -skill.SanityCost, Message = $"{actor.Name} perde {skill.SanityCost} de Sanidade no ritual." });

            var targets = skill.TargetsAll
                ? GetValidTargets(actor, skill).ToList()
                : action.Targets.Where(t => t.IsAlive).ToList();

            foreach (var target in targets)
            {
                ApplySkill(actor, target, skill, events);
                if (CheckEnd(events)) return;
            }
        }

        private void ApplySkill(BattleUnit actor, BattleUnit target, SkillData skill, List<BattleEvent> events)
        {
            bool landed = true;

            switch (skill.Kind)
            {
                case SkillKind.Damage:
                {
                    var hit = DamageCalculator.Resolve(actor, target, skill, _rng);
                    if (!hit.Hit)
                    {
                        landed = false;
                        Emit(events, new BattleEvent { Type = BattleEventType.Miss, Actor = actor, Target = target, Skill = skill, Roll = hit.Roll, Message = $"{skill.Name} errou {target.Name}." });
                        break;
                    }
                    string halfNote = hit.SavedHalf ? " Resistiu: metade." : "";

                    if (skill.Element == Element.Fear && target.Stats.MaxSanity > 0)
                    {
                        int delta = target.ChangeSanity(-hit.Amount);
                        Emit(events, new BattleEvent { Type = BattleEventType.SanityChanged, Actor = actor, Target = target, Skill = skill, Amount = delta, IsCritical = hit.IsCritical, IsWeakness = hit.IsWeakness, IsResisted = hit.IsResisted, Roll = hit.Roll, Message = $"{target.Name} perde {-delta} de Sanidade.{halfNote}" });
                    }
                    else
                    {
                        int dealt = target.TakeDamage(hit.Amount);
                        string extra = hit.IsWeakness ? " Vulnerável!" : hit.IsResisted ? " Resistente." : "";
                        if (hit.IsCritical) extra = " Crítico!" + extra;
                        Emit(events, new BattleEvent { Type = BattleEventType.Damage, Actor = actor, Target = target, Skill = skill, Amount = dealt, IsCritical = hit.IsCritical, IsWeakness = hit.IsWeakness, IsResisted = hit.IsResisted, Roll = hit.Roll, Message = $"{target.Name} sofre {dealt} de dano.{extra}{halfNote}" });
                    }

                    if (skill.SanityDamage > 0 && target.IsAlive)
                    {
                        int delta = target.ChangeSanity(-OrdemRules.RolarDados(_rng, 1, skill.SanityDamage));
                        if (delta != 0)
                            Emit(events, new BattleEvent { Type = BattleEventType.SanityChanged, Actor = actor, Target = target, Skill = skill, Amount = delta, Message = $"{target.Name} perde {-delta} de Sanidade." });
                    }
                    break;
                }
                case SkillKind.Heal:
                {
                    int healed = target.Heal(DamageCalculator.ResolveRestore(actor, skill, _rng));
                    Emit(events, new BattleEvent { Type = BattleEventType.Heal, Actor = actor, Target = target, Skill = skill, Amount = healed, Message = $"{target.Name} recupera {healed} de vida." });
                    break;
                }
                case SkillKind.RestoreSanity:
                {
                    int delta = target.ChangeSanity(DamageCalculator.ResolveRestore(actor, skill, _rng));
                    Emit(events, new BattleEvent { Type = BattleEventType.SanityChanged, Actor = actor, Target = target, Skill = skill, Amount = delta, Message = $"{target.Name} recupera {delta} de Sanidade." });
                    break;
                }
                case SkillKind.Buff:
                    break;
            }

            if (!target.IsAlive)
            {
                OnUnitDied(target, events);
                return;
            }

            if (landed && skill.AppliesStatus && _rng.NextDouble() < skill.StatusChance)
            {
                target.AddStatus(skill.Status, skill.StatusDuration);
                Emit(events, new BattleEvent { Type = BattleEventType.StatusApplied, Actor = actor, Target = target, Skill = skill, Status = skill.Status, Amount = skill.StatusDuration, Message = $"{target.Name}: {StatusName(skill.Status)}!" });
            }
        }

        private void OnUnitDied(BattleUnit unit, List<BattleEvent> events)
        {
            Emit(events, new BattleEvent { Type = BattleEventType.UnitDied, Actor = unit, Target = unit, Message = $"{unit.Name} caiu!" });

            // Ver um companheiro cair abala a sanidade dos heróis.
            if (unit.Team != Team.Heroes) return;
            foreach (var ally in _heroes.Where(h => h.IsAlive && h != unit))
            {
                int delta = ally.ChangeSanity(-HorrorOnAllyDeath);
                if (delta != 0)
                    Emit(events, new BattleEvent { Type = BattleEventType.SanityChanged, Actor = unit, Target = ally, Amount = delta, Message = $"{ally.Name} fica horrorizado(a) ({delta} SAN)." });
            }
        }

        private bool CheckEnd(List<BattleEvent> events)
        {
            if (IsOver) return true;
            if (_enemies.All(e => !e.IsAlive))
            {
                State = BattleState.Victory;
                CurrentUnit = null;
                Emit(events, new BattleEvent { Type = BattleEventType.Victory, Message = "Vitória! O Outro Lado recua... por enquanto." });
                return true;
            }
            if (_heroes.All(h => !h.IsAlive))
            {
                State = BattleState.Defeat;
                CurrentUnit = null;
                Emit(events, new BattleEvent { Type = BattleEventType.Defeat, Message = "Derrota. A névoa engole a expedição." });
                return true;
            }
            return false;
        }

        public static string StatusName(StatusType type)
        {
            switch (type)
            {
                case StatusType.Bleeding: return "Sangrando";
                case StatusType.Stunned: return "Atordoado";
                case StatusType.Shielded: return "Protegido";
                case StatusType.Terrified: return "Aterrorizado";
                case StatusType.Regenerating: return "Regenerando";
                default: return type.ToString();
            }
        }
    }
}
