#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MidnightFamiliar.Combat.Presentation.UI;

namespace MidnightFamiliar.Combat.Presentation.Editor
{
    public static partial class BattleHudCanvasCreator
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
