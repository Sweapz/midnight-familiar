using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleCombatLogPanelController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform contentRoot;

        [Header("Controls")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleButtonLabel;
        [SerializeField] private Text logText;
        [SerializeField] private float foldDurationSeconds = 0.15f;

        private bool _isContentVisible = true;
        private bool _listenersBound;
        private float _expandedPanelHeight;
        private float _collapsedPanelHeight;
        private float _expandedButtonY;
        private float _collapsedButtonY;
        private Coroutine _foldRoutine;
        private bool _layoutCached;

        private void Awake()
        {
            AutoResolveReferences();
            BindListeners();
            CacheLayoutDefaults();
            ApplyVisibility();
        }

        public void SetEntries(IReadOnlyList<string> entries)
        {
            AutoResolveReferences();
            if (logText != null)
            {
                logText.text = entries == null || entries.Count == 0
                    ? "No log entries yet."
                    : string.Join("\n", entries);
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
    }
}
