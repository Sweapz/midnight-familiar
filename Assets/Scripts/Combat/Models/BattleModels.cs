using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public enum TeamSide
    {
        Player = 0,
        Enemy = 1
    }

    [Serializable]
    public class TeamRoster
    {
        public TeamSide Side;
        public List<CuidUnit> Units = new List<CuidUnit>(4);
    }

    [Serializable]
    public class CombatantState
    {
        public string CombatantId = Guid.NewGuid().ToString("N");
        public TeamSide Team;
        public CuidUnit Unit = new CuidUnit();
        public GridPosition Position;
        public bool IsDefeated;
        public List<ActiveStatusEffect> ActiveEffects = new List<ActiveStatusEffect>(4);

        public int CurrentHealth
        {
            get => Unit != null ? Unit.CurrentHealth : 0;
            set
            {
                if (Unit == null)
                {
                    return;
                }

                Unit.CurrentHealth = Mathf.Clamp(value, 0, GetMaxHealth());
                IsDefeated = Unit.CurrentHealth <= 0;
            }
        }

        public CuidStats GetEffectiveStats()
        {
            CuidStats baseStats = Unit != null && Unit.Stats != null ? Unit.Stats : new CuidStats();
            CuidStats effective = baseStats.Clone();
            if (ActiveEffects == null)
            {
                return effective;
            }

            for (int i = 0; i < ActiveEffects.Count; i++)
            {
                ActiveStatusEffect effect = ActiveEffects[i];
                if (effect == null || effect.Kind != SupportEffectKind.StatModifier || effect.RemainingTurns <= 0)
                {
                    continue;
                }

                ApplyStatModifier(effective, effect.TargetStat, effect.Magnitude);
            }

            return effective;
        }

        public int GetMaxHealth()
        {
            return Mathf.Max(1, GetEffectiveStats().Constitution);
        }

        public int GetBonusFlatDamageReduction()
        {
            return SumActiveEffectMagnitude(SupportEffectKind.FlatDamageReduction);
        }

        public int GetBonusFlatDamageIncrease()
        {
            return SumActiveEffectMagnitude(SupportEffectKind.FlatDamageIncrease);
        }

        public int GetThornsDamage()
        {
            return SumActiveEffectMagnitude(SupportEffectKind.Thorns);
        }

        public int AbsorbWithShields(int incomingDamage)
        {
            int damageToAbsorb = Mathf.Max(0, incomingDamage);
            if (damageToAbsorb == 0 || ActiveEffects == null || ActiveEffects.Count == 0)
            {
                return 0;
            }

            int absorbed = 0;
            for (int i = 0; i < ActiveEffects.Count && damageToAbsorb > 0; i++)
            {
                ActiveStatusEffect effect = ActiveEffects[i];
                if (effect == null ||
                    effect.Kind != SupportEffectKind.Shield ||
                    effect.RemainingTurns <= 0 ||
                    effect.Magnitude <= 0)
                {
                    continue;
                }

                int block = Mathf.Min(effect.Magnitude, damageToAbsorb);
                effect.Magnitude -= block;
                absorbed += block;
                damageToAbsorb -= block;
            }

            RemoveExpiredEffects();
            return absorbed;
        }

        public void AddActiveEffect(ActiveStatusEffect effect)
        {
            if (effect == null)
            {
                return;
            }

            if (effect.Kind == SupportEffectKind.Heal)
            {
                return;
            }

            if (ActiveEffects == null)
            {
                ActiveEffects = new List<ActiveStatusEffect>(4);
            }

            if (effect.RemainingTurns <= 0)
            {
                effect.RemainingTurns = 1;
            }

            ActiveEffects.Add(effect);
        }

        public void TickActiveEffectsAtTurnEnd()
        {
            if (ActiveEffects == null || ActiveEffects.Count == 0)
            {
                return;
            }

            for (int i = 0; i < ActiveEffects.Count; i++)
            {
                ActiveStatusEffect effect = ActiveEffects[i];
                if (effect == null || effect.RemainingTurns <= 0)
                {
                    continue;
                }

                effect.RemainingTurns--;
            }

            RemoveExpiredEffects();
            CurrentHealth = Mathf.Min(CurrentHealth, GetMaxHealth());
        }

        private int SumActiveEffectMagnitude(SupportEffectKind kind)
        {
            if (ActiveEffects == null || ActiveEffects.Count == 0)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < ActiveEffects.Count; i++)
            {
                ActiveStatusEffect effect = ActiveEffects[i];
                if (effect == null || effect.Kind != kind || effect.RemainingTurns <= 0)
                {
                    continue;
                }

                total += effect.Magnitude;
            }

            return Mathf.Max(0, total);
        }

        private void RemoveExpiredEffects()
        {
            if (ActiveEffects == null)
            {
                return;
            }

            ActiveEffects.RemoveAll(effect => effect == null || effect.IsExpired);
        }

        private static void ApplyStatModifier(CuidStats stats, CuidStatType statType, int amount)
        {
            switch (statType)
            {
                case CuidStatType.Attack:
                    stats.Attack += amount;
                    break;
                case CuidStatType.Defense:
                    stats.Defense += amount;
                    break;
                case CuidStatType.AbilityEffectiveness:
                    stats.AbilityEffectiveness += amount;
                    break;
                case CuidStatType.AbilityResistance:
                    stats.AbilityResistance += amount;
                    break;
                case CuidStatType.Speed:
                    stats.Speed += amount;
                    break;
            }
        }
    }

    [Serializable]
    public class BattleState
    {
        public int GridWidth = 8;
        public int GridHeight = 8;
        public int RoundNumber = 1;
        public int CurrentTurnIndex = 0;
        public List<CombatantState> Combatants = new List<CombatantState>(8);
        public List<string> TurnOrder = new List<string>(8);

        public bool IsInsideGrid(GridPosition position)
        {
            return position.IsInside(GridWidth, GridHeight);
        }

        public CombatantState FindCombatant(string combatantId)
        {
            return Combatants.Find(combatant => combatant.CombatantId == combatantId);
        }
    }
}
