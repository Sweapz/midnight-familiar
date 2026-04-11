using System.Collections.Generic;
using MidnightFamiliar.Combat.Models;

namespace MidnightFamiliar.Combat.Systems
{
    public sealed class TypeChartEffectivenessProvider : ITypeEffectivenessProvider
    {
        private readonly Dictionary<CuidType, Dictionary<CuidType, float>> _chart;

        public TypeChartEffectivenessProvider()
        {
            _chart = BuildDefaultChart();
        }

        public TypeChartEffectivenessProvider(Dictionary<CuidType, Dictionary<CuidType, float>> chart)
        {
            _chart = chart ?? BuildDefaultChart();
        }

        public float GetMultiplier(CuidType actionType, CuidUnit target)
        {
            if (target == null)
            {
                return 1f;
            }

            float multiplier = 1f;
            foreach (CuidType targetType in target.GetTypes())
            {
                multiplier *= GetSingleTypeMultiplier(actionType, targetType);
            }

            return multiplier;
        }

        public void SetMultiplier(CuidType actionType, CuidType targetType, float multiplier)
        {
            if (!_chart.TryGetValue(actionType, out Dictionary<CuidType, float> targets))
            {
                targets = new Dictionary<CuidType, float>();
                _chart[actionType] = targets;
            }

            targets[targetType] = multiplier;
        }

        private float GetSingleTypeMultiplier(CuidType actionType, CuidType targetType)
        {
            if (!System.Enum.IsDefined(typeof(CuidType), actionType) ||
                !System.Enum.IsDefined(typeof(CuidType), targetType))
            {
                return 1f;
            }

            if (_chart.TryGetValue(actionType, out Dictionary<CuidType, float> targets) &&
                targets.TryGetValue(targetType, out float multiplier))
            {
                return multiplier;
            }

            return 1f;
        }

        private static Dictionary<CuidType, Dictionary<CuidType, float>> BuildDefaultChart()
        {
            // Baseline chart for prototyping; tune as combat tests come in.
            return new Dictionary<CuidType, Dictionary<CuidType, float>>
            {
                [CuidType.Ember] = new Dictionary<CuidType, float>
                {
                    [CuidType.Flora] = 1.5f,
                    [CuidType.Tide] = 0.5f,
                    [CuidType.Stone] = 0.75f
                },
                [CuidType.Tide] = new Dictionary<CuidType, float>
                {
                    [CuidType.Ember] = 1.5f,
                    [CuidType.Stone] = 1.25f,
                    [CuidType.Flora] = 0.5f
                },
                [CuidType.Flora] = new Dictionary<CuidType, float>
                {
                    [CuidType.Tide] = 1.5f,
                    [CuidType.Stone] = 1.25f,
                    [CuidType.Ember] = 0.5f
                },
                [CuidType.Stone] = new Dictionary<CuidType, float>
                {
                    [CuidType.Volt] = 1.5f,
                    [CuidType.Flora] = 0.75f
                },
                [CuidType.Volt] = new Dictionary<CuidType, float>
                {
                    [CuidType.Tide] = 1.5f,
                    [CuidType.Stone] = 0.5f
                },
                [CuidType.Arcane] = new Dictionary<CuidType, float>
                {
                    [CuidType.Beast] = 1.25f,
                    [CuidType.Arcane] = 0.75f
                },
                [CuidType.Beast] = new Dictionary<CuidType, float>
                {
                    [CuidType.Arcane] = 1.25f,
                    [CuidType.Stone] = 0.75f
                },
                [CuidType.Mental] = new Dictionary<CuidType, float>
                {
                    [CuidType.Arcane] = 1.5f,
                    [CuidType.Darkin] = 0.5f
                },
                [CuidType.Darkin] = new Dictionary<CuidType, float>
                {
                    [CuidType.Mental] = 1.5f,
                    [CuidType.Volt] = 0.5f
                }
            };
        }
    }
}
