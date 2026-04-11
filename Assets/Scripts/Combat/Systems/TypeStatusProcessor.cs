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
        private readonly ICombatMovementService _movementService;

        public TypeStatusProcessor(IDiceRoller diceRoller, ICombatMovementService movementService = null)
        {
            _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
            _movementService = movementService ?? new CombatMovementService();
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
                int movedSteps = _movementService.SpendMovementFleeing(actor, state);
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
    }
}
