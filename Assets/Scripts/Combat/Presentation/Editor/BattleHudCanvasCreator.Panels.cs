#if UNITY_EDITOR
using System.Collections.Generic;
using MidnightFamiliar.Combat.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.Editor
{
    public static partial class BattleHudCanvasCreator
    {
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
    }
}
#endif
