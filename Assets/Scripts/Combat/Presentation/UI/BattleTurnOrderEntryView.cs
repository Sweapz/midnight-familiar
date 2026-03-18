using MidnightFamiliar.Combat.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleTurnOrderEntryView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image background;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text hpLabel;

        [Header("Colors")]
        [SerializeField] private Color playerColor = new Color(0.22f, 0.55f, 0.92f, 0.88f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.34f, 0.34f, 0.88f);
        [SerializeField] private Color currentTurnColor = new Color(1f, 0.85f, 0.32f, 0.96f);
        [SerializeField] private Color defeatedTint = new Color(0.45f, 0.45f, 0.45f, 0.85f);

        public void Bind(TurnOrderHudEntry entry, bool isCurrentTurn)
        {
            if (entry == null)
            {
                return;
            }

            if (nameLabel != null)
            {
                nameLabel.text = entry.DisplayName;
            }

            if (hpLabel != null)
            {
                hpLabel.text = $"{Mathf.Max(0, entry.CurrentHp)}/{Mathf.Max(1, entry.MaxHp)} HP";
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = entry.Portrait;
                portraitImage.enabled = entry.Portrait != null;
            }

            if (background == null)
            {
                return;
            }

            Color baseColor = entry.Team == TeamSide.Player ? playerColor : enemyColor;
            if (entry.IsDefeated)
            {
                baseColor = defeatedTint;
            }

            background.color = isCurrentTurn ? currentTurnColor : baseColor;
        }
    }
}
