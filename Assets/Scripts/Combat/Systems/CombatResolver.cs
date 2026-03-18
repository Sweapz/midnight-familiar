using System;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Systems
{
    public interface IDiceRoller
    {
        int RollD20();
    }

    public sealed class UnityDiceRoller : IDiceRoller
    {
        public int RollD20()
        {
            return UnityEngine.Random.Range(1, 21);
        }
    }

    public interface ITypeEffectivenessProvider
    {
        float GetMultiplier(CuidType actionType, CuidUnit target);
    }

    public sealed class NeutralTypeEffectivenessProvider : ITypeEffectivenessProvider
    {
        public float GetMultiplier(CuidType actionType, CuidUnit target)
        {
            return 1f;
        }
    }

    [Serializable]
    public class ActionResolution
    {
        public string ActorCombatantId = string.Empty;
        public string TargetCombatantId = string.Empty;
        public string ActionId = string.Empty;
        public ActionKind Kind = ActionKind.Attack;
        public AbilityIntent AbilityIntent = AbilityIntent.None;
        public int AttackRoll;
        public int DefenseRoll;
        public bool Succeeded;
        public bool WasResisted;
        public int AppliedMagnitude;
        public int TargetHealthBefore;
        public int TargetHealthAfter;
        public float TypeMultiplier = 1f;
        public string Summary = string.Empty;
    }

    public sealed class CombatResolver
    {
        private const int BaseDefenseTarget = 10;
        private readonly IDiceRoller _diceRoller;
        private readonly ITypeEffectivenessProvider _typeEffectiveness;

        public CombatResolver(
            IDiceRoller diceRoller = null,
            ITypeEffectivenessProvider typeEffectiveness = null)
        {
            _diceRoller = diceRoller ?? new UnityDiceRoller();
            _typeEffectiveness = typeEffectiveness ?? new TypeChartEffectivenessProvider();
        }

        public ActionResolution ResolveAction(CombatantState actor, CombatantState target, CuidAction action)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (actor.Unit == null) throw new ArgumentException("Actor must have a unit.", nameof(actor));
            if (target.Unit == null) throw new ArgumentException("Target must have a unit.", nameof(target));
            if (actor.Unit.Stats == null) throw new ArgumentException("Actor unit must have stats.", nameof(actor));
            if (target.Unit.Stats == null) throw new ArgumentException("Target unit must have stats.", nameof(target));

            switch (action.Kind)
            {
                case ActionKind.Attack:
                    return ResolveAttack(actor, target, action);
                case ActionKind.Ability:
                    return ResolveAbility(actor, target, action);
                default:
                    throw new NotSupportedException($"Unsupported action kind: {action.Kind}");
            }
        }

        private ActionResolution ResolveAttack(CombatantState actor, CombatantState target, CuidAction action)
        {
            var result = BuildBaseResult(actor, target, action);
            result.Kind = ActionKind.Attack;
            result.AbilityIntent = AbilityIntent.None;

            result.AttackRoll = _diceRoller.RollD20() + actor.Unit.Stats.Attack + action.HitBonus;
            result.DefenseRoll = BaseDefenseTarget + target.Unit.Stats.Defense;
            result.Succeeded = result.AttackRoll >= result.DefenseRoll;
            result.WasResisted = !result.Succeeded;

            if (!result.Succeeded)
            {
                result.Summary = $"{actor.Unit.DisplayName} missed {target.Unit.DisplayName} with {action.DisplayName}.";
                return result;
            }

            var rawDamage = Mathf.Max(0, action.Potency + actor.Unit.Stats.Damage);
            result.TypeMultiplier = _typeEffectiveness.GetMultiplier(action.ActionType, target.Unit);
            var scaledDamage = Mathf.RoundToInt(rawDamage * result.TypeMultiplier);
            var reducedDamage = scaledDamage - target.Unit.Stats.DamageReduction;
            var finalDamage = Mathf.Max(1, reducedDamage);

            result.AppliedMagnitude = ApplyDamage(target, finalDamage);
            result.TargetHealthAfter = target.CurrentHealth;
            result.Summary = $"{actor.Unit.DisplayName} hit {target.Unit.DisplayName} with {action.DisplayName} for {result.AppliedMagnitude} damage.";
            return result;
        }

        private ActionResolution ResolveAbility(CombatantState actor, CombatantState target, CuidAction action)
        {
            var result = BuildBaseResult(actor, target, action);
            result.Kind = ActionKind.Ability;
            result.AbilityIntent = action.AbilityIntent;

            switch (action.AbilityIntent)
            {
                case AbilityIntent.Offensive:
                    ResolveOffensiveAbility(actor, target, action, result);
                    break;
                case AbilityIntent.Supportive:
                    ResolveSupportiveAbility(actor, target, action, result);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Ability actions require an intent. Action '{action.Id}' has '{action.AbilityIntent}'.");
            }

            return result;
        }

        private void ResolveOffensiveAbility(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ActionResolution result)
        {
            result.AttackRoll = _diceRoller.RollD20() + actor.Unit.Stats.AbilityEffectiveness + action.HitBonus;
            result.DefenseRoll = BaseDefenseTarget + target.Unit.Stats.AbilityResistance;
            result.Succeeded = result.AttackRoll >= result.DefenseRoll;
            result.WasResisted = !result.Succeeded;

            if (!result.Succeeded)
            {
                result.Summary = $"{target.Unit.DisplayName} resisted {actor.Unit.DisplayName}'s {action.DisplayName}.";
                return;
            }

            var rawMagnitude = Mathf.Max(
                0,
                action.Potency + actor.Unit.Stats.AbilityEffectiveness + actor.Unit.Stats.AbilityDamage);
            result.TypeMultiplier = _typeEffectiveness.GetMultiplier(action.ActionType, target.Unit);
            var scaledMagnitude = Mathf.RoundToInt(rawMagnitude * result.TypeMultiplier);
            var reducedMagnitude = scaledMagnitude - target.Unit.Stats.AbilityReduction;
            var finalDamage = Mathf.Max(1, reducedMagnitude);

            result.AppliedMagnitude = ApplyDamage(target, finalDamage);
            result.TargetHealthAfter = target.CurrentHealth;
            result.Summary = $"{actor.Unit.DisplayName} used {action.DisplayName} for {result.AppliedMagnitude} ability damage.";
        }

        private void ResolveSupportiveAbility(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ActionResolution result)
        {
            var isFriendlyTarget = actor.Team == target.Team;
            if (isFriendlyTarget)
            {
                result.AttackRoll = 0;
                result.DefenseRoll = 0;
                result.Succeeded = true;
                result.WasResisted = false;
            }
            else
            {
                result.AttackRoll = _diceRoller.RollD20() + actor.Unit.Stats.AbilityEffectiveness + action.HitBonus;
                result.DefenseRoll = BaseDefenseTarget + target.Unit.Stats.AbilityResistance;
                result.Succeeded = result.AttackRoll >= result.DefenseRoll;
                result.WasResisted = !result.Succeeded;
            }

            if (!result.Succeeded)
            {
                result.Summary = $"{target.Unit.DisplayName} resisted supportive effect {action.DisplayName}.";
                return;
            }

            result.AppliedMagnitude = Mathf.Max(1, action.Potency + actor.Unit.Stats.AbilityEffectiveness);
            result.TargetHealthAfter = target.CurrentHealth;
            result.Summary =
                $"{actor.Unit.DisplayName} applied supportive ability {action.DisplayName} (strength {result.AppliedMagnitude}).";
        }

        private ActionResolution BuildBaseResult(CombatantState actor, CombatantState target, CuidAction action)
        {
            return new ActionResolution
            {
                ActorCombatantId = actor.CombatantId,
                TargetCombatantId = target.CombatantId,
                ActionId = action.Id,
                TargetHealthBefore = target.CurrentHealth,
                TargetHealthAfter = target.CurrentHealth,
                TypeMultiplier = 1f
            };
        }

        private static int ApplyDamage(CombatantState target, int damage)
        {
            var before = target.CurrentHealth;
            target.CurrentHealth = before - Mathf.Max(0, damage);
            return before - target.CurrentHealth;
        }
    }
}
