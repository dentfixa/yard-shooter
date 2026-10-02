// 플레이 방법: 3번 광역. 좌클릭 즉시 발사, 우클릭 홀드 시 포물선(24스텝) 낙하 마커, 놓으면 발사.
// 예측과 실제 투사체는 같은 고정 서브스텝 적분을 공유해 마커 오차가 생기지 않는다.
// 폭발은 OverlapSphere + 중심→가슴 엄폐 레이, 중심 72 → 가장자리 26 선형 감쇠.
// 자기 폭발은 30% 자해 + 약 2.5m/s 넉백. 탄약 6, 쿨 0.85초.
// 충격파 구체가 0.25초에 반경까지 커진 뒤 페이드, 카메라 셰이크.
using System.Collections.Generic;
using UnityEngine;

namespace YardShooter
{
    public class BlastWeapon : WeaponBase
    {
        BlastStats S => data.blast;
        public int Ammo;
        float cooldown;
        public LineRenderer arc;
        public Transform marker;
        public PlayerHealth health;

        class Shell { public Transform t; public Vector3 pos, vel; public float life, acc; }
        readonly List<Shell> shells = new();

        void Start() { Ammo = S.ammo; }
        public override void Refill() { Ammo = S.ammo; }
        public override string HudText => $"광역탄 {Ammo}";
        public override float HudFill => cooldown > 0 ? 1 - cooldown / S.cooldown : 1;
        public override float CrosshairSpreadDeg => 0.6f;
        public override void OnUnequip() { base.OnUnequip(); arc.enabled = false; marker.gameObject.SetActive(false); }

        float SubDt => S.predictStepTime / S.subSteps;

        void Launch(out Vector3 pos, out Vector3 vel)
        {
            var ar = rig.AimRay();
            pos = muzzle.position;
            Vector3 dir = ar.direction;
            if (!motor.FirstPerson)
            {
                var aimPt = Physics.Raycast(ar, out var h, 200f, Hits.Mask, QueryTriggerInteraction.Ignore) ? h.point : ar.origin + ar.direction * 200f;
                dir = (aimPt - pos).normalized;
            }
            vel = dir * S.speed;
        }

        bool Step(ref Vector3 pos, ref Vector3 vel, float dt, out Vector3 hitPoint)
        {
            var prev = pos;
            vel += Physics.gravity.normalized * (-tuning.gravity) * S.gravityScale * dt;
            pos += vel * dt;
            var seg = pos - prev;
            if (Physics.Raycast(prev, seg.normalized, out var h, seg.magnitude, Hits.Mask, QueryTriggerInteraction.Ignore)) { hitPoint = h.point; return true; }
            hitPoint = default; return false;
        }

        public override void Tick(WeaponInput input)
        {
            cooldown -= Time.deltaTime;
            if (input.altHeld) Predict(); else { arc.enabled = false; marker.gameObject.SetActive(false); }
            if (input.altReleased || (input.firePressed && !input.altHeld)) Fire();
        }

        void Predict()
        {
            Launch(out var pos, out var vel);
            var pts = new List<Vector3> { pos };
            bool hit = false; Vector3 hp = default;
            for (int s = 0; s < S.predictSteps && !hit; s++)
            {
                for (int k = 0; k < S.subSteps; k++)
                    if (Step(ref pos, ref vel, SubDt, out hp)) { hit = true; break; }
                pts.Add(hit ? hp : pos);
            }
            arc.enabled = true; arc.positionCount = pts.Count; arc.SetPositions(pts.ToArray());
            marker.gameObject.SetActive(hit);
            if (hit) marker.position = hp + Vector3.up * 0.03f;
        }

        void Fire()
        {
            if (cooldown > 0 || Ammo <= 0) return;
            Ammo--; cooldown = S.cooldown;
            Launch(out var pos, out var vel);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * 0.24f;
            go.GetComponent<Renderer>().material = CombatFx.I.Additive(new Color(1, 0.6f, 0.2f));
            shells.Add(new Shell { t = go.transform, pos = pos, vel = vel });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = shells.Count - 1; i >= 0; i--)
            {
                var s = shells[i];
                s.acc += dt; s.life += dt;
                bool hit = false; Vector3 hp = default;
                while (s.acc >= SubDt && !hit) { s.acc -= SubDt; hit = Step(ref s.pos, ref s.vel, SubDt, out hp); }
                s.t.position = s.pos;
                if (hit || s.life > S.maxLifetime) { Explode(hit ? hp : s.pos); Destroy(s.t.gameObject); shells.RemoveAt(i); }
            }
        }

        float Falloff(float d) => Mathf.Lerp(S.damageCenter, S.damageEdge, Mathf.Clamp01(d / S.radius));

        void Explode(Vector3 c)
        {
            var center = c + Vector3.up * 0.1f;
            var done = new HashSet<Enemy>();
            foreach (var col in Physics.OverlapSphere(center, S.radius, Hits.Mask, QueryTriggerInteraction.Ignore))
            {
                var hb = col.GetComponent<Hitbox>();
                if (!hb || hb.owner.Dead || !done.Add(hb.owner)) continue;
                var chest = hb.owner.Chest;
                if (Blocked(center, chest)) continue;
                float d = Vector3.Distance(center, col.ClosestPoint(center));
                var knock = (chest - center).normalized * S.enemyImpulse / hb.owner.data.mass;
                HitEnemy(hb.owner.BodyCollider, Falloff(d), chest, false, knock);
            }
            var pc = motor.Chest;
            float pd = Vector3.Distance(center, pc);
            if (pd <= S.radius && !Blocked(center, pc))
            {
                health.TakeDamage(Falloff(pd) * S.selfDamageMultiplier, center, false, true);
                motor.PendingKnock += (pc - center).normalized * S.playerKnockSpeed;
            }
            var wave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(wave.GetComponent<Collider>());
            wave.transform.position = center;
            var m = CombatFx.I.Additive(new Color(1, 0.6f, 0.25f, 0.6f));
            wave.GetComponent<Renderer>().material = m;
            CombatFx.I.Add(wave, S.shockwaveTime * 2, (g, k) =>
            {
                g.transform.localScale = Vector3.one * Mathf.Max(0.01f, Mathf.Min(1, k * 2) * S.radius * 2);
                var col = m.color; col.a = k < 0.5f ? 0.6f : 0.6f * (1 - (k - 0.5f) * 2); m.color = col;
            });
            CombatFx.I.Spark(center, new Color(1, 0.5f, 0.1f), 20);
            CombatFx.I.Shake();
        }

        static bool Blocked(Vector3 a, Vector3 b)
        {
            var d = b - a;
            if (!Physics.Raycast(a, d.normalized, out var h, d.magnitude - 0.05f, Hits.Mask, QueryTriggerInteraction.Ignore)) return false;
            return !h.collider.GetComponent<Hitbox>();
        }
    }
}
