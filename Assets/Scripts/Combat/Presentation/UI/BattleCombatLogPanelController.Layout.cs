using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed partial class BattleCombatLogPanelController
    {
        private void AutoResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (contentRoot == null)
            {
                Transform t = transform.Find("Content");
                contentRoot = t != null ? t as RectTransform : null;
            }

            if (toggleButton == null)
            {
                Transform t = transform.Find("ToggleLogButton");
                toggleButton = t != null ? t.GetComponent<Button>() : null;
            }

            if (toggleButtonLabel == null)
            {
                Transform t = transform.Find("ToggleLogButton/Label");
                toggleButtonLabel = t != null ? t.GetComponent<Text>() : null;
            }

            if (logText == null)
            {
                Transform t = transform.Find("Content/LogText");
                logText = t != null ? t.GetComponent<Text>() : null;
            }

            if (logScrollRect == null)
            {
                Transform t = transform.Find("Content/LogScrollView");
                logScrollRect = t != null ? t.GetComponent<ScrollRect>() : null;
            }

            EnsureLogEntriesContainer();
            if (logEntryTemplate == null && logEntriesContainer != null)
            {
                Transform template = logEntriesContainer.Find("LogEntryTemplate");
                logEntryTemplate = template != null ? template.GetComponent<Text>() : null;
            }

            EnsureReadableLayout();
            EnsureHoverTooltipReferences();
        }

        private void EnsureLogEntriesContainer()
        {
            if (contentRoot == null)
            {
                return;
            }

            RectTransform parentForEntries = contentRoot;
            if (logScrollRect != null && logScrollRect.content != null)
            {
                parentForEntries = logScrollRect.content;
            }
            else if (logScrollRect != null && logScrollRect.viewport != null)
            {
                parentForEntries = logScrollRect.viewport;
            }

            if (logEntriesContainer == null)
            {
                Transform existing = parentForEntries.Find("LogEntriesContainer");
                if (existing != null)
                {
                    logEntriesContainer = existing as RectTransform;
                }
            }

            if (logEntriesContainer == null)
            {
                logEntriesContainer = new GameObject(
                    "LogEntriesContainer",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                logEntriesContainer.SetParent(parentForEntries, false);
                logEntriesContainer.anchorMin = new Vector2(0f, 1f);
                logEntriesContainer.anchorMax = new Vector2(1f, 1f);
                logEntriesContainer.pivot = new Vector2(0.5f, 1f);
                logEntriesContainer.anchoredPosition = Vector2.zero;
                logEntriesContainer.sizeDelta = Vector2.zero;
            }
            else
            {
                // Ensure existing scene objects use top-stretched scroll content anchors.
                logEntriesContainer.anchorMin = new Vector2(0f, 1f);
                logEntriesContainer.anchorMax = new Vector2(1f, 1f);
                logEntriesContainer.pivot = new Vector2(0.5f, 1f);
                logEntriesContainer.anchoredPosition = Vector2.zero;
            }

            VerticalLayoutGroup layout = logEntriesContainer.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = logEntriesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(10, 10, 0, 10);

            ContentSizeFitter fitter = logEntriesContainer.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = logEntriesContainer.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            if (logScrollRect != null && logScrollRect.content == null)
            {
                logScrollRect.content = logEntriesContainer;
            }
        }

        private void EnsureEntryPoolSize(int required)
        {
            while (_entryTextPool.Count < required)
            {
                Text rowText;
                if (logEntryTemplate != null)
                {
                    rowText = Instantiate(logEntryTemplate, logEntriesContainer);
                    rowText.name = $"LogEntry_{_entryTextPool.Count + 1}";
                }
                else
                {
                    var rowRect = new GameObject(
                        $"LogEntry_{_entryTextPool.Count + 1}",
                        typeof(RectTransform),
                        typeof(Text),
                        typeof(LayoutElement)).GetComponent<RectTransform>();
                    rowRect.SetParent(logEntriesContainer, false);
                    rowRect.anchorMin = new Vector2(0f, 1f);
                    rowRect.anchorMax = new Vector2(1f, 1f);
                    rowRect.pivot = new Vector2(0.5f, 1f);
                    rowRect.offsetMin = Vector2.zero;
                    rowRect.offsetMax = Vector2.zero;

                    rowText = rowRect.GetComponent<Text>();
                    rowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    rowText.color = Color.white;
                    rowText.alignment = TextAnchor.UpperLeft;
                    rowText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    rowText.verticalOverflow = VerticalWrapMode.Overflow;
                }

                rowText.supportRichText = true;
                rowText.fontSize = logFontSize;

                LayoutElement element = rowText.GetComponent<LayoutElement>();
                if (element == null)
                {
                    element = rowText.gameObject.AddComponent<LayoutElement>();
                }

                element.preferredHeight = logFontSize + 6f;
                _entryTextPool.Add(rowText);
            }
        }

        private void EnsureReadableLayout()
        {
            if (panelRoot != null && panelRoot.sizeDelta.x < minimumPanelWidth)
            {
                Vector2 panelSize = panelRoot.sizeDelta;
                panelSize.x = minimumPanelWidth;
                panelRoot.sizeDelta = panelSize;
            }

            if (contentRoot != null && panelRoot != null && contentRoot.sizeDelta.x < panelRoot.sizeDelta.x)
            {
                Vector2 contentSize = contentRoot.sizeDelta;
                contentSize.x = panelRoot.sizeDelta.x;
                contentRoot.sizeDelta = contentSize;
            }

            if (toggleButton != null && panelRoot != null)
            {
                RectTransform toggleRect = toggleButton.transform as RectTransform;
                if (toggleRect != null)
                {
                    Vector2 pos = toggleRect.anchoredPosition;
                    pos.x = panelRoot.sizeDelta.x - toggleRect.sizeDelta.x;
                    toggleRect.anchoredPosition = pos;
                }
            }
        }
    }
}
