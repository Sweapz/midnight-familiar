#if UNITY_EDITOR
using MidnightFamiliar.Combat.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.Editor
{
    public static partial class BattleHudCanvasCreator
    {
        private static BattleCombatLogPanelController EnsureCombatLogPanel(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("BattleCombatLogPanel");
            RectTransform panelRect;
            if (existing == null)
            {
                panelRect = new GameObject("BattleCombatLogPanel", typeof(RectTransform), typeof(Image), typeof(BattleCombatLogPanelController))
                    .GetComponent<RectTransform>();
                panelRect.SetParent(canvasRoot, false);
            }
            else
            {
                panelRect = existing as RectTransform ?? ReplaceWithRectTransform(existing, canvasRoot);
                if (panelRect.GetComponent<Image>() == null)
                {
                    panelRect.gameObject.AddComponent<Image>();
                }

                if (panelRect.GetComponent<BattleCombatLogPanelController>() == null)
                {
                    panelRect.gameObject.AddComponent<BattleCombatLogPanelController>();
                }
            }

            RemoveMissingScriptsRecursive(panelRect.gameObject);
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-16f, 16f);
            panelRect.sizeDelta = new Vector2(520f, 320f);

            Image panelBackground = panelRect.GetComponent<Image>();
            panelBackground.color = new Color(0f, 0f, 0f, 0.58f);

            Button toggleButton = EnsureButton(panelRect, "ToggleLogButton", "Hide Log", new Vector2(380f, 274f), new Vector2(132f, 38f));

            Transform existingContent = panelRect.Find("Content");
            RectTransform contentRect;
            if (existingContent == null)
            {
                contentRect = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                contentRect.SetParent(panelRect, false);
            }
            else
            {
                contentRect = existingContent as RectTransform ?? ReplaceWithRectTransform(existingContent, panelRect);
            }

            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = new Vector2(0f, -48f);

            Text title = EnsureChildText(contentRect, "Title", new Vector2(10f, -6f), new Vector2(340f, 34f), 25, TextAnchor.MiddleLeft);
            title.text = "Combat Log";

            RectTransform dividerRect = EnsurePanel(contentRect, "TitleDivider", new Vector2(5f, 264f), new Vector2(510f, 2f));
            Image dividerImage = dividerRect.GetComponent<Image>();
            dividerImage.color = new Color(1f, 1f, 1f, 0.22f);

            ScrollRect scrollRect = EnsureCombatLogScrollView(contentRect);
            RectTransform logEntriesContainer = EnsureLogEntriesContainer(scrollRect.content);
            Text logEntryTemplate = EnsureLogEntryTemplate(logEntriesContainer);
            Text legacyLogText = EnsureChildText(contentRect, "LogText", new Vector2(10f, -43f), new Vector2(500f, 220f), 19, TextAnchor.UpperLeft);
            legacyLogText.text = "No log entries yet.";
            legacyLogText.gameObject.SetActive(false);

            RectTransform lineTooltipRoot = EnsureCombatLogHoverTooltip(canvasRoot);
            Text lineTooltipText = FindChildText(lineTooltipRoot, "TooltipText");

            BattleCombatLogPanelController controller = panelRect.GetComponent<BattleCombatLogPanelController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = panelRect;
            so.FindProperty("contentRoot").objectReferenceValue = contentRect;
            so.FindProperty("toggleButton").objectReferenceValue = toggleButton;
            so.FindProperty("toggleButtonLabel").objectReferenceValue = FindChildText(toggleButton.transform as RectTransform, "Label");
            so.FindProperty("logText").objectReferenceValue = legacyLogText;
            so.FindProperty("logScrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("logEntriesContainer").objectReferenceValue = logEntriesContainer;
            so.FindProperty("logEntryTemplate").objectReferenceValue = logEntryTemplate;
            so.FindProperty("lineHoverTooltipRoot").objectReferenceValue = lineTooltipRoot;
            so.FindProperty("lineHoverTooltipText").objectReferenceValue = lineTooltipText;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static ScrollRect EnsureCombatLogScrollView(RectTransform contentRect)
        {
            Transform existing = contentRect.Find("LogScrollView");
            RectTransform scrollRectTransform;
            ScrollRect scrollRect;
            if (existing == null)
            {
                scrollRectTransform = new GameObject("LogScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect))
                    .GetComponent<RectTransform>();
                scrollRectTransform.SetParent(contentRect, false);
                scrollRect = scrollRectTransform.GetComponent<ScrollRect>();
            }
            else
            {
                scrollRectTransform = existing as RectTransform ?? ReplaceWithRectTransform(existing, contentRect);
                if (scrollRectTransform.GetComponent<Image>() == null)
                {
                    scrollRectTransform.gameObject.AddComponent<Image>();
                }

                scrollRect = scrollRectTransform.GetComponent<ScrollRect>() ?? scrollRectTransform.gameObject.AddComponent<ScrollRect>();
            }

            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = new Vector2(0f, -43f);

            Image scrollBackground = scrollRectTransform.GetComponent<Image>();
            scrollBackground.color = new Color(0f, 0f, 0f, 0f);

            RectTransform viewport = EnsureScrollViewport(scrollRectTransform);
            RectTransform scrollContent = EnsureScrollContent(viewport);

            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;
            scrollRect.viewport = viewport;
            scrollRect.content = scrollContent;
            return scrollRect;
        }

        private static RectTransform EnsureScrollViewport(RectTransform scrollRectTransform)
        {
            Transform existing = scrollRectTransform.Find("Viewport");
            RectTransform viewportRect;
            if (existing == null)
            {
                viewportRect = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
                viewportRect.SetParent(scrollRectTransform, false);
            }
            else
            {
                viewportRect = existing as RectTransform ?? ReplaceWithRectTransform(existing, scrollRectTransform);
                if (viewportRect.GetComponent<Image>() == null)
                {
                    viewportRect.gameObject.AddComponent<Image>();
                }

                if (viewportRect.GetComponent<Mask>() == null)
                {
                    viewportRect.gameObject.AddComponent<Mask>();
                }
            }

            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = viewportRect.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            Mask mask = viewportRect.GetComponent<Mask>();
            mask.showMaskGraphic = false;
            return viewportRect;
        }

        private static RectTransform EnsureScrollContent(RectTransform viewportRect)
        {
            Transform existing = viewportRect.Find("LogContent");
            RectTransform contentRect;
            if (existing == null)
            {
                contentRect = new GameObject("LogContent", typeof(RectTransform)).GetComponent<RectTransform>();
                contentRect.SetParent(viewportRect, false);
            }
            else
            {
                contentRect = existing as RectTransform ?? ReplaceWithRectTransform(existing, viewportRect);
            }

            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            return contentRect;
        }

        private static RectTransform EnsureLogEntriesContainer(RectTransform scrollContent)
        {
            Transform existing = scrollContent.Find("LogEntriesContainer");
            RectTransform container;
            if (existing == null)
            {
                container = new GameObject("LogEntriesContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter))
                    .GetComponent<RectTransform>();
                container.SetParent(scrollContent, false);
            }
            else
            {
                container = existing as RectTransform ?? ReplaceWithRectTransform(existing, scrollContent);
                if (container.GetComponent<VerticalLayoutGroup>() == null)
                {
                    container.gameObject.AddComponent<VerticalLayoutGroup>();
                }

                if (container.GetComponent<ContentSizeFitter>() == null)
                {
                    container.gameObject.AddComponent<ContentSizeFitter>();
                }
            }

            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.anchoredPosition = Vector2.zero;
            container.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(10, 10, 0, 10);

            ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return container;
        }

        private static Text EnsureLogEntryTemplate(RectTransform logEntriesContainer)
        {
            Transform existing = logEntriesContainer.Find("LogEntryTemplate");
            Text template;
            if (existing == null)
            {
                var rowRect = new GameObject("LogEntryTemplate", typeof(RectTransform), typeof(Text), typeof(LayoutElement))
                    .GetComponent<RectTransform>();
                rowRect.SetParent(logEntriesContainer, false);
                template = rowRect.GetComponent<Text>();
            }
            else
            {
                template = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
                if (template.GetComponent<LayoutElement>() == null)
                {
                    template.gameObject.AddComponent<LayoutElement>();
                }
            }

            RectTransform templateRect = template.rectTransform;
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(1f, 1f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.offsetMin = Vector2.zero;
            templateRect.offsetMax = Vector2.zero;

            template.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            template.fontSize = 19;
            template.color = Color.white;
            template.alignment = TextAnchor.UpperLeft;
            template.horizontalOverflow = HorizontalWrapMode.Wrap;
            template.verticalOverflow = VerticalWrapMode.Overflow;
            template.supportRichText = true;
            template.text = "Sample log entry";

            LayoutElement element = template.GetComponent<LayoutElement>();
            element.preferredHeight = 25f;

            template.gameObject.SetActive(false);
            return template;
        }

        private static RectTransform EnsureCombatLogHoverTooltip(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("CombatLogHoverTooltip");
            RectTransform root;
            if (existing == null)
            {
                root = new GameObject("CombatLogHoverTooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                root.SetParent(canvasRoot, false);
            }
            else
            {
                root = existing as RectTransform ?? ReplaceWithRectTransform(existing, canvasRoot);
                if (root.GetComponent<Image>() == null)
                {
                    root.gameObject.AddComponent<Image>();
                }
            }

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(450f, 130f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.86f);

            Text tooltipText = EnsureChildText(root, "TooltipText", new Vector2(10f, -8f), new Vector2(430f, 114f), 15, TextAnchor.MiddleLeft);
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            root.gameObject.SetActive(false);
            return root;
        }
    }
}
#endif
