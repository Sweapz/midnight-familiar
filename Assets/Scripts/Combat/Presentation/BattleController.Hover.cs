using System.Text;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void HandleHudCombatantHoverStarted(string combatantId)
        {
            _hudHoveredCombatantId = combatantId ?? string.Empty;
            RefreshHoverTooltipIfNeeded();
        }

        private void HandleHudCombatantHoverEnded(string combatantId)
        {
            if (_hudHoveredCombatantId == combatantId)
            {
                _hudHoveredCombatantId = string.Empty;
                RefreshHoverTooltipIfNeeded();
            }
        }

        private void UpdateWorldHoveredCombatantId()
        {
            bool isPointerOverTooltip = hudController != null && hudController.IsPointerOverHoverTooltip();
            if (isPointerOverTooltip && !string.IsNullOrWhiteSpace(_hoveredCombatantId))
            {
                // Keep world hover stable while cursor is on the tooltip opened from a world Cuid.
                _worldHoveredCombatantId = _hoveredCombatantId;
                return;
            }

            if (!TryGetClickedCombatant(out CombatantState combatant) || combatant == null || combatant.IsDefeated)
            {
                _worldHoveredCombatantId = string.Empty;
                return;
            }

            _worldHoveredCombatantId = combatant.CombatantId;
        }

        private void ShowCombatantHover(string combatantId)
        {
            if (string.IsNullOrWhiteSpace(combatantId) || hudController == null || _turnController?.BattleState == null)
            {
                return;
            }

            CombatantState combatant = _turnController.BattleState.FindCombatant(combatantId);
            if (combatant == null)
            {
                return;
            }

            _hoveredCombatantId = combatantId;
            string title = $"{combatant.Unit.DisplayName} ({combatant.Team})";
            string statsText = BuildHoverStatsText(combatant);
            string effectsText = BuildHoverEffectsText(combatant);
            bool opportunityReady = !_spentOpportunityCombatants.Contains(combatant.CombatantId);
            effectsText = $"Opportunity: {(opportunityReady ? "Ready" : "Spent")}\n{effectsText}";

            if (TryGetHoverScreenPoint(combatant, out Vector2 screenPoint))
            {
                hudController.ShowHoverTooltip(title, statsText, effectsText, screenPoint);
            }
            else
            {
                hudController.ShowHoverTooltip(title, statsText, effectsText);
            }
        }

        private void RefreshHoverTooltipIfNeeded()
        {
            string targetCombatantId = !string.IsNullOrWhiteSpace(_hudHoveredCombatantId)
                ? _hudHoveredCombatantId
                : _worldHoveredCombatantId;

            if (string.IsNullOrWhiteSpace(targetCombatantId))
            {
                _hoveredCombatantId = string.Empty;
                hudController?.HideHoverTooltip();
                return;
            }

            if (_turnController?.BattleState == null)
            {
                _hoveredCombatantId = string.Empty;
                hudController?.HideHoverTooltip();
                return;
            }

            CombatantState hovered = _turnController.BattleState.FindCombatant(targetCombatantId);
            if (hovered == null)
            {
                _hoveredCombatantId = string.Empty;
                hudController?.HideHoverTooltip();
                return;
            }

            ShowCombatantHover(targetCombatantId);

            if (TryGetHoverScreenPoint(hovered, out Vector2 screenPoint))
            {
                hudController?.SetHoverTooltipScreenPoint(screenPoint);
            }
        }

        private bool TryGetHoverScreenPoint(CombatantState combatant, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (combatant == null)
            {
                return false;
            }

            Camera cam = GetInputCamera();
            if (cam == null)
            {
                return false;
            }

            Vector3 worldPosition;
            if (_views.TryGetValue(combatant.CombatantId, out CuidView view) && view != null)
            {
                worldPosition = view.transform.position + Vector3.up * hoverWorldYOffset;
            }
            else
            {
                worldPosition = gridController.GridToWorld(combatant.Position) + Vector3.up * hoverWorldYOffset;
            }

            Vector3 projected = cam.WorldToScreenPoint(worldPosition);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPoint = new Vector2(projected.x, projected.y);
            return true;
        }

        private string BuildHoverStatsText(CombatantState combatant)
        {
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

        private static void AppendStatLine(StringBuilder sb, string label, int baseValue, int effectiveValue)
        {
            if (baseValue == effectiveValue)
            {
                sb.AppendLine($"{label} {effectiveValue}");
                return;
            }

            sb.AppendLine($"{label} {effectiveValue} ({baseValue:+#;-#;0})");
        }

        private static string BuildHoverEffectsText(CombatantState combatant)
        {
            if (combatant.ActiveEffects == null || combatant.ActiveEffects.Count == 0)
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
