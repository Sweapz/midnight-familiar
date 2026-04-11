using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public class CuidUnit
    {
        public string UnitId = Guid.NewGuid().ToString("N");
        public string SpeciesId = "cuid_species";
        public string DisplayName = "Cuid";
        public int Level = 1;
        public CuidType PrimaryType = CuidType.Ember;
        public CuidType SecondaryType = CuidType.Ember;
        public CuidStats Stats = new CuidStats();
        public CuidTrait Trait = new CuidTrait();
        public List<CuidAction> Actions = new List<CuidAction>(4);
        public int CurrentHealth = 30;

        public bool HasSecondaryType => SecondaryType != PrimaryType;

        public IEnumerable<CuidType> GetTypes()
        {
            yield return PrimaryType;

            if (HasSecondaryType && SecondaryType != PrimaryType)
            {
                yield return SecondaryType;
            }
        }

        public void InitializeHealth()
        {
            CurrentHealth = Mathf.Max(1, Stats.Constitution);
        }

        public CuidUnit Clone()
        {
            var copy = new CuidUnit
            {
                UnitId = UnitId,
                SpeciesId = SpeciesId,
                DisplayName = DisplayName,
                Level = Level,
                PrimaryType = PrimaryType,
                SecondaryType = SecondaryType,
                Stats = Stats != null ? Stats.Clone() : new CuidStats(),
                Trait = Trait != null
                    ? new CuidTrait
                    {
                        Id = Trait.Id,
                        DisplayName = Trait.DisplayName,
                        Description = Trait.Description
                    }
                    : new CuidTrait(),
                CurrentHealth = CurrentHealth
            };

            if (Actions != null)
            {
                foreach (CuidAction action in Actions)
                {
                    copy.Actions.Add(action != null ? action.Clone() : new CuidAction());
                }
            }

            return copy;
        }
    }
}
