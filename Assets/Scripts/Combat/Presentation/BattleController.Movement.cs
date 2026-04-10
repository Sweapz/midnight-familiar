using System.Collections;
using System.Linq;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void HandlePlayerClick()
        {
            switch (_inputPhase)
            {
                case PlayerInputPhase.SelectMove:
                case PlayerInputPhase.SelectAction:
                    TryHandleMoveClick();
                    break;
                case PlayerInputPhase.SelectTarget:
                    TryHandleTargetClick();
                    break;
            }
        }

        private void TryHandleMoveClick()
        {
            if (!TryGetClickedCell(out GridPosition cell))
            {
                Debug.Log("Move click: no valid grid cell detected.");
                return;
            }

            if (!_validMoveCells.Any(c => c.X == cell.X && c.Y == cell.Y))
            {
                Debug.Log($"Move click rejected: cell [{cell.X},{cell.Y}] is outside current move range.");
                return;
            }

            int moveCost = _activePlayerActor.Position.ManhattanDistanceTo(cell);
            if (moveCost <= 0 || moveCost > _remainingMovement)
            {
                Debug.Log($"Move click rejected: movement cost {moveCost} exceeds remaining {_remainingMovement}.");
                return;
            }

            StartCoroutine(AnimatePlayerMove(cell, moveCost));
        }

        private IEnumerator AnimatePlayerMove(GridPosition destination, int moveCost)
        {
            _isMovingActor = true;
            _inputPhase = PlayerInputPhase.None;
            ClearMoveMarkers();
            ClearActionMarkers();

            GridPosition start = _activePlayerActor.Position;
            yield return ResolveOpportunityAttacksBeforeMove(_activePlayerActor, start, destination);
            if (_activePlayerActor == null || _activePlayerActor.IsDefeated)
            {
                _isMovingActor = false;
                yield break;
            }

            _activePlayerActor.Position = destination;

            if (_views.TryGetValue(_activePlayerActor.CombatantId, out CuidView view) && view != null)
            {
                Vector3 worldStart = gridController.GridToWorld(start);
                Vector3 worldEnd = gridController.GridToWorld(destination);
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

            _remainingMovement = Mathf.Max(0, _remainingMovement - moveCost);
            BuildValidMoveCells(_activePlayerActor);
            _isMovingActor = false;
            _inputPhase = PlayerInputPhase.SelectAction;
            RebuildMoveMarkers();
            RefreshActionPanel();
        }

        private void TryHandleTargetClick()
        {
            if (!TryGetClickedCombatant(out CombatantState target))
            {
                return;
            }

            if (!IsValidTargetForSelectedAction(_activePlayerActor, target, _selectedAction))
            {
                return;
            }

            ExecutePlayerAction(target);
        }

        private void ExecutePlayerAction(CombatantState target)
        {
            var choice = new TurnChoice
            {
                IsPass = false,
                ActionId = _selectedAction.Id,
                TargetCombatantId = target.CombatantId
            };

            TurnStepResult step = _turnController.ExecuteTurn(choice, advanceTurn: false);
            AddLogFromStep(step);
            RefreshAllViews();

            _hasUsedAction = true;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.SelectAction;
            ClearActionMarkers();

            if (step.BattleEnded)
            {
                _activePlayerActor = null;
                _playerTurnResolved = true;
                ClearMoveMarkers();
                RefreshActionPanel();
                return;
            }

            BuildValidMoveCells(_activePlayerActor);
            RebuildMoveMarkers();
            RefreshActionPanel();
        }

        private bool TryGetClickedCell(out GridPosition cell)
        {
            cell = default;
            Camera cam = GetInputCamera();
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(gridController.transform.up, gridController.transform.position);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 worldHit = ray.GetPoint(enter);
            return gridController.TryWorldToGrid(worldHit, out cell);
        }

        private bool TryGetClickedCombatant(out CombatantState combatant)
        {
            combatant = null;
            Camera cam = GetInputCamera();
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                return false;
            }

            CuidView view = hit.collider.GetComponentInParent<CuidView>();
            if (view == null)
            {
                return TryGetClickedCombatantFromCell(out combatant);
            }

            combatant = _turnController.BattleState.FindCombatant(view.CombatantId);
            return combatant != null || TryGetClickedCombatantFromCell(out combatant);
        }

        private bool TryGetClickedCombatantFromCell(out CombatantState combatant)
        {
            combatant = null;
            if (!TryGetClickedCell(out GridPosition cell))
            {
                return false;
            }

            combatant = _turnController.BattleState.Combatants
                .FirstOrDefault(c => !c.IsDefeated && c.Position.X == cell.X && c.Position.Y == cell.Y);
            return combatant != null;
        }

        private Camera GetInputCamera()
        {
            Camera cam = battleCamera != null ? battleCamera : Camera.main;
            if (cam == null && !_loggedCameraError)
            {
                _loggedCameraError = true;
                Debug.LogError("No input camera found. Assign Battle Camera on BattleController or tag a camera as MainCamera.");
            }

            return cam;
        }

        private void BuildValidMoveCells(CombatantState actor)
        {
            _validMoveCells.Clear();
            int range = _remainingMovement;
            if (range <= 0)
            {
                return;
            }

            GridPosition start = actor.Position;

            for (int x = 0; x < gridController.GridWidth; x++)
            {
                for (int y = 0; y < gridController.GridHeight; y++)
                {
                    var candidate = new GridPosition(x, y);
                    if (candidate.X == start.X && candidate.Y == start.Y)
                    {
                        continue;
                    }

                    if (start.ManhattanDistanceTo(candidate) > range)
                    {
                        continue;
                    }

                    if (IsOccupied(candidate, actor.CombatantId))
                    {
                        continue;
                    }

                    _validMoveCells.Add(candidate);
                }
            }
        }

        private int GetMoveRange(CombatantState actor)
        {
            int speed = actor != null ? actor.GetEffectiveStats().Speed : 1;
            return Mathf.Clamp(Mathf.Max(1, speed / 4), 1, 3);
        }

        private IEnumerator MoveActorTowardTarget(CombatantState actor, CombatantState target)
        {
            GridPosition start = actor.Position;
            GridPosition end = CalculateMoveDestination(actor, target);
            if (start.X == end.X && start.Y == end.Y)
            {
                yield break;
            }

            yield return ResolveOpportunityAttacksBeforeMove(actor, start, end);
            if (actor == null || actor.IsDefeated)
            {
                yield break;
            }

            actor.Position = end;
            if (!_views.TryGetValue(actor.CombatantId, out CuidView view) || view == null)
            {
                yield break;
            }

            Vector3 worldStart = gridController.GridToWorld(start);
            Vector3 worldEnd = gridController.GridToWorld(end);
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

        private GridPosition CalculateMoveDestination(CombatantState actor, CombatantState target)
        {
            int steps = GetMoveRange(actor);
            GridPosition current = actor.Position;

            for (int i = 0; i < steps; i++)
            {
                GridPosition next = FindBestNeighborStep(current, target.Position, actor.CombatantId);
                if (next.X == current.X && next.Y == current.Y)
                {
                    break;
                }

                current = next;
            }

            return current;
        }

        private GridPosition FindBestNeighborStep(GridPosition from, GridPosition target, string actorId)
        {
            GridPosition[] candidates =
            {
                new GridPosition(from.X + 1, from.Y),
                new GridPosition(from.X - 1, from.Y),
                new GridPosition(from.X, from.Y + 1),
                new GridPosition(from.X, from.Y - 1)
            };

            int bestDistance = from.ManhattanDistanceTo(target);
            GridPosition best = from;
            foreach (GridPosition candidate in candidates)
            {
                if (!gridController.IsInside(candidate))
                {
                    continue;
                }

                if (IsOccupied(candidate, actorId))
                {
                    continue;
                }

                int distance = candidate.ManhattanDistanceTo(target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private bool IsOccupied(GridPosition position, string exceptCombatantId)
        {
            foreach (CombatantState combatant in _turnController.BattleState.Combatants)
            {
                if (combatant.IsDefeated || combatant.CombatantId == exceptCombatantId)
                {
                    continue;
                }

                if (combatant.Position.X == position.X && combatant.Position.Y == position.Y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
