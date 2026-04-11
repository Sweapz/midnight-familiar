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
                PrimaryType = NormalizeType(definition.PrimaryType),
                SecondaryType = NormalizeType(definition.SecondaryType),
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
                Actions = BuildActions(definition.Actions, definition.PrimaryType)
            };

            unit.InitializeHealth();
            return unit;
        }

        private static List<CuidAction> BuildActions(IReadOnlyList<ActionDefinition> definitions, CuidType primaryType)
        {
            var actions = new List<CuidAction>(4);
            if (definitions != null)
            {
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
                        ActionType = NormalizeType(actionDefinition.ActionType),
                        TargetRule = actionDefinition.TargetRule,
                        Range = actionDefinition.Range,
                        HitBonus = actionDefinition.HitBonus,
                        Potency = actionDefinition.Potency,
                        CooldownTurns = actionDefinition.CooldownTurns,
                        SupportEffects = BuildSupportEffects(actionDefinition.SupportEffects),
                        TypeStatusApplications = BuildTypeStatusApplications(actionDefinition.TypeStatusApplications)
                    });
                }
            }

            bool hasExistingBasicAttack = actions.Exists(action =>
                action != null &&
                (action.IsBasicAttack || action.Id.StartsWith("basic_", StringComparison.OrdinalIgnoreCase)));
            if (!hasExistingBasicAttack)
            {
                actions.Add(BuildBasicAttackForPrimaryType(primaryType));
            }

            return actions;
        }

        private static List<SupportEffect> BuildSupportEffects(IReadOnlyList<SupportEffect> definitions)
        {
            var effects = new List<SupportEffect>(definitions != null ? definitions.Count : 0);
            if (definitions == null)
            {
                return effects;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                SupportEffect effect = definitions[i];
                if (effect == null)
                {
                    continue;
                }

                effects.Add(effect.Clone());
            }

            return effects;
        }

        private static List<TypeStatusApplication> BuildTypeStatusApplications(IReadOnlyList<TypeStatusApplication> definitions)
        {
            var applications = new List<TypeStatusApplication>(definitions != null ? definitions.Count : 0);
            if (definitions == null)
            {
                return applications;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                TypeStatusApplication application = definitions[i];
                if (application == null)
                {
                    continue;
                }

                applications.Add(application.Clone());
            }

            return applications;
        }

        private static CuidAction BuildBasicAttackForPrimaryType(CuidType primaryType)
        {
            return BasicAttackCatalog.CreateForType(primaryType);
        }

        private static CuidType NormalizeType(CuidType type)
        {
            return Enum.IsDefined(typeof(CuidType), type) ? type : CuidType.Ember;
        }
    }

    internal static class BasicAttackCatalog
    {
        public static CuidAction CreateForType(CuidType type)
        {
            if (!Enum.IsDefined(typeof(CuidType), type))
            {
                type = CuidType.Ember;
            }

            string typeName = type.ToString();
            return new CuidAction
            {
                Id = $"basic_{typeName.ToLowerInvariant()}",
                DisplayName = GetBasicAttackDisplayName(type),
                Description = GetBasicAttackDescription(type),
                Kind = ActionKind.Attack,
                AbilityIntent = AbilityIntent.None,
                ActionType = type,
                TargetRule = TargetRule.EnemySingle,
                Range = 1,
                HitBonus = 0,
                Potency = 4,
                CooldownTurns = 0,
                IsBasicAttack = true
            };
        }

        private static string GetBasicAttackDisplayName(CuidType type)
        {
            switch (type)
            {
                case CuidType.Ember:
                    return "Ember Snap";
                case CuidType.Tide:
                    return "Tidal Jab";
                case CuidType.Flora:
                    return "Vine Lash";
                case CuidType.Stone:
                    return "Stone Knuckle";
                case CuidType.Arcane:
                    return "Arc Spark";
                case CuidType.Beast:
                    return "Feral Swipe";
                case CuidType.Mental:
                    return "Mind Lance";
                case CuidType.Darkin:
                    return "Darkin Rend";
                default:
                    return "Basic Strike";
            }
        }

        private static string GetBasicAttackDescription(CuidType type)
        {
            string label = type.ToString().ToLowerInvariant();
            return $"Reliable {label} basic attack.";
        }
    }
}
