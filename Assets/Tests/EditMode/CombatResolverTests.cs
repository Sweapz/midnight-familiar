using System.Collections.Generic;
using System.Linq;
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
        public void SupportiveAbility_OnEnemyTarget_Fails()
        {
            var caster = BuildCombatant(
                TeamSide.Player,
                CuidType.Tide,
                CuidType.Ember,
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
            var enemy = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Ember,
                CuidType.Tide,
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

            var action = new CuidAction
            {
                Id = "ally_boon",
                DisplayName = "Ally Boon",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Supportive,
                ActionType = CuidType.Tide,
                TargetRule = TargetRule.AllySingle,
                Potency = 5
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(caster, enemy, action);

            Assert.IsFalse(resolution.Succeeded);
            Assert.IsTrue(resolution.WasResisted);
            Assert.AreEqual(100, enemy.CurrentHealth);
            StringAssert.Contains("only target allies", resolution.Summary.ToLowerInvariant());
        }

        [Test]
        public void FlatDamageReduction_ReducesIncomingDamage()
        {
            var attacker = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Ember,
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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
                CuidType.Ember,
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

        [Test]
        public void BasicAttack_DoesNotApplyTypeStatuses()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Ember,
                CuidType.Ember,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);

            var basicAttack = new CuidAction
            {
                Id = "basic_ember",
                DisplayName = "Ember Snap",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Ember,
                Potency = 5,
                IsBasicAttack = true
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, defender, basicAttack);

            Assert.IsFalse(defender.HasStatus(TypeStatusId.Burned));
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Aflame));
        }

        [Test]
        public void TideOffensiveHit_AppliesDrenched()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Tide,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var defender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Ember,
                CuidType.Tide,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "tide_hit",
                DisplayName = "Tide Hit",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Offensive,
                ActionType = CuidType.Tide,
                Potency = 6
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Drenched));
        }

        [Test]
        public void Drenched_IncreasesOffensiveAbilityDamage_ButNotAttackDamage()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Arcane,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);

            var abilityTarget = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Beast,
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            abilityTarget.ApplyOrRefreshStatus(TypeStatusId.Drenched, 1);

            var attackTarget = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Beast,
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            attackTarget.ApplyOrRefreshStatus(TypeStatusId.Drenched, 1);

            var offensiveAbility = new CuidAction
            {
                Id = "arcane_burst",
                DisplayName = "Arcane Burst",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Offensive,
                ActionType = CuidType.Arcane,
                Potency = 10
            };
            var attack = new CuidAction
            {
                Id = "arcane_strike",
                DisplayName = "Arcane Strike",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Arcane,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 20), new NeutralTypeEffectivenessProvider());
            ActionResolution abilityResolution = resolver.ResolveAction(attacker, abilityTarget, offensiveAbility);
            ActionResolution attackResolution = resolver.ResolveAction(attacker, attackTarget, attack);

            Assert.AreEqual(11, abilityResolution.AppliedMagnitude);
            Assert.AreEqual(10, attackResolution.AppliedMagnitude);
        }

        [Test]
        public void VoltRehit_UpgradesElectrifiedIntoParalyzed_AndBlocksElectrifiedWhileParalyzed()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Volt,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "volt_hit",
                DisplayName = "Volt Hit",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Volt,
                Potency = 3
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 20, 20), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Electrified));
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Paralyzed));

            resolver.ResolveAction(attacker, defender, action);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Electrified));
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Paralyzed));

            resolver.ResolveAction(attacker, defender, action);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Electrified));
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Paralyzed));
        }

        [Test]
        public void FloraRehit_UpgradesVinedIntoRooted_AndBlocksVinedWhileRooted()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Flora,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
                abilityResistance: 5,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var defender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Tide,
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "flora_hit",
                DisplayName = "Flora Hit",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Flora,
                Potency = 3
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 20, 20), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Vined));
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Rooted));

            resolver.ResolveAction(attacker, defender, action);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Vined));
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Rooted));
            Assert.IsTrue(defender.TryGetStatus(TypeStatusId.Rooted, out ActiveStatusEffect rooted));
            Assert.AreEqual(2, rooted.RemainingTurns);

            resolver.ResolveAction(attacker, defender, action);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Vined));
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Rooted));
        }

        [Test]
        public void ApplyOrRefreshStatus_UsesMaxDurationForNonUpgradeStatuses()
        {
            var combatant = BuildCombatant(
                TeamSide.Player,
                CuidType.Tide,
                CuidType.Ember,
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

            combatant.ApplyOrRefreshStatus(TypeStatusId.Drenched, 1);
            combatant.ApplyOrRefreshStatus(TypeStatusId.Drenched, 3);
            combatant.ApplyOrRefreshStatus(TypeStatusId.Drenched, 1);

            Assert.IsTrue(combatant.TryGetStatus(TypeStatusId.Drenched, out ActiveStatusEffect drenched));
            Assert.AreEqual(3, drenched.RemainingTurns);
        }

        [Test]
        public void ArcaneWithered_ProgressesOverFiveTurns_UsingMaxHpPercent()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Arcane,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "arcane_wither",
                DisplayName = "Arcane Wither",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Offensive,
                ActionType = CuidType.Arcane,
                Potency = 5,
                TypeStatusApplications = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Withered,
                        DurationTurns = 5,
                        ApplyChancePercent = 100
                    }
                }
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Withered));

            var processor = new TypeStatusProcessor(new FixedDiceRoller(20));
            int beforeTicks = defender.CurrentHealth;
            processor.ProcessTurnEnd(defender, null);
            processor.ProcessTurnEnd(defender, null);
            processor.ProcessTurnEnd(defender, null);
            processor.ProcessTurnEnd(defender, null);
            processor.ProcessTurnEnd(defender, null);

            // 2% + 4% + 6% + 8% + 10% of max 100 = 30 total status damage.
            Assert.AreEqual(beforeTicks - 30, defender.CurrentHealth);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Withered));
        }

        [Test]
        public void ExplicitStatusActions_UseConfiguredProcChance()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Stone,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "splinter_strike",
                DisplayName = "Splinter Strike",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Stone,
                Potency = 7,
                TypeStatusApplications = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Jagged,
                        DurationTurns = 2,
                        ApplyChancePercent = 25
                    }
                }
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 6), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Jagged));

            var secondDefender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Beast,
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            resolver = new CombatResolver(new FixedDiceRoller(20, 5), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, secondDefender, action);
            Assert.IsTrue(secondDefender.HasStatus(TypeStatusId.Jagged));
        }

        [Test]
        public void ArcaneStoneMentalDarkin_DoNotApplyTypeStatusesByDefault()
        {
            var provider = new TypeStatusRuleProvider();

            Assert.AreEqual(0, provider.GetOnHitApplications(CuidType.Arcane).Count);
            Assert.AreEqual(0, provider.GetOnHitApplications(CuidType.Stone).Count);
            Assert.AreEqual(0, provider.GetOnHitApplications(CuidType.Mental).Count);
            Assert.AreEqual(0, provider.GetOnHitApplications(CuidType.Darkin).Count);
        }

        [Test]
        public void DebuffEnemyStatusAction_AppliesStatusWithoutHealing()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Darkin,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            int healthBefore = defender.CurrentHealth;

            var action = new CuidAction
            {
                Id = "jumpscare",
                DisplayName = "Jumpscare",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Debuff,
                ActionType = CuidType.Darkin,
                TargetRule = TargetRule.EnemySingle,
                Potency = 0,
                TypeStatusApplications = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Frightened,
                        DurationTurns = 3,
                        ApplyChancePercent = 100
                    }
                }
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreEqual(healthBefore, defender.CurrentHealth);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Frightened));
        }

        [Test]
        public void DebuffAbility_CanBeResisted()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Darkin,
                CuidType.Ember,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 4,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
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
                Id = "jumpscare",
                DisplayName = "Jumpscare",
                Kind = ActionKind.Ability,
                AbilityIntent = AbilityIntent.Debuff,
                ActionType = CuidType.Darkin,
                TargetRule = TargetRule.EnemySingle,
                Potency = 0,
                TypeStatusApplications = new List<TypeStatusApplication>
                {
                    new TypeStatusApplication
                    {
                        Kind = TypeStatusApplicationKind.ApplyOrRefresh,
                        Status = TypeStatusId.Frightened,
                        DurationTurns = 3,
                        ApplyChancePercent = 100
                    }
                }
            };

            var resolver = new CombatResolver(new FixedDiceRoller(1), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsFalse(resolution.Succeeded);
            Assert.IsTrue(resolution.WasResisted);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Frightened));
        }

        [Test]
        public void BeastDebilitated_ReducesOutgoingDamageByFivePercent()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Ember,
                CuidType.Tide,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "big_hit",
                DisplayName = "Big Hit",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Ember,
                Potency = 23
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 20), new NeutralTypeEffectivenessProvider());
            ActionResolution baseline = resolver.ResolveAction(attacker, defender, action);

            attacker.ApplyOrRefreshStatus(TypeStatusId.Debilitated, 2);
            var otherDefender = BuildCombatant(
                TeamSide.Enemy,
                CuidType.Stone,
                CuidType.Ember,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            ActionResolution reduced = resolver.ResolveAction(attacker, otherDefender, action);

            Assert.Greater(baseline.AppliedMagnitude, reduced.AppliedMagnitude);
        }

        [Test]
        public void EmberBurned_UpgradesToAflame_AndAflameTicksForTwoTurns()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Ember,
                CuidType.Tide,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Tide,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            var action = new CuidAction
            {
                Id = "ember_hit",
                DisplayName = "Ember Hit",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Ember,
                Potency = 3
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 20, 20), new NeutralTypeEffectivenessProvider());
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Burned));
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsTrue(defender.HasStatus(TypeStatusId.Aflame));
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Burned));
            resolver.ResolveAction(attacker, defender, action);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Burned));

            var processor = new TypeStatusProcessor(new FixedDiceRoller(20));
            int before = defender.CurrentHealth;
            processor.ProcessTurnEnd(defender, null);
            processor.ProcessTurnEnd(defender, null);
            Assert.AreEqual(before - 16, defender.CurrentHealth);
            Assert.IsFalse(defender.HasStatus(TypeStatusId.Aflame));
        }

        [Test]
        public void StoneJagged_AppliesOnHitExtraDamage_ButNotOnStatusTicks()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Ember,
                CuidType.Tide,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Tide,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            defender.ApplyOrRefreshStatus(TypeStatusId.Jagged, 2);
            var action = new CuidAction
            {
                Id = "hit",
                DisplayName = "Hit",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Ember,
                Potency = 10
            };

            var resolver = new CombatResolver(new FixedDiceRoller(20, 20), new NeutralTypeEffectivenessProvider());
            ActionResolution onHit = resolver.ResolveAction(attacker, defender, action);
            Assert.AreEqual(13, onHit.AppliedMagnitude);

            defender.ApplyOrRefreshStatus(TypeStatusId.Withered, 5);
            int beforeTick = defender.CurrentHealth;
            var processor = new TypeStatusProcessor(new FixedDiceRoller(20));
            processor.ProcessTurnEnd(defender, null);
            Assert.AreEqual(beforeTick - 2, defender.CurrentHealth);
        }

        [Test]
        public void MentalConfused_CanFailActionBeforeHitRoll()
        {
            var attacker = BuildCombatant(
                TeamSide.Player,
                CuidType.Mental,
                CuidType.Tide,
                attack: 10,
                defense: 5,
                abilityEffectiveness: 10,
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
                CuidType.Tide,
                attack: 5,
                defense: 0,
                abilityEffectiveness: 5,
                abilityResistance: 0,
                speed: 5,
                constitution: 100,
                damage: 0,
                damageReduction: 0,
                abilityDamage: 0,
                abilityReduction: 0);
            attacker.ApplyOrRefreshStatus(TypeStatusId.Confused, 3);

            var action = new CuidAction
            {
                Id = "mental_strike",
                DisplayName = "Mental Strike",
                Kind = ActionKind.Attack,
                ActionType = CuidType.Mental,
                Potency = 10
            };

            // First roll is confusion check: <=10 means fail action immediately.
            var resolver = new CombatResolver(new FixedDiceRoller(1), new NeutralTypeEffectivenessProvider());
            ActionResolution resolution = resolver.ResolveAction(attacker, defender, action);

            Assert.IsFalse(resolution.Succeeded);
            Assert.AreEqual(100, defender.CurrentHealth);
            StringAssert.Contains("confused", resolution.Summary.ToLowerInvariant());
        }

        [Test]
        public void TypeStatusRules_UseSharedBuildUpDuration_ForVoltFloraEmber()
        {
            var provider = new TypeStatusRuleProvider();
            Assert.AreEqual(
                TypeStatusRuleProvider.BuildUpStatusDuration,
                provider.GetOnHitApplications(CuidType.Volt).First(a => a.Status == TypeStatusId.Electrified).DurationTurns);
            Assert.AreEqual(
                TypeStatusRuleProvider.BuildUpStatusDuration,
                provider.GetOnHitApplications(CuidType.Flora).First(a => a.Status == TypeStatusId.Vined).DurationTurns);
            Assert.AreEqual(
                TypeStatusRuleProvider.BuildUpStatusDuration,
                provider.GetOnHitApplications(CuidType.Ember).First(a => a.Status == TypeStatusId.Burned).DurationTurns);
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
