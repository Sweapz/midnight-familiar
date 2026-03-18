using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Content
{
    [CreateAssetMenu(
        fileName = "CuidDefinition",
        menuName = "Midnight Familiar/Combat/Cuid Definition")]
    public class CuidDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string cuidId = "cuid_species";
        [SerializeField] private string displayName = "Cuid";
        [SerializeField] private int level = 1;

        [Header("Typing")]
        [SerializeField] private CuidType primaryType = CuidType.None;
        [SerializeField] private CuidType secondaryType = CuidType.None;

        [Header("Trait")]
        [SerializeField] private string traitId = "trait_id";
        [SerializeField] private string traitName = "Trait";
        [SerializeField] private string traitDescription = string.Empty;

        [Header("Stats")]
        [SerializeField] private int attack = 10;
        [SerializeField] private int defense = 10;
        [SerializeField] private int abilityEffectiveness = 10;
        [SerializeField] private int abilityResistance = 10;
        [SerializeField] private int speed = 10;
        [SerializeField] private int constitution = 30;
        [SerializeField] private int damage = 0;
        [SerializeField] private int damageReduction = 0;
        [SerializeField] private int abilityDamage = 0;
        [SerializeField] private int abilityReduction = 0;

        [Header("Actions")]
        [SerializeField] private List<ActionDefinition> actions = new List<ActionDefinition>(4);

        public string CuidId => cuidId;
        public string DisplayName => displayName;
        public int Level => level;
        public CuidType PrimaryType => primaryType;
        public CuidType SecondaryType => secondaryType;
        public string TraitId => traitId;
        public string TraitName => traitName;
        public string TraitDescription => traitDescription;
        public int Attack => attack;
        public int Defense => defense;
        public int AbilityEffectiveness => abilityEffectiveness;
        public int AbilityResistance => abilityResistance;
        public int Speed => speed;
        public int Constitution => constitution;
        public int Damage => damage;
        public int DamageReduction => damageReduction;
        public int AbilityDamage => abilityDamage;
        public int AbilityReduction => abilityReduction;
        public IReadOnlyList<ActionDefinition> Actions => actions;

        public void ConfigureForRuntime(
            string newCuidId,
            string newDisplayName,
            int newLevel,
            CuidType newPrimaryType,
            CuidType newSecondaryType,
            string newTraitId,
            string newTraitName,
            string newTraitDescription,
            int newAttack,
            int newDefense,
            int newAbilityEffectiveness,
            int newAbilityResistance,
            int newSpeed,
            int newConstitution,
            int newDamage,
            int newDamageReduction,
            int newAbilityDamage,
            int newAbilityReduction,
            List<ActionDefinition> newActions)
        {
            cuidId = newCuidId;
            displayName = newDisplayName;
            level = newLevel;
            primaryType = newPrimaryType;
            secondaryType = newSecondaryType;
            traitId = newTraitId;
            traitName = newTraitName;
            traitDescription = newTraitDescription;
            attack = newAttack;
            defense = newDefense;
            abilityEffectiveness = newAbilityEffectiveness;
            abilityResistance = newAbilityResistance;
            speed = newSpeed;
            constitution = newConstitution;
            damage = newDamage;
            damageReduction = newDamageReduction;
            abilityDamage = newAbilityDamage;
            abilityReduction = newAbilityReduction;
            actions = newActions ?? new List<ActionDefinition>(4);
        }
    }
}
