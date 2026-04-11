using System.Collections.Generic;
using System.Reflection;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation;
using MidnightFamiliar.Combat.Systems;
using NUnit.Framework;
using UnityEngine;

namespace MidnightFamiliar.Tests
{
    public class TypeStatusFlowTests
    {
        [Test]
        public void Paralyzed_Combatant_AutoPassesTurn()
        {
            var controller = new TurnController(
                new FixedDiceRoller(20, 1),
                new NeutralTypeEffectivenessProvider());

            var player = BuildUnit("player", CuidType.Volt, speed: 12);
            var enemy = BuildUnit("enemy", CuidType.Stone, speed: 4);
            controller.StartBattle(
                new TeamRoster { Side = TeamSide.Player, Units = new List<CuidUnit> { player } },
                new TeamRoster { Side = TeamSide.Enemy, Units = new List<CuidUnit> { enemy } },
                gridWidth: 6,
                gridHeight: 6);

            CombatantState current = controller.GetCurrentCombatant();
            Assert.NotNull(current);
            Assert.AreEqual(TeamSide.Player, current.Team);
            current.ApplyOrRefreshStatus(TypeStatusId.Paralyzed, 1);

            string enemyId = controller.BattleState.Combatants.Find(c => c.Team == TeamSide.Enemy).CombatantId;
            TurnStepResult step = controller.ExecuteTurn(new TurnChoice
            {
                IsPass = false,
                ActionId = "basic_attack",
                TargetCombatantId = enemyId
            });

            Assert.IsTrue(step.Success);
            StringAssert.Contains("paralyzed", step.Message.ToLowerInvariant());
            Assert.IsNull(step.Resolution);
            Assert.AreEqual(enemyId, controller.GetCurrentCombatant().CombatantId);
            Assert.IsFalse(current.HasStatus(TypeStatusId.Paralyzed));
        }

        [Test]
        public void Rooted_Combatant_HasZeroMoveRange()
        {
            var go = new GameObject("BattleControllerTest");
            try
            {
                BattleController controller = go.AddComponent<BattleController>();
                var actor = new CombatantState
                {
                    Unit = new CuidUnit
                    {
                        Stats = new CuidStats
                        {
                            Speed = 20
                        }
                    }
                };
                actor.ApplyOrRefreshStatus(TypeStatusId.Rooted, 2);

                MethodInfo method = typeof(BattleController).GetMethod("GetMoveRange", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);

                int moveRange = (int)method.Invoke(controller, new object[] { actor });
                Assert.AreEqual(0, moveRange);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Frightened_Combatant_FleesAtTurnStart()
        {
            var controller = new TurnController(new FixedDiceRoller(20), new NeutralTypeEffectivenessProvider());
            var player = BuildUnit("player", CuidType.Darkin, speed: 12);
            var enemy = BuildUnit("enemy", CuidType.Ember, speed: 4);
            controller.StartBattle(
                new TeamRoster { Side = TeamSide.Player, Units = new List<CuidUnit> { player } },
                new TeamRoster { Side = TeamSide.Enemy, Units = new List<CuidUnit> { enemy } },
                gridWidth: 8,
                gridHeight: 8);

            CombatantState actor = controller.GetCurrentCombatant();
            CombatantState opponent = controller.BattleState.Combatants.Find(c => c.Team != actor.Team);
            actor.Position = new GridPosition(3, 3);
            opponent.Position = new GridPosition(4, 3);
            actor.ApplyOrRefreshStatus(TypeStatusId.Frightened, 3);

            int beforeDistance = actor.Position.ManhattanDistanceTo(opponent.Position);
            TurnStartStatusResult start = controller.ProcessTurnStartEffects(actor);
            int afterDistance = actor.Position.ManhattanDistanceTo(opponent.Position);

            Assert.IsFalse(start.ForcedSkipTurn);
            Assert.Greater(afterDistance, beforeDistance);
            StringAssert.Contains("frightened", start.Message.ToLowerInvariant());
        }

        private static CuidUnit BuildUnit(string id, CuidType type, int speed)
        {
            var unit = new CuidUnit
            {
                UnitId = id,
                SpeciesId = id,
                DisplayName = id,
                PrimaryType = type,
                SecondaryType = CuidType.Ember,
                Stats = new CuidStats
                {
                    Attack = 10,
                    Defense = 5,
                    AbilityEffectiveness = 5,
                    AbilityResistance = 5,
                    Speed = speed,
                    Constitution = 40
                },
                Actions = new List<CuidAction>
                {
                    new CuidAction
                    {
                        Id = "basic_attack",
                        DisplayName = "Basic Attack",
                        Kind = ActionKind.Attack,
                        ActionType = type,
                        Potency = 5
                    }
                }
            };
            unit.InitializeHealth();
            return unit;
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
