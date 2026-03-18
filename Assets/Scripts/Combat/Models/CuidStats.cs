using System;

namespace MidnightFamiliar.Combat.Models
{
    [Serializable]
    public class CuidStats
    {
        // Base stats.
        public int Attack = 10;
        public int Defense = 10;
        public int AbilityEffectiveness = 10;
        public int AbilityResistance = 10;
        public int Speed = 10;

        // Secondary stats.
        public int Constitution = 30;
        public int Damage = 0;
        public int DamageReduction = 0;
        public int AbilityDamage = 0;
        public int AbilityReduction = 0;

        public CuidStats Clone()
        {
            return new CuidStats
            {
                Attack = Attack,
                Defense = Defense,
                AbilityEffectiveness = AbilityEffectiveness,
                AbilityResistance = AbilityResistance,
                Speed = Speed,
                Constitution = Constitution,
                Damage = Damage,
                DamageReduction = DamageReduction,
                AbilityDamage = AbilityDamage,
                AbilityReduction = AbilityReduction
            };
        }
    }
}
