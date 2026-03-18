using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public enum TeamSide
    {
        Player = 0,
        Enemy = 1
    }

    [Serializable]
    public class TeamRoster
    {
        public TeamSide Side;
        public List<CuidUnit> Units = new List<CuidUnit>(4);
    }

    [Serializable]
    public class CombatantState
    {
        public string CombatantId = Guid.NewGuid().ToString("N");
        public TeamSide Team;
        public CuidUnit Unit = new CuidUnit();
        public GridPosition Position;
        public bool IsDefeated;

        public int CurrentHealth
        {
            get => Unit != null ? Unit.CurrentHealth : 0;
            set
            {
                if (Unit == null)
                {
                    return;
                }

                Unit.CurrentHealth = Mathf.Clamp(value, 0, Unit.Stats.Constitution);
                IsDefeated = Unit.CurrentHealth <= 0;
            }
        }
    }

    [Serializable]
    public class BattleState
    {
        public int GridWidth = 8;
        public int GridHeight = 8;
        public int RoundNumber = 1;
        public int CurrentTurnIndex = 0;
        public List<CombatantState> Combatants = new List<CombatantState>(8);
        public List<string> TurnOrder = new List<string>(8);

        public bool IsInsideGrid(GridPosition position)
        {
            return position.IsInside(GridWidth, GridHeight);
        }

        public CombatantState FindCombatant(string combatantId)
        {
            return Combatants.Find(combatant => combatant.CombatantId == combatantId);
        }
    }
}
