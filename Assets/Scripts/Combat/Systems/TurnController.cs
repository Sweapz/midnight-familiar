using System;
using System.Collections.Generic;
using System.Linq;
using MidnightFamiliar.Combat.Models;

namespace MidnightFamiliar.Combat.Systems
{
    public enum BattlePhase
    {
        NotStarted = 0,
        InProgress = 1,
        Completed = 2
    }

    public sealed class TurnChoice
    {
        public bool IsPass;
        public string ActionId = string.Empty;
        public string TargetCombatantId = string.Empty;

        public static TurnChoice Pass()
        {
            return new TurnChoice { IsPass = true };
        }
    }

    public sealed class TurnStepResult
    {
        public bool Success;
        public string Message = string.Empty;
        public ActionResolution Resolution;
        public bool BattleEnded;
        public TeamSide? WinningSide;
        public string NextCombatantId = string.Empty;
        public int RoundNumber;
    }

    public sealed class AutoBattleResult
    {
        public TeamSide? WinningSide;
        public int TurnsExecuted;
        public int FinalRoundNumber;
        public bool StoppedBySafetyLimit;
        public List<TurnStepResult> Steps = new List<TurnStepResult>();
    }

    public sealed class TurnController
    {
        private readonly IDiceRoller _diceRoller;
        private readonly CombatResolver _combatResolver;

        public BattleState BattleState { get; private set; }
        public BattlePhase Phase { get; private set; } = BattlePhase.NotStarted;
        public TeamSide? WinningSide { get; private set; }

        public TurnController(IDiceRoller diceRoller = null, ITypeEffectivenessProvider typeEffectivenessProvider = null)
        {
            _diceRoller = diceRoller ?? new UnityDiceRoller();
            _combatResolver = new CombatResolver(_diceRoller, typeEffectivenessProvider);
        }

        public BattleState StartBattle(TeamRoster playerTeam, TeamRoster enemyTeam, int gridWidth = 8, int gridHeight = 8)
        {
            if (playerTeam == null) throw new ArgumentNullException(nameof(playerTeam));
            if (enemyTeam == null) throw new ArgumentNullException(nameof(enemyTeam));

            var state = new BattleState
            {
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                RoundNumber = 1,
                CurrentTurnIndex = 0,
                Combatants = new List<CombatantState>(8),
                TurnOrder = new List<string>(8)
            };

            AddTeam(state, playerTeam, TeamSide.Player, startingX: 0);
            AddTeam(state, enemyTeam, TeamSide.Enemy, startingX: gridWidth - 1);
            BuildInitiativeOrder(state);

            BattleState = state;
            Phase = BattlePhase.InProgress;
            WinningSide = null;

            if (TryResolveBattleEnd(out TeamSide? winner))
            {
                Phase = BattlePhase.Completed;
                WinningSide = winner;
            }

            return BattleState;
        }

        public CombatantState GetCurrentCombatant()
        {
            if (Phase != BattlePhase.InProgress || BattleState == null || BattleState.TurnOrder.Count == 0)
            {
                return null;
            }

            NormalizeTurnPointer();
            if (BattleState.TurnOrder.Count == 0)
            {
                return null;
            }

            string combatantId = BattleState.TurnOrder[BattleState.CurrentTurnIndex];
            return BattleState.FindCombatant(combatantId);
        }

        public TurnStepResult ExecuteTurn(TurnChoice choice)
        {
            return ExecuteTurn(choice, advanceTurn: true);
        }

