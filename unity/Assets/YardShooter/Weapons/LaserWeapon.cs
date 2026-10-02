// 플레이 방법: 2번 레이저. 좌클릭 탭 = 펄스(34, 쿨 0.42초), 0.18초 이상 홀드 = 지속 빔(0.1초 틱).
// 지속 빔은 에너지를 소모하고 0이 되면 0.6초 과열, 손을 떼면 회복. HUD 바와 같은 값을 쓴다.
// 유리(Cover.Thin)는 1회 관통하며 이후 데미지 55%, ThickCover는 막힌다.
// 빔 시작은 항상 총구, 3인칭 조준은 화면 중앙 히트 포인트로 보정한다.
// 판정은 SphereCastAll(반경 0.06/0.04), 비주얼 LineRenderer와 분리.
using System.Linq;
using UnityEngine;

namespace YardShooter
{
    public class LaserWeapon : WeaponBase
    {
        LaserStats S => data.laser;
        public float Energy;
        public float Overheat;
        float cooldown, holdTime, tickAcc;
        bool holding, beaming;
        public LineRenderer beam;
        public Transform beamEnd;

        void Start() { Energy = S.maxEnergy; }
        public override void Refill() { Energy = S.maxEnergy; Overheat = 0; }
        public override string HudText => Overheat > 0 ? "과열" : $"에너지 {Mathf.CeilToInt(Energy)}";
        public override float HudFill => Energy / S.maxEnergy;
        public override void OnUnequip() { base.OnUnequip(); StopBeam(); holding = false; }

        void StopBeam() { beaming = false; tickAcc = 0; beam.enabled = false; beamEnd.gameObject.SetActive(false); }

        Vector3 Trace(Ray ray, float far, float damage, float radius)
        {
            var hits = Physics.SphereCastAll(ray, radius, far, Hits.Mask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
            float mult = 1f; bool pierced = false;
            foreach (var h in hits)
            {
                var p = h.distance <= 0 ? ray.origin : h.point;
                if (h.collider.GetComponent<Hitbox>()) { HitEnemy(h.collider, damage * mult, p, false); return p; }
                var cover = Hits.CoverOf(h.collider);
                if (cover == CoverType.Thin && !pierced) { pierced = true; mult = S.pierceMultiplier; continue; }
                return p;
            }
            return ray.origin + ray.direction * far;
        }

        public override void Tick(WeaponInput input)
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (Overheat > 0) Overheat -= dt;
            if (input.firePressed) { holding = true; holdTime = 0; }
            if (holding && input.fireHeld)
            {
                holdTime += dt;
                if (holdTime >= S.holdThreshold && Overheat <= 0 && Energy > 0) beaming = true;
            }
            if (holding && !input.fireHeld)
            {
                if (holdTime < S.holdThreshold && cooldown <= 0 && Overheat <= 0) Pulse();
                holding = false; StopBeam();
            }
            if (beaming)
            {
                Energy -= S.drainPerSec * dt;
                var ray = ShotRay(S.range, 0, out float far);
                Vector3 end = Physics.Raycast(ray, out var vh, far, Hits.Mask, QueryTriggerInteraction.Ignore) ? vh.point : ray.origin + ray.direction * far;
                tickAcc += dt;
                while (tickAcc >= S.beamTick) { tickAcc -= S.beamTick; end = Trace(ray, far, S.beamDps * S.beamTick, S.beamRadius); }
                beam.enabled = true; beam.SetPosition(0, muzzle.position); beam.SetPosition(1, end);
                beamEnd.gameObject.SetActive(true); beamEnd.position = end;
                if (Energy <= 0) { Energy = 0; Overheat = S.overheatTime; StopBeam(); holding = false; }
            }
            else if (!input.fireHeld) Energy = Mathf.Min(S.maxEnergy, Energy + S.regenPerSec * dt);
        }

        void Pulse()
        {
            cooldown = S.pulseCooldown;
            var ray = ShotRay(S.range, 0, out float far);
            var end = Trace(ray, far, S.pulseDamage, S.pulseRadius);
            CombatFx.I.Tracer(muzzle.position, end, Color.cyan, S.afterglow, 0.05f);
        }
    }
}
