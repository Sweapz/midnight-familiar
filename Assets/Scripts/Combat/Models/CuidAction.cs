using System;
using System.Collections.Generic;

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
        public bool IsBasicAttack = false;
        public List<SupportEffect> SupportEffects = new List<SupportEffect>(2);

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
                CooldownTurns = CooldownTurns,
                IsBasicAttack = IsBasicAttack,
                SupportEffects = CloneSupportEffects()
            };
        }

        private List<SupportEffect> CloneSupportEffects()
        {
            var copy = new List<SupportEffect>(SupportEffects != null ? SupportEffects.Count : 0);
            if (SupportEffects == null)
            {
                return copy;
            }

            for (int i = 0; i < SupportEffects.Count; i++)
            {
                SupportEffect effect = SupportEffects[i];
                if (effect == null)
                {
                    continue;
                }

                copy.Add(effect.Clone());
            }

            return copy;
        }
    }
}
