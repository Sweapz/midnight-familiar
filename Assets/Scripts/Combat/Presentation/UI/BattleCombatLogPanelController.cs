using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleCombatLogPanelController : MonoBehaviour
    {
        [Serializable]
        public sealed class LogEntry
        {
            public string DisplayText = string.Empty;
            public string HoverDetail = string.Empty;
        }

        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform contentRoot;

        [Header("Controls")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleButtonLabel;
        [SerializeField] private Text logText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private RectTransform logEntriesContainer;
        [SerializeField] private Text logEntryTemplate;
        [SerializeField] private float foldDurationSeconds = 0.15f;
        [SerializeField] private int logFontSize = 19;
        [SerializeField] private float minimumPanelWidth = 520f;

        [Header("Hover Tooltip")]
        [SerializeField] private RectTransform lineHoverTooltipRoot;
        [SerializeField] private Text lineHoverTooltipText;
        [SerializeField] private Vector2 lineHoverTooltipOffset = new Vector2(14f, 14f);

        private bool _isContentVisible = true;
        private bool _listenersBound;
        private float _expandedPanelHeight;
        private float _collapsedPanelHeight;
        private float _expandedButtonY;
        private float _collapsedButtonY;
        private Coroutine _foldRoutine;
        private bool _layoutCached;
        private readonly List<LogEntry> _entries = new List<LogEntry>(12);
        private readonly List<Text> _entryTextPool = new List<Text>(12);
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _isLineDetailPopupOpen;

        private void Awake()
        {
            AutoResolveReferences();
            BindListeners();
            CacheLayoutDefaults();
            ApplyVisibility();
        }

        private void Update()
        {
            UpdateLineHoverTooltip();
        }

        public void SetEntries(IReadOnlyList<LogEntry> entries)
        {
            AutoResolveReferences();
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries);
            }

            EnsureLogEntriesContainer();
            int count = _entries.Count > 0 ? _entries.Count : 1;
            EnsureEntryPoolSize(count);

            for (int i = 0; i < _entryTextPool.Count; i++)
            {
                Text row = _entryTextPool[i];
                bool active = i < count;
                row.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                row.supportRichText = true;
                row.fontSize = logFontSize;
                row.text = _entries.Count == 0
                    ? "No log entries yet."
                    : (_entries[i] != null ? _entries[i].DisplayText : string.Empty);

                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.preferredHeight = Mathf.Max(logFontSize + 6f, row.preferredHeight + 2f);
                }
            }

            if (logEntriesContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(logEntriesContainer);
            }

            if (logText != null)
            {
                logText.gameObject.SetActive(false);
            }

            if (logScrollRect != null)
            {
                logScrollRect.verticalNormalizedPosition = 1f;
            }
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

        private void BindListeners()
        {
            if (_listenersBound)
            {
                return;
            }

            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleContentVisibility);
            }

            _listenersBound = true;
        }

        private void ToggleContentVisibility()
        {
            _isContentVisible = !_isContentVisible;
            if (_foldRoutine != null)
            {
                StopCoroutine(_foldRoutine);
            }

            _foldRoutine = StartCoroutine(AnimateFold(_isContentVisible));
        }

        private void ApplyVisibility()
        {
            if (contentRoot != null)
            {
                contentRoot.gameObject.SetActive(_isContentVisible);
            }

            ApplyFoldInstant(_isContentVisible);

            if (toggleButtonLabel != null)
            {
                toggleButtonLabel.text = _isContentVisible ? "Hide Log" : "Show Log";
            }
        }

        private void CacheLayoutDefaults()
        {
            if (_layoutCached)
            {
                return;
            }

            if (panelRoot != null)
            {
                _expandedPanelHeight = panelRoot.sizeDelta.y > 0f ? panelRoot.sizeDelta.y : 280f;
            }
            else
            {
                _expandedPanelHeight = 280f;
            }

            RectTransform toggleRect = toggleButton != null ? toggleButton.transform as RectTransform : null;
            _expandedButtonY = toggleRect != null ? toggleRect.anchoredPosition.y : 242f;
            _collapsedPanelHeight = toggleRect != null && toggleRect.sizeDelta.y > 0f ? toggleRect.sizeDelta.y : 32f;
            _collapsedButtonY = 0f;
            _layoutCached = true;
        }

        private IEnumerator AnimateFold(bool showContent)
        {
            AutoResolveReferences();
            CacheLayoutDefaults();

            RectTransform toggleRect = toggleButton != null ? toggleButton.transform as RectTransform : null;
            if (panelRoot == null || toggleRect == null)
            {
                ApplyVisibility();
                yield break;
            }

            if (showContent && contentRoot != null)
            {
                contentRoot.gameObject.SetActive(true);
            }

            float startHeight = panelRoot.sizeDelta.y;
            float endHeight = showContent ? _expandedPanelHeight : _collapsedPanelHeight;
            float startButtonY = toggleRect.anchoredPosition.y;
            float endButtonY = showContent ? _expandedButtonY : _collapsedButtonY;

            float duration = Mathf.Max(0.01f, foldDurationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                ApplyFoldValues(Mathf.Lerp(startHeight, endHeight, eased), Mathf.Lerp(startButtonY, endButtonY, eased));
                yield return null;
            }

            ApplyFoldValues(endHeight, endButtonY);
            if (!showContent && contentRoot != null)
            {
                contentRoot.gameObject.SetActive(false);
            }

            if (toggleButtonLabel != null)
            {
                toggleButtonLabel.text = showContent ? "Hide Log" : "Show Log";
            }

            _foldRoutine = null;
        }

        private void ApplyFoldInstant(bool showContent)
        {
            RectTransform toggleRect = toggleButton != null ? toggleButton.transform as RectTransform : null;
            if (panelRoot == null || toggleRect == null)
            {
                return;
            }

            float targetHeight = showContent ? _expandedPanelHeight : _collapsedPanelHeight;
            float targetButtonY = showContent ? _expandedButtonY : _collapsedButtonY;
            ApplyFoldValues(targetHeight, targetButtonY);
        }

        private void ApplyFoldValues(float panelHeight, float buttonY)
        {
            if (panelRoot != null)
            {
                Vector2 size = panelRoot.sizeDelta;
                size.y = panelHeight;
                panelRoot.sizeDelta = size;
            }

            if (toggleButton != null)
            {
                RectTransform toggleRect = toggleButton.transform as RectTransform;
                if (toggleRect != null)
                {
                    Vector2 pos = toggleRect.anchoredPosition;
                    pos.y = buttonY;
                    toggleRect.anchoredPosition = pos;
                }
            }
        }

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
            text.alignment = TextAnchor.MiddleLeft;
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
            lineHoverTooltipRoot.gameObject.SetActive(true);
            _isLineDetailPopupOpen = true;
            PositionLineHoverTooltip(screenPoint);
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
