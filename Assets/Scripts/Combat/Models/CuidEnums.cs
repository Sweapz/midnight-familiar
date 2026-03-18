using System;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public enum CuidType
    {
        None = 0,
        Arcane = 1,
        Beast = 2,
        Ember = 3,
        Flora = 4,
        Stone = 5,
        Tide = 6,
        Volt = 7
    }

    [Serializable]
    public enum ActionKind
    {
        Attack = 0,
        Ability = 1
    }

    [Serializable]
    public enum AbilityIntent
    {
        None = 0,
        Offensive = 1,
        Supportive = 2
    }

    [Serializable]
    public enum TargetRule
    {
        EnemySingle = 0,
        EnemyArea = 1,
        AllySingle = 2,
        Self = 3
    }
}
