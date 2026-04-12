using System.Text;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public interface ICombatStatusPresentationService
    {
        string BuildHoverTitle(CombatantState combatant);
        string BuildStatsText(CombatantState combatant);
        string BuildEffectsText(CombatantState combatant, bool opportunityReady);
    }

    public sealed class CombatStatusPresentationService : ICombatStatusPresentationService
    {
        public string BuildHoverTitle(CombatantState combatant)
        {
            if (combatant == null || combatant.Unit == null)
            {
                return "Unknown";
            }

            return $"{combatant.Unit.DisplayName} ({combatant.Team})";
        }

        public string BuildStatsText(CombatantState combatant)
        {
            if (combatant == null)
            {
                return string.Empty;
            }

            CuidStats baseStats = combatant.Unit != null && combatant.Unit.Stats != null
                ? combatant.Unit.Stats
                : new CuidStats();
            CuidStats effective = combatant.GetEffectiveStats();

            var sb = new StringBuilder(256);
            sb.AppendLine($"HP {Mathf.Max(0, combatant.CurrentHealth)}/{combatant.GetMaxHealth()}");
            AppendStatLine(sb, "ATK", baseStats.Attack, effective.Attack);
            AppendStatLine(sb, "DEF", baseStats.Defense, effective.Defense);
            AppendStatLine(sb, "A.EFF", baseStats.AbilityEffectiveness, effective.AbilityEffectiveness);
            AppendStatLine(sb, "A.RES", baseStats.AbilityResistance, effective.AbilityResistance);
            AppendStatLine(sb, "SPD", baseStats.Speed, effective.Speed);
            AppendStatLine(sb, "DMG", baseStats.Damage, effective.Damage);
            AppendStatLine(sb, "DMG RED", baseStats.DamageReduction, effective.DamageReduction);
            AppendStatLine(sb, "A.DMG", baseStats.AbilityDamage, effective.AbilityDamage);
            AppendStatLine(sb, "A.RED", baseStats.AbilityReduction, effective.AbilityReduction);
            return sb.ToString().TrimEnd();
        }

        public string BuildEffectsText(CombatantState combatant, bool opportunityReady)
        {
            string statusSummary = BuildEffectsSummary(combatant);
            return $"Opportunity: {(opportunityReady ? "Ready" : "Spent")}\n{statusSummary}";
        }

        private static string BuildEffectsSummary(CombatantState combatant)
        {
            if (combatant == null || combatant.ActiveEffects == null || combatant.ActiveEffects.Count == 0)
            {
                return "Effects: None";
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("Effects:");
            for (int i = 0; i < combatant.ActiveEffects.Count; i++)
            {
                ActiveStatusEffect effect = combatant.ActiveEffects[i];
                if (effect == null || effect.RemainingTurns <= 0)
                {
                    continue;
                }

                sb.AppendLine($"- {FormatEffectLabel(effect)}");
            }

            string summary = sb.ToString().TrimEnd();
            return summary == "Effects:" ? "Effects: None" : summary;
        }

        private static void AppendStatLine(StringBuilder sb, string label, int baseValue, int effectiveValue)
        {
            if (baseValue == effectiveValue)
            {
                sb.AppendLine($"{label} {effectiveValue}");
                return;
            }

            sb.AppendLine($"{label} {effectiveValue} ({baseValue:+#;-#;0})");
        }

        private static string FormatEffectLabel(ActiveStatusEffect effect)
        {
            if (effect.TypeStatus != TypeStatusId.None)
            {
                return $"{effect.TypeStatus} ({effect.RemainingTurns}t)";
            }

            switch (effect.Kind)
            {
                case SupportEffectKind.StatModifier:
                    return $"{effect.TargetStat} {(effect.Magnitude >= 0 ? "+" : string.Empty)}{effect.Magnitude} ({effect.RemainingTurns}t)";
                case SupportEffectKind.FlatDamageReduction:
                    return $"Flat DR +{effect.Magnitude} ({effect.RemainingTurns}t)";
                case SupportEffectKind.FlatDamageIncrease:
                    return $"Flat DMG +{effect.Magnitude} ({effect.RemainingTurns}t)";
                case SupportEffectKind.Shield:
                    return $"Shield {effect.Magnitude} ({effect.RemainingTurns}t)";
                case SupportEffectKind.Thorns:
                    return $"Thorns {effect.Magnitude} ({effect.RemainingTurns}t)";
                case SupportEffectKind.Heal:
                default:
                    return $"{effect.Kind} {effect.Magnitude} ({effect.RemainingTurns}t)";
            }
        }
    }
}
