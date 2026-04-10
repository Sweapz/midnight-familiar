using MidnightFamiliar.Combat.Models;
using System;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public class CuidView : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color playerColor = new Color(0.2f, 0.8f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.45f, 0.2f);
        [SerializeField] private Color defeatedColor = Color.gray;

        public string CombatantId { get; private set; } = string.Empty;
        public event Action<string> HoverStarted;
        public event Action<string> HoverEnded;

        public void Initialize(CombatantState combatant)
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            CombatantId = combatant.CombatantId;
            ApplyState(combatant);
        }

        public void ApplyState(CombatantState combatant)
        {
            gameObject.name = $"{combatant.Unit.DisplayName} [{combatant.CurrentHealth}]";

            if (targetRenderer == null)
            {
                return;
            }

            if (combatant.IsDefeated)
            {
                targetRenderer.material.color = defeatedColor;
                return;
            }

            targetRenderer.material.color = combatant.Team == TeamSide.Player ? playerColor : enemyColor;
        }

        private void OnMouseEnter()
        {
            if (!string.IsNullOrWhiteSpace(CombatantId))
            {
                HoverStarted?.Invoke(CombatantId);
            }
        }

        private void OnMouseExit()
        {
            if (!string.IsNullOrWhiteSpace(CombatantId))
            {
                HoverEnded?.Invoke(CombatantId);
            }
        }
    }
}
