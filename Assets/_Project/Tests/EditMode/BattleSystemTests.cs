using System.Collections.Generic;
using System.Linq;
using BelleEpoque.Core;
using NUnit.Framework;

namespace BelleEpoque.Tests
{
    /// <summary>
    /// Testes da lógica de batalha e das regras de Ordem Paranormal.
    /// Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class BattleSystemTests
    {
        /// <summary>RNG fixo: todo dado cai na mesma fração da face (0.9 → d20 = 19, d8 = 8).</summary>
        private sealed class FixedRandom : IRandom
        {
            private readonly double _value;
            public FixedRandom(double value) { _value = value; }
            public double NextDouble() => _value;
            public int Next(int min, int maxExclusive) => min + (int)(_value * (maxExclusive - min));
        }

        private static SkillData Slash() => new SkillData
        {
            Id = "slash", Name = "Golpe", TestAttribute = Atributo.For, TestBonus = 5,
            DiceCount = 1, DiceSides = 8, DamageAttribute = Atributo.For
        };

        private static readonly Atributos Media = new Atributos(1, 2, 1, 1, 2);

        private static BattleUnit Hero(string name, int agi = 3, int hp = 100, params SkillData[] skills) =>
            new BattleUnit(name, name, Team.Heroes,
                new StatBlock(hp, 30, 100, 12, 7, new Atributos(agi, 2, 1, 1, 2)), Slash(), skills);

        private static BattleUnit Enemy(string name, int agi = 1, int hp = 50, int defesa = 12, IEnumerable<Element> weak = null) =>
            new BattleUnit(name, name, Team.Enemies,
                new StatBlock(hp, 10, 0, defesa, 5, new Atributos(agi, 2, 1, 1, 2)), Slash(), null, weak);

        // ------------------------------------------------------------------ Fórmulas (iguais à ficha DadosRPG)

        [Test]
        public void Ficha_CombatenteNex35_TemNumerosDaFicha()
        {
            var stats = OrdemRules.CalcularStats(Trilha.Combatente, 35, new Atributos(2, 3, 1, 1, 3));
            Assert.AreEqual(65, stats.MaxHp);     // (20+3) + 6*(4+3)
            Assert.AreEqual(21, stats.MaxPe);     // (2+1) + 6*(2+1)
            Assert.AreEqual(30, stats.MaxSanity); // 12 + 6*3
            Assert.AreEqual(12, stats.Defesa);    // 10 + AGI
            Assert.AreEqual(7, stats.PePorRodada);
        }

        [Test]
        public void Ficha_BateComAFichaWebEmVariosNex()
        {
            // Valores gerados pelo próprio src/lib/pericias.js do DadosRPG
            var a = OrdemRules.CalcularStats(Trilha.Combatente, 99, new Atributos(2, 3, 1, 1, 3));
            Assert.AreEqual(156, a.MaxHp); Assert.AreEqual(60, a.MaxPe); Assert.AreEqual(69, a.MaxSanity); Assert.AreEqual(20, a.PePorRodada);
            var b = OrdemRules.CalcularStats(Trilha.Ocultista, 35, new Atributos(2, 0, 4, 3, 1));
            Assert.AreEqual(31, b.MaxHp); Assert.AreEqual(49, b.MaxPe); Assert.AreEqual(50, b.MaxSanity);
            var c = OrdemRules.CalcularStats(Trilha.Especialista, 35, new Atributos(2, 1, 3, 2, 2));
            Assert.AreEqual(48, c.MaxHp); Assert.AreEqual(35, c.MaxPe); Assert.AreEqual(40, c.MaxSanity);
        }

        [Test]
        public void Ficha_OcultistaNex5_ValoresIniciais()
        {
            var stats = OrdemRules.CalcularStats(Trilha.Ocultista, 5, new Atributos(1, 0, 3, 2, 1));
            Assert.AreEqual(13, stats.MaxHp);
            Assert.AreEqual(6, stats.MaxPe);
            Assert.AreEqual(20, stats.MaxSanity);
            Assert.AreEqual(1, stats.PePorRodada);
        }

        [Test]
        public void TesteComAtributoZero_FicaComOMenorDe2d20()
        {
            var rng = new SequenceRandom(15, 4);
            int r = OrdemRules.RolarTeste(rng, 0, out _);
            Assert.AreEqual(4, r);
        }

        // ------------------------------------------------------------------ Batalha

        [Test]
        public void Start_IniciativaMaiorAgeAntes()
        {
            var hero = Hero("Lucien", agi: 3);
            var battle = new BattleSystem(new[] { hero }, new[] { Enemy("Cultista", agi: 1) }, new FixedRandom(0.5));
            battle.Start();
            Assert.AreEqual(BattleState.WaitingForPlayer, battle.State);
            Assert.AreSame(hero, battle.CurrentUnit);
        }

        [Test]
        public void Ataque_AcertaEDanoSegueADiceEAtributo()
        {
            var hero = Hero("Lucien");
            var enemy = Enemy("Cultista", hp: 100);
            // 0.9: d20 = 19 (+5 = 24 vs Defesa 12, acerta sem crítico); d8 = 8; + FOR 2 = 10
            var battle = new BattleSystem(new[] { hero }, new[] { enemy }, new FixedRandom(0.9));
            battle.Start();
            battle.SubmitPlayerAction(BattleAction.UseSkill(hero, hero.BasicAttack, enemy));
            Assert.AreEqual(90, enemy.Hp);
        }

        [Test]
        public void Ataque_ErraContraDefesaAlta()
        {
            var hero = Hero("Lucien");
            var enemy = Enemy("Manequim", hp: 100, defesa: 30);
            var battle = new BattleSystem(new[] { hero }, new[] { enemy }, new FixedRandom(0.5));
            battle.Start();
            var events = battle.SubmitPlayerAction(BattleAction.UseSkill(hero, hero.BasicAttack, enemy));
            Assert.AreEqual(100, enemy.Hp);
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.Miss && e.Actor == hero));
        }

        [Test]
        public void Vulnerabilidade_MultiplicaODano()
        {
            var hero = Hero("Margot");
            var enemy = Enemy("Manequim", hp: 100, weak: new[] { Element.Energy });
            var bolt = new SkillData { Name = "Eletrocussão", Element = Element.Energy, Resolution = SkillResolution.AttackTest,
                TestAttribute = Atributo.Int, DiceCount = 2, DiceSides = 6 };
            var battle = new BattleSystem(new[] { hero }, new[] { enemy }, new FixedRandom(0.9));
            battle.Start();
            battle.SubmitPlayerAction(BattleAction.UseSkill(hero, bolt, enemy));
            Assert.AreEqual(100 - 18, enemy.Hp); // 2d6 = 12 × 1,5
        }

        [Test]
        public void Ritual_AlvoQuePassaNaResistenciaSofreMetade()
        {
            var hero = Hero("Margot");
            var enemy = Enemy("Cultista", hp: 100);
            var decay = new SkillData { Name = "Decadência", Element = Element.Death, Resolution = SkillResolution.RitualResistance,
                ResistAttribute = Atributo.Vig, DiceCount = 2, DiceSides = 8, Power = 2 };
            // 0.9: resistência 2d20 → 19 vs DT 10+7+1 = 18 → passa; dano 8+8+2 = 18 → 9
            var battle = new BattleSystem(new[] { hero }, new[] { enemy }, new FixedRandom(0.9));
            battle.Start();
            battle.SubmitPlayerAction(BattleAction.UseSkill(hero, decay, enemy));
            Assert.AreEqual(91, enemy.Hp);
        }

        [Test]
        public void Custo_AcimaDoLimiteDePePorRodadaEBloqueado()
        {
            var big = new SkillData { Name = "Ritual de 3º círculo", PeCost = 10 };
            var hero = Hero("Margot", skills: big); // limite 7
            var battle = new BattleSystem(new[] { hero }, new[] { Enemy("Cultista") }, new FixedRandom(0.5));
            battle.Start();
            Assert.IsFalse(battle.CanUse(hero, big));
            StringAssert.Contains("limite", hero.CannotPayReason(big));
        }

        [Test]
        public void MatarUltimoInimigo_Vitoria()
        {
            var hero = Hero("Lucien");
            var enemy = Enemy("Rato", hp: 5);
            var battle = new BattleSystem(new[] { hero }, new[] { enemy }, new FixedRandom(0.9));
            battle.Start();
            battle.SubmitPlayerAction(BattleAction.UseSkill(hero, hero.BasicAttack, enemy));
            Assert.AreEqual(BattleState.Victory, battle.State);
        }

        [Test]
        public void Medo_TiraSanidadeENaoPV()
        {
            var hero = Hero("Céleste", agi: 0);
            var whisper = new SkillData { Name = "Sussurro", Element = Element.Fear, Resolution = SkillResolution.RitualResistance,
                ResistAttribute = Atributo.Pre, DiceCount = 2, DiceSides = 6 };
            var ghost = new BattleUnit("ghost", "Aparição", Team.Enemies,
                new StatBlock(50, 10, 0, 12, 5, new Atributos(4, 0, 2, 3, 1)), whisper, null);
            // 0.1: resistência falha (d20 = 2); 2d6 = 1+1 = 2
            var battle = new BattleSystem(new[] { hero }, new[] { ghost }, new FixedRandom(0.1));
            battle.Start();
            Assert.AreEqual(100, hero.Hp);
            Assert.AreEqual(98, hero.Sanity);
        }

        [Test]
        public void Item_ConsomeInventario()
        {
            var tonic = new SkillData { Name = "Tônico", Kind = SkillKind.Heal, Target = TargetType.SingleAlly,
                Resolution = SkillResolution.Automatic, DiceCount = 2, DiceSides = 8, IsItem = true };
            var hero = Hero("Lucien");
            var battle = new BattleSystem(new[] { hero }, new[] { Enemy("Cultista") }, new FixedRandom(0.5),
                inventory: new Dictionary<SkillData, int> { { tonic, 1 } });
            battle.Start();
            battle.SubmitPlayerAction(BattleAction.UseSkill(hero, tonic, hero));
            Assert.AreEqual(0, battle.GetItemCount(tonic));
        }

        [Test]
        public void Atordoado_PerdeOTurno()
        {
            var bash = new SkillData { Name = "Coronhada", TestAttribute = Atributo.For, DiceCount = 1, DiceSides = 4,
                AppliesStatus = true, Status = StatusType.Stunned, StatusChance = 1f, StatusDuration = 1 };
            var hero = Hero("Lucien", skills: bash);
            var enemy = Enemy("Cultista", hp: 500);
            var battle = new BattleSystem(new[] { hero }, new[] { enemy }, new FixedRandom(0.9));
            battle.Start();
            var events = battle.SubmitPlayerAction(BattleAction.UseSkill(hero, bash, enemy));
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.TurnSkipped && e.Actor == enemy));
            Assert.AreEqual(100, hero.Hp);
        }

        /// <summary>Devolve os valores pedidos, em ordem.</summary>
        private sealed class SequenceRandom : IRandom
        {
            private readonly int[] _values;
            private int _i;
            public SequenceRandom(params int[] values) { _values = values; }
            public double NextDouble() => 0.5;
            public int Next(int min, int maxExclusive) => _values[_i++ % _values.Length];
        }
    }
}
