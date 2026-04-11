using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed partial class BattleCombatLogPanelController
    {
        private const float TooltipMinHeight = 130f;
        private const float TooltipMaxHeight = 420f;
        private const float TooltipHorizontalPadding = 20f;
        private const float TooltipVerticalPadding = 16f;

        private void EnsureHoverTooltipReferences()
        {
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;

            if (lineHoverTooltipRoot == null)
            {
                Transform existing = _canvasRect != null ? _canvasRect.Find("CombatLogHoverTooltip") : null;
                if (existing != null)
                {
                    lineHoverTooltipRoot = existing as RectTransform;
                }
            }

            if (lineHoverTooltipRoot == null)
            {
                lineHoverTooltipRoot = CreateLineHoverTooltip();
            }

            if (lineHoverTooltipRoot == null)
            {
                return;
            }

            if (lineHoverTooltipText == null)
            {
                Transform t = lineHoverTooltipRoot.Find("TooltipText");
                lineHoverTooltipText = t != null ? t.GetComponent<Text>() : null;
            }

            lineHoverTooltipRoot.gameObject.SetActive(false);
        }

        private RectTransform CreateLineHoverTooltip()
        {
            if (_canvasRect == null)
            {
                return null;
            }

            var root = new GameObject("CombatLogHoverTooltip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(_canvasRect, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(450f, 130f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.86f);

            var textRect = new GameObject("TooltipText", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            textRect.SetParent(root, false);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);

            Text text = textRect.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            lineHoverTooltipText = text;
            return root;
        }

        private void UpdateLineHoverTooltip()
        {
            if (!_isContentVisible || _entries.Count == 0 || lineHoverTooltipRoot == null)
            {
                HideLineHoverTooltip();
                return;
            }

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (_isLineDetailPopupOpen)
            {
                bool stillInsidePopup = RectTransformUtility.RectangleContainsScreenPoint(
                    lineHoverTooltipRoot,
                    Input.mousePosition,
                    cam);

                if (stillInsidePopup && Input.GetMouseButtonDown(0))
                {
                    HideLineHoverTooltip();
                    return;
                }

                if (!stillInsidePopup)
                {
                    HideLineHoverTooltip();
                }

                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            int clickedIndex = -1;
            for (int i = 0; i < _entries.Count && i < _entryTextPool.Count; i++)
            {
                Text row = _entryTextPool[i];
                if (row == null || !row.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(row.rectTransform, Input.mousePosition, cam))
                {
                    clickedIndex = i;
                    break;
                }
            }

            if (clickedIndex < 0)
            {
                HideLineHoverTooltip();
                return;
            }

            LogEntry entry = _entries[clickedIndex];
            if (entry == null || string.IsNullOrWhiteSpace(entry.HoverDetail))
            {
                HideLineHoverTooltip();
                return;
            }

            ShowLineHoverTooltip(entry.HoverDetail, Input.mousePosition);
        }

        private void ShowLineHoverTooltip(string detail, Vector2 screenPoint)
        {
            if (lineHoverTooltipRoot == null || lineHoverTooltipText == null || _canvasRect == null)
            {
                return;
            }

            lineHoverTooltipText.text = detail;
            ResizeLineHoverTooltipToFitText(detail);
            lineHoverTooltipRoot.gameObject.SetActive(true);
            _isLineDetailPopupOpen = true;
            PositionLineHoverTooltip(screenPoint);
        }

        private void ResizeLineHoverTooltipToFitText(string detail)
        {
            if (lineHoverTooltipRoot == null || lineHoverTooltipText == null || string.IsNullOrEmpty(detail))
            {
                return;
            }

            float width = Mathf.Max(120f, lineHoverTooltipRoot.sizeDelta.x - TooltipHorizontalPadding);
            var settings = lineHoverTooltipText.GetGenerationSettings(new Vector2(width, 0f));
            float preferredTextHeight = lineHoverTooltipText.cachedTextGeneratorForLayout
                .GetPreferredHeight(detail, settings) / Mathf.Max(0.01f, lineHoverTooltipText.pixelsPerUnit);

            float canvasBound = _canvasRect != null ? _canvasRect.rect.height - 20f : TooltipMaxHeight;
            float maxHeight = Mathf.Max(TooltipMinHeight, Mathf.Min(TooltipMaxHeight, canvasBound));
            float targetHeight = Mathf.Clamp(preferredTextHeight + TooltipVerticalPadding, TooltipMinHeight, maxHeight);

            Vector2 size = lineHoverTooltipRoot.sizeDelta;
            size.y = targetHeight;
            lineHoverTooltipRoot.sizeDelta = size;
        }

        private void PositionLineHoverTooltip(Vector2 screenPoint)
        {
            if (lineHoverTooltipRoot == null || _canvasRect == null)
            {
                return;
            }

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, cam, out Vector2 localCursor))
            {
                return;
            }

            Vector2 anchored = localCursor + lineHoverTooltipOffset;
            float halfWidth = lineHoverTooltipRoot.rect.width * 0.5f;
            float halfHeight = lineHoverTooltipRoot.rect.height * 0.5f;
            anchored.x = Mathf.Clamp(anchored.x, _canvasRect.rect.xMin + halfWidth, _canvasRect.rect.xMax - halfWidth);
            anchored.y = Mathf.Clamp(anchored.y, _canvasRect.rect.yMin + halfHeight, _canvasRect.rect.yMax - halfHeight);
            lineHoverTooltipRoot.anchoredPosition = anchored;
        }

        private void HideLineHoverTooltip()
        {
            if (lineHoverTooltipRoot != null)
            {
                lineHoverTooltipRoot.gameObject.SetActive(false);
            }

            _isLineDetailPopupOpen = false;
        }
    }
}
