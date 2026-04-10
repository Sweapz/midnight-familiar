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

        [Test]
        public void SupportiveAbility_HealEffect_RestoresHealthUpToMax()
        {
            var caster = BuildCombatant(
                TeamSide.Player,
                CuidType.Tide,
                CuidType.None,
                attack: 5,
                defense: 5,
                abilityEffectiveness: 4,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var ally = BuildCombatant(
                TeamSide.Player,
                CuidType.Tide,
                CuidType.None,
                attack: 5,
                defense: 5,
                abilityEffectiveness: 5,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            ally.CurrentHealth = 80;

            var action = new CuidAction
            {
                Id = "heal",
                DisplayName = "Heal",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Supportive,
                ActionType = CuidType.Tide,
                TargetRule = TargetRule.AllySingle,
                SupportEffects = new List<SupportEffect>
                {
                    new SupportEffect
                    {
                        Kind = SupportEffectKind.Heal,
                        Magnitude = 5
                    }
                }
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(caster, ally, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreEqual(9, resolution.AppliedMagnitude);
            Assert.AreEqual(89, ally.CurrentHealth);
        }

        [Test]
        public void SupportiveAbility_StatModifier_AppliesAndExpiresByDuration()
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
                abilityDamage: 0,
                abilityReduction: 0);
            var ally = BuildCombatant(
                TeamSide.Player,
                CuidType.Arcane,
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

            var action = new CuidAction
            {
                Id = "buff",
                DisplayName = "Buff",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Supportive,
                TargetRule = TargetRule.AllySingle,
                SupportEffects = new List<SupportEffect>
                {
                    new SupportEffect
                    {
                        Kind = SupportEffectKind.StatModifier,
                        TargetStat = CuidStatType.Attack,
                        Magnitude = 4,
                        DurationTurns = 2
                    }
                }
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(caster, ally, action);

            Assert.AreEqual(14, ally.GetEffectiveStats().Attack);
            ally.TickActiveEffectsAtTurnEnd();
            Assert.AreEqual(14, ally.GetEffectiveStats().Attack);
            ally.TickActiveEffectsAtTurnEnd();
            Assert.AreEqual(10, ally.GetEffectiveStats().Attack);
        }

        [Test]
        public void FlatDamageReduction_ReducesIncomingDamage()
        {
            var attacker = BuildCombatant(
                TeamSide.Enemy,
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
                TeamSide.Player,
                CuidType.Flora,
                CuidType.None,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 1,
                abilityDamage: 0,
                abilityReduction: 0);
            defender.AddActiveEffect(new ActiveStatusEffect
            {
                Kind = SupportEffectKind.FlatDamageReduction,
                Magnitude = 3,
                RemainingTurns = 2
            });

            var attack = new CuidAction
            {
                Id = "attack",
                DisplayName = "Attack",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Ember,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, attack);

            Assert.AreEqual(6, resolution.AppliedMagnitude);
            Assert.AreEqual(94, defender.CurrentHealth);
        }

        [Test]
        public void FlatDamageIncrease_BoostsOutgoingDamage()
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
                CuidType.None,
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
            attacker.AddActiveEffect(new ActiveStatusEffect
            {
                Kind = SupportEffectKind.FlatDamageIncrease,
                Magnitude = 4,
                RemainingTurns = 2
            });

            var attack = new CuidAction
            {
                Id = "attack",
                DisplayName = "Attack",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Ember,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, attack);

            Assert.AreEqual(14, resolution.AppliedMagnitude);
            Assert.AreEqual(86, defender.CurrentHealth);
        }

        [Test]
        public void Shield_AbsorbsDamageBeforeHealth()
        {
            var attacker = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Stone,
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
                TeamSide.Player,
                CuidType.Tide,
                CuidType.None,
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
            defender.AddActiveEffect(new ActiveStatusEffect
            {
                Kind = SupportEffectKind.Shield,
                Magnitude = 6,
                RemainingTurns = 2
            });

            var attack = new CuidAction
            {
                Id = "attack",
                DisplayName = "Attack",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Stone,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, attack);

            Assert.AreEqual(4, resolution.AppliedMagnitude);
            Assert.AreEqual(96, defender.CurrentHealth);
        }

        [Test]
        public void Thorns_ReflectsDamageToAttacker_WhenDefenderTakesHealthDamage()
        {
            var attacker = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Beast,
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
                TeamSide.Player,
                CuidType.Stone,
                CuidType.None,
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
            defender.AddActiveEffect(new ActiveStatusEffect
            {
                Kind = SupportEffectKind.Thorns,
                Magnitude = 3,
                RemainingTurns = 2
            });

            var attack = new CuidAction
            {
                Id = "attack",
                DisplayName = "Attack",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Beast,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, attack);

            Assert.AreEqual(10, resolution.AppliedMagnitude);
            Assert.AreEqual(90, defender.CurrentHealth);
            Assert.AreEqual(97, attacker.CurrentHealth);
        }

        [Test]
        public void BasicAttack_UsesHigherAbilityEffectiveness_AndTargetsAbilityResistance()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Arcane,
                CuidType.None,
                attack: 1,
                defense: 5,
                abilityEffectiveness: 12,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var defender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Beast,
                CuidType.None,
                attack: 5,
                defense: 20,
                abilityEffectiveness: 5,
                abilityResistance: 2,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "basic_arcane",
                DisplayName = "Arcane Strike",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Arcane,
                Potency = 5,
                IsBasicAttack = true
            };

            var resolver = new CombatResolver(new FixedDiceRoller(10), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreEqual(22, resolution.AttackRoll);
            Assert.AreEqual(12, resolution.DefenseRoll);
        }

        [Test]
        public void BasicAttack_UsesHigherAttack_AndTargetsDefense()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Beast,
                CuidType.None,
                attack: 12,
                defense: 5,
                abilityEffectiveness: 3,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var defender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Stone,
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
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "basic_beast",
                DisplayName = "Beast Strike",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Beast,
                Potency = 5,
                IsBasicAttack = true
            };

            var resolver = new CombatResolver(new FixedDiceRoller(10), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreEqual(22, resolution.AttackRoll);
            Assert.AreEqual(15, resolution.DefenseRoll);
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
