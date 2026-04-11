using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleHudController : MonoBehaviour
    {
        private static readonly Vector2 HoverTooltipSize = new Vector2(480f, 240f);
        private static readonly Vector2 HoverNamePosition = new Vector2(10f, -8f);
        private static readonly Vector2 HoverNameSize = new Vector2(460f, 26f);
        private static readonly Vector2 HoverStatsPosition = new Vector2(10f, -40f);
        private static readonly Vector2 HoverStatsSize = new Vector2(220f, 190f);
        private static readonly Vector2 HoverEffectsPosition = new Vector2(248f, -40f);
        private static readonly Vector2 HoverEffectsSize = new Vector2(220f, 190f);

        [Header("Turn Order")]
        [SerializeField] private RectTransform turnOrderContainer;
        [SerializeField] private BattleTurnOrderEntryView turnOrderItemPrefab;
        [SerializeField] private Text roundLabel;

        [Header("Hover Tooltip")]
        [SerializeField] private RectTransform hoverTooltipRoot;
        [SerializeField] private Text hoverNameLabel;
        [SerializeField] private Text hoverStatsLabel;
        [SerializeField] private Text hoverEffectsLabel;
        [SerializeField] private Vector2 hoverTooltipOffset = new Vector2(20f, 16f);

        private readonly List<BattleTurnOrderEntryView> _entryPool = new List<BattleTurnOrderEntryView>();
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _usePinnedScreenPoint;
        private Vector2 _pinnedScreenPoint;

        public event Action<string> TurnOrderHoverStarted;
        public event Action<string> TurnOrderHoverEnded;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Update()
        {
            if (hoverTooltipRoot != null && hoverTooltipRoot.gameObject.activeSelf)
            {
                if (_usePinnedScreenPoint)
                {
                    PositionHoverTooltipAtScreenPoint(_pinnedScreenPoint);
                }
                else
                {
                    PositionHoverTooltipAtCursor();
                }
            }
        }

        public void SetTurnOrder(IReadOnlyList<TurnOrderHudEntry> entries, string currentCombatantId, int roundNumber)
        {
            AutoResolveReferences();
            if (roundLabel != null)
            {
                roundLabel.text = $"Round {Mathf.Max(1, roundNumber)}";
            }

            if (turnOrderContainer == null || turnOrderItemPrefab == null)
            {
                return;
            }

            int count = entries != null ? entries.Count : 0;
            EnsurePoolSize(count);

            for (int i = 0; i < _entryPool.Count; i++)
            {
                BattleTurnOrderEntryView view = _entryPool[i];
                bool shouldShow = i < count;
                view.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                {
                    continue;
                }

                TurnOrderHudEntry entry = entries[i];
                bool isCurrent = !string.IsNullOrWhiteSpace(currentCombatantId) &&
                                 currentCombatantId == entry.CombatantId;
                view.Bind(entry, isCurrent);
            }
        }

        public void ClearTurnOrder()
        {
            AutoResolveReferences();
            if (roundLabel != null)
            {
                roundLabel.text = "Round -";
            }

            for (int i = 0; i < _entryPool.Count; i++)
            {
                if (_entryPool[i] != null)
                {
                    _entryPool[i].gameObject.SetActive(false);
                }
            }

            HideHoverTooltip();
        }

        public void ShowHoverTooltip(string nameText, string statsText, string effectsText, Vector2? screenPoint = null)
        {
            AutoResolveReferences();
            if (hoverTooltipRoot == null)
            {
                return;
            }

            hoverTooltipRoot.gameObject.SetActive(true);
            if (hoverNameLabel != null)
            {
                hoverNameLabel.text = string.IsNullOrWhiteSpace(nameText) ? "Cuid" : nameText;
            }

            if (hoverStatsLabel != null)
            {
                hoverStatsLabel.text = statsText ?? string.Empty;
            }

            if (hoverEffectsLabel != null)
            {
                hoverEffectsLabel.text = effectsText ?? string.Empty;
            }

            if (screenPoint.HasValue)
            {
                _usePinnedScreenPoint = true;
                _pinnedScreenPoint = screenPoint.Value;
                PositionHoverTooltipAtScreenPoint(_pinnedScreenPoint);
            }
            else
            {
                _usePinnedScreenPoint = false;
                PositionHoverTooltipAtCursor();
            }
        }

        public void SetHoverTooltipScreenPoint(Vector2 screenPoint)
        {
            if (hoverTooltipRoot == null || !hoverTooltipRoot.gameObject.activeSelf)
            {
                return;
            }

            _usePinnedScreenPoint = true;
            _pinnedScreenPoint = screenPoint;
            PositionHoverTooltipAtScreenPoint(screenPoint);
        }

        public void HideHoverTooltip()
        {
            if (hoverTooltipRoot != null)
            {
                hoverTooltipRoot.gameObject.SetActive(false);
            }

            _usePinnedScreenPoint = false;
        }

        public bool IsPointerOverHoverTooltip()
        {
            if (hoverTooltipRoot == null || !hoverTooltipRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(hoverTooltipRoot, Input.mousePosition, cam);
        }

        private void EnsurePoolSize(int required)
        {
            while (_entryPool.Count < required)
            {
                BattleTurnOrderEntryView instance = Instantiate(turnOrderItemPrefab, turnOrderContainer);
                instance.gameObject.SetActive(false);
                instance.HoverStarted += HandleEntryHoverStarted;
                instance.HoverEnded += HandleEntryHoverEnded;
                _entryPool.Add(instance);
            }
        }

        private void AutoResolveReferences()
        {
            if (turnOrderContainer == null)
            {
                Transform container = transform.Find("TurnOrderContainer");
                if (container != null)
                {
                    turnOrderContainer = container as RectTransform;
                }
            }

            if (roundLabel == null)
            {
                Transform label = transform.Find("RoundLabel");
                if (label != null)
                {
                    roundLabel = label.GetComponent<Text>();
                }
            }

            if (turnOrderItemPrefab == null && turnOrderContainer != null)
            {
                turnOrderItemPrefab = turnOrderContainer.GetComponentInChildren<BattleTurnOrderEntryView>(true);
            }

            EnsureTooltipReferences();
        }

        private void EnsureTooltipReferences()
        {
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

            if (hoverTooltipRoot == null)
            {
                Transform existing = _canvasRect != null ? _canvasRect.Find("HoverTooltip") : null;
                if (existing != null)
                {
                    hoverTooltipRoot = existing as RectTransform;
                }
            }

            if (hoverTooltipRoot == null)
            {
                hoverTooltipRoot = CreateHoverTooltipPanel();
            }

            if (hoverTooltipRoot == null)
            {
                return;
            }

            // Keep tooltip in center-anchor space so local canvas points map directly.
            hoverTooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            hoverTooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            hoverTooltipRoot.pivot = new Vector2(0.5f, 0.5f);

            if (hoverNameLabel == null)
            {
                Transform t = hoverTooltipRoot.Find("NameText");
                hoverNameLabel = t != null ? t.GetComponent<Text>() : null;
            }

            if (hoverStatsLabel == null)
            {
                Transform t = hoverTooltipRoot.Find("StatsText");
                hoverStatsLabel = t != null ? t.GetComponent<Text>() : null;
            }

            if (hoverEffectsLabel == null)
            {
                Transform t = hoverTooltipRoot.Find("EffectsText");
                hoverEffectsLabel = t != null ? t.GetComponent<Text>() : null;
            }

            ApplyHoverTooltipLayout();

            if (hoverTooltipRoot != null)
            {
                hoverTooltipRoot.gameObject.SetActive(false);
            }
        }

        private RectTransform CreateHoverTooltipPanel()
        {
            if (_canvasRect == null)
            {
                return null;
            }

            var root = new GameObject("HoverTooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(_canvasRect, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = HoverTooltipSize;

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.82f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hoverNameLabel = CreateTooltipText(root, "NameText", font, 17, TextAnchor.UpperLeft, HoverNamePosition, HoverNameSize);
            hoverStatsLabel = CreateTooltipText(root, "StatsText", font, 14, TextAnchor.UpperLeft, HoverStatsPosition, HoverStatsSize);
            hoverEffectsLabel = CreateTooltipText(root, "EffectsText", font, 14, TextAnchor.UpperLeft, HoverEffectsPosition, HoverEffectsSize);
            ApplyHoverTooltipLayout();

            return root;
        }

        private void ApplyHoverTooltipLayout()
        {
            if (hoverTooltipRoot == null)
            {
                return;
            }

            hoverTooltipRoot.sizeDelta = HoverTooltipSize;
            ApplyTooltipTextLayout(hoverNameLabel, HoverNamePosition, HoverNameSize, TextAnchor.UpperLeft, wrap: false);
            ApplyTooltipTextLayout(hoverStatsLabel, HoverStatsPosition, HoverStatsSize, TextAnchor.UpperLeft, wrap: true);
            ApplyTooltipTextLayout(hoverEffectsLabel, HoverEffectsPosition, HoverEffectsSize, TextAnchor.UpperLeft, wrap: true);
        }

        private static void ApplyTooltipTextLayout(
            Text text,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment,
            bool wrap)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            text.alignment = alignment;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static Text CreateTooltipText(
            RectTransform parent,
            string name,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var textRect = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            Text text = textRect.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = string.Empty;
            return text;
        }

        private void PositionHoverTooltipAtCursor()
        {
            PositionHoverTooltipAtScreenPoint(Input.mousePosition);
        }

        private void PositionHoverTooltipAtScreenPoint(Vector2 screenPoint)
        {
            if (hoverTooltipRoot == null || _canvasRect == null)
            {
                return;
            }

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    cam,
                    out Vector2 localCursor))
            {
                return;
            }

            Vector2 anchored = localCursor + hoverTooltipOffset;
            float halfWidth = hoverTooltipRoot.rect.width * 0.5f;
            float halfHeight = hoverTooltipRoot.rect.height * 0.5f;
            anchored.x = Mathf.Clamp(anchored.x, _canvasRect.rect.xMin + halfWidth, _canvasRect.rect.xMax - halfWidth);
            anchored.y = Mathf.Clamp(anchored.y, _canvasRect.rect.yMin + halfHeight, _canvasRect.rect.yMax - halfHeight);
            hoverTooltipRoot.anchoredPosition = anchored;
        }

        private void HandleEntryHoverStarted(string combatantId)
        {
            TurnOrderHoverStarted?.Invoke(combatantId);
        }

        private void HandleEntryHoverEnded(string combatantId)
        {
            TurnOrderHoverEnded?.Invoke(combatantId);
        }
    }
}
