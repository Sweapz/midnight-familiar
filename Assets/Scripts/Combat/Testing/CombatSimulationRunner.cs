using System.Collections.Generic;
using System.Linq;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Testing
{
    public class CombatSimulationRunner : MonoBehaviour
    {
        [SerializeField] private int maxSimulationTurns = 200;
        [SerializeField] private List<CuidDefinition> playerTeamDefinitions = new List<CuidDefinition>(4);
        [SerializeField] private List<CuidDefinition> enemyTeamDefinitions = new List<CuidDefinition>(4);

        [ContextMenu("Run Combat Simulation")]
        private void RunCombatSimulation()
        {
            var controller = new TurnController();
            EnsureDefaultDefinitionsIfNeeded();

            TeamRoster playerTeam = BuildTeamFromDefinitions(TeamSide.Player, playerTeamDefinitions);
            TeamRoster enemyTeam = BuildTeamFromDefinitions(TeamSide.Enemy, enemyTeamDefinitions);

            controller.StartBattle(playerTeam, enemyTeam);
            AutoBattleResult result = controller.RunToCompletion(safetyTurnLimit: maxSimulationTurns);

            Debug.Log("=== Midnight Familiar Combat Simulation ===");
            foreach (TurnStepResult step in result.Steps)
            {
                if (step.Resolution != null)
                {
                    Debug.Log(
                        $"[Round {step.RoundNumber}] {step.Resolution.Summary} " +
                        $"(roll {step.Resolution.AttackRoll} vs {step.Resolution.DefenseRoll}, " +
                        $"x{step.Resolution.TypeMultiplier:0.##})");
                }
                else
                {
                    Debug.Log($"[Round {step.RoundNumber}] {step.Message}");
                }
            }

            if (result.StoppedBySafetyLimit)
            {
                Debug.LogWarning($"Simulation stopped at safety turn limit ({maxSimulationTurns}).");
            }

            string winnerText = result.WinningSide.HasValue ? result.WinningSide.Value.ToString() : "None";
            Debug.Log(
                $"Simulation complete. Winner: {winnerText}. " +
                $"Turns: {result.TurnsExecuted}. Final Round: {result.FinalRoundNumber}.");
        }

        private TeamRoster BuildTeamFromDefinitions(TeamSide side, List<CuidDefinition> definitions)
        {
            var units = new List<CuidUnit>(4);
            if (definitions != null)
            {
                foreach (CuidDefinition definition in definitions.Where(definition => definition != null))
                {
                    units.Add(CuidFactory.CreateUnit(definition));
                }
            }

            return new TeamRoster
            {
                Side = side,
                Units = units
            };
        }

        private void EnsureDefaultDefinitionsIfNeeded()
        {
            if (playerTeamDefinitions.Count > 0 && enemyTeamDefinitions.Count > 0)
            {
                return;
            }

            if (TryLoadStarterDefinitionsFromResources(out List<CuidDefinition> persistentStarters))
            {
                if (playerTeamDefinitions.Count == 0)
                {
                    playerTeamDefinitions = new List<CuidDefinition> { persistentStarters[0], persistentStarters[1] };
                }

                if (enemyTeamDefinitions.Count == 0)
                {
                    enemyTeamDefinitions = new List<CuidDefinition> { persistentStarters[1], persistentStarters[0] };
                }

                Debug.Log("Loaded persistent starter Cuid definitions from Resources.");
                return;
            }

            var starterDefs = BuildRuntimeStarterDefinitions();
            if (playerTeamDefinitions.Count == 0)
            {
                playerTeamDefinitions = new List<CuidDefinition> { starterDefs[0], starterDefs[1] };
            }

            if (enemyTeamDefinitions.Count == 0)
            {
                enemyTeamDefinitions = new List<CuidDefinition> { starterDefs[1], starterDefs[0] };
            }

            Debug.LogWarning(
                "Using runtime starter Cuid definitions (Ember Fox, Tide Toad). " +
                "Create CuidDefinition assets and assign them in the inspector for persistent content.");
        }

        private static bool TryLoadStarterDefinitionsFromResources(out List<CuidDefinition> definitions)
        {
            definitions = new List<CuidDefinition>(2);

            CuidDefinition emberFox = Resources.Load<CuidDefinition>("Combat/Cuids/EmberFox");
            CuidDefinition tideToad = Resources.Load<CuidDefinition>("Combat/Cuids/TideToad");
            if (emberFox == null || tideToad == null)
            {
                return false;
            }

            definitions.Add(emberFox);
            definitions.Add(tideToad);
            return true;
        }

        private static List<CuidDefinition> BuildRuntimeStarterDefinitions()
        {
            ActionDefinition blazeClaw = CreateRuntimeAction(
                "blaze_claw",
                "Blaze Claw",
                ActionKind.Attack,
                AbilityIntent.None,
                CuidType.Ember,
                TargetRule.EnemySingle,
                range: 1,
                hitBonus: 1,
                potency: 8);

            ActionDefinition cinderBurst = CreateRuntimeAction(
                "cinder_burst",
                "Cinder Burst",
                ActionKind.Ability,
                AbilityIntent.Offensive,
                CuidType.Ember,
                TargetRule.EnemySingle,
                range: 3,
                hitBonus: 0,
                potency: 7);

            ActionDefinition tidalSlam = CreateRuntimeAction(
                "tidal_slam",
                "Tidal Slam",
                ActionKind.Attack,
                AbilityIntent.None,
                CuidType.Tide,
                TargetRule.EnemySingle,
                range: 1,
                hitBonus: 0,
                potency: 6);

            ActionDefinition restoringMist = CreateRuntimeAction(
                "restoring_mist",
                "Restoring Mist",
                ActionKind.Ability,
                AbilityIntent.Supportive,
                CuidType.Tide,
                TargetRule.AllySingle,
                range: 3,
                hitBonus: 0,
                potency: 6);

            CuidDefinition emberFox = CreateRuntimeCuid(
                cuidId: "ember_fox",
                displayName: "Ember Fox",
                primaryType: CuidType.Ember,
                secondaryType: CuidType.Ember,
                traitId: "flare_instinct",
                traitName: "Flare Instinct",
                traitDescription: "First offensive action each round gains extra potency.",
                attack: 13,
                defense: 9,
                abilityEffectiveness: 12,
                abilityResistance: 9,
                speed: 11,
                constitution: 40,
                damage: 3,
                damageReduction: 1,
                abilityDamage: 2,
                abilityReduction: 0,
                actions: new List<ActionDefinition> { blazeClaw, cinderBurst });

            CuidDefinition tideToad = CreateRuntimeCuid(
                cuidId: "tide_toad",
                displayName: "Tide Toad",
                primaryType: CuidType.Tide,
                secondaryType: CuidType.Tide,
                traitId: "calming_pulse",
                traitName: "Calming Pulse",
                traitDescription: "Supportive abilities gain improved effect.",
                attack: 9,
                defense: 11,
                abilityEffectiveness: 13,
                abilityResistance: 11,
                speed: 8,
                constitution: 44,
                damage: 1,
                damageReduction: 2,
                abilityDamage: 3,
                abilityReduction: 1,
                actions: new List<ActionDefinition> { tidalSlam, restoringMist });

            return new List<CuidDefinition> { emberFox, tideToad };
        }

        private static ActionDefinition CreateRuntimeAction(
            string actionId,
            string displayName,
            ActionKind kind,
            AbilityIntent abilityIntent,
            CuidType actionType,
            TargetRule targetRule,
            int range,
            int hitBonus,
            int potency)
        {
            var action = ScriptableObject.CreateInstance<ActionDefinition>();
            action.hideFlags = HideFlags.HideAndDontSave;
            action.ConfigureForRuntime(
                actionId,
                displayName,
                string.Empty,
                kind,
                abilityIntent,
                actionType,
                targetRule,
                range,
                hitBonus,
                potency,
                newCooldownTurns: 0);
            return action;
        }

        private static CuidDefinition CreateRuntimeCuid(
            string cuidId,
            string displayName,
            CuidType primaryType,
            CuidType secondaryType,
            string traitId,
            string traitName,
            string traitDescription,
            int attack,
            int defense,
            int abilityEffectiveness,
            int abilityResistance,
            int speed,
            int constitution,
            int damage,
            int damageReduction,
            int abilityDamage,
            int abilityReduction,
            List<ActionDefinition> actions)
        {
            var definition = ScriptableObject.CreateInstance<CuidDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.ConfigureForRuntime(
                newCuidId: cuidId,
                newDisplayName: displayName,
                newLevel: 1,
                newPrimaryType: primaryType,
                newSecondaryType: secondaryType,
                newTraitId: traitId,
                newTraitName: traitName,
                newTraitDescription: traitDescription,
                newAttack: attack,
                newDefense: defense,
                newAbilityEffectiveness: abilityEffectiveness,
                newAbilityResistance: abilityResistance,
                newSpeed: speed,
                newConstitution: constitution,
                newDamage: damage,
                newDamageReduction: damageReduction,
                newAbilityDamage: abilityDamage,
                newAbilityReduction: abilityReduction,
                newActions: actions);
            return definition;
        }
    }
}
