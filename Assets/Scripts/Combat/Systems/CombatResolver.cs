using System;
using System.Collections.Generic;
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
        public string DamageBreakdown = string.Empty;
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
            CuidStats actorStats = actor.GetEffectiveStats();
            CuidStats targetStats = target.GetEffectiveStats();

            int attackBonus = actorStats.Attack;
            int defenseTargetBonus = targetStats.Defense;
            if (action.IsBasicAttack)
            {
                bool useAbilityTrack = actorStats.AbilityEffectiveness > actorStats.Attack;
                attackBonus = useAbilityTrack ? actorStats.AbilityEffectiveness : actorStats.Attack;
                defenseTargetBonus = useAbilityTrack ? targetStats.AbilityResistance : targetStats.Defense;
            }

            result.AttackRoll = _diceRoller.RollD20() + attackBonus + action.HitBonus;
            result.DefenseRoll = BaseDefenseTarget + defenseTargetBonus;
            result.Succeeded = result.AttackRoll >= result.DefenseRoll;
            result.WasResisted = !result.Succeeded;

            if (!result.Succeeded)
            {
                result.Summary = $"{actor.Unit.DisplayName} missed {target.Unit.DisplayName} with {action.DisplayName}.";
                return result;
            }

            var rawDamage = Mathf.Max(0, action.Potency + actorStats.Damage);
            result.TypeMultiplier = _typeEffectiveness.GetMultiplier(action.ActionType, target.Unit);
            var scaledDamage = Mathf.RoundToInt(rawDamage * result.TypeMultiplier);
            var adjustedDamage = scaledDamage + actor.GetBonusFlatDamageIncrease();
            var reducedDamage = adjustedDamage - targetStats.DamageReduction - target.GetBonusFlatDamageReduction();
            var finalDamage = Mathf.Max(1, reducedDamage);

            result.AppliedMagnitude = ApplyDamageWithMitigation(actor, target, finalDamage, out int shieldAbsorbed);
            result.TargetHealthAfter = target.CurrentHealth;
            result.DamageBreakdown = BuildDamageBreakdown(
                action.Potency,
                actorStats.Damage,
                result.TypeMultiplier,
                actor.GetBonusFlatDamageIncrease(),
                targetStats.DamageReduction,
                target.GetBonusFlatDamageReduction(),
                finalDamage,
                shieldAbsorbed,
                result.AppliedMagnitude);
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
            CuidStats actorStats = actor.GetEffectiveStats();
            CuidStats targetStats = target.GetEffectiveStats();
            result.AttackRoll = _diceRoller.RollD20() + actorStats.AbilityEffectiveness + action.HitBonus;
            result.DefenseRoll = BaseDefenseTarget + targetStats.AbilityResistance;
            result.Succeeded = result.AttackRoll >= result.DefenseRoll;
            result.WasResisted = !result.Succeeded;

            if (!result.Succeeded)
            {
                result.Summary = $"{target.Unit.DisplayName} resisted {actor.Unit.DisplayName}'s {action.DisplayName}.";
                return;
            }

            var rawMagnitude = Mathf.Max(
                0,
                action.Potency + actorStats.AbilityDamage);
            result.TypeMultiplier = _typeEffectiveness.GetMultiplier(action.ActionType, target.Unit);
            var scaledMagnitude = Mathf.RoundToInt(rawMagnitude * result.TypeMultiplier);
            var adjustedMagnitude = scaledMagnitude + actor.GetBonusFlatDamageIncrease();
            var reducedMagnitude = adjustedMagnitude - targetStats.AbilityReduction - target.GetBonusFlatDamageReduction();
            var finalDamage = Mathf.Max(1, reducedMagnitude);

            result.AppliedMagnitude = ApplyDamageWithMitigation(actor, target, finalDamage, out int shieldAbsorbed);
            result.TargetHealthAfter = target.CurrentHealth;
            int abilityStatContribution = actorStats.AbilityDamage;
            result.DamageBreakdown = BuildDamageBreakdown(
                action.Potency,
                abilityStatContribution,
                result.TypeMultiplier,
                actor.GetBonusFlatDamageIncrease(),
                targetStats.AbilityReduction,
                target.GetBonusFlatDamageReduction(),
                finalDamage,
                shieldAbsorbed,
                result.AppliedMagnitude);
            result.Summary = $"{actor.Unit.DisplayName} used {action.DisplayName} for {result.AppliedMagnitude} ability damage.";
        }

        private void ResolveSupportiveAbility(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ActionResolution result)
        {
            CuidStats actorStats = actor.GetEffectiveStats();
            CuidStats targetStats = target.GetEffectiveStats();
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
                result.AttackRoll = _diceRoller.RollD20() + actorStats.AbilityEffectiveness + action.HitBonus;
                result.DefenseRoll = BaseDefenseTarget + targetStats.AbilityResistance;
                result.Succeeded = result.AttackRoll >= result.DefenseRoll;
                result.WasResisted = !result.Succeeded;
            }

            if (!result.Succeeded)
            {
                result.Summary = $"{target.Unit.DisplayName} resisted supportive effect {action.DisplayName}.";
                return;
            }

            int totalAppliedMagnitude = ApplySupportiveEffects(actor, target, action, actorStats, out string effectsSummary);
            if (totalAppliedMagnitude <= 0)
            {
                totalAppliedMagnitude = Mathf.Max(1, action.Potency + actorStats.AbilityEffectiveness);
                int preHeal = target.CurrentHealth;
                target.CurrentHealth = preHeal + totalAppliedMagnitude;
                totalAppliedMagnitude = target.CurrentHealth - preHeal;
                effectsSummary = $"healed {target.Unit.DisplayName} for {totalAppliedMagnitude}";
            }

            result.AppliedMagnitude = totalAppliedMagnitude;
            result.TargetHealthAfter = target.CurrentHealth;
            result.Summary = $"{actor.Unit.DisplayName} used {action.DisplayName}: {effectsSummary}.";
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

        private int ApplyDamageWithMitigation(CombatantState actor, CombatantState target, int damage, out int shieldAbsorbed)
        {
            int rawDamage = Mathf.Max(0, damage);
            shieldAbsorbed = 0;
            if (rawDamage <= 0)
            {
                return 0;
            }

            shieldAbsorbed = target.AbsorbWithShields(rawDamage);
            int remaining = Mathf.Max(0, rawDamage - shieldAbsorbed);
            int healthDamage = ApplyDamage(target, remaining);

            if (healthDamage > 0)
            {
                ApplyThornsReflection(target, actor);
            }

            return healthDamage;
        }

        private static int ApplyDamage(CombatantState target, int damage)
        {
            int before = target.CurrentHealth;
            target.CurrentHealth = before - Mathf.Max(0, damage);
            return before - target.CurrentHealth;
        }

        private static void ApplyThornsReflection(CombatantState defender, CombatantState attacker)
        {
            if (defender == null || attacker == null || defender.CombatantId == attacker.CombatantId || attacker.IsDefeated)
            {
                return;
            }

            int thornsDamage = defender.GetThornsDamage();
            if (thornsDamage <= 0)
            {
                return;
            }

            int absorbed = attacker.AbsorbWithShields(thornsDamage);
            int remaining = Mathf.Max(0, thornsDamage - absorbed);
            if (remaining > 0)
            {
                ApplyDamage(attacker, remaining);
            }
        }

        private static int ApplySupportiveEffects(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            CuidStats actorStats,
            out string summary)
        {
            int totalApplied = 0;
            var summaryParts = new List<string>(2);

            if (action.SupportEffects == null || action.SupportEffects.Count == 0)
            {
                summary = string.Empty;
                return 0;
            }

            for (int i = 0; i < action.SupportEffects.Count; i++)
            {
                SupportEffect effect = action.SupportEffects[i];
                if (effect == null)
                {
                    continue;
                }

                switch (effect.Kind)
                {
                    case SupportEffectKind.Heal:
                    {
                        int healAmount = Mathf.Max(1, effect.Magnitude + actorStats.AbilityEffectiveness);
                        int preHeal = target.CurrentHealth;
                        target.CurrentHealth = preHeal + healAmount;
                        int applied = target.CurrentHealth - preHeal;
                        totalApplied += applied;
                        summaryParts.Add($"healed {target.Unit.DisplayName} for {applied}");
                        break;
                    }
                    case SupportEffectKind.StatModifier:
                    case SupportEffectKind.FlatDamageReduction:
                    case SupportEffectKind.FlatDamageIncrease:
                    case SupportEffectKind.Shield:
                    case SupportEffectKind.Thorns:
                    {
                        int duration = Mathf.Max(1, effect.DurationTurns);
                        int magnitude = Mathf.Max(0, effect.Magnitude);
                        if (magnitude <= 0)
                        {
                            break;
                        }

                        target.AddActiveEffect(new ActiveStatusEffect
                        {
                            Kind = effect.Kind,
                            TargetStat = effect.TargetStat,
                            Magnitude = magnitude,
                            RemainingTurns = duration
                        });

                        totalApplied += magnitude;
                        summaryParts.Add(BuildEffectSummary(effect, magnitude, duration));
                        break;
                    }
                }
            }

            summary = summaryParts.Count > 0
                ? string.Join(", ", summaryParts)
                : $"applied support effects to {target.Unit.DisplayName}";
            return totalApplied;
        }

        private static string BuildEffectSummary(SupportEffect effect, int magnitude, int duration)
        {
            switch (effect.Kind)
            {
                case SupportEffectKind.StatModifier:
                    return $"boosted {effect.TargetStat} by {magnitude} for {duration} turns";
                case SupportEffectKind.FlatDamageReduction:
                    return $"granted {magnitude} flat damage reduction for {duration} turns";
                case SupportEffectKind.FlatDamageIncrease:
                    return $"granted {magnitude} flat damage increase for {duration} turns";
                case SupportEffectKind.Shield:
                    return $"granted a {magnitude} shield for {duration} turns";
                case SupportEffectKind.Thorns:
                    return $"granted {magnitude} thorns for {duration} turns";
                default:
                    return $"applied {effect.Kind}";
            }
        }

        private static string BuildDamageBreakdown(
            int potency,
            int sourceStatBonus,
            float typeMultiplier,
            int flatIncrease,
            int targetReduction,
            int targetFlatReduction,
            int reducedDamageFloorApplied,
            int shieldAbsorbed,
            int healthDamageApplied)
        {
            int preType = Mathf.Max(0, potency + sourceStatBonus);
            int scaled = Mathf.RoundToInt(preType * typeMultiplier);
            int withFlatIncrease = scaled + Mathf.Max(0, flatIncrease);

            return
                $"Damage: ({potency} + {sourceStatBonus}) x {typeMultiplier:0.##} + {Mathf.Max(0, flatIncrease)} - {targetReduction} - {targetFlatReduction} = {reducedDamageFloorApplied}; " +
                $"shield {shieldAbsorbed}, hp {healthDamageApplied} (scaled {scaled}, pre-scale {preType}, boosted {withFlatIncrease})";
        }
    }
}
