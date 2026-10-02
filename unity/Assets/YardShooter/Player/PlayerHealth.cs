using UnityEngine;

namespace YardShooter
{
    public class PlayerHealth : MonoBehaviour
    {
        public PlayerTuning t;
        public PlayerMotor motor;
        public PlayerCombat combat;
        public float Hp;
        public bool Dead;
        public float LastHurtTime = -9;
        public System.Action<Vector3> Hurt;
        float deadTimer;
        Vector3 spawn;

        void Start() { Hp = t.maxHealth; spawn = transform.position; }

        public enum Result { Hit, Parried, Dodged }

        public Result TakeDamage(float amount, Vector3 from, bool melee, bool self = false)
        {
            if (Dead || motor.Invulnerable > 0) return Result.Dodged;
            if (!self && combat.Sword != null && combat.IsSword)
            {
                var r = combat.Sword.ResolveIncoming(ref amount, from, melee);
                if (r == Result.Parried) return r;
            }
            Hp -= amount;
            LastHurtTime = Time.time;
            if (!self) Hurt?.Invoke(from);
            if (Hp <= 0) { Hp = 0; Dead = true; deadTimer = t.respawnDelay; }
            return Result.Hit;
        }

        void Update()
        {
            if (!Dead) return;
            deadTimer -= Time.deltaTime;
            if (deadTimer <= 0)
            {
                Dead = false; Hp = t.maxHealth; motor.Teleport(spawn);
                combat.Refill();
            }
        }
    }
}
