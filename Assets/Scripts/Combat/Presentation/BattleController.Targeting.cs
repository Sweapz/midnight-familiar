using System.Linq;
using System.Collections.Generic;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Systems;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private CombatantState FindClosestOpponent(CombatantState actor)
        {
            return _spatialQueryService.FindClosestOpponent(_turnController.BattleState, actor);
        }

        private bool IsValidTargetForSelectedAction(CombatantState actor, CombatantState target, CuidAction action)
        {
            return _actionQueryService.IsValidTarget(actor, target, action, _spatialQueryService);
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
