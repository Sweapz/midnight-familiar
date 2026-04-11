using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation.UI;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private IEnumerator ResolveOpportunityAttacksBeforeMove(CombatantState mover, GridPosition from, GridPosition to)
        {
            if (mover == null || mover.IsDefeated || _turnController?.BattleState == null)
            {
                yield break;
            }

            List<CombatantState> threateners = GetThreatenersOnMoveAway(mover, from, to);
            for (int i = 0; i < threateners.Count; i++)
            {
                CombatantState attacker = threateners[i];
                if (attacker == null || attacker.IsDefeated || mover.IsDefeated)
                {
                    continue;
                }

                List<CuidAction> availableActions = GetAvailableOpportunityActions(attacker);
                if (availableActions.Count == 0)
                {
                    continue;
                }

                CuidAction chosenAction = null;
                if (attacker.Team == TeamSide.Player && mover.Team == TeamSide.Enemy)
                {
                    yield return PromptPlayerOpportunityChoice(attacker, mover, availableActions);
                    if (_declinedOpportunity)
                    {
                        continue;
                    }

                    if (_selectedOpportunityIndex >= 0 && _selectedOpportunityIndex < _pendingOpportunityActions.Count)
                    {
                        chosenAction = _pendingOpportunityActions[_selectedOpportunityIndex];
                    }
                }
                else
                {
                    // Enemy reactions are automatic for now.
                    chosenAction = availableActions[0];
                }

                if (chosenAction == null)
                {
                    continue;
                }

                ResolveOpportunityAction(attacker, mover, chosenAction);
                if (mover.IsDefeated && mover.Team == TeamSide.Player && mover == _activePlayerActor)
                {
                    EndCurrentPlayerTurnAfterDefeat();
                    yield break;
                }
            }
        }

        private List<CombatantState> GetThreatenersOnMoveAway(CombatantState mover, GridPosition from, GridPosition to)
        {
            if (_turnController?.BattleState == null)
            {
                return new List<CombatantState>(0);
            }

            return _spatialQueryService.GetThreatenersOnMoveAway(_turnController.BattleState, mover, from, to);
        }

        private List<CuidAction> GetAvailableOpportunityActions(CombatantState attacker)
        {
            if (attacker?.Unit?.Actions == null)
            {
                return new List<CuidAction>(0);
            }

            if (_spentOpportunityCombatants.Contains(attacker.CombatantId))
            {
                return new List<CuidAction>(0);
            }

            return attacker.Unit.Actions
                .Where(action => action != null && action.IsBasicAttack && action.Kind == ActionKind.Attack)
                .ToList();
        }

        private IEnumerator PromptPlayerOpportunityChoice(
            CombatantState attacker,
            CombatantState movingTarget,
            IReadOnlyList<CuidAction> availableActions)
        {
            _pendingOpportunityActions.Clear();
            for (int i = 0; i < availableActions.Count; i++)
            {
                CuidAction action = availableActions[i];
                if (action != null)
                {
                    _pendingOpportunityActions.Add(action);
                }
            }

            if (_pendingOpportunityActions.Count == 0)
            {
                _selectedOpportunityIndex = -1;
                _declinedOpportunity = true;
                yield break;
            }

            EnsureOpportunityPanelReference();
            if (opportunityPanelController == null)
            {
                _selectedOpportunityIndex = -1;
                _declinedOpportunity = true;
                yield break;
            }

            string prompt = $"{attacker.Unit.DisplayName}: take opportunity action on {movingTarget.Unit.DisplayName}?";
            List<string> labels = _pendingOpportunityActions.Select(action => action.DisplayName).ToList();
            _selectedOpportunityIndex = -1;
            _declinedOpportunity = false;
            _awaitingOpportunityChoice = true;
            opportunityPanelController.ShowPrompt(prompt, labels);

            while (_awaitingOpportunityChoice)
            {
                yield return null;
            }

            opportunityPanelController.SetVisible(false);
        }

        private void ResolveOpportunityAction(CombatantState attacker, CombatantState target, CuidAction action)
        {
            if (attacker == null || target == null || action == null || attacker.IsDefeated || target.IsDefeated)
            {
                return;
            }

            _spentOpportunityCombatants.Add(attacker.CombatantId);

            ActionResolution resolution = _opportunityResolver.ResolveAction(attacker, target, action);
            resolution.Summary = $"Opportunity: {resolution.Summary}";
            _combatLog.Insert(0, BuildResolutionLogEntry(_turnController.BattleState.RoundNumber, resolution));
            TrimCombatLog();
            RefreshCombatLogPanel();
            RefreshAllViews();
        }

        private void EndCurrentPlayerTurnAfterDefeat()
        {
            _remainingMovement = 0;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.None;
            ClearMoveMarkers();
            ClearActionMarkers();

            TurnStepResult endStep = _turnController.ExecuteTurn(TurnChoice.Pass(), advanceTurn: true);
            AddLogFromStep(endStep);
            _activePlayerActor = null;
            _playerTurnResolved = true;
            RefreshHud();
            RefreshActionPanel();
        }

        private IEnumerator TryEnemyDisengageForTesting(CombatantState actor, int movementRemaining)
        {
            if (actor == null || actor.IsDefeated || actor.Team != TeamSide.Enemy)
            {
                yield break;
            }

            if (movementRemaining <= 0)
            {
                yield break;
            }

            if (!HasAnyRangedAction(actor) || !IsAdjacentToAnyOpponent(actor))
            {
                yield break;
            }

            if (Random.value > 1f)
            {
                yield break;
            }

            GridPosition from = actor.Position;
            GridPosition to = FindDisengageDestination(actor);
            if (from.X == to.X && from.Y == to.Y)
            {
                yield break;
            }

            int disengageCost = from.ManhattanDistanceTo(to);
            if (disengageCost <= 0 || disengageCost > movementRemaining)
            {
                yield break;
            }

            yield return ResolveOpportunityAttacksBeforeMove(actor, from, to);
            if (actor.IsDefeated)
            {
                yield break;
            }

            actor.Position = to;
            if (!_views.TryGetValue(actor.CombatantId, out CuidView view) || view == null)
            {
                yield break;
            }

            Vector3 worldStart = gridController.GridToWorld(from);
            Vector3 worldEnd = gridController.GridToWorld(to);
            float elapsed = 0f;
            while (elapsed < moveDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = moveDurationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / moveDurationSeconds);
                view.transform.position = Vector3.Lerp(worldStart, worldEnd, t);
                yield return null;
            }

            view.transform.position = worldEnd;
        }

        private bool HasAnyRangedAction(CombatantState actor)
        {
            if (actor?.Unit?.Actions == null)
            {
                return false;
            }

            return actor.Unit.Actions.Any(action => action != null && action.Range > 1);
        }

        private bool IsAdjacentToAnyOpponent(CombatantState actor)
        {
            if (_turnController?.BattleState == null || actor == null)
            {
                return false;
            }

            return _spatialQueryService.IsAdjacentToAnyOpponent(_turnController.BattleState, actor);
        }

        private GridPosition FindDisengageDestination(CombatantState actor)
        {
            if (actor == null || _turnController?.BattleState == null)
            {
                return actor != null ? actor.Position : default;
            }

            return _movementService.FindBestFleeStep(
                _turnController.BattleState,
                actor.Team,
                actor.Position,
                actor.CombatantId);
        }

        private void EnsureOpportunityPanelReference()
        {
            if (opportunityPanelController == null)
            {
                opportunityPanelController = FindFirstObjectByType<BattleOpportunityPanelController>();
            }

            if (opportunityPanelController != null && !_opportunityPanelCallbacksRegistered)
            {
                opportunityPanelController.ActionSelected += HandleOpportunityActionSelected;
                opportunityPanelController.Declined += HandleOpportunityDeclined;
                _opportunityPanelCallbacksRegistered = true;
            }
        }

        private void HandleOpportunityActionSelected(int index)
        {
            if (!_awaitingOpportunityChoice)
            {
                return;
            }

            _selectedOpportunityIndex = index;
            _declinedOpportunity = false;
            _awaitingOpportunityChoice = false;
        }

        private void HandleOpportunityDeclined()
        {
            if (!_awaitingOpportunityChoice)
            {
                return;
            }

            _selectedOpportunityIndex = -1;
            _declinedOpportunity = true;
            _awaitingOpportunityChoice = false;
        }
    }
}
