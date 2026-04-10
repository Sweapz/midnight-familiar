#if UNITY_EDITOR
using System.Collections.Generic;
using MidnightFamiliar.Combat.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.Editor
{
    public static class BattleHudCanvasCreator
    {
        [MenuItem("Midnight Familiar/Combat/Create Battle HUD Canvas")]
        public static void CreateBattleHudCanvas()
        {
            Canvas canvas = FindOrCreateBattleHudCanvas();
            if (canvas == null)
            {
                Debug.LogError("Could not create or find BattleHUDCanvas.");
                return;
            }

            RemoveMissingScriptsRecursive(canvas.gameObject);
            EnsureEventSystemExists();

            Transform hudRoot = canvas.transform.Find("BattleHUD");
            if (hudRoot == null)
            {
                hudRoot = new GameObject("BattleHUD", typeof(RectTransform)).transform;
                hudRoot.SetParent(canvas.transform, false);
            }

            var hudRootRect = hudRoot as RectTransform;
            if (hudRootRect == null)
            {
                hudRootRect = ReplaceWithRectTransform(hudRoot, canvas.transform);
                hudRoot = hudRootRect;
            }

            hudRootRect.anchorMin = new Vector2(0f, 1f);
            hudRootRect.anchorMax = new Vector2(1f, 1f);
            hudRootRect.pivot = new Vector2(0.5f, 1f);
            hudRootRect.anchoredPosition = Vector2.zero;
            hudRootRect.sizeDelta = new Vector2(0f, 140f);

            BattleHudController hudController = hudRoot.GetComponent<BattleHudController>();
            if (hudController == null)
            {
                hudController = hudRoot.gameObject.AddComponent<BattleHudController>();
            }

            RemoveMissingScriptsRecursive(hudRoot.gameObject);

            Text roundLabel = EnsureRoundLabel(hudRootRect);
            RectTransform container = EnsureTurnOrderContainer(hudRootRect);
            BattleTurnOrderEntryView template = EnsureItemTemplate(container);
            RectTransform hoverTooltipRoot = EnsureHudHoverTooltip(canvas.transform);
            Text hoverNameLabel = FindChildText(hoverTooltipRoot, "NameText");
            Text hoverStatsLabel = FindChildText(hoverTooltipRoot, "StatsText");
            Text hoverEffectsLabel = FindChildText(hoverTooltipRoot, "EffectsText");
            BattleActionPanelController actionPanel = EnsureActionPanel(canvas.transform);
            BattleCombatLogPanelController combatLogPanel = EnsureCombatLogPanel(canvas.transform);
            BattleOpportunityPanelController opportunityPanel = EnsureOpportunityPanel(canvas.transform);

            WireHudController(
                hudController,
                roundLabel,
                container,
                template,
                hoverTooltipRoot,
                hoverNameLabel,
                hoverStatsLabel,
                hoverEffectsLabel);
            WireBattleController(hudController, actionPanel, combatLogPanel, opportunityPanel);

            Selection.activeGameObject = hudRoot.gameObject;
            EditorGUIUtility.PingObject(hudRoot.gameObject);
            Debug.Log("Created/updated Battle HUD Canvas and wired BattleController.");
        }

        [MenuItem("Midnight Familiar/Combat/Cleanup Missing Scripts In Scene")]
        public static void CleanupMissingScriptsInScene()
        {
            int cleanedObjects = 0;
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                cleanedObjects += RemoveMissingScriptsRecursive(roots[i]);
            }

            Debug.Log($"Removed missing-script components from {cleanedObjects} object(s) in the active scene.");
        }

        private static Canvas FindOrCreateBattleHudCanvas()
        {
            GameObject existing = GameObject.Find("BattleHUDCanvas");
            if (existing != null)
            {
                Canvas existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null)
                {
                    EnsureCanvasDefaults(existingCanvas);
                    return existingCanvas;
                }
            }

            var canvasGo = new GameObject("BattleHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            EnsureCanvasDefaults(canvas);
            return canvas;
        }

        private static void EnsureCanvasDefaults(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static Text EnsureRoundLabel(RectTransform hudRoot)
        {
            Transform existing = hudRoot.Find("RoundLabel");
            Text label;
            if (existing == null)
            {
                var go = new GameObject("RoundLabel", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(hudRoot, false);
                label = go.GetComponent<Text>();
            }
            else
            {
                label = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
            }

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -90f);
            rect.sizeDelta = new Vector2(200f, 24f);

            label.text = "Round -";
            label.alignment = TextAnchor.MiddleLeft;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            return label;
        }

        private static RectTransform EnsureTurnOrderContainer(RectTransform hudRoot)
        {
            Transform existing = hudRoot.Find("TurnOrderContainer");
            RectTransform container;
            if (existing == null)
            {
                var go = new GameObject("TurnOrderContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(hudRoot, false);
                container = go.GetComponent<RectTransform>();
            }
            else
            {
                container = existing as RectTransform;
                if (container == null)
                {
                    container = ReplaceWithRectTransform(existing, hudRoot);
                    existing = container;
                }
            }

            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.anchoredPosition = new Vector2(0f, -16f);
            container.sizeDelta = new Vector2(-24f, 82f);

            HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 0, 0);
            return container;
        }

        private static BattleTurnOrderEntryView EnsureItemTemplate(RectTransform container)
        {
            Transform existing = container.Find("TurnOrderItemTemplate");
            BattleTurnOrderEntryView view;
            if (existing == null)
            {
                var go = new GameObject("TurnOrderItemTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(BattleTurnOrderEntryView));
                go.transform.SetParent(container, false);
                view = go.GetComponent<BattleTurnOrderEntryView>();
            }
            else
            {
                view = existing.GetComponent<BattleTurnOrderEntryView>() ?? existing.gameObject.AddComponent<BattleTurnOrderEntryView>();
            }

            RemoveMissingScriptsRecursive(view.gameObject);

            Image background = view.GetComponent<Image>();
            background.color = new Color(0.17f, 0.21f, 0.28f, 0.88f);

            LayoutElement layout = view.GetComponent<LayoutElement>();
            layout.preferredWidth = 180f;
            layout.preferredHeight = 70f;

            RectTransform rect = view.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 70f);

            Image portrait = EnsureChildImage(view.transform, "Portrait", new Vector2(8f, -8f), new Vector2(54f, 54f));
            Text name = EnsureChildText(view.transform, "NameLabel", new Vector2(70f, -10f), new Vector2(104f, 24f), 15, TextAnchor.UpperLeft);
            Text hp = EnsureChildText(view.transform, "HpLabel", new Vector2(70f, -34f), new Vector2(104f, 24f), 14, TextAnchor.UpperLeft);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("portraitImage").objectReferenceValue = portrait;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("hpLabel").objectReferenceValue = hp;
            so.ApplyModifiedPropertiesWithoutUndo();

            view.gameObject.SetActive(false);
            return view;
        }

        private static Image EnsureChildImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            Image image;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                image = go.GetComponent<Image>();
            }
            else
            {
                image = existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>();
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return image;
        }

        private static Text EnsureChildText(Transform parent, string name, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor alignment)
        {
            Transform existing = parent.Find(name);
            Text text;
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                text = go.GetComponent<Text>();
            }
            else
            {
                text = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            return text;
        }

        private static void WireHudController(
            BattleHudController hudController,
            Text roundLabel,
            RectTransform turnOrderContainer,
            BattleTurnOrderEntryView template,
            RectTransform hoverTooltipRoot,
            Text hoverNameLabel,
            Text hoverStatsLabel,
            Text hoverEffectsLabel)
        {
            SerializedObject so = new SerializedObject(hudController);
            so.FindProperty("turnOrderContainer").objectReferenceValue = turnOrderContainer;
            so.FindProperty("turnOrderItemPrefab").objectReferenceValue = template;
            so.FindProperty("roundLabel").objectReferenceValue = roundLabel;
            so.FindProperty("hoverTooltipRoot").objectReferenceValue = hoverTooltipRoot;
            so.FindProperty("hoverNameLabel").objectReferenceValue = hoverNameLabel;
            so.FindProperty("hoverStatsLabel").objectReferenceValue = hoverStatsLabel;
            so.FindProperty("hoverEffectsLabel").objectReferenceValue = hoverEffectsLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hudController);
        }

        private static BattleActionPanelController EnsureActionPanel(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("BattleActionPanel");
            RectTransform panelRect;
            if (existing == null)
            {
                panelRect = new GameObject("BattleActionPanel", typeof(RectTransform), typeof(Image), typeof(BattleActionPanelController))
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
                if (panelRect.GetComponent<BattleActionPanelController>() == null)
                {
                    panelRect.gameObject.AddComponent<BattleActionPanelController>();
                }
            }

            RemoveMissingScriptsRecursive(panelRect.gameObject);

            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(16f, 16f);
            panelRect.sizeDelta = new Vector2(520f, 260f);

            Image background = panelRect.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);

            Button endTurnButton = EnsureButton(panelRect, "EndTurnButton", "End Turn", new Vector2(8f, 172f), new Vector2(132f, 56f));

            RectTransform statusBox = EnsurePanel(panelRect, "StatusBox", new Vector2(8f, 84f), new Vector2(256f, 84f));
            Image actorPortrait = EnsureChildImage(statusBox, "ActorPortrait", new Vector2(6f, -6f), new Vector2(72f, 72f));
            Text actorName = EnsureChildText(statusBox, "ActorName", new Vector2(83.4f, -3.2f), new Vector2(300f, 24f), 20, TextAnchor.MiddleLeft);
            Text stats = EnsureChildText(statusBox, "StatsText", new Vector2(83.4f, -31.9f), new Vector2(230f, 64f), 16, TextAnchor.UpperLeft);

            RectTransform actionRow = EnsureActionRow(panelRect);
            var buttons = new List<Button>(5);
            float x = 0f;
            for (int i = 0; i < 5; i++)
            {
                Button button = EnsureButton(actionRow, $"ActionButton{i + 1}", $"Action {i + 1}", new Vector2(x, 0f), new Vector2(96f, 72f));
                buttons.Add(button);
                x += 102f;
            }

            BattleActionPanelController controller = panelRect.GetComponent<BattleActionPanelController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = panelRect;
            so.FindProperty("actorNameText").objectReferenceValue = actorName;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("actorPortraitImage").objectReferenceValue = actorPortrait;
            so.FindProperty("endTurnButton").objectReferenceValue = endTurnButton;

            SerializedProperty actionButtonsProp = so.FindProperty("actionButtons");
            actionButtonsProp.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
            {
                actionButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static BattleCombatLogPanelController EnsureCombatLogPanel(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("BattleCombatLogPanel");
            RectTransform panelRect;
            if (existing == null)
            {
                panelRect = new GameObject("BattleCombatLogPanel", typeof(RectTransform), typeof(BattleCombatLogPanelController))
                    .GetComponent<RectTransform>();
                panelRect.SetParent(canvasRoot, false);
            }
            else
            {
                panelRect = existing as RectTransform ?? ReplaceWithRectTransform(existing, canvasRoot);
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
            panelRect.sizeDelta = new Vector2(520f, 280f);

            RectTransform content = EnsurePanel(panelRect, "Content", new Vector2(0f, 0f), new Vector2(520f, 236f));
            Text title = EnsureChildText(content, "Title", new Vector2(10f, -8f), new Vector2(320f, 32f), 25, TextAnchor.UpperLeft);
            title.text = "Combat Log";
            Image divider = EnsureChildImage(content, "TitleDivider", new Vector2(5f, -38f), new Vector2(510f, 2f));
            divider.color = new Color(1f, 1f, 1f, 0.35f);
            ScrollRect logScrollRect = EnsureCombatLogScrollView(content);
            Text logText = EnsureChildText(content, "LogText", new Vector2(10f, -30f), new Vector2(340f, 196f), 13, TextAnchor.UpperLeft);
            logText.text = "No log entries yet.";
            logText.gameObject.SetActive(false);
            RectTransform entriesContainer = EnsureLogEntriesContainer(logScrollRect.viewport);
            logScrollRect.content = entriesContainer;
            Text logEntryTemplate = EnsureLogEntryTemplate(entriesContainer);

            Button toggleButton = EnsureButton(panelRect, "ToggleLogButton", "Hide Log", new Vector2(408f, 242f), new Vector2(112f, 32f));

            BattleCombatLogPanelController controller = panelRect.GetComponent<BattleCombatLogPanelController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = panelRect;
            so.FindProperty("contentRoot").objectReferenceValue = content;
            so.FindProperty("toggleButton").objectReferenceValue = toggleButton;
            so.FindProperty("toggleButtonLabel").objectReferenceValue = toggleButton.GetComponentInChildren<Text>(true);
            so.FindProperty("logText").objectReferenceValue = logText;
            so.FindProperty("logScrollRect").objectReferenceValue = logScrollRect;
            so.FindProperty("logEntriesContainer").objectReferenceValue = entriesContainer;
            so.FindProperty("logEntryTemplate").objectReferenceValue = logEntryTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            return controller;
        }

        private static BattleOpportunityPanelController EnsureOpportunityPanel(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("BattleOpportunityPanel");
            RectTransform panelRect;
            if (existing == null)
            {
                panelRect = new GameObject("BattleOpportunityPanel", typeof(RectTransform), typeof(Image), typeof(BattleOpportunityPanelController))
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

                if (panelRect.GetComponent<BattleOpportunityPanelController>() == null)
                {
                    panelRect.gameObject.AddComponent<BattleOpportunityPanelController>();
                }
            }

            RemoveMissingScriptsRecursive(panelRect.gameObject);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -120f);
            panelRect.sizeDelta = new Vector2(700f, 190f);

            Image background = panelRect.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.84f);

            Text promptLabel = EnsureChildText(panelRect, "PromptLabel", new Vector2(16f, -14f), new Vector2(668f, 34f), 21, TextAnchor.MiddleLeft);
            RectTransform actionsRow = EnsureOpportunityActionsRow(panelRect);
            Button template = EnsureButton(actionsRow, "OpportunityActionTemplate", "Basic Strike", new Vector2(0f, 0f), new Vector2(208f, 62f));
            Button decline = EnsureButton(panelRect, "DeclineButton", "Skip", new Vector2(552f, 14f), new Vector2(132f, 46f));
            template.gameObject.SetActive(false);

            BattleOpportunityPanelController controller = panelRect.GetComponent<BattleOpportunityPanelController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = panelRect;
            so.FindProperty("promptLabel").objectReferenceValue = promptLabel;
            so.FindProperty("actionsContainer").objectReferenceValue = actionsRow;
            so.FindProperty("actionButtonTemplate").objectReferenceValue = template;
            so.FindProperty("declineButton").objectReferenceValue = decline;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            panelRect.gameObject.SetActive(false);
            return controller;
        }

        private static RectTransform EnsureLogEntriesContainer(RectTransform contentRoot)
        {
            Transform existing = contentRoot.Find("LogEntriesContainer");
            RectTransform container;
            if (existing == null)
            {
                container = new GameObject(
                    "LogEntriesContainer",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                container.SetParent(contentRoot, false);
            }
            else
            {
                container = existing as RectTransform ?? ReplaceWithRectTransform(existing, contentRoot);
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
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            return container;
        }

        private static ScrollRect EnsureCombatLogScrollView(RectTransform contentRoot)
        {
            Transform existing = contentRoot.Find("LogScrollView");
            RectTransform scrollRectTransform;
            ScrollRect scrollRect;
            if (existing == null)
            {
                scrollRectTransform = new GameObject(
                    "LogScrollView",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(ScrollRect)).GetComponent<RectTransform>();
                scrollRectTransform.SetParent(contentRoot, false);
                scrollRect = scrollRectTransform.GetComponent<ScrollRect>();
            }
            else
            {
                scrollRectTransform = existing as RectTransform ?? ReplaceWithRectTransform(existing, contentRoot);
                if (scrollRectTransform.GetComponent<Image>() == null)
                {
                    scrollRectTransform.gameObject.AddComponent<Image>();
                }

                scrollRect = scrollRectTransform.GetComponent<ScrollRect>() ?? scrollRectTransform.gameObject.AddComponent<ScrollRect>();
            }

            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, -43f);

            Image scrollImage = scrollRectTransform.GetComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);

            RectTransform viewport = EnsureScrollViewport(scrollRectTransform);
            RectTransform content = EnsureScrollContent(viewport);
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            return scrollRect;
        }

        private static RectTransform EnsureScrollViewport(RectTransform scrollView)
        {
            Transform existing = scrollView.Find("Viewport");
            RectTransform viewport;
            if (existing == null)
            {
                viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
                viewport.SetParent(scrollView, false);
            }
            else
            {
                viewport = existing as RectTransform ?? ReplaceWithRectTransform(existing, scrollView);
                if (viewport.GetComponent<Image>() == null)
                {
                    viewport.gameObject.AddComponent<Image>();
                }
                if (viewport.GetComponent<RectMask2D>() == null)
                {
                    viewport.gameObject.AddComponent<RectMask2D>();
                }
            }

            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            return viewport;
        }

        private static RectTransform EnsureScrollContent(RectTransform viewport)
        {
            Transform existing = viewport.Find("Content");
            RectTransform content;
            if (existing == null)
            {
                content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
            }
            else
            {
                content = existing as RectTransform ?? ReplaceWithRectTransform(existing, viewport);
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            return content;
        }

        private static Text EnsureLogEntryTemplate(RectTransform container)
        {
            Transform existing = container.Find("LogEntryTemplate");
            Text templateText;
            if (existing == null)
            {
                templateText = new GameObject(
                    "LogEntryTemplate",
                    typeof(RectTransform),
                    typeof(Text),
                    typeof(LayoutElement)).GetComponent<Text>();
                templateText.transform.SetParent(container, false);
            }
            else
            {
                templateText = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
                if (templateText.GetComponent<LayoutElement>() == null)
                {
                    templateText.gameObject.AddComponent<LayoutElement>();
                }
            }

            RectTransform rect = templateText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            templateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            templateText.fontSize = 19;
            templateText.color = Color.white;
            templateText.alignment = TextAnchor.UpperLeft;
            templateText.horizontalOverflow = HorizontalWrapMode.Wrap;
            templateText.verticalOverflow = VerticalWrapMode.Overflow;
            templateText.supportRichText = true;
            templateText.text = "Log Entry Template";

            LayoutElement layoutElement = templateText.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 28f;

            templateText.gameObject.SetActive(false);
            return templateText;
        }

        private static RectTransform EnsureHudHoverTooltip(Transform canvasRoot)
        {
            Transform existing = canvasRoot.Find("HoverTooltip");
            RectTransform root;
            if (existing == null)
            {
                root = new GameObject("HoverTooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
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
            root.sizeDelta = new Vector2(320f, 220f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.82f);

            EnsureChildText(root, "NameText", new Vector2(10f, -8f), new Vector2(300f, 26f), 17, TextAnchor.UpperLeft);
            EnsureChildText(root, "StatsText", new Vector2(10f, -36f), new Vector2(300f, 108f), 14, TextAnchor.UpperLeft);
            EnsureChildText(root, "EffectsText", new Vector2(10f, -146f), new Vector2(300f, 68f), 14, TextAnchor.UpperLeft);
            root.gameObject.SetActive(false);
            return root;
        }

        private static Text FindChildText(RectTransform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static RectTransform EnsurePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect;
            if (existing == null)
            {
                rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            else
            {
                rect = existing as RectTransform ?? ReplaceWithRectTransform(existing, parent);
                if (rect.GetComponent<Image>() == null)
                {
                    rect.gameObject.AddComponent<Image>();
                }
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            rect.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            return rect;
        }

        private static RectTransform EnsureActionRow(RectTransform panelRect)
        {
            Transform existing = panelRect.Find("ActionRow");
            RectTransform row;
            if (existing == null)
            {
                row = new GameObject("ActionRow", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(panelRect, false);
            }
            else
            {
                row = existing as RectTransform ?? ReplaceWithRectTransform(existing, panelRect);
            }

            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(0f, 0f);
            row.pivot = new Vector2(0f, 0f);
            row.anchoredPosition = new Vector2(8f, 8f);
            row.sizeDelta = new Vector2(504f, 72f);
            return row;
        }

        private static RectTransform EnsureOpportunityActionsRow(RectTransform panelRect)
        {
            Transform existing = panelRect.Find("ActionsRow");
            RectTransform row;
            if (existing == null)
            {
                row = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                row.SetParent(panelRect, false);
            }
            else
            {
                row = existing as RectTransform ?? ReplaceWithRectTransform(existing, panelRect);
                if (row.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    row.gameObject.AddComponent<HorizontalLayoutGroup>();
                }
            }

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
            return row;
        }

        private static Button EnsureButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            RectTransform rect;
            Button button;
            if (existing == null)
            {
                rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                button = rect.GetComponent<Button>();
            }
            else
            {
                rect = existing as RectTransform ?? ReplaceWithRectTransform(existing, parent);
                if (rect.GetComponent<Image>() == null)
                {
                    rect.gameObject.AddComponent<Image>();
                }
                button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>();
            }

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Image image = rect.GetComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            Text text = EnsureChildText(rect, "Label", Vector2.zero, size, 16, TextAnchor.MiddleCenter);
            RectTransform labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            text.text = label;
            return button;
        }

        private static void WireBattleController(
            BattleHudController hudController,
            BattleActionPanelController actionPanelController,
            BattleCombatLogPanelController combatLogPanelController,
            BattleOpportunityPanelController opportunityPanelController)
        {
            BattleController battleController = Object.FindFirstObjectByType<BattleController>();
            if (battleController == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(battleController);
            SerializedProperty hudProp = so.FindProperty("hudController");
            if (hudProp != null)
            {
                hudProp.objectReferenceValue = hudController;
            }

            SerializedProperty actionPanelProp = so.FindProperty("actionPanelController");
            if (actionPanelProp != null)
            {
                actionPanelProp.objectReferenceValue = actionPanelController;
            }

            SerializedProperty combatLogProp = so.FindProperty("combatLogPanelController");
            if (combatLogProp != null)
            {
                combatLogProp.objectReferenceValue = combatLogPanelController;
            }

            SerializedProperty opportunityPanelProp = so.FindProperty("opportunityPanelController");
            if (opportunityPanelProp != null)
            {
                opportunityPanelProp.objectReferenceValue = opportunityPanelController;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(battleController);
        }

        private static void EnsureEventSystemExists()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static int RemoveMissingScriptsRecursive(GameObject go)
        {
            if (go == null)
            {
                return 0;
            }

            int cleaned = 0;
            int before = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            int after = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (before != after)
            {
                cleaned++;
            }

            foreach (Transform child in go.transform)
            {
                cleaned += RemoveMissingScriptsRecursive(child.gameObject);
            }

            return cleaned;
        }

        private static RectTransform ReplaceWithRectTransform(Transform source, Transform parent)
        {
            var replacement = new GameObject(source.name, typeof(RectTransform)).GetComponent<RectTransform>();
            replacement.SetParent(parent, false);
            replacement.SetSiblingIndex(source.GetSiblingIndex());
            replacement.localPosition = source.localPosition;
            replacement.localRotation = source.localRotation;
            replacement.localScale = source.localScale;

            while (source.childCount > 0)
            {
                source.GetChild(0).SetParent(replacement, true);
            }

            Object.DestroyImmediate(source.gameObject);
            return replacement;
        }
    }
}
#endif