        public TurnStepResult ExecuteTurn(TurnChoice choice, bool advanceTurn)
        {
            if (Phase != BattlePhase.InProgress || BattleState == null)
            {
                return BuildStepFailure("Battle is not in progress.");
            }

            var actor = GetCurrentCombatant();
            if (actor == null)
            {
                return BuildStepFailure("No current combatant is available.");
            }

            if (choice == null || choice.IsPass)
            {
                if (advanceTurn)
                {
                    AdvanceTurn();
                }
                return BuildStepSuccess("Turn passed.", null);
            }

            CuidAction action = actor.Unit.Actions.FirstOrDefault(a => a != null && a.Id == choice.ActionId);
            if (action == null)
            {
                return BuildStepFailure($"Action '{choice.ActionId}' was not found for actor '{actor.CombatantId}'.");
            }

            var target = BattleState.FindCombatant(choice.TargetCombatantId);
            if (target == null || target.IsDefeated)
            {
                return BuildStepFailure($"Target '{choice.TargetCombatantId}' is invalid or defeated.");
            }

            var resolution = _combatResolver.ResolveAction(actor, target, action);

            if (TryResolveBattleEnd(out TeamSide? winner))
            {
                Phase = BattlePhase.Completed;
                WinningSide = winner;
                return BuildStepSuccess(resolution.Summary, resolution);
            }

            if (advanceTurn)
            {
                AdvanceTurn();
            }
            return BuildStepSuccess(resolution.Summary, resolution);
        }

        public AutoBattleResult RunToCompletion(Func<CombatantState, BattleState, TurnChoice> chooser = null, int safetyTurnLimit = 500)
        {
            var result = new AutoBattleResult();
            if (Phase != BattlePhase.InProgress || BattleState == null)
            {
                result.StoppedBySafetyLimit = false;
                return result;
            }

            int turns = 0;
            while (Phase == BattlePhase.InProgress && turns < safetyTurnLimit)
            {
                var actor = GetCurrentCombatant();
                if (actor == null)
                {
                    break;
                }

                TurnChoice choice = chooser != null
                    ? chooser(actor, BattleState)
                    : BuildDefaultChoice(actor);

                TurnStepResult step = ExecuteTurn(choice);
                result.Steps.Add(step);

                // If a bad external chooser returns invalid turns repeatedly, pass to avoid deadlock.
                if (!step.Success && Phase == BattlePhase.InProgress)
                {
                    TurnStepResult recoveryStep = ExecuteTurn(TurnChoice.Pass());
                    result.Steps.Add(recoveryStep);
                }

                turns++;
            }

            result.WinningSide = WinningSide;
            result.TurnsExecuted = turns;
            result.FinalRoundNumber = BattleState != null ? BattleState.RoundNumber : 0;
            result.StoppedBySafetyLimit = Phase == BattlePhase.InProgress && turns >= safetyTurnLimit;
            return result;
        }

        private void AddTeam(BattleState state, TeamRoster roster, TeamSide expectedSide, int startingX)
        {
            if (roster.Units == null)
            {
                return;
            }

            int y = 0;
            foreach (CuidUnit unit in roster.Units)
            {
                if (unit == null)
                {
                    continue;
                }

                var cloned = unit.Clone();
                cloned.InitializeHealth();

                var combatant = new CombatantState
                {
                    Team = expectedSide,
                    Unit = cloned,
                    Position = new GridPosition(startingX, y),
                    IsDefeated = false
                };

                state.Combatants.Add(combatant);
                y++;
            }
        }

        private void BuildInitiativeOrder(BattleState state)
        {
            var rolls = new Dictionary<string, int>();
            foreach (CombatantState combatant in state.Combatants)
            {
                if (combatant?.Unit?.Stats == null)
                {
                    continue;
                }

                int initiative = _diceRoller.RollD20() + combatant.Unit.Stats.Speed;
                rolls[combatant.CombatantId] = initiative;
            }

            state.TurnOrder = state.Combatants
                .Where(c => c != null && !c.IsDefeated)
                .OrderByDescending(c => rolls.ContainsKey(c.CombatantId) ? rolls[c.CombatantId] : int.MinValue)
                .ThenByDescending(c => c.Unit != null && c.Unit.Stats != null ? c.Unit.Stats.Speed : 0)
                .ThenBy(c => c.CombatantId, StringComparer.Ordinal)
                .Select(c => c.CombatantId)
                .ToList();

            state.CurrentTurnIndex = 0;
        }

