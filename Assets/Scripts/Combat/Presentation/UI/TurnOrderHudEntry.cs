using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    [System.Serializable]
    public sealed class TurnOrderHudEntry
    {
        public string CombatantId;
        public string DisplayName;
        public int CurrentHp;
        public int MaxHp;
        public Sprite Portrait;
        public TeamSide Team;
        public bool IsDefeated;
    }
}
