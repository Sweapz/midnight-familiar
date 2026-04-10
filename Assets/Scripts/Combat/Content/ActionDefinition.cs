using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Content
{
    [CreateAssetMenu(
        fileName = "ActionDefinition",
        menuName = "Midnight Familiar/Combat/Action Definition")]
    public class ActionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string actionId = "action_id";
        [SerializeField] private string displayName = "Action";
        [SerializeField] private string description = string.Empty;

        [Header("Behavior")]
        [SerializeField] private ActionKind kind = ActionKind.Attack;
        [SerializeField] private AbilityIntent abilityIntent = AbilityIntent.None;
        [SerializeField] private CuidType actionType = CuidType.None;
        [SerializeField] private TargetRule targetRule = TargetRule.EnemySingle;
        [SerializeField] private int range = 1;
        [SerializeField] private int hitBonus = 0;
        [SerializeField] private int potency = 5;
        [SerializeField] private int cooldownTurns = 0;
        [SerializeField] private List<SupportEffect> supportEffects = new List<SupportEffect>(2);

        public string ActionId => actionId;
        public string DisplayName => displayName;
        public string Description => description;
        public ActionKind Kind => kind;
        public AbilityIntent AbilityIntent => abilityIntent;
        public CuidType ActionType => actionType;
        public TargetRule TargetRule => targetRule;
        public int Range => range;
        public int HitBonus => hitBonus;
        public int Potency => potency;
        public int CooldownTurns => cooldownTurns;
        public IReadOnlyList<SupportEffect> SupportEffects => supportEffects;

        public void ConfigureForRuntime(
            string newActionId,
            string newDisplayName,
            string newDescription,
            ActionKind newKind,
            AbilityIntent newAbilityIntent,
            CuidType newActionType,
            TargetRule newTargetRule,
            int newRange,
            int newHitBonus,
            int newPotency,
            int newCooldownTurns,
            List<SupportEffect> newSupportEffects = null)
        {
            actionId = newActionId;
            displayName = newDisplayName;
            description = newDescription;
            kind = newKind;
            abilityIntent = newAbilityIntent;
            actionType = newActionType;
            targetRule = newTargetRule;
            range = newRange;
            hitBonus = newHitBonus;
            potency = newPotency;
            cooldownTurns = newCooldownTurns;
            supportEffects = newSupportEffects ?? new List<SupportEffect>(2);
        }
    }
}
