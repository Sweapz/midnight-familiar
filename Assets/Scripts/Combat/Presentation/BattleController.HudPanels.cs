using System.Collections.Generic;
using System.Linq;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation.UI;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void RefreshHud()
        {
            EnsureHudReference();
            if (hudController == null)
            {
                if (!_loggedHudMissingError)
                {
                    _loggedHudMissingError = true;
                    Debug.LogWarning("BattleController could not find BattleHudController in scene. Turn order HUD will not update.");
                }

                return;
            }

            _loggedHudMissingError = false;
            if (_turnController?.BattleState == null)
            {
                return;
            }

            string currentCombatantId = _turnController.GetCurrentCombatant()?.CombatantId;
            var entries = new List<TurnOrderHudEntry>(_turnController.BattleState.TurnOrder.Count);
            foreach (string combatantId in _turnController.BattleState.TurnOrder)
            {
                CombatantState combatant = _turnController.BattleState.FindCombatant(combatantId);
                if (combatant == null)
                {
                    continue;
                }

                Sprite portrait = null;
                if (!string.IsNullOrWhiteSpace(combatant.Unit?.SpeciesId) &&
                    _definitionsBySpeciesId.TryGetValue(combatant.Unit.SpeciesId, out CuidDefinition definition))
                {
                    portrait = definition.Portrait;
                }

                entries.Add(new TurnOrderHudEntry
                {
                    CombatantId = combatant.CombatantId,
                    DisplayName = combatant.Unit?.DisplayName ?? "Cuid",
                    CurrentHp = Mathf.Max(0, combatant.CurrentHealth),
                    MaxHp = combatant.GetMaxHealth(),
                    Portrait = portrait,
                    Team = combatant.Team,
                    IsDefeated = combatant.IsDefeated
                });
            }

            if (entries.Count == 0 && !_loggedTurnOrderEmptyWarning)
            {
                _loggedTurnOrderEmptyWarning = true;
                Debug.LogWarning("Turn order HUD received zero entries. Check team definitions and battle initialization.");
            }
            else if (entries.Count > 0)
            {
                _loggedTurnOrderEmptyWarning = false;
            }

            hudController.SetTurnOrder(entries, currentCombatantId, _turnController.BattleState.RoundNumber);
            RefreshHoverTooltipIfNeeded();
        }

        private void EnsureHudReference()
        {
            if (hudController != null)
            {
                if (!_hudHoverCallbacksRegistered)
                {
                    hudController.TurnOrderHoverStarted += HandleHudCombatantHoverStarted;
                    hudController.TurnOrderHoverEnded += HandleHudCombatantHoverEnded;
                    _hudHoverCallbacksRegistered = true;
                }

                return;
            }

            hudController = FindFirstObjectByType<BattleHudController>();
            if (hudController != null && !_hudHoverCallbacksRegistered)
            {
                hudController.TurnOrderHoverStarted += HandleHudCombatantHoverStarted;
                hudController.TurnOrderHoverEnded += HandleHudCombatantHoverEnded;
                _hudHoverCallbacksRegistered = true;
            }
        }

        private void EnsureActionPanelReference()
        {
            if (actionPanelController == null)
            {
                actionPanelController = FindFirstObjectByType<BattleActionPanelController>();
            }

            if (actionPanelController != null && !_actionPanelCallbacksRegistered)
            {
                actionPanelController.ActionPressed += HandleActionPanelActionClicked;
                actionPanelController.EndTurnPressed += HandleActionPanelEndTurnClicked;
                _actionPanelCallbacksRegistered = true;
            }
        }

        private void HandleActionPanelActionClicked(int buttonIndex)
        {
            if (_activePlayerActor == null)
            {
                return;
            }

            if (_inputPhase == PlayerInputPhase.SelectTarget)
            {
                _inputPhase = PlayerInputPhase.SelectAction;
                _selectedAction = null;
                ClearActionMarkers();
                RebuildMoveMarkers();
                RefreshActionPanel();
                return;
            }

            if (_inputPhase != PlayerInputPhase.SelectAction || _hasUsedAction)
            {
                return;
            }

            List<CuidAction> actions = GetOrderedActionsForUi(_activePlayerActor);
            if (buttonIndex < 0 || buttonIndex >= actions.Count)
            {
                return;
            }

            CuidAction action = actions[buttonIndex];
            _selectedAction = action;
            if (action.TargetRule == TargetRule.Self)
            {
                ExecutePlayerAction(_activePlayerActor);
            }
            else
            {
                _inputPhase = PlayerInputPhase.SelectTarget;
                ClearMoveMarkers();
                RebuildActionMarkers();
                RefreshActionPanel();
            }
        }

        private void HandleActionPanelEndTurnClicked()
        {
            if (_activePlayerActor == null)
            {
                return;
            }

            TurnStepResult endStep = _turnController.ExecuteTurn(TurnChoice.Pass(), advanceTurn: true);
            AddLogFromStep(endStep);
            _activePlayerActor = null;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.None;
            ClearMoveMarkers();
            ClearActionMarkers();
            _playerTurnResolved = true;
            RefreshHud();
            RefreshActionPanel();
        }

        private void RefreshActionPanel()
        {
            EnsureActionPanelReference();
            if (actionPanelController == null)
            {
                return;
            }

            if (_activePlayerActor == null)
            {
                actionPanelController.SetVisible(false);
                return;
            }

            actionPanelController.SetVisible(true);
            actionPanelController.SetEndTurnInteractable(true);

            Sprite portrait = null;
            if (!string.IsNullOrWhiteSpace(_activePlayerActor.Unit?.SpeciesId) &&
                _definitionsBySpeciesId.TryGetValue(_activePlayerActor.Unit.SpeciesId, out CuidDefinition definition))
            {
                portrait = definition.Portrait;
            }

            if (_inputPhase == PlayerInputPhase.SelectTarget)
            {
                actionPanelController.SetActions(new[] { "Back" }, true);
            }
            else
            {
                List<string> labels = GetOrderedActionsForUi(_activePlayerActor)
                    .Select(action => action.DisplayName)
                    .ToList();

                actionPanelController.SetActions(labels, !_hasUsedAction);
            }

            actionPanelController.SetStatus(
                _activePlayerActor.Unit.DisplayName,
                Mathf.Max(0, _activePlayerActor.CurrentHealth),
                _activePlayerActor.GetMaxHealth(),
                _remainingMovement,
                portrait);
        }

        private List<CuidAction> GetOrderedActionsForUi(CombatantState actor)
        {
            return _actionQueryService.GetOrderedActionsForUi(actor);
        }

        private void EnsureCombatLogPanelReference()
        {
            if (combatLogPanelController != null)
            {
                return;
            }

            combatLogPanelController = FindFirstObjectByType<BattleCombatLogPanelController>();
        }
    }
}
