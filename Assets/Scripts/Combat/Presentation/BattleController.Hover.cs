using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
        private void HandleHudCombatantHoverStarted(string combatantId)
        {
            _hudHoveredCombatantId = combatantId ?? string.Empty;
            RefreshHoverTooltipIfNeeded();
        }

        private void HandleHudCombatantHoverEnded(string combatantId)
        {
            if (_hudHoveredCombatantId == combatantId)
            {
                _hudHoveredCombatantId = string.Empty;
                RefreshHoverTooltipIfNeeded();
            }
        }

        private void UpdateWorldHoveredCombatantId()
        {
            bool isPointerOverTooltip = hudController != null && hudController.IsPointerOverHoverTooltip();
            if (isPointerOverTooltip && !string.IsNullOrWhiteSpace(_hoveredCombatantId))
            {
                // Keep world hover stable while cursor is on the tooltip opened from a world Cuid.
                _worldHoveredCombatantId = _hoveredCombatantId;
                return;
            }

            if (!TryGetClickedCombatant(out CombatantState combatant) || combatant == null || combatant.IsDefeated)
            {
                _worldHoveredCombatantId = string.Empty;
                return;
            }

            _worldHoveredCombatantId = combatant.CombatantId;
        }

        private void ShowCombatantHover(string combatantId)
        {
            if (string.IsNullOrWhiteSpace(combatantId) || hudController == null || _turnController?.BattleState == null)
            {
                return;
            }

            CombatantState combatant = _turnController.BattleState.FindCombatant(combatantId);
            if (combatant == null)
            {
                return;
            }

            _hoveredCombatantId = combatantId;
            bool opportunityReady = !_spentOpportunityCombatants.Contains(combatant.CombatantId);
            string title = _statusPresentationService.BuildHoverTitle(combatant);
            string statsText = _statusPresentationService.BuildStatsText(combatant);
            string effectsText = _statusPresentationService.BuildEffectsText(combatant, opportunityReady);

            if (TryGetHoverScreenPoint(combatant, out Vector2 screenPoint))
            {
                hudController.ShowHoverTooltip(title, statsText, effectsText, screenPoint);
            }
            else
            {
                hudController.ShowHoverTooltip(title, statsText, effectsText);
            }
        }

        private void RefreshHoverTooltipIfNeeded()
        {
            string targetCombatantId = !string.IsNullOrWhiteSpace(_hudHoveredCombatantId)
                ? _hudHoveredCombatantId
                : _worldHoveredCombatantId;

            if (string.IsNullOrWhiteSpace(targetCombatantId))
            {
                _hoveredCombatantId = string.Empty;
                hudController?.HideHoverTooltip();
                return;
            }

            if (_turnController?.BattleState == null)
            {
                _hoveredCombatantId = string.Empty;
                hudController?.HideHoverTooltip();
                return;
            }

            CombatantState hovered = _turnController.BattleState.FindCombatant(targetCombatantId);
            if (hovered == null)
            {
                _hoveredCombatantId = string.Empty;
                hudController?.HideHoverTooltip();
                return;
            }

            ShowCombatantHover(targetCombatantId);

            if (TryGetHoverScreenPoint(hovered, out Vector2 screenPoint))
            {
                hudController?.SetHoverTooltipScreenPoint(screenPoint);
            }
        }

        private bool TryGetHoverScreenPoint(CombatantState combatant, out Vector2 screenPoint)
        {
            screenPoint = default;
            if (combatant == null)
            {
                return false;
            }

            Camera cam = GetInputCamera();
            if (cam == null)
            {
                return false;
            }

            Vector3 worldPosition;
            if (_views.TryGetValue(combatant.CombatantId, out CuidView view) && view != null)
            {
                worldPosition = view.transform.position + Vector3.up * hoverWorldYOffset;
            }
            else
            {
                worldPosition = gridController.GridToWorld(combatant.Position) + Vector3.up * hoverWorldYOffset;
            }

            Vector3 projected = cam.WorldToScreenPoint(worldPosition);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPoint = new Vector2(projected.x, projected.y);
            return true;
        }

    }
}
