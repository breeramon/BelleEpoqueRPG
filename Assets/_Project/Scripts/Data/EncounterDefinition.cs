using System.Collections.Generic;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>Uma batalha: quem luta, com quais itens e com qual trilha sonora.</summary>
    [CreateAssetMenu(menuName = "Belle Époque/Encontro (Batalha)", fileName = "NovoEncontro")]
    public class EncounterDefinition : ScriptableObject
    {
        public string title = "Rue des Ombres";
        public List<UnitDefinition> heroes = new List<UnitDefinition>();
        public List<UnitDefinition> enemies = new List<UnitDefinition>();
        public List<ItemStack> items = new List<ItemStack>();

        [Header("Áudio")]
        public AudioClip battleMusic;
        public AudioClip ambienceLoop;
        public AudioClip victoryStinger;
        public AudioClip defeatStinger;
    }
}
