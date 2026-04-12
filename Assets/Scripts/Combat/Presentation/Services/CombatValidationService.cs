using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Systems;

namespace MidnightFamiliar.Combat.Presentation
{
    public interface ICombatValidationService
    {
        bool IsValidTargetSelection(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ICombatActionQueryService actionQueryService,
            ICombatSpatialQueryService spatialQueryService);
        bool CanResolveOpportunityAction(CombatantState attacker, CombatantState target, CuidAction action);
        bool IsReachableMoveCell(GridPosition cell, IReadOnlyList<GridPosition> validMoveCells);
        bool IsMoveCostWithinBudget(int moveCost, int remainingMovement);
    }

    public sealed class CombatValidationService : ICombatValidationService
    {
        public bool IsValidTargetSelection(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ICombatActionQueryService actionQueryService,
            ICombatSpatialQueryService spatialQueryService)
        {
            if (actionQueryService == null)
            {
                return false;
            }

            return actionQueryService.IsValidTarget(actor, target, action, spatialQueryService);
        }

        public bool CanResolveOpportunityAction(CombatantState attacker, CombatantState target, CuidAction action)
        {
            return attacker != null &&
                   target != null &&
                   action != null &&
                   !attacker.IsDefeated &&
                   !target.IsDefeated;
        }

        public bool IsReachableMoveCell(GridPosition cell, IReadOnlyList<GridPosition> validMoveCells)
        {
            if (validMoveCells == null)
            {
                return false;
            }

            for (int i = 0; i < validMoveCells.Count; i++)
            {
                GridPosition candidate = validMoveCells[i];
                if (candidate.X == cell.X && candidate.Y == cell.Y)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsMoveCostWithinBudget(int moveCost, int remainingMovement)
        {
            return moveCost > 0 && moveCost <= remainingMovement;
        }
    }
}
