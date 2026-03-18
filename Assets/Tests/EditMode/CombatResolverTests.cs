using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Systems;
using NUnit.Framework;

namespace MidnightFamiliar.Tests
{
    public class CombatResolverTests
    {
        [Test]
        public void Attack_UsesDamageReduction_WhenHit()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Ember,
                CuidType.None,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 5,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 2,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var defender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Flora,
                CuidType.None,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 3,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "attack",
                DisplayName = "Attack",
                Kind = ActionKind.Attack,
                AbilityIntent = AbilityIntent.None,
                ActionType = CuidType.Ember,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreEqual(9, resolution.AppliedMagnitude);
            Assert.AreEqual(91, defender.CurrentHealth);
        }

        [Test]
        public void OffensiveAbility_CanBeResisted()
        {
            var caster = BuildCombatant(
                TeamSide.Player,
                CuidType.Arcane,
                CuidType.None,
                attack: 5,
                defense: 5,
                abilityEffectiveness: 10,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 4,
                abilityReduction: 0);
            var target = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Beast,
                CuidType.None,
                attack: 5,
                defense: 5,
                abilityEffectiveness: 5,
                abilityResistance: 20,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 3);
            var action = new CuidAction
            {
                Id = "spell",
                DisplayName = "Spell",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Offensive,
                ActionType = CuidType.Arcane,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(1), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(caster, target, action);

            Assert.IsFalse(resolution.Succeeded);
            Assert.IsTrue(resolution.WasResisted);
            Assert.AreEqual(0, resolution.AppliedMagnitude);
            Assert.AreEqual(100, target.CurrentHealth);
        }

        [Test]
        public void Attack_AppliesDualTypeMultiplier()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Ember,
                CuidType.None,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 5,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var defender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Flora,
                CuidType.Stone,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "attack",
                DisplayName = "Attack",
                Kind = ActionKind.Attack,
                AbilityIntent = AbilityIntent.None,
                ActionType = CuidType.Ember,
                Potency = 10
            };

            var chart = new Dictionary<CuidType, Dictionary<CuidType, float>>();
            var provider = new TypeChartEffectivenessProvider(chart);
            provider.SetMultiplier(CuidType.Ember, CuidType.Flora, 2f);
            provider.SetMultiplier(CuidType.Ember, CuidType.Stone, 2f);

            var resolver = new CombatResolver(new FixedDiceRoller(20), provider);
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreEqual(4f, resolution.TypeMultiplier);
            Assert.AreEqual(40, resolution.AppliedMagnitude);
            Assert.AreEqual(60, defender.CurrentHealth);
        }

        private static CombatantState BuildCombatant(
            TeamSide side,
            CuidType primaryType,
            CuidType secondaryType,
            int attack,
            int defense,
            int abilityEffectiveness,
            int abilityResistance,
            int speed,
            int constitution,
            int damage,
            int damageReduction,
            int abilityDamage,
            int abilityReduction)
        {
            var unit = new CuidUnit
            {
                DisplayName = "Test Cuid",
                PrimaryType = primaryType,
                SecondaryType = secondaryType,
                Stats = new CuidStats
                {
                    Attack = attack,
                    Defense = defense,
                    AbilityEffectiveness = abilityEffectiveness,
                    AbilityResistance = abilityResistance,
                    Speed = speed,
                    Constitution = constitution,
                    Damage = damage,
                    DamageReduction = damageReduction,
                    AbilityDamage = abilityDamage,
                    AbilityReduction = abilityReduction
                }
            };
            unit.InitializeHealth();

            return new CombatantState
            {
                Team = side,
                Unit = unit,
                IsDefeated = false
            };
        }

        private sealed class FixedDiceRoller : IDiceRoller
        {
            private readonly Queue<int> _rolls;

            public FixedDiceRoller(params int[] rolls)
            {
                _rolls = new Queue<int>(rolls);
            }

            public int RollD20()
            {
                return _rolls.Count > 0 ? _rolls.Dequeue() : 10;
            }
        }
    }
}
