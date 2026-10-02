// 플레이 방법: 1번 소총. 좌클릭 홀드 연사, R 재장전, 탄창 0에서 트리거를 누르면 자동 재장전.
// 연사할수록 산포가 커지고(크로스헤어 벌어짐) 멈추면 0.18초에 복귀한다.
// 반동은 카메라 pitch에 적용 후 0.12초 동안 60% 되돌아온다.
// 3인칭에서는 총구→착탄점 트레이서가 보이며 착탄은 크로스헤어와 일치한다.
// 헤드샷 1.75배.
using UnityEngine;

namespace YardShooter
{
    public class RifleWeapon : WeaponBase
    {
        RifleStats S => data.rifle;
        public int Mag, Reserve;
        float cooldown, spread, sinceShot = 9, reloadTimer;
        int reloadStage;
        public AudioSource audioSrc;
        public AudioClip fire, shell, click, reload1, reload2;

        void Start() { Refill(); spread = S.spreadMin; }
        public override void Refill() { Mag = S.magSize; Reserve = S.reserve; }
        public override string HudText => reloadTimer > 0 ? "재장전…" : $"{Mag} / {Reserve}";
        public override float HudFill => reloadTimer > 0 ? 1 - reloadTimer / S.reloadTime : (float)Mag / S.magSize;
        public override float CrosshairSpreadDeg => spread;
        public override void OnUnequip() { base.OnUnequip(); reloadTimer = 0; }

        void StartReload()
        {
            if (reloadTimer > 0 || Mag == S.magSize || Reserve <= 0) return;
            reloadTimer = S.reloadTime; reloadStage = 0;
        }

        public override void Tick(WeaponInput input)
        {
            float dt = Time.deltaTime;
            cooldown -= dt; sinceShot += dt;
            if (sinceShot > 60f / S.rpm * 1.5f)
                spread = Mathf.MoveTowards(spread, S.spreadMin, (S.spreadMax - S.spreadMin) / S.spreadRecoverTime * dt);

            if (reloadTimer > 0)
            {
                reloadTimer -= dt;
                float p = 1 - reloadTimer / S.reloadTime;
                if (reloadStage == 0 && p > 0.35f) { Play(reload1); reloadStage = 1; }
                if (reloadStage == 1 && p > 0.85f) { Play(reload2); reloadStage = 2; }
                if (reloadTimer <= 0) { int n = Mathf.Min(S.magSize - Mag, Reserve); Mag += n; Reserve -= n; }
                return;
            }
            if (input.reloadPressed) { StartReload(); return; }
            if (input.fireHeld && cooldown <= 0)
            {
                if (Mag > 0) Fire();
                else { if (input.firePressed) Play(click); StartReload(); }
            }
        }

        void Fire()
        {
            float interval = 60f / S.rpm;
            Mag--; cooldown = interval; sinceShot = 0;
            var ray = ShotRay(S.range, spread, out float far);
            spread = Mathf.Min(S.spreadMax, spread + S.spreadPerSec * interval);
            Vector3 end = ray.origin + ray.direction * far;
            if (Physics.Raycast(ray, out var h, far, Hits.Mask, QueryTriggerInteraction.Ignore))
            {
                end = h.point;
                if (h.collider.GetComponent<Hitbox>()) HitEnemy(h.collider, S.damage, h.point, false);
                else CombatFx.I.Spark(h.point, Color.gray, 4);
            }
            CombatFx.I.Tracer(muzzle.position, end, new Color(1, 0.95f, 0.6f), S.tracerTime, motor.FirstPerson ? 0.01f : 0.025f);
            motor.AddRecoil(S.kickVertical, Random.Range(-S.kickHorizontal, S.kickHorizontal), S.recoverFraction, S.recoverTime);
            Play(fire); Play(shell);
        }

        void Play(AudioClip c) { if (audioSrc && c) audioSrc.PlayOneShot(c); }
    }
}
