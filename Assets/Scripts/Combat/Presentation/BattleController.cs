using System.Collections;
using System.Collections.Generic;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation.UI;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController : MonoBehaviour
    {
        [System.Serializable]
        private class CuidPrefabBinding
        {
            [SerializeField] public string speciesId;
            [SerializeField] public GameObject prefab;
        }

        private enum PlayerInputPhase
        {
            None = 0,
            SelectMove = 1,
            SelectAction = 2,
            SelectTarget = 3
        }

        [Header("Scene References")]
        [SerializeField] private BattleGridController gridController;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private BattleHudController hudController;
        [SerializeField] private BattleActionPanelController actionPanelController;
        [SerializeField] private BattleCombatLogPanelController combatLogPanelController;
        [SerializeField] private BattleOpportunityPanelController opportunityPanelController;
        [SerializeField] private GameObject defaultCuidViewPrefab;
        [SerializeField] private List<CuidPrefabBinding> cuidPrefabBindings = new List<CuidPrefabBinding>();

        [Header("Teams")]
        [SerializeField] private List<CuidDefinition> playerTeamDefinitions = new List<CuidDefinition>(4);
        [SerializeField] private List<CuidDefinition> enemyTeamDefinitions = new List<CuidDefinition>(4);

        [Header("Flow")]
        [SerializeField] private bool startBattleOnStart = true;
        [SerializeField] private float turnDelaySeconds = 0.7f;
        [SerializeField] private float moveDurationSeconds = 0.25f;
        [SerializeField] private int maxCombatLogEntries = 300;
        [SerializeField] private float hoverWorldYOffset = 1.2f;

        [Header("Move Markers")]
        [SerializeField] private bool showReachableMoveMarkers = true;
        [SerializeField] private Color reachableMarkerColor = new Color(0.2f, 1f, 0.4f, 0.6f);
        [SerializeField] private float reachableMarkerOutlineInset = 0.18f;
        [SerializeField] private float reachableMarkerYOffset = 0.04f;
        [SerializeField] private bool showActionRangeMarkers = true;
        [SerializeField] private Color actionMarkerColor = new Color(1f, 0.75f, 0.2f, 0.7f);
        [SerializeField] private float actionMarkerOutlineInset = 0.2f;
        [SerializeField] private float actionMarkerYOffset = 0.08f;
        [SerializeField] private float markerLineThickness = 0.06f;
        [SerializeField] private float markerHeight = 0.03f;

        private readonly Dictionary<string, CuidView> _views = new Dictionary<string, CuidView>();
        private readonly Dictionary<string, CuidDefinition> _definitionsBySpeciesId = new Dictionary<string, CuidDefinition>();
        private readonly List<BattleCombatLogPanelController.LogEntry> _combatLog = new List<BattleCombatLogPanelController.LogEntry>(12);
        private readonly List<GridPosition> _validMoveCells = new List<GridPosition>();
        private readonly List<GameObject> _moveMarkers = new List<GameObject>();
        private readonly List<GameObject> _actionMarkers = new List<GameObject>();
        private readonly CombatResolver _opportunityResolver = new CombatResolver();
        private readonly ICombatMovementService _movementService = new CombatMovementService();
        private readonly ICombatSpatialQueryService _spatialQueryService = new CombatSpatialQueryService();
        private readonly ICombatActionQueryService _actionQueryService = new CombatActionQueryService();
        private readonly ICombatLogFormattingService _combatLogFormattingService = new CombatLogFormattingService();
        private readonly ICombatStatusPresentationService _statusPresentationService = new CombatStatusPresentationService();
        private readonly ICombatTurnFlowService _turnFlowService = new CombatTurnFlowService();
        private readonly ICombatValidationService _validationService = new CombatValidationService();
        private readonly List<CuidAction> _pendingOpportunityActions = new List<CuidAction>(3);
        private readonly HashSet<string> _spentOpportunityCombatants = new HashSet<string>();

        private TurnController _turnController;
        private Coroutine _loopRoutine;

        private CombatantState _activePlayerActor;
        private PlayerInputPhase _inputPhase = PlayerInputPhase.None;
        private CuidAction _selectedAction;
        private bool _playerTurnResolved;
        private bool _isMovingActor;
        private bool _loggedCameraError;
        private bool _loggedHudMissingError;
        private bool _loggedTurnOrderEmptyWarning;
        private bool _actionPanelCallbacksRegistered;
        private bool _hudHoverCallbacksRegistered;
        private bool _opportunityPanelCallbacksRegistered;
        private bool _awaitingOpportunityChoice;
        private int _selectedOpportunityIndex = -1;
        private bool _declinedOpportunity;
        private string _hoveredCombatantId = string.Empty;
        private string _hudHoveredCombatantId = string.Empty;
        private string _worldHoveredCombatantId = string.Empty;
        private int _remainingMovement;
        private bool _hasUsedAction;

        private void Start()
        {
            EnsureHudReference();
            EnsureActionPanelReference();
            EnsureCombatLogPanelReference();
            EnsureOpportunityPanelReference();
            if (startBattleOnStart)
            {
                StartBattle();
            }
        }

        private void Update()
        {
            if (_turnController == null || _turnController.Phase != BattlePhase.InProgress)
            {
                return;
            }

            UpdateWorldHoveredCombatantId();
            RefreshHoverTooltipIfNeeded();
            HandlePlayerKeyboardShortcuts();

            if (_activePlayerActor != null && !_isMovingActor && !_awaitingOpportunityChoice && Input.GetMouseButtonDown(0))
            {
                HandlePlayerClick();
            }
        }

        private void OnDestroy()
        {
            if (actionPanelController != null && _actionPanelCallbacksRegistered)
            {
                actionPanelController.ActionPressed -= HandleActionPanelActionClicked;
                actionPanelController.EndTurnPressed -= HandleActionPanelEndTurnClicked;
                _actionPanelCallbacksRegistered = false;
            }

            if (hudController != null && _hudHoverCallbacksRegistered)
            {
                hudController.TurnOrderHoverStarted -= HandleHudCombatantHoverStarted;
                hudController.TurnOrderHoverEnded -= HandleHudCombatantHoverEnded;
                _hudHoverCallbacksRegistered = false;
            }

            if (opportunityPanelController != null && _opportunityPanelCallbacksRegistered)
            {
                opportunityPanelController.ActionSelected -= HandleOpportunityActionSelected;
                opportunityPanelController.Declined -= HandleOpportunityDeclined;
                _opportunityPanelCallbacksRegistered = false;
            }
        }

        [ContextMenu("Start Battle")]
        public void StartBattle()
        {
            EnsureHudReference();
            EnsureActionPanelReference();
            EnsureCombatLogPanelReference();
            EnsureOpportunityPanelReference();
            if (gridController == null)
            {
                Debug.LogError("BattleController needs a BattleGridController reference.");
                return;
            }

            if (_loopRoutine != null)
            {
                StopCoroutine(_loopRoutine);
            }

            _activePlayerActor = null;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.None;
            _playerTurnResolved = false;
            _remainingMovement = 0;
            _hasUsedAction = false;
            _combatLog.Clear();
            _hoveredCombatantId = string.Empty;
            _hudHoveredCombatantId = string.Empty;
            _worldHoveredCombatantId = string.Empty;
            _pendingOpportunityActions.Clear();
            _spentOpportunityCombatants.Clear();
            _awaitingOpportunityChoice = false;
            _selectedOpportunityIndex = -1;
            _declinedOpportunity = false;
            ClearMoveMarkers();
            ClearActionMarkers();
            if (hudController != null)
            {
                hudController.ClearTurnOrder();
                hudController.HideHoverTooltip();
            }

            if (actionPanelController != null)
            {
                actionPanelController.SetVisible(false);
            }

            if (opportunityPanelController != null)
            {
                opportunityPanelController.SetVisible(false);
            }

            if (combatLogPanelController != null)
            {
                combatLogPanelController.SetVisible(true);
            }

            RefreshCombatLogPanel();

            ClearSpawnedViews();
            EnsureDefaultDefinitionsIfNeeded();
            RebuildDefinitionLookup();

            _turnController = new TurnController();
            TeamRoster playerTeam = BuildTeam(TeamSide.Player, playerTeamDefinitions);
            TeamRoster enemyTeam = BuildTeam(TeamSide.Enemy, enemyTeamDefinitions);
            _turnController.StartBattle(playerTeam, enemyTeam, gridController.GridWidth, gridController.GridHeight);
            SpawnViews();
            RefreshHud();

            _loopRoutine = StartCoroutine(BattleLoop());
        }

        private IEnumerator BattleLoop()
        {
            while (_turnController != null && _turnController.Phase == BattlePhase.InProgress)
            {
                CombatantState actor = _turnController.GetCurrentCombatant();
                if (actor == null)
                {
                    break;
                }

                // Opportunity actions refresh when this Cuid's turn starts.
                _spentOpportunityCombatants.Remove(actor.CombatantId);

                if (actor.Team == TeamSide.Player)
                {
                    yield return BeginPlayerTurn(actor);
                    while (!_playerTurnResolved)
                    {
                        yield return null;
                    }

                    _playerTurnResolved = false;
                    RefreshAllViews();
                    if (_turnController.Phase == BattlePhase.Completed)
                    {
                        FinishBattle(_turnController.WinningSide);
                        yield break;
                    }

                    yield return new WaitForSeconds(turnDelaySeconds);
                    continue;
                }

                yield return ExecuteEnemyTurn(actor);
                RefreshAllViews();
                if (_turnController.Phase == BattlePhase.Completed)
                {
                    FinishBattle(_turnController.WinningSide);
                    yield break;
                }

                yield return new WaitForSeconds(turnDelaySeconds);
            }
        }

        private IEnumerator BeginPlayerTurn(CombatantState actor)
        {
            GridPosition turnStartPosition = actor.Position;
            TurnStartStatusResult start = _turnController.ProcessTurnStartEffects(actor);
            if (_turnFlowService.TryBuildTurnStartLogEntry(
                start,
                _turnController.BattleState.RoundNumber,
                _combatLogFormattingService,
                out BattleCombatLogPanelController.LogEntry startEntry))
            {
                _combatLog.Insert(0, startEntry);
                TrimCombatLog();
                RefreshCombatLogPanel();
                RefreshAllViews();
            }

            if (_turnFlowService.DidActorRepositionAtTurnStart(start, turnStartPosition, actor.Position))
            {
                yield return ResolveOpportunityAttacksBeforeMove(actor, turnStartPosition, actor.Position);
                if (_turnFlowService.TryExecutePassIfDefeated(
                    actor,
                    _turnController,
                    advanceTurn: true,
                    out TurnStepResult defeatedStep))
                {
                    AddLogFromStep(defeatedStep);
                    _activePlayerActor = null;
                    _selectedAction = null;
                    _inputPhase = PlayerInputPhase.None;
                    ClearMoveMarkers();
                    ClearActionMarkers();
                    _playerTurnResolved = true;
                    RefreshHud();
                    RefreshActionPanel();
                    yield break;
                }
            }

            if (_turnFlowService.TryExecuteForcedSkipTurn(
                start,
                _turnController,
                advanceTurn: true,
                out TurnStepResult forcedSkipStep))
            {
                AddLogFromStep(forcedSkipStep);
                _activePlayerActor = null;
                _selectedAction = null;
                _inputPhase = PlayerInputPhase.None;
                ClearMoveMarkers();
                ClearActionMarkers();
                _playerTurnResolved = true;
                RefreshHud();
                RefreshActionPanel();
                yield break;
            }

            _activePlayerActor = actor;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.SelectAction;
            _remainingMovement = _turnFlowService.ComputeTurnStartMovementBudget(start, GetMoveRange(actor));
            _hasUsedAction = false;
            BuildValidMoveCells(actor);
            RebuildMoveMarkers();
            ClearActionMarkers();
            RefreshHud();
            RefreshActionPanel();
            yield break;
        }

        private IEnumerator ExecuteEnemyTurn(CombatantState actor)
        {
            GridPosition turnStartPosition = actor.Position;
            TurnStartStatusResult start = _turnController.ProcessTurnStartEffects(actor);
            if (_turnFlowService.TryBuildTurnStartLogEntry(
                start,
                _turnController.BattleState.RoundNumber,
                _combatLogFormattingService,
                out BattleCombatLogPanelController.LogEntry startEntry))
            {
                _combatLog.Insert(0, startEntry);
                TrimCombatLog();
                RefreshCombatLogPanel();
                RefreshAllViews();
            }

            if (_turnFlowService.DidActorRepositionAtTurnStart(start, turnStartPosition, actor.Position))
            {
                yield return ResolveOpportunityAttacksBeforeMove(actor, turnStartPosition, actor.Position);
                if (_turnFlowService.TryExecutePassIfDefeated(
                    actor,
                    _turnController,
                    advanceTurn: false,
                    out TurnStepResult defeatedStep))
                {
                    AddLogFromStep(defeatedStep);
                    RefreshHud();
                    yield break;
                }
            }

            if (_turnFlowService.TryExecuteForcedSkipTurn(
                start,
                _turnController,
                advanceTurn: true,
                out TurnStepResult paralyzedStep))
            {
                AddLogFromStep(paralyzedStep);
                RefreshHud();
                yield break;
            }

            CombatantState target = FindClosestOpponent(actor);
            if (target == null)
            {
                TurnStepResult pass = _turnController.ExecuteTurn(TurnChoice.Pass());
                AddLogFromStep(pass);
                yield break;
            }

            GridPosition moveStart = actor.Position;
            if (!start.ConsumedMovement)
            {
                yield return MoveActorTowardTarget(actor, target);
            }

            if (_turnFlowService.TryExecutePassIfDefeated(
                actor,
                _turnController,
                advanceTurn: false,
                out TurnStepResult forcedPass))
            {
                AddLogFromStep(forcedPass);
                RefreshHud();
                yield break;
            }

            int movementRemaining = _turnFlowService.ComputeRemainingMovementAfterMove(
                start,
                GetMoveRange(actor),
                moveStart,
                actor.Position);

            TurnChoice choice = _actionQueryService.BuildEnemyChoice(actor, target, _spatialQueryService);
            TurnStepResult step = _turnController.ExecuteTurn(choice);
            if (!step.Success && !choice.IsPass)
            {
                // Safety fallback to avoid deadlocks if an enemy choice becomes invalid.
                step = _turnController.ExecuteTurn(TurnChoice.Pass());
            }

            AddLogFromStep(step);
            yield return TryEnemyDisengageForTesting(actor, movementRemaining);
            RefreshHud();
        }
    }
}
