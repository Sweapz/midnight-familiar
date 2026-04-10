using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed partial class BattleCombatLogPanelController : MonoBehaviour
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

            _expandedPanelHeight = panelRoot != null && panelRoot.sizeDelta.y > 0f ? panelRoot.sizeDelta.y : 280f;

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
    }
}
