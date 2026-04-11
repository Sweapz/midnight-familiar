using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Systems
{
    public interface ICombatMovementService
    {
        int GetMovementBudget(CombatantState actor);
        void CollectReachableCells(
            BattleState state,
            GridPosition start,
            int range,
            string exceptCombatantId,
            ICollection<GridPosition> results);
        GridPosition CalculateMoveDestinationToward(
            BattleState state,
            GridPosition start,
            GridPosition target,
            int steps,
            string actorId);
        GridPosition FindBestFleeStep(BattleState state, TeamSide actorTeam, GridPosition from, string actorId);
        bool IsOccupied(BattleState state, GridPosition position, string exceptCombatantId);
        int SpendMovementFleeing(CombatantState actor, BattleState state);
    }

    public sealed class CombatMovementService : ICombatMovementService
    {
        public int GetMovementBudget(CombatantState actor)
        {
            int speed = actor != null ? actor.GetEffectiveStats().Speed : 1;
            return Mathf.Clamp(Mathf.Max(1, speed / 4), 1, 3);
        }

        public void CollectReachableCells(
            BattleState state,
            GridPosition start,
            int range,
            string exceptCombatantId,
            ICollection<GridPosition> results)
        {
            if (state == null || results == null || range <= 0)
            {
                return;
            }

            for (int x = 0; x < state.GridWidth; x++)
            {
                for (int y = 0; y < state.GridHeight; y++)
                {
                    var candidate = new GridPosition(x, y);
                    if (candidate.X == start.X && candidate.Y == start.Y)
                    {
                        continue;
                    }

                    if (start.ManhattanDistanceTo(candidate) > range)
                    {
                        continue;
                    }

                    if (IsOccupied(state, candidate, exceptCombatantId))
                    {
                        continue;
                    }

                    results.Add(candidate);
                }
            }
        }

        public GridPosition CalculateMoveDestinationToward(
            BattleState state,
            GridPosition start,
            GridPosition target,
            int steps,
            string actorId)
        {
            if (state == null || steps <= 0)
            {
                return start;
            }

            GridPosition current = start;
            for (int i = 0; i < steps; i++)
            {
                GridPosition next = FindBestNeighborStepToward(state, current, target, actorId);
                if (next.X == current.X && next.Y == current.Y)
                {
                    break;
                }

                current = next;
            }

            return current;
        }

        public GridPosition FindBestFleeStep(BattleState state, TeamSide actorTeam, GridPosition from, string actorId)
        {
            if (state == null)
            {
                return from;
            }

            GridPosition[] candidates =
            {
                new GridPosition(from.X + 1, from.Y),
                new GridPosition(from.X - 1, from.Y),
                new GridPosition(from.X, from.Y + 1),
                new GridPosition(from.X, from.Y - 1)
            };

            int currentScore = GetNearestOpponentDistance(state, actorTeam, from);
            int bestScore = currentScore;
            GridPosition best = from;
            for (int i = 0; i < candidates.Length; i++)
            {
                GridPosition candidate = candidates[i];
                if (!state.IsInsideGrid(candidate) || IsOccupied(state, candidate, actorId))
                {
                    continue;
                }

                int score = GetNearestOpponentDistance(state, actorTeam, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        public bool IsOccupied(BattleState state, GridPosition position, string exceptCombatantId)
        {
            if (state == null || state.Combatants == null)
            {
                return false;
            }

            for (int i = 0; i < state.Combatants.Count; i++)
            {
                CombatantState combatant = state.Combatants[i];
                if (combatant == null || combatant.IsDefeated || combatant.CombatantId == exceptCombatantId)
                {
                    continue;
                }

                if (combatant.Position.X == position.X && combatant.Position.Y == position.Y)
                {
                    return true;
                }
            }

            return false;
        }

        public int SpendMovementFleeing(CombatantState actor, BattleState state)
        {
            if (actor == null || state == null)
            {
                return 0;
            }

            int budget = GetMovementBudget(actor);
            if (budget <= 0)
            {
                return 0;
            }

            int moved = 0;
            for (int step = 0; step < budget; step++)
            {
                GridPosition from = actor.Position;
                GridPosition to = FindBestFleeStep(state, actor.Team, from, actor.CombatantId);
                if (to.X == from.X && to.Y == from.Y)
                {
                    break;
                }

                actor.Position = to;
                moved++;
            }

            return moved;
        }

        private GridPosition FindBestNeighborStepToward(
            BattleState state,
            GridPosition from,
            GridPosition target,
            string actorId)
        {
            GridPosition[] candidates =
            {
                new GridPosition(from.X + 1, from.Y),
                new GridPosition(from.X - 1, from.Y),
                new GridPosition(from.X, from.Y + 1),
                new GridPosition(from.X, from.Y - 1)
            };

            int bestDistance = from.ManhattanDistanceTo(target);
            GridPosition best = from;
            for (int i = 0; i < candidates.Length; i++)
            {
                GridPosition candidate = candidates[i];
                if (!state.IsInsideGrid(candidate) || IsOccupied(state, candidate, actorId))
                {
                    continue;
                }

                int distance = candidate.ManhattanDistanceTo(target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static int GetNearestOpponentDistance(BattleState state, TeamSide actorTeam, GridPosition position)
        {
            if (state == null || state.Combatants == null)
            {
                return 0;
            }

            int best = int.MaxValue;
            for (int i = 0; i < state.Combatants.Count; i++)
            {
                CombatantState other = state.Combatants[i];
                if (other == null || other.IsDefeated || other.Team == actorTeam)
                {
                    continue;
                }

                int distance = other.Position.ManhattanDistanceTo(position);
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best == int.MaxValue ? 0 : best;
        }
    }
}
