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

            BattleState state = _turnController != null ? _turnController.BattleState : null;
            BattleCombatLogPanelController.LogEntry entry = _combatLogFormattingService.BuildStepEntry(step, state);
            if (entry == null)
            {
                return;
            }

            _combatLog.Insert(0, entry);
            TrimCombatLog();
            RefreshCombatLogPanel();
        }

        private void FinishBattle(TeamSide? winner)
        {
            _combatLog.Insert(0, _combatLogFormattingService.BuildBattleEndedEntry(winner));

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
    }

    public interface ICombatLogFormattingService
    {
        BattleCombatLogPanelController.LogEntry BuildStepEntry(TurnStepResult step, BattleState battleState);
        BattleCombatLogPanelController.LogEntry BuildResolutionEntry(
            int roundNumber,
            ActionResolution resolution,
            BattleState battleState);
        BattleCombatLogPanelController.LogEntry BuildRoundMessageEntry(int roundNumber, string message);
        BattleCombatLogPanelController.LogEntry BuildBattleEndedEntry(TeamSide? winner);
    }

    public sealed class CombatLogFormattingService : ICombatLogFormattingService
    {
        public BattleCombatLogPanelController.LogEntry BuildStepEntry(TurnStepResult step, BattleState battleState)
        {
            if (step == null)
            {
                return null;
            }

            if (step.Resolution != null)
            {
                return BuildResolutionEntry(step.RoundNumber, step.Resolution, battleState);
            }

            return BuildRoundMessageEntry(step.RoundNumber, step.Message);
        }

        public BattleCombatLogPanelController.LogEntry BuildResolutionEntry(
            int roundNumber,
            ActionResolution resolution,
            BattleState battleState)
        {
            if (resolution == null)
            {
                return BuildRoundMessageEntry(roundNumber, string.Empty);
            }

            CombatantState actor = battleState?.FindCombatant(resolution.ActorCombatantId);
            CombatantState target = battleState?.FindCombatant(resolution.TargetCombatantId);

            string actorText = FormatCombatantLabel(actor, resolution.ActorCombatantId);
            string targetText = FormatCombatantLabel(target, resolution.TargetCombatantId);
            string summary = EscapeRichText(resolution.Summary);
            string display = $"R{roundNumber}: {actorText} -> {targetText}: {summary}";

            string hoverDetail = string.Empty;
            bool isOffensive =
                resolution.Kind == ActionKind.Attack ||
                (resolution.Kind == ActionKind.Ability &&
                 (resolution.AbilityIntent == AbilityIntent.Offensive ||
                  resolution.AbilityIntent == AbilityIntent.Debuff));
            if (isOffensive)
            {
                string rollType;
                if (resolution.Kind == ActionKind.Attack)
                {
                    rollType = "Attack vs Defense";
                }
                else if (resolution.AbilityIntent == AbilityIntent.Debuff)
                {
                    rollType = "Debuff vs Resistance";
                }
                else
                {
                    rollType = "Ability vs Resistance";
                }

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

        public BattleCombatLogPanelController.LogEntry BuildRoundMessageEntry(int roundNumber, string message)
        {
            return new BattleCombatLogPanelController.LogEntry
            {
                DisplayText = $"R{roundNumber}: {EscapeRichText(message)}",
                HoverDetail = string.Empty
            };
        }

        public BattleCombatLogPanelController.LogEntry BuildBattleEndedEntry(TeamSide? winner)
        {
            string winnerText = winner.HasValue ? winner.Value.ToString() : "None";
            return new BattleCombatLogPanelController.LogEntry
            {
                DisplayText = $"Battle ended. Winner: {EscapeRichText(winnerText)}",
                HoverDetail = string.Empty
            };
        }

        private static string FormatCombatantLabel(CombatantState combatant, string fallbackId)
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
