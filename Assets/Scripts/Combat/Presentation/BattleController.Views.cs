using System.Linq;
using MidnightFamiliar.Combat.Content;
using MidnightFamiliar.Combat.Models;
using UnityEngine;

namespace MidnightFamiliar.Combat.Presentation
{
    public partial class BattleController
    {
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
            RefreshHoverTooltipIfNeeded();
        }

        private void RebuildDefinitionLookup()
        {
            _definitionsBySpeciesId.Clear();
            AddDefinitionsToLookup(playerTeamDefinitions);
            AddDefinitionsToLookup(enemyTeamDefinitions);
        }

        private void AddDefinitionsToLookup(System.Collections.Generic.List<CuidDefinition> definitions)
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
                playerTeamDefinitions = new System.Collections.Generic.List<CuidDefinition> { emberFox, tideToad };
            }

            if (enemyTeamDefinitions.Count == 0)
            {
                enemyTeamDefinitions = new System.Collections.Generic.List<CuidDefinition> { tideToad, emberFox };
            }
        }
    }
}
