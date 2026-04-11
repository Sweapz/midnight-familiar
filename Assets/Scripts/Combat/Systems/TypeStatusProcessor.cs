using System;
using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Systems
{
    public readonly struct TurnStartStatusResult
    {
        public readonly bool ForcedSkipTurn;
        public readonly bool ConsumedMovement;
        public readonly string Message;

        public TurnStartStatusResult(bool forcedSkipTurn, bool consumedMovement, string message)
        {
            ForcedSkipTurn = forcedSkipTurn;
            ConsumedMovement = consumedMovement;
            Message = message ?? string.Empty;
        }
    }

    public sealed class TypeStatusProcessor
    {
        private readonly IDiceRoller _diceRoller;

        public TypeStatusProcessor(IDiceRoller diceRoller)
        {
            _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
        }

        public bool TryHandlePreActionFailure(CombatantState actor, out string message)
        {
            message = string.Empty;
            if (actor == null || actor.IsDefeated || !actor.HasStatus(TypeStatusId.Confused))
            {
                return false;
            }

            int roll = _diceRoller.RollD20();
            if (roll > 10)
            {
                return false;
            }

            string actorName = actor.Unit != null ? actor.Unit.DisplayName : "Cuid";
            message = $"{actorName} is confused and fails to act.";
            return true;
        }

        public float ComputeOutgoingDamageMultiplier(CombatantState actor)
        {
            return actor != null && actor.HasStatus(TypeStatusId.Debilitated) ? 0.95f : 1f;
        }

        public int ComputeIncomingOnHitExtraDamage(CombatantState attacker, CombatantState target, bool isStatusDamageEvent)
        {
            if (isStatusDamageEvent || attacker == null || target == null || target.IsDefeated)
            {
                return 0;
            }

            if (!target.HasStatus(TypeStatusId.Jagged))
            {
                return 0;
            }

            int maxHealth = Mathf.Max(1, target.GetMaxHealth());
            return Mathf.Max(1, Mathf.RoundToInt(maxHealth * 0.03f));
        }

        public int ApplyOnSuccessfulOffense(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            IReadOnlyList<TypeStatusApplication> defaultApplications)
        {
            if (actor == null || target == null || action == null || target.IsDefeated || action.IsBasicAttack)
            {
                return 0;
            }

            IReadOnlyList<TypeStatusApplication> applications =
                action.TypeStatusApplications != null && action.TypeStatusApplications.Count > 0
                    ? action.TypeStatusApplications
                    : defaultApplications;
            return ApplyStatusApplications(target, applications);
        }

        public int ApplyFromActionConfig(CombatantState actor, CombatantState target, CuidAction action)
        {
            if (actor == null || target == null || action == null || target.IsDefeated || action.IsBasicAttack)
            {
                return 0;
            }

            return ApplyStatusApplications(target, action.TypeStatusApplications);
        }

        private int ApplyStatusApplications(CombatantState target, IReadOnlyList<TypeStatusApplication> applications)
        {
            if (applications == null || applications.Count == 0)
            {
                return 0;
            }

            int appliedCount = 0;
            for (int i = 0; i < applications.Count; i++)
            {
                TypeStatusApplication application = applications[i];
                if (application == null)
                {
                    continue;
                }

                if (!RollsForApplication(application.ApplyChancePercent))
                {
                    continue;
                }

                int duration = Mathf.Max(1, application.DurationTurns);
                switch (application.Kind)
                {
                    case TypeStatusApplicationKind.Upgrade:
                        if (application.UpgradeFrom == TypeStatusId.None || application.UpgradeTo == TypeStatusId.None)
                        {
                            continue;
                        }

                        if (target.UpgradeStatus(application.UpgradeFrom, application.UpgradeTo, duration))
                        {
                            appliedCount++;
                        }
                        break;

                    case TypeStatusApplicationKind.ApplyOrRefresh:
                        if (application.Status == TypeStatusId.None || IsBuildUpBlockedByGoalStatus(target, application.Status))
                        {
                            continue;
                        }

                        target.ApplyOrRefreshStatus(application.Status, duration);
                        appliedCount++;
                        break;
                }
            }

            return appliedCount;
        }

        private bool RollsForApplication(int chancePercent)
        {
            int clampedChance = Mathf.Clamp(chancePercent, 0, 100);
            if (clampedChance <= 0)
            {
                return false;
            }

            if (clampedChance >= 100)
            {
                return true;
            }

            int threshold = Mathf.Clamp(Mathf.CeilToInt(clampedChance / 5f), 1, 20);
            int roll = _diceRoller.RollD20();
            return roll <= threshold;
        }

        public TurnStartStatusResult ProcessTurnStart(CombatantState actor, BattleState state)
        {
            if (actor == null || actor.IsDefeated || state == null)
            {
                return new TurnStartStatusResult(false, false, string.Empty);
            }

            string actorName = actor.Unit != null ? actor.Unit.DisplayName : "Cuid";
            string movementMessage = string.Empty;
            if (actor.HasStatus(TypeStatusId.Frightened))
            {
                int movedSteps = SpendMovementFleeing(actor, state);
                if (movedSteps > 0)
                {
                    movementMessage = $"{actorName} is frightened and flees {movedSteps} step(s).";
                }
                else
                {
                    movementMessage = $"{actorName} is frightened but cannot flee.";
                }
            }

            if (actor.HasStatus(TypeStatusId.Paralyzed))
            {
                string paralyzedMessage = $"{actorName} is paralyzed and skips the turn.";
                string message = string.IsNullOrWhiteSpace(movementMessage)
                    ? paralyzedMessage
                    : $"{movementMessage} {paralyzedMessage}";
                return new TurnStartStatusResult(true, actor.HasStatus(TypeStatusId.Frightened), message);
            }

            return new TurnStartStatusResult(false, actor.HasStatus(TypeStatusId.Frightened), movementMessage);
        }

        public int ProcessTurnEnd(CombatantState actor, BattleState state)
        {
            if (actor == null || actor.IsDefeated)
            {
                return 0;
            }

            int statusDamageTotal = 0;
            if (actor.ActiveEffects != null)
            {
                for (int i = 0; i < actor.ActiveEffects.Count; i++)
                {
                    ActiveStatusEffect effect = actor.ActiveEffects[i];
                    if (effect == null || effect.RemainingTurns <= 0)
                    {
                        continue;
                    }

                    switch (effect.TypeStatus)
                    {
                        case TypeStatusId.Withered:
                        {
                            int stage = Mathf.Clamp(effect.Magnitude + 1, 1, 5);
                            int percent = stage * 2;
                            int damage = ComputePercentDamage(actor.GetMaxHealth(), percent);
                            statusDamageTotal += ApplyStatusDamage(actor, damage);
                            effect.Magnitude = stage;
                            break;
                        }
                        case TypeStatusId.Aflame:
                        {
                            int damage = ComputePercentDamage(actor.GetMaxHealth(), 8);
                            statusDamageTotal += ApplyStatusDamage(actor, damage);
                            break;
                        }
                    }
                }
            }

            actor.TickActiveEffectsAtTurnEnd();
            return statusDamageTotal;
        }

        private static bool IsBuildUpBlockedByGoalStatus(CombatantState target, TypeStatusId buildUpStatus)
        {
            switch (buildUpStatus)
            {
                case TypeStatusId.Electrified:
                    return target.HasStatus(TypeStatusId.Paralyzed);
                case TypeStatusId.Vined:
                    return target.HasStatus(TypeStatusId.Rooted);
                case TypeStatusId.Burned:
                    return target.HasStatus(TypeStatusId.Aflame);
                default:
                    return false;
            }
        }

        private static int ComputePercentDamage(int maxHealth, int percent)
        {
            int clampedMax = Mathf.Max(1, maxHealth);
            return Mathf.Max(1, Mathf.RoundToInt(clampedMax * (percent / 100f)));
        }

        private static int ApplyStatusDamage(CombatantState target, int damage)
        {
            int before = target.CurrentHealth;
            target.CurrentHealth = before - Mathf.Max(0, damage);
            return Mathf.Max(0, before - target.CurrentHealth);
        }

        private static int GetMovementBudget(CombatantState actor)
        {
            int speed = actor != null ? actor.GetEffectiveStats().Speed : 1;
            return Mathf.Clamp(Mathf.Max(1, speed / 4), 1, 3);
        }

        private static int SpendMovementFleeing(CombatantState actor, BattleState state)
        {
            int budget = GetMovementBudget(actor);
            if (budget <= 0)
            {
                return 0;
            }

            int moved = 0;
            for (int step = 0; step < budget; step++)
            {
                GridPosition from = actor.Position;
                GridPosition to = FindFleeStep(actor, state, from);
                if (to.X == from.X && to.Y == from.Y)
                {
                    break;
                }

                actor.Position = to;
                moved++;
            }

            return moved;
        }

        private static GridPosition FindFleeStep(CombatantState actor, BattleState state, GridPosition from)
        {
            GridPosition[] candidates =
            {
                new GridPosition(from.X + 1, from.Y),
                new GridPosition(from.X - 1, from.Y),
                new GridPosition(from.X, from.Y + 1),
                new GridPosition(from.X, from.Y - 1)
            };

            int currentScore = GetNearestOpponentDistance(actor, state, from);
            int bestScore = currentScore;
            GridPosition best = from;
            for (int i = 0; i < candidates.Length; i++)
            {
                GridPosition candidate = candidates[i];
                if (!state.IsInsideGrid(candidate) || IsOccupied(state, candidate, actor.CombatantId))
                {
                    continue;
                }

                int score = GetNearestOpponentDistance(actor, state, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static int GetNearestOpponentDistance(CombatantState actor, BattleState state, GridPosition position)
        {
            if (state == null || state.Combatants == null)
            {
                return 0;
            }

            int best = int.MaxValue;
            for (int i = 0; i < state.Combatants.Count; i++)
            {
                CombatantState other = state.Combatants[i];
                if (other == null || other.IsDefeated || other.Team == actor.Team)
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

        private static bool IsOccupied(BattleState state, GridPosition position, string exceptCombatantId)
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
    }
}
