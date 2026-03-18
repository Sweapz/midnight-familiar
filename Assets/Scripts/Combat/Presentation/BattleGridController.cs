using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public class BattleGridController : MonoBehaviour
    {
        [SerializeField] private int gridWidth = 8;
        [SerializeField] private int gridHeight = 8;
        [SerializeField] private float cellSize = 1.5f;
        [SerializeField] private Vector3 origin = Vector3.zero;
        [SerializeField] private bool drawGridGizmos = true;
        [SerializeField] private bool renderGridInGame = true;
        [SerializeField] private Color runtimeGridColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        [SerializeField] private float runtimeLineWidth = 0.03f;
        [SerializeField] private float runtimeYOffset = 0.02f;

        private readonly List<LineRenderer> _runtimeLines = new List<LineRenderer>();
        private Transform _runtimeGridRoot;
        private Material _runtimeMaterial;
        private bool _pendingRuntimeRebuild;

        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public float CellSize => cellSize;

        public Vector3 GridToWorld(GridPosition position)
        {
            return GridCellCenterToWorld(position.X, position.Y, 0f);
        }

        public bool IsInside(GridPosition position)
        {
            return position.IsInside(gridWidth, gridHeight);
        }

        public bool TryWorldToGrid(Vector3 worldPosition, out GridPosition position)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition) - origin;
            int x = Mathf.FloorToInt(local.x / cellSize);
            int y = Mathf.FloorToInt(local.z / cellSize);
            position = new GridPosition(x, y);
            return IsInside(position);
        }

        private void OnEnable()
        {
            _pendingRuntimeRebuild = true;
        }

        private void OnValidate()
        {
            _pendingRuntimeRebuild = true;
        }

        private void OnDisable()
        {
            ClearRuntimeGrid();
        }

        private void Update()
        {
            if (!_pendingRuntimeRebuild)
            {
                return;
            }

            _pendingRuntimeRebuild = false;
            RebuildRuntimeGrid();
        }

        private void OnDrawGizmos()
        {
            if (!drawGridGizmos)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = GridPointToWorld(x, 0, 0f);
                Vector3 end = GridPointToWorld(x, gridHeight, 0f);
                Gizmos.DrawLine(start, end);
            }

            for (int y = 0; y <= gridHeight; y++)
            {
                Vector3 start = GridPointToWorld(0, y, 0f);
                Vector3 end = GridPointToWorld(gridWidth, y, 0f);
                Gizmos.DrawLine(start, end);
            }
        }

        private void RebuildRuntimeGrid()
        {
            ClearRuntimeGrid();
            if (!Application.isPlaying || !renderGridInGame || !isActiveAndEnabled)
            {
                return;
            }

            _runtimeGridRoot = new GameObject("RuntimeGridLines").transform;
            _runtimeGridRoot.SetParent(transform, false);

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            _runtimeMaterial = new Material(shader);
            _runtimeMaterial.color = runtimeGridColor;

            for (int x = 0; x <= gridWidth; x++)
            {
                Vector3 start = GridPointToWorld(x, 0, runtimeYOffset);
                Vector3 end = GridPointToWorld(x, gridHeight, runtimeYOffset);
                CreateRuntimeLine(start, end);
            }

            for (int y = 0; y <= gridHeight; y++)
            {
                Vector3 start = GridPointToWorld(0, y, runtimeYOffset);
                Vector3 end = GridPointToWorld(gridWidth, y, runtimeYOffset);
                CreateRuntimeLine(start, end);
            }
        }

        private void CreateRuntimeLine(Vector3 start, Vector3 end)
        {
            var lineObject = new GameObject("GridLine");
            lineObject.transform.SetParent(_runtimeGridRoot, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = runtimeLineWidth;
            line.endWidth = runtimeLineWidth;
            line.useWorldSpace = true;
            line.sharedMaterial = _runtimeMaterial;
            line.startColor = runtimeGridColor;
            line.endColor = runtimeGridColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            _runtimeLines.Add(line);
        }

        private Vector3 GridPointToWorld(int x, int y, float yOffset)
        {
            Vector3 local = origin + new Vector3(x * cellSize, yOffset, y * cellSize);
            return transform.TransformPoint(local);
        }

        private Vector3 GridCellCenterToWorld(int cellX, int cellY, float yOffset)
        {
            Vector3 local = origin + new Vector3((cellX + 0.5f) * cellSize, yOffset, (cellY + 0.5f) * cellSize);
            return transform.TransformPoint(local);
        }

        private void ClearRuntimeGrid()
        {
            for (int i = 0; i < _runtimeLines.Count; i++)
            {
                if (_runtimeLines[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_runtimeLines[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(_runtimeLines[i].gameObject);
                    }
                }
            }

            _runtimeLines.Clear();

            if (_runtimeGridRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeGridRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(_runtimeGridRoot.gameObject);
                }
                _runtimeGridRoot = null;
            }

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeMaterial);
                }
                else
                {
                    DestroyImmediate(_runtimeMaterial);
                }
                _runtimeMaterial = null;
            }
        }
    }
}
