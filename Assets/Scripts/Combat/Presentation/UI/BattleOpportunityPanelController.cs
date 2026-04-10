using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleOpportunityPanelController : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Text promptLabel;
        [SerializeField] private RectTransform actionsContainer;
        [SerializeField] private Button actionButtonTemplate;
        [SerializeField] private Button declineButton;

        private readonly List<Button> _actionButtons = new List<Button>(3);
        private bool _listenersBound;

        public event Action<int> ActionSelected;
        public event Action Declined;

        private void Awake()
        {
            AutoResolveReferences();
            BindListeners();
            SetVisible(false);
        }

        public void ShowPrompt(string prompt, IReadOnlyList<string> actionLabels)
        {
            AutoResolveReferences();
            BindListeners();
            SetVisible(true);

            if (promptLabel != null)
            {
                promptLabel.text = string.IsNullOrWhiteSpace(prompt)
                    ? "Take opportunity action?"
                    : prompt;
            }

            int count = actionLabels != null ? actionLabels.Count : 0;
            RebuildActionButtons(actionLabels, count);
        }

        public void SetVisible(bool isVisible)
        {
            AutoResolveReferences();
            gameObject.SetActive(isVisible);
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(isVisible);
            }
        }

        private void AutoResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }
            else if (panelRoot != transform)
            {
                // Repair wrong serialized references by always using this object as root.
                panelRoot = transform as RectTransform;
            }

            if (promptLabel == null)
            {
                Transform t = transform.Find("PromptLabel");
                promptLabel = t != null ? t.GetComponent<Text>() : null;
            }

            if (actionsContainer == null || actionsContainer.transform.parent != panelRoot)
            {
                Transform t = transform.Find("ActionsRow");
                actionsContainer = t != null ? t as RectTransform : null;
            }

            if (actionsContainer == null && panelRoot != null)
            {
                var row = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                row.SetParent(panelRoot, false);
                row.anchorMin = new Vector2(0f, 0f);
                row.anchorMax = new Vector2(1f, 0f);
                row.pivot = new Vector2(0.5f, 0f);
                row.anchoredPosition = new Vector2(0f, 66f);
                row.sizeDelta = new Vector2(-20f, 64f);

                HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.spacing = 10f;
                layout.padding = new RectOffset(10, 10, 0, 0);
                actionsContainer = row;
            }

            if (actionsContainer != null)
            {
                actionsContainer.gameObject.SetActive(true);
                actionsContainer.localScale = Vector3.one;
                actionsContainer.anchorMin = new Vector2(0f, 0f);
                actionsContainer.anchorMax = new Vector2(1f, 0f);
                actionsContainer.pivot = new Vector2(0.5f, 0f);
                actionsContainer.anchoredPosition = new Vector2(0f, 66f);
                actionsContainer.sizeDelta = new Vector2(-20f, 64f);

                HorizontalLayoutGroup layout = actionsContainer.GetComponent<HorizontalLayoutGroup>();
                if (layout == null)
                {
                    layout = actionsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                }

                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.spacing = 10f;
                layout.padding = new RectOffset(10, 10, 0, 0);
            }

            if (actionButtonTemplate == null && actionsContainer != null)
            {
                actionButtonTemplate = actionsContainer.GetComponentInChildren<Button>(true);
            }

            if (actionButtonTemplate != null)
            {
                if (actionButtonTemplate == declineButton ||
                    actionButtonTemplate.transform.parent != actionsContainer)
                {
                    actionButtonTemplate = null;
                }
            }

            if (actionButtonTemplate == null && actionsContainer != null)
            {
                actionButtonTemplate = CreateDefaultActionTemplate(actionsContainer);
            }

            if (declineButton == null)
            {
                Transform t = transform.Find("DeclineButton");
                declineButton = t != null ? t.GetComponent<Button>() : null;
            }
        }

        private void BindListeners()
        {
            if (_listenersBound)
            {
                return;
            }

            if (declineButton != null)
            {
                declineButton.onClick.AddListener(() => Declined?.Invoke());
            }

            _listenersBound = true;
        }

        private void RebuildActionButtons(IReadOnlyList<string> actionLabels, int count)
        {
            if (actionsContainer == null)
            {
                return;
            }

            for (int i = 0; i < actionsContainer.childCount; i++)
            {
                Transform child = actionsContainer.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            _actionButtons.Clear();
            for (int i = 0; i < count; i++)
            {
                string label = actionLabels != null && i < actionLabels.Count
                    ? actionLabels[i]
                    : $"Action {i + 1}";

                Button button = CreateRuntimeActionButton(actionsContainer, i, label);
                button.name = $"OpportunityActionButton{i + 1}";
                button.gameObject.SetActive(true);
                button.transform.SetAsLastSibling();
                int index = i;
                button.onClick.AddListener(() => ActionSelected?.Invoke(index));

                _actionButtons.Add(button);
            }
        }

        private static Button CreateDefaultActionTemplate(RectTransform parent)
        {
            const float buttonWidth = 208f;
            const float buttonHeight = 62f;
            var buttonRect = new GameObject("OpportunityActionTemplate", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0f, 0f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            LayoutElement layoutElement = buttonRect.gameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = buttonWidth;
            layoutElement.minHeight = buttonHeight;
            layoutElement.preferredWidth = buttonWidth;
            layoutElement.preferredHeight = buttonHeight;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
            Button button = buttonRect.GetComponent<Button>();

            var labelRect = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            Text label = labelRect.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "Basic Strike";

            button.gameObject.SetActive(false);
            return button;
        }

        private static Button CreateRuntimeActionButton(RectTransform parent, int index, string labelText)
        {
            const float buttonWidth = 208f;
            const float buttonHeight = 62f;
            const float spacing = 10f;

            var buttonRect = new GameObject($"OpportunityActionButton{index + 1}", typeof(RectTransform), typeof(Image), typeof(Button))
                .GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0f, 0f);
            buttonRect.anchoredPosition = new Vector2(index * (buttonWidth + spacing), 0f);
            buttonRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            Image image = buttonRect.GetComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            var labelRect = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            Text label = labelRect.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = labelText;

            return buttonRect.GetComponent<Button>();
        }
    }
}
