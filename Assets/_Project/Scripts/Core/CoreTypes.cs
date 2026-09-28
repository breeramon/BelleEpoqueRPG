using System;

namespace BelleEpoque.Core
{
    /// <summary>Elementos paranormais de Ordem Paranormal, mais Físico (sem elemento).</summary>
    public enum Element
    {
        Physical,   // Físico
        Blood,      // Sangue
        Death,      // Morte
        Knowledge,  // Conhecimento
        Energy,     // Energia
        Fear        // Medo: dano vai para a Sanidade
    }

    public enum Team { Heroes, Enemies }

    public enum TargetType { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }

    public enum SkillKind
    {
        Damage,        // dano de PV (ou de Sanidade se Element == Fear)
        Heal,          // recupera PV
        RestoreSanity, // recupera Sanidade
        Buff           // só aplica status
    }

    /// <summary>Os cinco atributos de Ordem Paranormal.</summary>
    public enum Atributo { None, Agi, For, Int, Pre, Vig }

    /// <summary>Classe (trilha) do agente. Ameaça = inimigo com números definidos à mão.</summary>
    public enum Trilha { Combatente, Especialista, Ocultista, Ameaca }

    /// <summary>Como a habilidade decide se funciona.</summary>
    public enum SkillResolution
    {
        AttackTest,       // teste de ataque (dados do atributo + bônus) contra a Defesa do alvo
        RitualResistance, // acerta sempre; o alvo faz teste de resistência contra a DT do ritual e, se passar, sofre metade
        Automatic         // curas, buffs e itens
    }

    public enum StatusType
    {
        Bleeding,    // Sangrando: perde PV no início do turno
        Stunned,     // Atordoado: perde o turno
        Shielded,    // Protegido: +5 na Defesa
        Terrified,   // Aterrorizado: perde Sanidade no início do turno
        Regenerating // Regenerando: recupera PV no início do turno
    }

    public enum ActionType { Skill, Defend, Item }

    /// <summary>Faixas de Sanidade e seus efeitos.</summary>
    public enum SanityState
    {
        Lucid,      // >= 50%
        Shaken,     // < 50%: −1 dado nos testes
        Panicked,   // < 25%: −2 dados e chance de perder o turno
        Broken      // 0 (enlouquecendo): −2 dados e chance alta de perder o turno
    }

    [Serializable]
    public struct Atributos
    {
        public int Agi;
        public int For;
        public int Int;
        public int Pre;
        public int Vig;

        public Atributos(int agi, int forca, int intelecto, int presenca, int vigor)
        {
            Agi = agi; For = forca; Int = intelecto; Pre = presenca; Vig = vigor;
        }

        public int Get(Atributo a)
        {
            switch (a)
            {
                case Atributo.Agi: return Agi;
                case Atributo.For: return For;
                case Atributo.Int: return Int;
                case Atributo.Pre: return Pre;
                case Atributo.Vig: return Vig;
                default: return 0;
            }
        }

        public int Total => Agi + For + Int + Pre + Vig;
    }

    /// <summary>
    /// Números finais de uma unidade na batalha. Para agentes, é calculado por
    /// <see cref="OrdemRules.CalcularStats"/> a partir da ficha; para ameaças, é preenchido à mão.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        public int MaxHp;          // PV
        public int MaxPe;          // Pontos de Esforço
        public int MaxSanity;      // SAN (0 = não usa Sanidade, típico de criaturas)
        public int Defesa;
        public int PePorRodada;    // limite de PE gasto por turno (NEX / 5)
        public Atributos Atributos;
        public int IniciativaBonus;
        public int ResistenciaBonus; // bônus em Fortitude/Reflexos/Vontade

        public StatBlock(int maxHp, int maxPe, int maxSanity, int defesa, int pePorRodada, Atributos atributos,
            int iniciativaBonus = 0, int resistenciaBonus = 0)
        {
            MaxHp = maxHp; MaxPe = maxPe; MaxSanity = maxSanity; Defesa = defesa; PePorRodada = pePorRodada;
            Atributos = atributos; IniciativaBonus = iniciativaBonus; ResistenciaBonus = resistenciaBonus;
        }
    }

    /// <summary>Abstração de aleatoriedade: permite testes determinísticos.</summary>
    public interface IRandom
    {
        double NextDouble();
        /// <summary>Inteiro em [min, maxExclusive).</summary>
        int Next(int min, int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        private readonly Random _random;
        public SystemRandom() { _random = new Random(); }
        public SystemRandom(int seed) { _random = new Random(seed); }
        public double NextDouble() => _random.NextDouble();
        public int Next(int min, int maxExclusive) => _random.Next(min, maxExclusive);
    }
}
