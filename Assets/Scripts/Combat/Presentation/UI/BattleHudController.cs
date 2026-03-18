using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightFamiliar.Combat.Presentation.UI
{
    public sealed class BattleHudController : MonoBehaviour
    {
        [Header("Turn Order")]
        [SerializeField] private RectTransform turnOrderContainer;
        [SerializeField] private BattleTurnOrderEntryView turnOrderItemPrefab;
        [SerializeField] private Text roundLabel;

        private readonly List<BattleTurnOrderEntryView> _entryPool = new List<BattleTurnOrderEntryView>();

        private void Awake()
        {
            AutoResolveReferences();
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
        }

        private void EnsurePoolSize(int required)
        {
            while (_entryPool.Count < required)
            {
                BattleTurnOrderEntryView instance = Instantiate(turnOrderItemPrefab, turnOrderContainer);
                instance.gameObject.SetActive(false);
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
        }
    }
}
