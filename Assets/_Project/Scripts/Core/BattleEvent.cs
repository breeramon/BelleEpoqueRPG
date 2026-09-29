using System.Collections.Generic;

namespace BelleEpoque.Core
{
    public enum BattleEventType
    {
        BattleStarted,
        InitiativeRolled,
        RoundStarted,
        TurnStarted,
        TurnSkipped,      // atordoado ou paralisado de medo
        ActionStarted,    // unidade começou uma ação (use para animação de ataque/conjuração)
        Damage,
        Heal,
        SanityChanged,
        Miss,
        StatusApplied,
        StatusExpired,
        StatusTick,       // sangramento, regeneração, terror no início do turno
        Defending,
        ItemUsed,
        UnitDied,
        Victory,
        Defeat
    }

    /// <summary>
    /// Algo que aconteceu na batalha. A camada Unity lê a lista de eventos
    /// e toca animações, sons, VFX e números de dano na ordem certa.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type;
        public BattleUnit Actor;
        public BattleUnit Target;
        public SkillData Skill;
        public int Amount;
        public bool IsCritical;
        public bool IsWeakness;
        public bool IsResisted;
        public StatusType Status;
        public string Message = "";
        /// <summary>Detalhe da rolagem (ex.: "3d20 [4,17,9]+5 = 22 vs Defesa 16").</summary>
        public string Roll = "";

        /// <summary>
        /// Como cada unidade estava logo depois deste evento. A lógica resolve a rodada inteira de uma vez;
        /// a HUD usa estes retratos para mostrar os números mudando na ordem certa (cura, depois dano...).
        /// </summary>
        public Dictionary<BattleUnit, UnitState> States;

        public override string ToString() => string.IsNullOrEmpty(Message) ? Type.ToString() : Message;
    }

    public sealed class BattleAction
    {
        public ActionType Type;
        public BattleUnit Actor;
        public SkillData Skill;
        public List<BattleUnit> Targets = new List<BattleUnit>();

        public static BattleAction Defend(BattleUnit actor) =>
            new BattleAction { Type = ActionType.Defend, Actor = actor };

        public static BattleAction UseSkill(BattleUnit actor, SkillData skill, params BattleUnit[] targets) =>
            new BattleAction { Type = skill.IsItem ? ActionType.Item : ActionType.Skill, Actor = actor, Skill = skill, Targets = new List<BattleUnit>(targets) };
    }

    /// <summary>Retrato dos números de uma unidade num instante da batalha.</summary>
    public sealed class UnitState
    {
        public int Hp, Pe, Sanity, Defesa;
        public bool Alive, Defending;
        public SanityState SanityState;
        public List<StatusInstance> Statuses = new List<StatusInstance>();

        public static UnitState Of(BattleUnit u)
        {
            var s = new UnitState
            {
                Hp = u.Hp, Pe = u.Pe, Sanity = u.Sanity, Defesa = u.CurrentDefesa,
                Alive = u.IsAlive, Defending = u.IsDefending, SanityState = u.SanityState
            };
            foreach (var st in u.Statuses) s.Statuses.Add(new StatusInstance(st.Type, st.RemainingTurns));
            return s;
        }
    }
}