        private void NormalizeTurnPointer()
        {
            if (BattleState == null || BattleState.TurnOrder.Count == 0)
            {
                return;
            }

            if (BattleState.CurrentTurnIndex >= BattleState.TurnOrder.Count)
            {
                BattleState.CurrentTurnIndex = 0;
            }

            int checkedCount = 0;
            while (checkedCount < BattleState.TurnOrder.Count)
            {
                string combatantId = BattleState.TurnOrder[BattleState.CurrentTurnIndex];
                CombatantState combatant = BattleState.FindCombatant(combatantId);
                if (combatant != null && !combatant.IsDefeated)
                {
                    return;
                }

                BattleState.TurnOrder.RemoveAt(BattleState.CurrentTurnIndex);
                if (BattleState.TurnOrder.Count == 0)
                {
                    return;
                }

                if (BattleState.CurrentTurnIndex >= BattleState.TurnOrder.Count)
                {
                    BattleState.CurrentTurnIndex = 0;
                }

                checkedCount++;
            }
        }

        private void AdvanceTurn()
        {
            if (BattleState == null || BattleState.TurnOrder.Count == 0)
            {
                return;
            }

            BattleState.CurrentTurnIndex++;
            if (BattleState.CurrentTurnIndex >= BattleState.TurnOrder.Count)
            {
                BattleState.CurrentTurnIndex = 0;
                BattleState.RoundNumber++;
            }

            NormalizeTurnPointer();
        }

        private bool TryResolveBattleEnd(out TeamSide? winner)
        {
            winner = null;
            if (BattleState == null)
            {
                return false;
            }

            bool playerAlive = BattleState.Combatants.Any(c => c.Team == TeamSide.Player && !c.IsDefeated);
            bool enemyAlive = BattleState.Combatants.Any(c => c.Team == TeamSide.Enemy && !c.IsDefeated);

            if (!playerAlive && !enemyAlive)
            {
                return true;
            }

            if (!playerAlive)
            {
                winner = TeamSide.Enemy;
                return true;
            }

            if (!enemyAlive)
            {
                winner = TeamSide.Player;
                return true;
            }

            return false;
        }

        private TurnChoice BuildDefaultChoice(CombatantState actor)
        {
            CuidAction action = actor.Unit.Actions.FirstOrDefault(a => a != null);
            if (action == null)
            {
                return TurnChoice.Pass();
            }

            TeamSide opposingTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            CombatantState target = BattleState.Combatants.FirstOrDefault(c => c.Team == opposingTeam && !c.IsDefeated);
            if (target == null)
            {
                return TurnChoice.Pass();
            }

            return new TurnChoice
            {
                IsPass = false,
                ActionId = action.Id,
                TargetCombatantId = target.CombatantId
            };
        }

        private TurnStepResult BuildStepSuccess(string message, ActionResolution resolution)
        {
            return new TurnStepResult
            {
                Success = true,
                Message = message ?? string.Empty,
                Resolution = resolution,
                BattleEnded = Phase == BattlePhase.Completed,
                WinningSide = WinningSide,
                NextCombatantId = GetCurrentCombatant()?.CombatantId ?? string.Empty,
                RoundNumber = BattleState != null ? BattleState.RoundNumber : 0
            };
        }

        private TurnStepResult BuildStepFailure(string message)
        {
            return new TurnStepResult
            {
                Success = false,
                Message = message,
                Resolution = null,
                BattleEnded = Phase == BattlePhase.Completed,
                WinningSide = WinningSide,
                NextCombatantId = GetCurrentCombatant()?.CombatantId ?? string.Empty,
                RoundNumber = BattleState != null ? BattleState.RoundNumber : 0
            };
        }
    }
}
