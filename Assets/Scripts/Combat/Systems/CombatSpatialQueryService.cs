using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Systems
{
    public interface ICombatSpatialQueryService
    {
        CombatantState FindClosestOpponent(BattleState state, CombatantState actor);
        bool IsTargetInRange(GridPosition from, GridPosition to, int range);
        bool IsAdjacentToAnyOpponent(BattleState state, CombatantState actor);
        List<CombatantState> GetThreatenersOnMoveAway(BattleState state, CombatantState mover, GridPosition from, GridPosition to);
        void CollectCellsInRange(BattleState state, GridPosition origin, int range, ICollection<GridPosition> results);
    }

    public sealed class CombatSpatialQueryService : ICombatSpatialQueryService
    {
        public CombatantState FindClosestOpponent(BattleState state, CombatantState actor)
        {
            if (state == null || state.Combatants == null || actor == null)
            {
                return null;
            }

            CombatantState closest = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < state.Combatants.Count; i++)
            {
                CombatantState candidate = state.Combatants[i];
                if (candidate == null || candidate.IsDefeated || candidate.Team == actor.Team)
                {
                    continue;
                }

                int distance = actor.Position.ManhattanDistanceTo(candidate.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = candidate;
                }
            }

            return closest;
        }

        public bool IsTargetInRange(GridPosition from, GridPosition to, int range)
        {
            int safeRange = Mathf.Max(0, range);
            return from.ManhattanDistanceTo(to) <= safeRange;
        }

        public bool IsAdjacentToAnyOpponent(BattleState state, CombatantState actor)
        {
            if (state == null || state.Combatants == null || actor == null)
            {
                return false;
            }

            for (int i = 0; i < state.Combatants.Count; i++)
            {
                CombatantState other = state.Combatants[i];
                if (other == null || other.IsDefeated || other.Team == actor.Team)
                {
                    continue;
                }

                if (other.Position.ManhattanDistanceTo(actor.Position) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        public List<CombatantState> GetThreatenersOnMoveAway(BattleState state, CombatantState mover, GridPosition from, GridPosition to)
        {
            var threateners = new List<CombatantState>(3);
            if (state == null || state.Combatants == null || mover == null)
            {
                return threateners;
            }

            for (int i = 0; i < state.Combatants.Count; i++)
            {
                CombatantState candidate = state.Combatants[i];
                if (candidate == null ||
                    candidate.IsDefeated ||
                    candidate.CombatantId == mover.CombatantId ||
                    candidate.Team == mover.Team)
                {
                    continue;
                }

                int fromDistance = candidate.Position.ManhattanDistanceTo(from);
                int toDistance = candidate.Position.ManhattanDistanceTo(to);
                if (fromDistance <= 1 && toDistance > 1)
                {
                    threateners.Add(candidate);
                }
            }

            threateners.Sort((a, b) => string.CompareOrdinal(a.CombatantId, b.CombatantId));
            return threateners;
        }

        public void CollectCellsInRange(BattleState state, GridPosition origin, int range, ICollection<GridPosition> results)
        {
            if (state == null || results == null)
            {
                return;
            }

            int safeRange = Mathf.Max(0, range);
            for (int x = 0; x < state.GridWidth; x++)
            {
                for (int y = 0; y < state.GridHeight; y++)
                {
                    GridPosition cell = new GridPosition(x, y);
                    if (origin.ManhattanDistanceTo(cell) <= safeRange)
                    {
                        results.Add(cell);
                    }
                }
            }
        }
    }
}
