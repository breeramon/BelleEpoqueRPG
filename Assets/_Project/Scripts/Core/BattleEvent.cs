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
}
