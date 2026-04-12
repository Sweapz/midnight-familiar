using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation.UI;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public interface ICombatTurnFlowService
    {
        bool TryBuildTurnStartLogEntry(
            TurnStartStatusResult turnStart,
            int roundNumber,
            ICombatLogFormattingService combatLogFormattingService,
            out BattleCombatLogPanelController.LogEntry entry);
        bool DidActorRepositionAtTurnStart(TurnStartStatusResult turnStart, GridPosition before, GridPosition after);
        int ComputeTurnStartMovementBudget(TurnStartStatusResult turnStart, int fullMovementBudget);
        int ComputeRemainingMovementAfterMove(
            TurnStartStatusResult turnStart,
            int fullMovementBudget,
            GridPosition before,
            GridPosition after);
        bool TryExecuteForcedSkipTurn(
            TurnStartStatusResult turnStart,
            TurnController turnController,
            bool advanceTurn,
            out TurnStepResult step);
        bool TryExecutePassIfDefeated(
            CombatantState actor,
            TurnController turnController,
            bool advanceTurn,
            out TurnStepResult step);
    }

    public sealed class CombatTurnFlowService : ICombatTurnFlowService
    {
        public bool TryBuildTurnStartLogEntry(
            TurnStartStatusResult turnStart,
            int roundNumber,
            ICombatLogFormattingService combatLogFormattingService,
            out BattleCombatLogPanelController.LogEntry entry)
        {
            entry = null;
            if (combatLogFormattingService == null || string.IsNullOrWhiteSpace(turnStart.Message))
            {
                return false;
            }

            entry = combatLogFormattingService.BuildRoundMessageEntry(roundNumber, turnStart.Message);
            return entry != null;
        }

        public bool DidActorRepositionAtTurnStart(TurnStartStatusResult turnStart, GridPosition before, GridPosition after)
        {
            return turnStart.ConsumedMovement && (before.X != after.X || before.Y != after.Y);
        }

        public int ComputeTurnStartMovementBudget(TurnStartStatusResult turnStart, int fullMovementBudget)
        {
            return turnStart.ConsumedMovement ? 0 : Mathf.Max(0, fullMovementBudget);
        }

        public int ComputeRemainingMovementAfterMove(
            TurnStartStatusResult turnStart,
            int fullMovementBudget,
            GridPosition before,
            GridPosition after)
        {
            int budget = ComputeTurnStartMovementBudget(turnStart, fullMovementBudget);
            int used = before.ManhattanDistanceTo(after);
            return Mathf.Max(0, budget - used);
        }

        public bool TryExecuteForcedSkipTurn(
            TurnStartStatusResult turnStart,
            TurnController turnController,
            bool advanceTurn,
            out TurnStepResult step)
        {
            step = null;
            if (!turnStart.ForcedSkipTurn || turnController == null)
            {
                return false;
            }

            step = turnController.ExecuteTurn(TurnChoice.Pass(), advanceTurn);
            return true;
        }

        public bool TryExecutePassIfDefeated(
            CombatantState actor,
            TurnController turnController,
            bool advanceTurn,
            out TurnStepResult step)
        {
            step = null;
            if (turnController == null || actor == null || !actor.IsDefeated)
            {
                return false;
            }

            step = turnController.ExecuteTurn(TurnChoice.Pass(), advanceTurn);
            return true;
        }
    }
}
