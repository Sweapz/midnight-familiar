using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;

namespace MidnightFamiliar.Combat.Systems
{
    public interface ICombatActionQueryService
    {
        TurnChoice BuildDefaultChoice(CombatantState actor, BattleState state);
        TurnChoice BuildEnemyChoice(
            CombatantState actor,
            CombatantState defaultTarget,
            ICombatSpatialQueryService spatialQueryService);
        CuidAction FindBestEnemyOffensiveAction(
            CombatantState actor,
            CombatantState defaultTarget,
            ICombatSpatialQueryService spatialQueryService);
        CuidAction FindBestEnemySupportiveAction(
            CombatantState actor,
            ICombatSpatialQueryService spatialQueryService);
        bool IsValidTarget(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ICombatSpatialQueryService spatialQueryService);
        List<CuidAction> GetAvailableOpportunityActions(CombatantState attacker, bool opportunitySpent);
        bool HasAnyRangedAction(CombatantState actor);
        List<CuidAction> GetOrderedActionsForUi(CombatantState actor);
    }

    public sealed class CombatActionQueryService : ICombatActionQueryService
    {
        public TurnChoice BuildDefaultChoice(CombatantState actor, BattleState state)
        {
            if (actor?.Unit?.Actions == null || state == null)
            {
                return TurnChoice.Pass();
            }

            CuidAction action = actor.Unit.Actions.Find(a => a != null);
            if (action == null)
            {
                return TurnChoice.Pass();
            }

            TeamSide opposingTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            CombatantState target = state.Combatants.Find(c => c != null && c.Team == opposingTeam && !c.IsDefeated);
            if (target == null)
            {
                return TurnChoice.Pass();
            }

            return new TurnChoice
            {
                IsPass = false,
                ActionId = action.Id,
                TargetCombatantId = target.CombatantId
            };
        }

        public TurnChoice BuildEnemyChoice(
            CombatantState actor,
            CombatantState defaultTarget,
            ICombatSpatialQueryService spatialQueryService)
        {
            CuidAction offensive = FindBestEnemyOffensiveAction(actor, defaultTarget, spatialQueryService);
            if (offensive != null)
            {
                return new TurnChoice
                {
                    IsPass = false,
                    ActionId = offensive.Id,
                    TargetCombatantId = defaultTarget.CombatantId
                };
            }

            CuidAction supportive = FindBestEnemySupportiveAction(actor, spatialQueryService);
            if (supportive != null)
            {
                return new TurnChoice
                {
                    IsPass = false,
                    ActionId = supportive.Id,
                    TargetCombatantId = actor != null ? actor.CombatantId : string.Empty
                };
            }

            return TurnChoice.Pass();
        }

        public CuidAction FindBestEnemyOffensiveAction(
            CombatantState actor,
            CombatantState defaultTarget,
            ICombatSpatialQueryService spatialQueryService)
        {
            if (actor?.Unit?.Actions == null || defaultTarget == null || spatialQueryService == null)
            {
                return null;
            }

            for (int i = 0; i < actor.Unit.Actions.Count; i++)
            {
                CuidAction action = actor.Unit.Actions[i];
                if (action == null)
                {
                    continue;
                }

                bool isOffensive =
                    action.Kind == ActionKind.Attack ||
                    (action.Kind == ActionKind.Ability &&
                     (action.AbilityIntent == AbilityIntent.Offensive ||
                      action.AbilityIntent == AbilityIntent.Debuff));
                if (!isOffensive)
                {
                    continue;
                }

                if (spatialQueryService.IsTargetInRange(actor.Position, defaultTarget.Position, action.Range))
                {
                    return action;
                }
            }

            return null;
        }

        public CuidAction FindBestEnemySupportiveAction(
            CombatantState actor,
            ICombatSpatialQueryService spatialQueryService)
        {
            if (actor?.Unit?.Actions == null || spatialQueryService == null)
            {
                return null;
            }

            for (int i = 0; i < actor.Unit.Actions.Count; i++)
            {
                CuidAction action = actor.Unit.Actions[i];
                if (action == null ||
                    action.Kind != ActionKind.Ability ||
                    action.AbilityIntent != AbilityIntent.Supportive)
                {
                    continue;
                }

                if (spatialQueryService.IsTargetInRange(actor.Position, actor.Position, action.Range))
                {
                    return action;
                }
            }

            return null;
        }

        public bool IsValidTarget(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ICombatSpatialQueryService spatialQueryService)
        {
            if (actor == null || target == null || action == null || target.IsDefeated || spatialQueryService == null)
            {
                return false;
            }

            bool inRange = spatialQueryService.IsTargetInRange(actor.Position, target.Position, action.Range);
            if (action.Kind == ActionKind.Attack)
            {
                return target.Team != actor.Team && inRange;
            }

            if (action.Kind == ActionKind.Ability)
            {
                switch (action.TargetRule)
                {
                    case TargetRule.Self:
                        return target.CombatantId == actor.CombatantId && inRange;
                    case TargetRule.AllySingle:
                        return target.Team == actor.Team && inRange;
                    case TargetRule.EnemySingle:
                    case TargetRule.EnemyArea:
                        return target.Team != actor.Team && inRange;
                }
            }

            return false;
        }

        public List<CuidAction> GetAvailableOpportunityActions(CombatantState attacker, bool opportunitySpent)
        {
            var actions = new List<CuidAction>(1);
            if (attacker?.Unit?.Actions == null || opportunitySpent)
            {
                return actions;
            }

            for (int i = 0; i < attacker.Unit.Actions.Count; i++)
            {
                CuidAction action = attacker.Unit.Actions[i];
                if (action != null && action.IsBasicAttack && action.Kind == ActionKind.Attack)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }

        public bool HasAnyRangedAction(CombatantState actor)
        {
            if (actor?.Unit?.Actions == null)
            {
                return false;
            }

            for (int i = 0; i < actor.Unit.Actions.Count; i++)
            {
                CuidAction action = actor.Unit.Actions[i];
                if (action != null && action.Range > 1)
                {
                    return true;
                }
            }

            return false;
        }

        public List<CuidAction> GetOrderedActionsForUi(CombatantState actor)
        {
            var basicActions = new List<CuidAction>(1);
            var otherActions = new List<CuidAction>(4);
            if (actor?.Unit?.Actions == null)
            {
                return otherActions;
            }

            for (int i = 0; i < actor.Unit.Actions.Count; i++)
            {
                CuidAction action = actor.Unit.Actions[i];
                if (action == null)
                {
                    continue;
                }

                if (action.IsBasicAttack)
                {
                    basicActions.Add(action);
                }
                else
                {
                    otherActions.Add(action);
                }
            }

            basicActions.AddRange(otherActions);
            return basicActions;
        }
    }
}
