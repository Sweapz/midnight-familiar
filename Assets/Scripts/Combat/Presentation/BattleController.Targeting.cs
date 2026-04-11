using System.Linq;
using System.Collections.Generic;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private CombatantState FindClosestOpponent(CombatantState actor)
        {
            TeamSide opponentSide = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            return _turnController.BattleState.Combatants
                .Where(c => c.Team == opponentSide && !c.IsDefeated)
                .OrderBy(c => c.Position.ManhattanDistanceTo(actor.Position))
                .FirstOrDefault();
        }

        private TurnChoice BuildEnemyChoice(CombatantState actor, CombatantState defaultTarget)
        {
            CuidAction offensive = actor.Unit.Actions
                .FirstOrDefault(action => action != null &&
                                          (action.Kind == ActionKind.Attack ||
                                           (action.Kind == ActionKind.Ability &&
                                            (action.AbilityIntent == AbilityIntent.Offensive ||
                                             action.AbilityIntent == AbilityIntent.Debuff))) &&
                                          IsTargetInRange(actor, defaultTarget, action));

            if (offensive != null)
            {
                return new TurnChoice
                {
                    IsPass = false,
                    ActionId = offensive.Id,
                    TargetCombatantId = defaultTarget.CombatantId
                };
            }

            CuidAction supportive = actor.Unit.Actions
                .FirstOrDefault(action => action != null &&
                                          action.Kind == ActionKind.Ability &&
                                          action.AbilityIntent == AbilityIntent.Supportive &&
                                          IsTargetInRange(actor, actor, action));

            if (supportive != null)
            {
                return new TurnChoice
                {
                    IsPass = false,
                    ActionId = supportive.Id,
                    TargetCombatantId = actor.CombatantId
                };
            }

            return TurnChoice.Pass();
        }

        private bool IsValidTargetForSelectedAction(CombatantState actor, CombatantState target, CuidAction action)
        {
            if (actor == null || target == null || action == null || target.IsDefeated)
            {
                return false;
            }

            if (action.Kind == ActionKind.Attack)
            {
                return target.Team != actor.Team && IsTargetInRange(actor, target, action);
            }

            if (action.Kind == ActionKind.Ability)
            {
                switch (action.TargetRule)
                {
                    case TargetRule.Self:
                        return target.CombatantId == actor.CombatantId && IsTargetInRange(actor, target, action);
                    case TargetRule.AllySingle:
                        return target.Team == actor.Team && IsTargetInRange(actor, target, action);
                    case TargetRule.EnemySingle:
                    case TargetRule.EnemyArea:
                        return target.Team != actor.Team && IsTargetInRange(actor, target, action);
                }
            }

            return false;
        }

        private bool IsTargetInRange(CombatantState actor, CombatantState target, CuidAction action)
        {
            int distance = actor.Position.ManhattanDistanceTo(target.Position);
            return distance <= Mathf.Max(0, action.Range);
        }

        private TeamRoster BuildTeam(TeamSide side, List<CuidDefinition> definitions)
        {
            var units = new List<CuidUnit>(4);
            foreach (CuidDefinition definition in definitions.Where(definition => definition != null))
            {
                units.Add(CuidFactory.CreateUnit(definition));
            }

            return new TeamRoster
            {
                Side = side,
                Units = units
            };
        }
    }
}
