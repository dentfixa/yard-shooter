using UnityEngine;

namespace YardShooter
{
    public enum WeaponKind { Rifle, Laser, Blast, Sword }

    [System.Serializable]
    public class RifleStats
    {
        public float damage, rpm, reloadTime, range;
        public int magSize, reserve;
        public float spreadMin, spreadPerSec, spreadMax, spreadRecoverTime;
        public float kickVertical, kickHorizontal, recoverFraction, recoverTime, tracerTime;
    }

    [System.Serializable]
    public class LaserStats
    {
        public float holdThreshold, pulseDamage, pulseCooldown, pulseRadius, range, afterglow;
        public float beamDps, beamTick, maxEnergy, drainPerSec, regenPerSec, overheatTime, beamRadius, pierceMultiplier;
    }

    [System.Serializable]
    public class BlastStats
    {
        public float speed, gravityScale, radius, damageCenter, damageEdge, selfDamageMultiplier;
        public float enemyImpulse, playerKnockSpeed, cooldown, shockwaveTime, maxLifetime;
        public int ammo, predictSteps, subSteps;
        public float predictStepTime;
    }

    [System.Serializable]
    public class SwingStats
    {
        public bool vertical;
        public float arcDegrees, range, damage, activeTime, recoveryTime, direction, postureBreak;
    }

    [System.Serializable]
    public class SwordStats
    {
        public float drawTime, moveMultiplier, inputBuffer, cancelFraction, windup, bladeRadius, hiltOffset;
        public SwingStats[] combo;
        public float guardArc, guardReduce, parryWindow, parryStagger, parryBonus;
        public float maxStamina, guardDrain, guardHitCost, staminaRegen, regenDelay;
        public float dodgeDistance, dodgeTime, dodgeIFrames, dodgeCooldown, dodgeCost;
    }

    [CreateAssetMenu(menuName = "YardShooter/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public WeaponKind kind;
        public string displayName;
        public Color crosshairColor = Color.white;
        public bool headshotAllowed;
        public RifleStats rifle;
        public LaserStats laser;
        public BlastStats blast;
        public SwordStats sword;
    }
}
