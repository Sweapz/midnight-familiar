using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using MidnightFamiliar.Combat.Presentation.UI;
using MidnightFamiliar.Combat.Systems;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public class BattleController : MonoBehaviour
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
        [SerializeField] private GameObject defaultCuidViewPrefab;
        [SerializeField] private List<CuidPrefabBinding> cuidPrefabBindings = new List<CuidPrefabBinding>();

        [Header("Teams")]
        [SerializeField] private List<CuidDefinition> playerTeamDefinitions = new List<CuidDefinition>(4);
        [SerializeField] private List<CuidDefinition> enemyTeamDefinitions = new List<CuidDefinition>(4);

        [Header("Flow")]
        [SerializeField] private bool startBattleOnStart = true;
        [SerializeField] private float turnDelaySeconds = 0.7f;
        [SerializeField] private float moveDurationSeconds = 0.25f;

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
        private readonly List<string> _combatLog = new List<string>(12);
        private readonly List<GridPosition> _validMoveCells = new List<GridPosition>();
        private readonly List<GameObject> _moveMarkers = new List<GameObject>();
        private readonly List<GameObject> _actionMarkers = new List<GameObject>();

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
        private int _remainingMovement;
        private bool _hasUsedAction;

        private void Start()
        {
            EnsureHudReference();
            EnsureActionPanelReference();
            EnsureCombatLogPanelReference();
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

            if (_activePlayerActor == null || _isMovingActor)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
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
        }

        [ContextMenu("Start Battle")]
        public void StartBattle()
        {
            EnsureHudReference();
            EnsureActionPanelReference();
            EnsureCombatLogPanelReference();
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
            ClearMoveMarkers();
            ClearActionMarkers();
            if (hudController != null)
            {
                hudController.ClearTurnOrder();
            }
            if (actionPanelController != null)
            {
                actionPanelController.SetVisible(false);
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

                if (actor.Team == TeamSide.Player)
                {
                    BeginPlayerTurn(actor);
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

        private void BeginPlayerTurn(CombatantState actor)
        {
            _activePlayerActor = actor;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.SelectAction;
            _remainingMovement = GetMoveRange(actor);
            _hasUsedAction = false;
            BuildValidMoveCells(actor);
            RebuildMoveMarkers();
            ClearActionMarkers();
            RefreshHud();
            RefreshActionPanel();
        }

        private IEnumerator ExecuteEnemyTurn(CombatantState actor)
        {
            CombatantState target = FindClosestOpponent(actor);
            if (target == null)
            {
                TurnStepResult pass = _turnController.ExecuteTurn(TurnChoice.Pass());
                AddLogFromStep(pass);
                yield break;
            }

            yield return MoveActorTowardTarget(actor, target);
            TurnChoice choice = BuildEnemyChoice(actor, target);
            TurnStepResult step = _turnController.ExecuteTurn(choice);
            if (!step.Success && !choice.IsPass)
            {
                // Safety fallback to avoid deadlocks if an enemy choice becomes invalid.
                step = _turnController.ExecuteTurn(TurnChoice.Pass());
            }
            AddLogFromStep(step);
            RefreshHud();
        }

        private void HandlePlayerClick()
        {
            switch (_inputPhase)
            {
                case PlayerInputPhase.SelectMove:
                case PlayerInputPhase.SelectAction:
                    TryHandleMoveClick();
                    break;
                case PlayerInputPhase.SelectTarget:
                    TryHandleTargetClick();
                    break;
            }
        }

        private void TryHandleMoveClick()
        {
            if (!TryGetClickedCell(out GridPosition cell))
            {
                Debug.Log("Move click: no valid grid cell detected.");
                return;
            }

            if (!_validMoveCells.Any(c => c.X == cell.X && c.Y == cell.Y))
            {
                Debug.Log($"Move click rejected: cell [{cell.X},{cell.Y}] is outside current move range.");
                return;
            }

            int moveCost = _activePlayerActor.Position.ManhattanDistanceTo(cell);
            if (moveCost <= 0 || moveCost > _remainingMovement)
            {
                Debug.Log($"Move click rejected: movement cost {moveCost} exceeds remaining {_remainingMovement}.");
                return;
            }

            StartCoroutine(AnimatePlayerMove(cell, moveCost));
        }

        private IEnumerator AnimatePlayerMove(GridPosition destination, int moveCost)
        {
            _isMovingActor = true;
            _inputPhase = PlayerInputPhase.None;
            ClearMoveMarkers();
            ClearActionMarkers();

            GridPosition start = _activePlayerActor.Position;
            _activePlayerActor.Position = destination;

            if (_views.TryGetValue(_activePlayerActor.CombatantId, out CuidView view) && view != null)
            {
                Vector3 worldStart = gridController.GridToWorld(start);
                Vector3 worldEnd = gridController.GridToWorld(destination);
                float elapsed = 0f;
                while (elapsed < moveDurationSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = moveDurationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / moveDurationSeconds);
                    view.transform.position = Vector3.Lerp(worldStart, worldEnd, t);
                    yield return null;
                }

                view.transform.position = worldEnd;
            }

            _remainingMovement = Mathf.Max(0, _remainingMovement - moveCost);
            BuildValidMoveCells(_activePlayerActor);
            _isMovingActor = false;
            _inputPhase = PlayerInputPhase.SelectAction;
            RebuildMoveMarkers();
            RefreshActionPanel();
        }

        private void TryHandleTargetClick()
        {
            if (!TryGetClickedCombatant(out CombatantState target))
            {
                return;
            }

            if (!IsValidTargetForSelectedAction(_activePlayerActor, target, _selectedAction))
            {
                return;
            }

            ExecutePlayerAction(target);
        }

        private void ExecutePlayerAction(CombatantState target)
        {
            var choice = new TurnChoice
            {
                IsPass = false,
                ActionId = _selectedAction.Id,
                TargetCombatantId = target.CombatantId
            };

            TurnStepResult step = _turnController.ExecuteTurn(choice, advanceTurn: false);
            AddLogFromStep(step);
            RefreshAllViews();

            _hasUsedAction = true;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.SelectAction;
            ClearActionMarkers();

            if (step.BattleEnded)
            {
                _activePlayerActor = null;
                _playerTurnResolved = true;
                ClearMoveMarkers();
                RefreshActionPanel();
                return;
            }

            BuildValidMoveCells(_activePlayerActor);
            RebuildMoveMarkers();
            RefreshActionPanel();
        }

        private bool TryGetClickedCell(out GridPosition cell)
        {
            cell = default;
            Camera cam = GetInputCamera();
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(gridController.transform.up, gridController.transform.position);
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 worldHit = ray.GetPoint(enter);
            return gridController.TryWorldToGrid(worldHit, out cell);
        }

        private bool TryGetClickedCombatant(out CombatantState combatant)
        {
            combatant = null;
            Camera cam = GetInputCamera();
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                return false;
            }

            CuidView view = hit.collider.GetComponentInParent<CuidView>();
            if (view == null)
            {
                return TryGetClickedCombatantFromCell(out combatant);
            }

            combatant = _turnController.BattleState.FindCombatant(view.CombatantId);
            return combatant != null || TryGetClickedCombatantFromCell(out combatant);
        }

        private bool TryGetClickedCombatantFromCell(out CombatantState combatant)
        {
            combatant = null;
            if (!TryGetClickedCell(out GridPosition cell))
            {
                return false;
            }

            combatant = _turnController.BattleState.Combatants
                .FirstOrDefault(c => !c.IsDefeated && c.Position.X == cell.X && c.Position.Y == cell.Y);
            return combatant != null;
        }

        private Camera GetInputCamera()
        {
            Camera cam = battleCamera != null ? battleCamera : Camera.main;
            if (cam == null && !_loggedCameraError)
            {
                _loggedCameraError = true;
                Debug.LogError("No input camera found. Assign Battle Camera on BattleController or tag a camera as MainCamera.");
            }

            return cam;
        }

        private void BuildValidMoveCells(CombatantState actor)
        {
            _validMoveCells.Clear();
            int range = _remainingMovement;
            if (range <= 0)
            {
                return;
            }

            GridPosition start = actor.Position;

            for (int x = 0; x < gridController.GridWidth; x++)
            {
                for (int y = 0; y < gridController.GridHeight; y++)
                {
                    var candidate = new GridPosition(x, y);
                    if (candidate.X == start.X && candidate.Y == start.Y)
                    {
                        continue;
                    }

                    if (start.ManhattanDistanceTo(candidate) > range)
                    {
                        continue;
                    }

                    if (IsOccupied(candidate, actor.CombatantId))
                    {
                        continue;
                    }

                    _validMoveCells.Add(candidate);
                }
            }
        }

        private void RebuildMoveMarkers()
        {
            ClearMoveMarkers();
            if (!showReachableMoveMarkers)
            {
                return;
            }

            if (_inputPhase != PlayerInputPhase.SelectMove && _inputPhase != PlayerInputPhase.SelectAction)
            {
                return;
            }

            if (_remainingMovement <= 0)
            {
                return;
            }

            foreach (GridPosition cell in _validMoveCells)
            {
                _moveMarkers.Add(CreateOutlineMarker(
                    $"MoveMarker_{cell.X}_{cell.Y}",
                    cell,
                    reachableMarkerColor,
                    reachableMarkerOutlineInset,
                    reachableMarkerYOffset));
            }
        }

        private void ClearMoveMarkers()
        {
            for (int i = 0; i < _moveMarkers.Count; i++)
            {
                if (_moveMarkers[i] != null)
                {
                    Destroy(_moveMarkers[i]);
                }
            }

            _moveMarkers.Clear();
        }

        private void RebuildActionMarkers()
        {
            ClearActionMarkers();
            if (!showActionRangeMarkers || _selectedAction == null || _activePlayerActor == null || _inputPhase != PlayerInputPhase.SelectTarget)
            {
                return;
            }

            List<GridPosition> actionCells = BuildActionRangeCells(_activePlayerActor, _selectedAction);
            for (int i = 0; i < actionCells.Count; i++)
            {
                GridPosition cell = actionCells[i];
                _actionMarkers.Add(CreateOutlineMarker(
                    $"ActionMarker_{cell.X}_{cell.Y}",
                    cell,
                    actionMarkerColor,
                    actionMarkerOutlineInset,
                    actionMarkerYOffset));
            }
        }

        private void ClearActionMarkers()
        {
            for (int i = 0; i < _actionMarkers.Count; i++)
            {
                if (_actionMarkers[i] != null)
                {
                    Destroy(_actionMarkers[i]);
                }
            }

            _actionMarkers.Clear();
        }

        private List<GridPosition> BuildActionRangeCells(CombatantState actor, CuidAction action)
        {
            var cells = new List<GridPosition>();
            if (actor == null || action == null)
            {
                return cells;
            }

            if (action.TargetRule == TargetRule.Self)
            {
                cells.Add(actor.Position);
                return cells;
            }

            int range = Mathf.Max(0, action.Range);
            for (int x = 0; x < gridController.GridWidth; x++)
            {
                for (int y = 0; y < gridController.GridHeight; y++)
                {
                    GridPosition cell = new GridPosition(x, y);
                    int distance = actor.Position.ManhattanDistanceTo(cell);
                    if (distance <= range)
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        private GameObject CreateOutlineMarker(
            string markerName,
            GridPosition cell,
            Color color,
            float insetRatio,
            float yOffset)
        {
            GameObject root = new GameObject(markerName);
            root.transform.SetParent(transform);
            root.transform.position = gridController.GridToWorld(cell) + new Vector3(0f, yOffset, 0f);

            float cellSize = gridController.CellSize;
            float inset = Mathf.Clamp(insetRatio, 0.02f, 0.45f) * cellSize;
            float outer = Mathf.Max(0.05f, cellSize - inset * 2f);
            float lineThickness = Mathf.Clamp(markerLineThickness, 0.01f, outer * 0.45f);
            float half = outer * 0.5f;
            float lineY = Mathf.Max(0.005f, markerHeight);

            CreateOutlineSegment(root.transform, new Vector3(0f, 0f, half), new Vector3(outer, lineY, lineThickness), color);
            CreateOutlineSegment(root.transform, new Vector3(0f, 0f, -half), new Vector3(outer, lineY, lineThickness), color);
            CreateOutlineSegment(root.transform, new Vector3(half, 0f, 0f), new Vector3(lineThickness, lineY, outer), color);
            CreateOutlineSegment(root.transform, new Vector3(-half, 0f, 0f), new Vector3(lineThickness, lineY, outer), color);

            return root;
        }

        private void CreateOutlineSegment(Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localScale = scale;
            Destroy(segment.GetComponent<Collider>());

            Renderer renderer = segment.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.material.color = color;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private int GetMoveRange(CombatantState actor)
        {
            return Mathf.Clamp(Mathf.Max(1, actor.Unit.Stats.Speed / 4), 1, 3);
        }

        private IEnumerator MoveActorTowardTarget(CombatantState actor, CombatantState target)
        {
            GridPosition start = actor.Position;
            GridPosition end = CalculateMoveDestination(actor, target);
            if (start.X == end.X && start.Y == end.Y)
            {
                yield break;
            }

            actor.Position = end;
            if (!_views.TryGetValue(actor.CombatantId, out CuidView view) || view == null)
            {
                yield break;
            }

            Vector3 worldStart = gridController.GridToWorld(start);
            Vector3 worldEnd = gridController.GridToWorld(end);
            float elapsed = 0f;
            while (elapsed < moveDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = moveDurationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / moveDurationSeconds);
                view.transform.position = Vector3.Lerp(worldStart, worldEnd, t);
                yield return null;
            }

            view.transform.position = worldEnd;
        }

        private GridPosition CalculateMoveDestination(CombatantState actor, CombatantState target)
        {
            int steps = GetMoveRange(actor);
            GridPosition current = actor.Position;

            for (int i = 0; i < steps; i++)
            {
                GridPosition next = FindBestNeighborStep(current, target.Position, actor.CombatantId);
                if (next.X == current.X && next.Y == current.Y)
                {
                    break;
                }

                current = next;
            }

            return current;
        }

        private GridPosition FindBestNeighborStep(GridPosition from, GridPosition target, string actorId)
        {
            GridPosition[] candidates =
            {
                new GridPosition(from.X + 1, from.Y),
                new GridPosition(from.X - 1, from.Y),
                new GridPosition(from.X, from.Y + 1),
                new GridPosition(from.X, from.Y - 1)
            };

            int bestDistance = from.ManhattanDistanceTo(target);
            GridPosition best = from;
            foreach (GridPosition candidate in candidates)
            {
                if (!gridController.IsInside(candidate))
                {
                    continue;
                }

                if (IsOccupied(candidate, actorId))
                {
                    continue;
                }

                int distance = candidate.ManhattanDistanceTo(target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private bool IsOccupied(GridPosition position, string exceptCombatantId)
        {
            foreach (CombatantState combatant in _turnController.BattleState.Combatants)
            {
                if (combatant.IsDefeated || combatant.CombatantId == exceptCombatantId)
                {
                    continue;
                }

                if (combatant.Position.X == position.X && combatant.Position.Y == position.Y)
                {
                    return true;
                }
            }

            return false;
        }

        private CombatantState FindClosestOpponent(CombatantState actor)
        {
            TeamSide opponentSide = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            return _turnController.BattleState.Combatants
                .Where(c => c.Team == opponentSide && !c.IsDefeated)
                .OrderBy(c => c.Position.ManhattanDistanceTo(actor.Position))
                .FirstOrDefault();
        }

        private TurnChoice BuildEnemyChoice(CombatantState actor, CombatantState defaultTarget)
        {
            CuidAction offensive = actor.Unit.Actions
                .FirstOrDefault(action => action != null &&
                                          (action.Kind == ActionKind.Attack ||
                                           (action.Kind == ActionKind.Ability && action.AbilityIntent == AbilityIntent.Offensive)) &&
                                          IsTargetInRange(actor, defaultTarget, action));

            if (offensive != null)
            {
                return new TurnChoice
                {
                    IsPass = false,
                    ActionId = offensive.Id,
                    TargetCombatantId = defaultTarget.CombatantId
                };
            }

            CuidAction supportive = actor.Unit.Actions
                .FirstOrDefault(action => action != null &&
                                          action.Kind == ActionKind.Ability &&
                                          action.AbilityIntent == AbilityIntent.Supportive &&
                                          IsTargetInRange(actor, actor, action));

            if (supportive != null)
            {
                return new TurnChoice
                {
                    IsPass = false,
                    ActionId = supportive.Id,
                    TargetCombatantId = actor.CombatantId
                };
            }

            return TurnChoice.Pass();
        }

        private bool IsValidTargetForSelectedAction(CombatantState actor, CombatantState target, CuidAction action)
        {
            if (actor == null || target == null || action == null || target.IsDefeated)
            {
                return false;
            }

            if (action.Kind == ActionKind.Attack)
            {
                return target.Team != actor.Team && IsTargetInRange(actor, target, action);
            }

            if (action.Kind == ActionKind.Ability)
            {
                switch (action.TargetRule)
                {
                    case TargetRule.Self:
                        return target.CombatantId == actor.CombatantId && IsTargetInRange(actor, target, action);
                    case TargetRule.AllySingle:
                        return target.Team == actor.Team && IsTargetInRange(actor, target, action);
                    case TargetRule.EnemySingle:
                    case TargetRule.EnemyArea:
                        return target.Team != actor.Team && IsTargetInRange(actor, target, action);
                }
            }

            return false;
        }

        private bool IsTargetInRange(CombatantState actor, CombatantState target, CuidAction action)
        {
            int distance = actor.Position.ManhattanDistanceTo(target.Position);
            return distance <= Mathf.Max(0, action.Range);
        }

        private TeamRoster BuildTeam(TeamSide side, List<CuidDefinition> definitions)
        {
            var units = new List<CuidUnit>(4);
            foreach (CuidDefinition definition in definitions.Where(definition => definition != null))
            {
                units.Add(CuidFactory.CreateUnit(definition));
            }

            return new TeamRoster
            {
                Side = side,
                Units = units
            };
        }

        private void SpawnViews()
        {
            foreach (CombatantState combatant in _turnController.BattleState.Combatants)
            {
                GameObject viewObject = CreateViewObject(combatant);
                viewObject.transform.SetParent(transform);
                viewObject.transform.position = gridController.GridToWorld(combatant.Position);

                CuidView view = viewObject.GetComponent<CuidView>();
                if (view == null)
                {
                    view = viewObject.AddComponent<CuidView>();
                }

                if (viewObject.GetComponentInChildren<Collider>() == null)
                {
                    viewObject.AddComponent<SphereCollider>();
                }

                view.Initialize(combatant);
                _views[combatant.CombatantId] = view;
            }
        }

        private GameObject CreateViewObject(CombatantState combatant)
        {
            GameObject mappedPrefab = ResolveMappedPrefab(combatant?.Unit?.SpeciesId);
            if (mappedPrefab != null)
            {
                return Instantiate(mappedPrefab);
            }

            if (defaultCuidViewPrefab != null)
            {
                return Instantiate(defaultCuidViewPrefab);
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            return fallback;
        }

        private GameObject ResolveMappedPrefab(string speciesId)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                return null;
            }

            foreach (CuidPrefabBinding binding in cuidPrefabBindings)
            {
                if (binding == null || binding.prefab == null || string.IsNullOrWhiteSpace(binding.speciesId))
                {
                    continue;
                }

                if (string.Equals(binding.speciesId, speciesId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return binding.prefab;
                }
            }

            return null;
        }

        private void ClearSpawnedViews()
        {
            foreach (CuidView view in _views.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _views.Clear();
        }

        private void RefreshAllViews()
        {
            foreach (CombatantState combatant in _turnController.BattleState.Combatants)
            {
                if (!_views.TryGetValue(combatant.CombatantId, out CuidView view) || view == null)
                {
                    continue;
                }

                view.transform.position = gridController.GridToWorld(combatant.Position);
                view.ApplyState(combatant);
            }

            RefreshHud();
        }

        private void AddLogFromStep(TurnStepResult step)
        {
            if (step == null)
            {
                return;
            }

            string line = step.Resolution != null
                ? $"R{step.RoundNumber}: {step.Resolution.Summary}"
                : $"R{step.RoundNumber}: {step.Message}";

            _combatLog.Insert(0, line);
            if (_combatLog.Count > 10)
            {
                _combatLog.RemoveAt(_combatLog.Count - 1);
            }

            RefreshCombatLogPanel();
        }

        private void FinishBattle(TeamSide? winner)
        {
            string winnerText = winner.HasValue ? winner.Value.ToString() : "None";
            _combatLog.Insert(0, $"Battle ended. Winner: {winnerText}");
            RefreshCombatLogPanel();
            RefreshHud();
            RefreshActionPanel();
        }

        private void RebuildDefinitionLookup()
        {
            _definitionsBySpeciesId.Clear();
            AddDefinitionsToLookup(playerTeamDefinitions);
            AddDefinitionsToLookup(enemyTeamDefinitions);
        }

        private void AddDefinitionsToLookup(List<CuidDefinition> definitions)
        {
            foreach (CuidDefinition definition in definitions.Where(d => d != null))
            {
                if (string.IsNullOrWhiteSpace(definition.CuidId))
                {
                    continue;
                }

                _definitionsBySpeciesId[definition.CuidId] = definition;
            }
        }

        private void RefreshHud()
        {
            EnsureHudReference();
            if (hudController == null)
            {
                if (!_loggedHudMissingError)
                {
                    _loggedHudMissingError = true;
                    Debug.LogWarning("BattleController could not find BattleHudController in scene. Turn order HUD will not update.");
                }
                return;
            }

            _loggedHudMissingError = false;
            if (_turnController?.BattleState == null)
            {
                return;
            }

            string currentCombatantId = _turnController.GetCurrentCombatant()?.CombatantId;
            var entries = new List<TurnOrderHudEntry>(_turnController.BattleState.TurnOrder.Count);
            foreach (string combatantId in _turnController.BattleState.TurnOrder)
            {
                CombatantState combatant = _turnController.BattleState.FindCombatant(combatantId);
                if (combatant == null)
                {
                    continue;
                }

                Sprite portrait = null;
                if (!string.IsNullOrWhiteSpace(combatant.Unit?.SpeciesId) &&
                    _definitionsBySpeciesId.TryGetValue(combatant.Unit.SpeciesId, out CuidDefinition definition))
                {
                    portrait = definition.Portrait;
                }

                entries.Add(new TurnOrderHudEntry
                {
                    CombatantId = combatant.CombatantId,
                    DisplayName = combatant.Unit?.DisplayName ?? "Cuid",
                    CurrentHp = Mathf.Max(0, combatant.CurrentHealth),
                    MaxHp = Mathf.Max(1, combatant.Unit?.Stats?.Constitution ?? 1),
                    Portrait = portrait,
                    Team = combatant.Team,
                    IsDefeated = combatant.IsDefeated
                });
            }

            if (entries.Count == 0 && !_loggedTurnOrderEmptyWarning)
            {
                _loggedTurnOrderEmptyWarning = true;
                Debug.LogWarning("Turn order HUD received zero entries. Check team definitions and battle initialization.");
            }
            else if (entries.Count > 0)
            {
                _loggedTurnOrderEmptyWarning = false;
            }

            hudController.SetTurnOrder(entries, currentCombatantId, _turnController.BattleState.RoundNumber);
        }

        private void EnsureHudReference()
        {
            if (hudController != null)
            {
                return;
            }

            hudController = FindFirstObjectByType<BattleHudController>();
        }

        private void EnsureActionPanelReference()
        {
            if (actionPanelController == null)
            {
                actionPanelController = FindFirstObjectByType<BattleActionPanelController>();
            }

            if (actionPanelController != null && !_actionPanelCallbacksRegistered)
            {
                actionPanelController.ActionPressed += HandleActionPanelActionClicked;
                actionPanelController.EndTurnPressed += HandleActionPanelEndTurnClicked;
                _actionPanelCallbacksRegistered = true;
            }
        }

        private void EnsureCombatLogPanelReference()
        {
            if (combatLogPanelController != null)
            {
                return;
            }

            combatLogPanelController = FindFirstObjectByType<BattleCombatLogPanelController>();
        }

        private void HandleActionPanelActionClicked(int buttonIndex)
        {
            if (_activePlayerActor == null)
            {
                return;
            }

            if (_inputPhase == PlayerInputPhase.SelectTarget)
            {
                _inputPhase = PlayerInputPhase.SelectAction;
                _selectedAction = null;
                ClearActionMarkers();
                RebuildMoveMarkers();
                RefreshActionPanel();
                return;
            }

            if (_inputPhase != PlayerInputPhase.SelectAction || _hasUsedAction)
            {
                return;
            }

            List<CuidAction> actions = _activePlayerActor.Unit.Actions.Where(a => a != null).ToList();
            if (buttonIndex < 0 || buttonIndex >= actions.Count)
            {
                return;
            }

            CuidAction action = actions[buttonIndex];
            _selectedAction = action;
            if (action.TargetRule == TargetRule.Self)
            {
                ExecutePlayerAction(_activePlayerActor);
            }
            else
            {
                _inputPhase = PlayerInputPhase.SelectTarget;
                ClearMoveMarkers();
                RebuildActionMarkers();
                RefreshActionPanel();
            }
        }

        private void HandleActionPanelEndTurnClicked()
        {
            if (_activePlayerActor == null)
            {
                return;
            }

            TurnStepResult endStep = _turnController.ExecuteTurn(TurnChoice.Pass(), advanceTurn: true);
            AddLogFromStep(endStep);
            _activePlayerActor = null;
            _selectedAction = null;
            _inputPhase = PlayerInputPhase.None;
            ClearMoveMarkers();
            ClearActionMarkers();
            _playerTurnResolved = true;
            RefreshHud();
            RefreshActionPanel();
        }

        private void RefreshActionPanel()
        {
            EnsureActionPanelReference();
            if (actionPanelController == null)
            {
                return;
            }

            if (_activePlayerActor == null)
            {
                actionPanelController.SetVisible(false);
                return;
            }

            actionPanelController.SetVisible(true);
            actionPanelController.SetEndTurnInteractable(true);

            Sprite portrait = null;
            if (!string.IsNullOrWhiteSpace(_activePlayerActor.Unit?.SpeciesId) &&
                _definitionsBySpeciesId.TryGetValue(_activePlayerActor.Unit.SpeciesId, out CuidDefinition definition))
            {
                portrait = definition.Portrait;
            }

            if (_inputPhase == PlayerInputPhase.SelectTarget)
            {
                actionPanelController.SetActions(new[] { "Back" }, true);
            }
            else
            {
                List<string> labels = _activePlayerActor.Unit.Actions
                    .Where(action => action != null)
                    .Select(action => action.DisplayName)
                    .ToList();

                actionPanelController.SetActions(labels, !_hasUsedAction);
            }

            actionPanelController.SetStatus(
                _activePlayerActor.Unit.DisplayName,
                Mathf.Max(0, _activePlayerActor.CurrentHealth),
                Mathf.Max(1, _activePlayerActor.Unit.Stats.Constitution),
                _remainingMovement,
                portrait);
        }

        private void RefreshCombatLogPanel()
        {
            EnsureCombatLogPanelReference();
            if (combatLogPanelController == null)
            {
                return;
            }

            combatLogPanelController.SetEntries(_combatLog);
        }

        private void EnsureDefaultDefinitionsIfNeeded()
        {
            if (playerTeamDefinitions.Count > 0 && enemyTeamDefinitions.Count > 0)
            {
                return;
            }

            CuidDefinition emberFox = Resources.Load<CuidDefinition>("Combat/Cuids/EmberFox");
            CuidDefinition tideToad = Resources.Load<CuidDefinition>("Combat/Cuids/TideToad");
            if (emberFox == null || tideToad == null)
            {
                Debug.LogError("Missing starter CuidDefinition assets under Resources/Combat/Cuids.");
                return;
            }

            if (playerTeamDefinitions.Count == 0)
            {
                playerTeamDefinitions = new List<CuidDefinition> { emberFox, tideToad };
            }

            if (enemyTeamDefinitions.Count == 0)
            {
                enemyTeamDefinitions = new List<CuidDefinition> { tideToad, emberFox };
            }
        }
    }
}
