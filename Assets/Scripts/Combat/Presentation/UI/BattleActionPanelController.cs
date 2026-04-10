using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleActionPanelController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;

        [Header("Texts")]
        [SerializeField] private Text actorNameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Image actorPortraitImage;

        [Header("Buttons")]
        [SerializeField] private Button endTurnButton;
        [SerializeField] private List<Button> actionButtons = new List<Button>(5);

        public event Action<int> ActionPressed;
        public event Action EndTurnPressed;

        private bool _listenersBound;

        private void Awake()
        {
            AutoResolveReferences();
            BindListeners();
        }

        public void SetVisible(bool isVisible)
        {
            AutoResolveReferences();
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(isVisible);
            }
            else
            {
                gameObject.SetActive(isVisible);
            }
        }

        public void SetStatus(string actorName, int currentHp, int maxHp, int movementRemaining, Sprite portrait)
        {
            AutoResolveReferences();
            if (actorNameText != null)
            {
                actorNameText.text = actorName;
            }

            if (statsText != null)
            {
                statsText.text = $"Health {currentHp}/{maxHp}\nMovement {movementRemaining}";
            }

            if (actorPortraitImage != null)
            {
                actorPortraitImage.sprite = portrait;
                actorPortraitImage.enabled = portrait != null;
            }
        }

        public void SetActions(IReadOnlyList<string> actionLabels, bool interactable)
        {
            AutoResolveReferences();
            BindListeners();

            int count = actionLabels != null ? actionLabels.Count : 0;
            for (int i = 0; i < actionButtons.Count; i++)
            {
                Button button = actionButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool visible = i < count;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = actionLabels[i];
                }

                button.interactable = interactable;
            }
        }

        public void SetEndTurnInteractable(bool interactable)
        {
            AutoResolveReferences();
            if (endTurnButton != null)
            {
                endTurnButton.interactable = interactable;
            }
        }

        private void AutoResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (actorNameText == null)
            {
                Transform t = transform.Find("ActorName");
                actorNameText = t != null ? t.GetComponent<Text>() : null;
            }

            if (statsText == null)
            {
                Transform t = transform.Find("StatusBox/StatsText");
                statsText = t != null ? t.GetComponent<Text>() : null;
            }

            if (actorPortraitImage == null)
            {
                Transform t = transform.Find("StatusBox/ActorPortrait");
                actorPortraitImage = t != null ? t.GetComponent<Image>() : null;
            }

            if (endTurnButton == null)
            {
                Transform t = transform.Find("EndTurnButton");
                endTurnButton = t != null ? t.GetComponent<Button>() : null;
            }

            if (actionButtons == null || actionButtons.Count < 5)
            {
                Transform row = transform.Find("ActionRow");
                if (row != null)
                {
                    actionButtons = row.GetComponentsInChildren<Button>(true)
                        .OrderBy(button => button.transform.GetSiblingIndex())
                        .ToList();
                }
            }
        }

        private void BindListeners()
        {
            if (_listenersBound)
            {
                return;
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(() => EndTurnPressed?.Invoke());
            }

            for (int i = 0; i < actionButtons.Count; i++)
            {
                Button button = actionButtons[i];
                if (button == null)
                {
                    continue;
                }

                int buttonIndex = i;
                button.onClick.AddListener(() => ActionPressed?.Invoke(buttonIndex));
            }

            _listenersBound = true;
        }
    }
}
