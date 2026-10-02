using UnityEngine;

namespace YardShooter
{
    public enum EnemyKind { Grunt, Bruiser }

    [CreateAssetMenu(menuName = "YardShooter/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        public EnemyKind kind;
        public float maxHealth, radius, height, mass, moveSpeed, flinchTime;
        public int spawnCount;
        public Color color = Color.gray;
        [Header("Grunt")]
        public float range, damage, rpm, burstPauseTime, reaimInterval, baseHitChance;
        public int burstCount;
        [Header("Bruiser")]
        public float triggerRange, windupTime, chargeDistance, chargeTime, recoverTime, cooldown;
        [Header("Shared")]
        public float ragdollFadeDelay, respawnDelay;
    }
}
