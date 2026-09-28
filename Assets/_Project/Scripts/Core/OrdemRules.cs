using System;

namespace BelleEpoque.Core
{
    /// <summary>
    /// Regras de Ordem Paranormal usadas no jogo. As fórmulas de PV, PE, Sanidade,
    /// Defesa e limite de PE foram portadas de <c>src/lib/pericias.js</c> do projeto DadosRPG
    /// (a ficha web), para que um agente tenha os mesmos números nos dois lugares.
    /// </summary>
    public static class OrdemRules
    {
        public struct RecursosTrilha
        {
            public int PvInicial, PvIncremento, PeInicial, PeIncremento, SanInicial, SanIncremento;
        }

        // RECURSOS do DadosRPG ("det" = PE)
        public static readonly RecursosTrilha Combatente = new RecursosTrilha { PvInicial = 20, PvIncremento = 4, PeInicial = 2, PeIncremento = 2, SanInicial = 12, SanIncremento = 3 };
        public static readonly RecursosTrilha Especialista = new RecursosTrilha { PvInicial = 16, PvIncremento = 3, PeInicial = 3, PeIncremento = 3, SanInicial = 16, SanIncremento = 4 };
        public static readonly RecursosTrilha Ocultista = new RecursosTrilha { PvInicial = 12, PvIncremento = 2, PeInicial = 4, PeIncremento = 4, SanInicial = 20, SanIncremento = 5 };

        public static RecursosTrilha Recursos(Trilha trilha)
        {
            switch (trilha)
            {
                case Trilha.Especialista: return Especialista;
                case Trilha.Ocultista: return Ocultista;
                default: return Combatente;
            }
        }

        /// <summary>NEX vai de 5 a 99, em passos de 5 (99 é o teto).</summary>
        public static int ClampNex(int nex)
        {
            int n = (int)Math.Round(nex / 5.0, MidpointRounding.AwayFromZero) * 5;
            if (n < 5) n = 5;
            if (n > 99) n = 99;
            return n;
        }

        /// <summary>Quantos aumentos de NEX além dos 5% iniciais.</summary>
        public static int PassosDeNex(int nex) => Math.Max(0, (int)Math.Round((ClampNex(nex) - 5) / 5.0, MidpointRounding.AwayFromZero));

        public static int VidaMaxima(Trilha trilha, int vigor, int nex)
        {
            var r = Recursos(trilha);
            return (r.PvInicial + vigor) + PassosDeNex(nex) * (r.PvIncremento + vigor);
        }

        /// <summary>Pontos de Esforço (no DadosRPG, "determinacaoMaxima").</summary>
        public static int PeMaximo(Trilha trilha, int presenca, int nex)
        {
            var r = Recursos(trilha);
            return (r.PeInicial + presenca) + PassosDeNex(nex) * (r.PeIncremento + presenca);
        }

        public static int SanidadeMaxima(Trilha trilha, int nex)
        {
            var r = Recursos(trilha);
            return r.SanInicial + PassosDeNex(nex) * r.SanIncremento;
        }

        /// <summary>Limite de PE que pode ser gasto por rodada.</summary>
        public static int PePorRodada(int nex) => (int)Math.Round(ClampNex(nex) / 5.0, MidpointRounding.AwayFromZero);

        /// <summary>Defesa = 10 + Agilidade + equipamento + outros.</summary>
        public static int Defesa(int agilidade, int bonusEquipamento, int bonusOutros) => 10 + agilidade + bonusEquipamento + bonusOutros;

        /// <summary>DT dos rituais de quem conjura: 10 + limite de PE + Presença.</summary>
        public static int DtRitual(BattleUnit conjurador) => 10 + conjurador.Stats.PePorRodada + conjurador.Stats.Atributos.Pre;

        /// <summary>Monta o StatBlock de um agente a partir da ficha.</summary>
        public static StatBlock CalcularStats(Trilha trilha, int nex, Atributos a, int bonusDefesa = 0,
            int bonusPv = 0, int bonusPe = 0, int bonusSan = 0, int iniciativaBonus = 0, int resistenciaBonus = 0)
        {
            return new StatBlock(
                VidaMaxima(trilha, a.Vig, nex) + bonusPv,
                PeMaximo(trilha, a.Pre, nex) + bonusPe,
                Math.Max(0, SanidadeMaxima(trilha, nex) + bonusSan),
                Defesa(a.Agi, bonusDefesa, 0),
                PePorRodada(nex),
                a,
                iniciativaBonus,
                resistenciaBonus);
        }

        // ------------------------------------------------------------------ Dados

        /// <summary>
        /// Teste de Ordem: rola tantos d20 quanto o atributo e fica com o maior.
        /// Com atributo 0 ou menos, rola 2d20 (ou mais) e fica com o menor.
        /// </summary>
        public static int RolarTeste(IRandom rng, int dados, out string detalhe)
        {
            bool menor = dados <= 0;
            int quantidade = menor ? 2 - dados : dados;
            int resultado = menor ? int.MaxValue : int.MinValue;
            var faces = new System.Text.StringBuilder();
            for (int i = 0; i < quantidade; i++)
            {
                int d = rng.Next(1, 21);
                if (i > 0) faces.Append(',');
                faces.Append(d);
                resultado = menor ? Math.Min(resultado, d) : Math.Max(resultado, d);
            }
            detalhe = $"{quantidade}d20{(menor ? " (menor)" : "")} [{faces}]";
            return resultado;
        }

        public static int RolarDados(IRandom rng, int quantidade, int faces)
        {
            int total = 0;
            for (int i = 0; i < quantidade; i++) total += rng.Next(1, faces + 1);
            return total;
        }
    }
}
