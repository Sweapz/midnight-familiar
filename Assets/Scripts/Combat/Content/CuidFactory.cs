using System;
using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;

namespace MidnightFamiliar.Combat.Content
{
    public static class CuidFactory
    {
        public static CuidUnit CreateUnit(CuidDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var unit = new CuidUnit
            {
                SpeciesId = definition.CuidId,
                DisplayName = definition.DisplayName,
                Level = definition.Level,
                PrimaryType = definition.PrimaryType,
                SecondaryType = definition.SecondaryType,
                Stats = new CuidStats
                {
                    Attack = definition.Attack,
                    Defense = definition.Defense,
                    AbilityEffectiveness = definition.AbilityEffectiveness,
                    AbilityResistance = definition.AbilityResistance,
                    Speed = definition.Speed,
                    Constitution = definition.Constitution,
                    Damage = definition.Damage,
                    DamageReduction = definition.DamageReduction,
                    AbilityDamage = definition.AbilityDamage,
                    AbilityReduction = definition.AbilityReduction
                },
                Trait = new CuidTrait
                {
                    Id = definition.TraitId,
                    DisplayName = definition.TraitName,
                    Description = definition.TraitDescription
                },
                Actions = BuildActions(definition.Actions)
            };

            unit.InitializeHealth();
            return unit;
        }

        private static List<CuidAction> BuildActions(IReadOnlyList<ActionDefinition> definitions)
        {
            var actions = new List<CuidAction>(4);
            if (definitions == null)
            {
                return actions;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ActionDefinition actionDefinition = definitions[i];
                if (actionDefinition == null)
                {
                    continue;
                }

                actions.Add(new CuidAction
                {
                    Id = actionDefinition.ActionId,
                    DisplayName = actionDefinition.DisplayName,
                    Description = actionDefinition.Description,
                    Kind = actionDefinition.Kind,
                    AbilityIntent = actionDefinition.AbilityIntent,
                    ActionType = actionDefinition.ActionType,
                    TargetRule = actionDefinition.TargetRule,
                    Range = actionDefinition.Range,
                    HitBonus = actionDefinition.HitBonus,
                    Potency = actionDefinition.Potency,
                    CooldownTurns = actionDefinition.CooldownTurns
                });
            }

            return actions;
        }
    }
}
