using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation.UI;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void AddLogFromStep(TurnStepResult step)
        {
            if (step == null)
            {
                return;
            }

            var entry = step.Resolution != null
                ? BuildResolutionLogEntry(step.RoundNumber, step.Resolution)
                : new BattleCombatLogPanelController.LogEntry
                {
                    DisplayText = $"R{step.RoundNumber}: {EscapeRichText(step.Message)}",
                    HoverDetail = string.Empty
                };

            _combatLog.Insert(0, entry);
            TrimCombatLog();
            RefreshCombatLogPanel();
        }

        private void FinishBattle(TeamSide? winner)
        {
            string winnerText = winner.HasValue ? winner.Value.ToString() : "None";
            _combatLog.Insert(0, new BattleCombatLogPanelController.LogEntry
            {
                DisplayText = $"Battle ended. Winner: {EscapeRichText(winnerText)}",
                HoverDetail = string.Empty
            });

            TrimCombatLog();
            RefreshCombatLogPanel();
            RefreshHud();
            RefreshActionPanel();
        }

        private void RefreshCombatLogPanel()
        {
            EnsureCombatLogPanelReference();
            if (combatLogPanelController == null)
            {
                return;
            }

            combatLogPanelController.SetEntries(_combatLog);
        }

        private void TrimCombatLog()
        {
            int cap = Mathf.Max(10, maxCombatLogEntries);
            while (_combatLog.Count > cap)
            {
                _combatLog.RemoveAt(_combatLog.Count - 1);
            }
        }

        private BattleCombatLogPanelController.LogEntry BuildResolutionLogEntry(int roundNumber, ActionResolution resolution)
        {
            CombatantState actor = _turnController?.BattleState?.FindCombatant(resolution.ActorCombatantId);
            CombatantState target = _turnController?.BattleState?.FindCombatant(resolution.TargetCombatantId);

            string actorText = FormatCombatantLogLabel(actor, resolution.ActorCombatantId);
            string targetText = FormatCombatantLogLabel(target, resolution.TargetCombatantId);
            string summary = EscapeRichText(resolution.Summary);
            string display = $"R{roundNumber}: {actorText} -> {targetText}: {summary}";

            string hoverDetail = string.Empty;
            bool isOffensive =
                resolution.Kind == ActionKind.Attack ||
                (resolution.Kind == ActionKind.Ability && resolution.AbilityIntent == AbilityIntent.Offensive);
            if (isOffensive)
            {
                string rollType = resolution.Kind == ActionKind.Attack
                    ? "Attack vs Defense"
                    : "Ability vs Resistance";
                hoverDetail = $"{rollType}: {resolution.AttackRoll} vs {resolution.DefenseRoll}";
                if (!string.IsNullOrWhiteSpace(resolution.DamageBreakdown))
                {
                    hoverDetail += $"\n{resolution.DamageBreakdown}";
                }
            }

            return new BattleCombatLogPanelController.LogEntry
            {
                DisplayText = display,
                HoverDetail = hoverDetail
            };
        }

        private static string FormatCombatantLogLabel(CombatantState combatant, string fallbackId)
        {
            string name = combatant != null && combatant.Unit != null
                ? combatant.Unit.DisplayName
                : string.IsNullOrWhiteSpace(fallbackId) ? "Unknown" : fallbackId;

            TeamSide team = combatant != null ? combatant.Team : TeamSide.Enemy;
            string role = team == TeamSide.Player ? "Ally" : "Enemy";
            string colorHex = team == TeamSide.Player ? "388CEB" : "D95757";
            return $"<color=#{colorHex}>{EscapeRichText(name)} [{role}]</color>";
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
