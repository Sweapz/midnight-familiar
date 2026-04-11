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

    public interface ITypeStatusRuleProvider
    {
        IReadOnlyList<TypeStatusApplication> GetOnHitApplications(CuidType actionType);
    }

    public sealed class NeutralTypeEffectivenessProvider : ITypeEffectivenessProvider
    {
        public float GetMultiplier(CuidType actionType, CuidUnit target)
        {
            return 1f;
        }
    }

    public sealed class TypeStatusRuleProvider : ITypeStatusRuleProvider
    {
        public const int BuildUpStatusDuration = 2;
        private static readonly IReadOnlyList<TypeStatusApplication> EmptyApplications = new List<TypeStatusApplication>(0);
        private readonly Dictionary<CuidType, List<TypeStatusApplication>> _rules = BuildRules();

        public IReadOnlyList<TypeStatusApplication> GetOnHitApplications(CuidType actionType)
        {
            if (_rules.TryGetValue(actionType, out List<TypeStatusApplication> applications))
            {
                return applications;
            }

            return EmptyApplications;
        }

        private static Dictionary<CuidType, List<TypeStatusApplication>> BuildRules()
        {
            return new Dictionary<CuidType, List<TypeStatusApplication>>
            {
                [CuidType.Tide] = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Drenched,
                        DurationTurns = 1
                    }
                },
                [CuidType.Volt] = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.Upgrade,
                        UpgradeFrom = TypeStatusId.Electrified,
                        UpgradeTo = TypeStatusId.Paralyzed,
                        DurationTurns = 1
                    },
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Electrified,
                        DurationTurns = BuildUpStatusDuration
                    }
                },
                [CuidType.Flora] = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.Upgrade,
                        UpgradeFrom = TypeStatusId.Vined,
                        UpgradeTo = TypeStatusId.Rooted,
                        DurationTurns = 2
                    },
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Vined,
                        DurationTurns = BuildUpStatusDuration
                    }
                },
                [CuidType.Beast] = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Debilitated,
                        DurationTurns = 2
                    }
                },
                [CuidType.Ember] = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.Upgrade,
                        UpgradeFrom = TypeStatusId.Burned,
                        UpgradeTo = TypeStatusId.Aflame,
                        DurationTurns = 2
                    },
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Burned,
                        DurationTurns = BuildUpStatusDuration
                    }
                },
            };
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
        private readonly ITypeStatusRuleProvider _typeStatusRules;
        private readonly TypeStatusProcessor _statusProcessor;

        public CombatResolver(
            IDiceRoller diceRoller = null,
            ITypeEffectivenessProvider typeEffectiveness = null,
            ITypeStatusRuleProvider typeStatusRules = null)
        {
            _diceRoller = diceRoller ?? new UnityDiceRoller();
            _typeEffectiveness = typeEffectiveness ?? new TypeChartEffectivenessProvider();
            _typeStatusRules = typeStatusRules ?? new TypeStatusRuleProvider();
            _statusProcessor = new TypeStatusProcessor(_diceRoller);
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

            if (_statusProcessor.TryHandlePreActionFailure(actor, out string preActionFailureMessage))
            {
                ActionResolution failed = BuildBaseResult(actor, target, action);
                failed.Kind = action.Kind;
                failed.AbilityIntent = action.AbilityIntent;
                failed.Succeeded = false;
                failed.WasResisted = false;
                failed.Summary = preActionFailureMessage;
                return failed;
            }

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
            float outgoingMultiplier = _statusProcessor.ComputeOutgoingDamageMultiplier(actor);
            var adjustedDamage = Mathf.RoundToInt((scaledDamage + actor.GetBonusFlatDamageIncrease()) * outgoingMultiplier);
            var reducedDamage = adjustedDamage - targetStats.DamageReduction - target.GetBonusFlatDamageReduction();
            var finalDamage = Mathf.Max(1, reducedDamage);

            int directHealthDamage = ApplyDamageWithMitigation(actor, target, finalDamage, out int shieldAbsorbed);
            result.AppliedMagnitude = directHealthDamage;
            int jaggedExtra = _statusProcessor.ComputeIncomingOnHitExtraDamage(actor, target, isStatusDamageEvent: false);
            int jaggedApplied = 0;
            if (jaggedExtra > 0 && !target.IsDefeated)
            {
                jaggedApplied = ApplyDamage(target, jaggedExtra);
                result.AppliedMagnitude += jaggedApplied;
            }

            result.TargetHealthAfter = target.CurrentHealth;
            result.DamageBreakdown = BuildDamageBreakdown(
                action.Potency,
                actorStats.Damage,
                result.TypeMultiplier,
                actor.GetBonusFlatDamageIncrease(),
                outgoingMultiplier,
                false,
                targetStats.DamageReduction,
                target.GetBonusFlatDamageReduction(),
                shieldAbsorbed,
                directHealthDamage,
                jaggedApplied);

            IReadOnlyList<TypeStatusApplication> defaults = _typeStatusRules.GetOnHitApplications(action.ActionType);
            _statusProcessor.ApplyOnSuccessfulOffense(actor, target, action, defaults);
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
                case AbilityIntent.Debuff:
                    ResolveDebuffAbility(actor, target, action, result);
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
            float outgoingMultiplier = _statusProcessor.ComputeOutgoingDamageMultiplier(actor);
            var adjustedMagnitude = Mathf.RoundToInt((scaledMagnitude + actor.GetBonusFlatDamageIncrease()) * outgoingMultiplier);
            var reducedMagnitude = adjustedMagnitude - targetStats.AbilityReduction - target.GetBonusFlatDamageReduction();
            int finalDamage = Mathf.Max(1, reducedMagnitude);
            if (target.HasStatus(TypeStatusId.Drenched))
            {
                finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 1.1f));
            }

            bool drenchedBoostApplied = target.HasStatus(TypeStatusId.Drenched);
            int directHealthDamage = ApplyDamageWithMitigation(actor, target, finalDamage, out int shieldAbsorbed);
            result.AppliedMagnitude = directHealthDamage;
            int jaggedExtra = _statusProcessor.ComputeIncomingOnHitExtraDamage(actor, target, isStatusDamageEvent: false);
            int jaggedApplied = 0;
            if (jaggedExtra > 0 && !target.IsDefeated)
            {
                jaggedApplied = ApplyDamage(target, jaggedExtra);
                result.AppliedMagnitude += jaggedApplied;
            }

            result.TargetHealthAfter = target.CurrentHealth;
            int abilityStatContribution = actorStats.AbilityDamage;
            result.DamageBreakdown = BuildDamageBreakdown(
                action.Potency,
                abilityStatContribution,
                result.TypeMultiplier,
                actor.GetBonusFlatDamageIncrease(),
                outgoingMultiplier,
                drenchedBoostApplied,
                targetStats.AbilityReduction,
                target.GetBonusFlatDamageReduction(),
                shieldAbsorbed,
                directHealthDamage,
                jaggedApplied);

            IReadOnlyList<TypeStatusApplication> defaults = _typeStatusRules.GetOnHitApplications(action.ActionType);
            _statusProcessor.ApplyOnSuccessfulOffense(actor, target, action, defaults);
            result.Summary = $"{actor.Unit.DisplayName} used {action.DisplayName} for {result.AppliedMagnitude} ability damage.";
        }

        private void ResolveSupportiveAbility(
            CombatantState actor,
            CombatantState target,
            CuidAction action,
            ActionResolution result)
        {
            CuidStats actorStats = actor.GetEffectiveStats();
            var isFriendlyTarget = actor.Team == target.Team;

            if (!isFriendlyTarget)
            {
                result.AttackRoll = 0;
                result.DefenseRoll = 0;
                result.Succeeded = false;
                result.WasResisted = true;
                result.Summary = $"{action.DisplayName} is supportive and can only target allies.";
                return;
            }

            result.AttackRoll = 0;
            result.DefenseRoll = 0;
            result.Succeeded = true;
            result.WasResisted = false;

            int appliedStatusCount = _statusProcessor.ApplyFromActionConfig(actor, target, action);
            int totalAppliedMagnitude = ApplySupportiveEffects(actor, target, action, actorStats, out string effectsSummary);
            if (totalAppliedMagnitude <= 0)
            {
                if (appliedStatusCount > 0)
                {
                    effectsSummary = $"applied {appliedStatusCount} status effect(s) to {target.Unit.DisplayName}";
                }
                else
                {
                    totalAppliedMagnitude = Mathf.Max(1, action.Potency + actorStats.AbilityEffectiveness);
                    int preHeal = target.CurrentHealth;
                    target.CurrentHealth = preHeal + totalAppliedMagnitude;
                    totalAppliedMagnitude = target.CurrentHealth - preHeal;
                    effectsSummary = $"healed {target.Unit.DisplayName} for {totalAppliedMagnitude}";
                }
            }
            else if (appliedStatusCount > 0)
            {
                effectsSummary += $", applied {appliedStatusCount} status effect(s)";
            }

            result.AppliedMagnitude = totalAppliedMagnitude;
            result.TargetHealthAfter = target.CurrentHealth;
            result.Summary = $"{actor.Unit.DisplayName} used {action.DisplayName}: {effectsSummary}.";
        }

        private void ResolveDebuffAbility(
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
                result.Summary = $"{target.Unit.DisplayName} resisted debuff {action.DisplayName}.";
                return;
            }

            int appliedStatusCount = _statusProcessor.ApplyFromActionConfig(actor, target, action);
            int totalAppliedMagnitude = ApplySupportiveEffects(actor, target, action, actorStats, out string effectsSummary);
            if (totalAppliedMagnitude <= 0)
            {
                effectsSummary = appliedStatusCount > 0
                    ? $"applied {appliedStatusCount} status effect(s) to {target.Unit.DisplayName}"
                    : $"had no effect on {target.Unit.DisplayName}";
            }
            else if (appliedStatusCount > 0)
            {
                effectsSummary += $", applied {appliedStatusCount} status effect(s)";
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
            float outgoingMultiplier,
            bool drenchedBoostApplied,
            int targetReduction,
            int targetFlatReduction,
            int shieldAbsorbed,
            int directHealthDamageApplied,
            int jaggedApplied)
        {
            int basePower = Mathf.Max(0, potency + sourceStatBonus);
            int afterType = Mathf.RoundToInt(basePower * typeMultiplier);
            int afterFlatIncrease = afterType + Mathf.Max(0, flatIncrease);
            int afterOutgoing = Mathf.RoundToInt(afterFlatIncrease * outgoingMultiplier);
            int afterReductions = afterOutgoing - targetReduction - targetFlatReduction;
            int afterFloor = Mathf.Max(1, afterReductions);
            int postDrenched = drenchedBoostApplied ? Mathf.Max(1, Mathf.RoundToInt(afterFloor * 1.1f)) : afterFloor;
            int finalHitBeforeShield = postDrenched;
            int totalApplied = Mathf.Max(0, directHealthDamageApplied) + Mathf.Max(0, jaggedApplied);

            return
                $"Base power: potency {potency} + stat {sourceStatBonus} = {basePower}\n" +
                $"Type scaling: {basePower} x {typeMultiplier:0.##} = {afterType}\n" +
                $"Flat damage bonus: +{Mathf.Max(0, flatIncrease)} => {afterFlatIncrease}\n" +
                $"Outgoing modifier: x{outgoingMultiplier:0.##} => {afterOutgoing}\n" +
                (drenchedBoostApplied ? $"Drenched bonus: x1.10 => {postDrenched}\n" : string.Empty) +
                $"Target reductions: -{targetReduction} (base), -{targetFlatReduction} (flat) => {afterReductions}\n" +
                $"Final hit damage before shield: {finalHitBeforeShield}\n" +
                $"Shield absorbed: {shieldAbsorbed}\n" +
                $"Direct HP damage: {directHealthDamageApplied}\n" +
                $"Jagged extra damage: {jaggedApplied}\n" +
                $"Total HP damage: {totalApplied}";
        }

    }
}
