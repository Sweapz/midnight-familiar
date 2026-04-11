using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void RebuildMoveMarkers()
        {
            ClearMoveMarkers();
            if (!showReachableMoveMarkers)
            {
                return;
            }

            if (_inputPhase != PlayerInputPhase.SelectMove && _inputPhase != PlayerInputPhase.SelectAction)
            {
                return;
            }

            if (_remainingMovement <= 0)
            {
                return;
            }

            foreach (GridPosition cell in _validMoveCells)
            {
                _moveMarkers.Add(CreateOutlineMarker(
                    $"MoveMarker_{cell.X}_{cell.Y}",
                    cell,
                    reachableMarkerColor,
                    reachableMarkerOutlineInset,
                    reachableMarkerYOffset));
            }
        }

        private void ClearMoveMarkers()
        {
            for (int i = 0; i < _moveMarkers.Count; i++)
            {
                if (_moveMarkers[i] != null)
                {
                    Destroy(_moveMarkers[i]);
                }
            }

            _moveMarkers.Clear();
        }

        private void RebuildActionMarkers()
        {
            ClearActionMarkers();
            if (!showActionRangeMarkers || _selectedAction == null || _activePlayerActor == null || _inputPhase != PlayerInputPhase.SelectTarget)
            {
                return;
            }

            List<GridPosition> actionCells = BuildActionRangeCells(_activePlayerActor, _selectedAction);
            for (int i = 0; i < actionCells.Count; i++)
            {
                GridPosition cell = actionCells[i];
                _actionMarkers.Add(CreateOutlineMarker(
                    $"ActionMarker_{cell.X}_{cell.Y}",
                    cell,
                    actionMarkerColor,
                    actionMarkerOutlineInset,
                    actionMarkerYOffset));
            }
        }

        private void ClearActionMarkers()
        {
            for (int i = 0; i < _actionMarkers.Count; i++)
            {
                if (_actionMarkers[i] != null)
                {
                    Destroy(_actionMarkers[i]);
                }
            }

            _actionMarkers.Clear();
        }

        private List<GridPosition> BuildActionRangeCells(CombatantState actor, CuidAction action)
        {
            var cells = new List<GridPosition>();
            BattleState state = _turnController != null ? _turnController.BattleState : null;
            if (actor == null || action == null || state == null)
            {
                return cells;
            }

            if (action.TargetRule == TargetRule.Self)
            {
                cells.Add(actor.Position);
                return cells;
            }

            _spatialQueryService.CollectCellsInRange(state, actor.Position, action.Range, cells);

            return cells;
        }

        private GameObject CreateOutlineMarker(
            string markerName,
            GridPosition cell,
            Color color,
            float insetRatio,
            float yOffset)
        {
            GameObject root = new GameObject(markerName);
            root.transform.SetParent(transform);
            root.transform.position = gridController.GridToWorld(cell) + new Vector3(0f, yOffset, 0f);

            float cellSize = gridController.CellSize;
            float inset = Mathf.Clamp(insetRatio, 0.02f, 0.45f) * cellSize;
            float outer = Mathf.Max(0.05f, cellSize - inset * 2f);
            float lineThickness = Mathf.Clamp(markerLineThickness, 0.01f, outer * 0.45f);
            float half = outer * 0.5f;
            float lineY = Mathf.Max(0.005f, markerHeight);

            CreateOutlineSegment(root.transform, new Vector3(0f, 0f, half), new Vector3(outer, lineY, lineThickness), color);
            CreateOutlineSegment(root.transform, new Vector3(0f, 0f, -half), new Vector3(outer, lineY, lineThickness), color);
            CreateOutlineSegment(root.transform, new Vector3(half, 0f, 0f), new Vector3(lineThickness, lineY, outer), color);
            CreateOutlineSegment(root.transform, new Vector3(-half, 0f, 0f), new Vector3(lineThickness, lineY, outer), color);

            return root;
        }

        private void CreateOutlineSegment(Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localScale = scale;
            Destroy(segment.GetComponent<Collider>());

            Renderer renderer = segment.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.material.color = color;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
