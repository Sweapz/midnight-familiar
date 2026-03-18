using System;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public class CuidAction
    {
        public string Id = "action_id";
        public string DisplayName = "Action";
        public string Description = string.Empty;
        public ActionKind Kind = ActionKind.Attack;
        public AbilityIntent AbilityIntent = AbilityIntent.None;
        public CuidType ActionType = CuidType.None;
        public TargetRule TargetRule = TargetRule.EnemySingle;
        public int Range = 1;
        public int HitBonus = 0;
        public int Potency = 5;
        public int CooldownTurns = 0;

        public bool IsAbility => Kind == ActionKind.Ability;
        public bool IsAttack => Kind == ActionKind.Attack;

        // All abilities, supportive or offensive, scale through Ability Effectiveness.
        public bool UsesAbilityEffectiveness()
        {
            return IsAbility;
        }

        public CuidAction Clone()
        {
            return new CuidAction
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                Kind = Kind,
                AbilityIntent = AbilityIntent,
                ActionType = ActionType,
                TargetRule = TargetRule,
                Range = Range,
                HitBonus = HitBonus,
                Potency = Potency,
                CooldownTurns = CooldownTurns
            };
        }
    }
}
