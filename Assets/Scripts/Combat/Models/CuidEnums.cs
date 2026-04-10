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

    [Serializable]
    public enum SupportEffectKind
    {
        Heal = 0,
        StatModifier = 1,
        FlatDamageReduction = 2,
        FlatDamageIncrease = 3,
        Shield = 4,
        Thorns = 5
    }

    [Serializable]
    public enum CuidStatType
    {
        Attack = 0,
        Defense = 1,
        AbilityEffectiveness = 2,
        AbilityResistance = 3,
        Speed = 4
    }

    [Serializable]
    public class SupportEffect
    {
        public SupportEffectKind Kind = SupportEffectKind.Heal;
        public CuidStatType TargetStat = CuidStatType.Attack;
        public int Magnitude = 5;
        public int DurationTurns = 0;

        public SupportEffect Clone()
        {
            return new SupportEffect
            {
                Kind = Kind,
                TargetStat = TargetStat,
                Magnitude = Magnitude,
                DurationTurns = DurationTurns
            };
        }
    }

    [Serializable]
    public class ActiveStatusEffect
    {
        public SupportEffectKind Kind = SupportEffectKind.Heal;
        public CuidStatType TargetStat = CuidStatType.Attack;
        public int Magnitude = 0;
        public int RemainingTurns = 0;

        public bool IsExpired =>
            RemainingTurns <= 0 || (Kind == SupportEffectKind.Shield && Magnitude <= 0);
    }
}
