// 플레이 방법: 4번 장검. 좌클릭 3단 콤보(가로→반대 가로→내려찍기), 각 단 후반 35%에서만 다음 단 캔슬, 입력 버퍼 0.2초.
// 우클릭 가드: 정면 100도 원거리 60% 경감. 가드 시작 0.16초 안에 근접(Bruiser 돌진)을 받으면 패링.
// 패링 성공: 공격자 0.45초 경직, 다음 공격 +40%, 스태미나 소모 없음.
// LeftAlt 구르기(4.5m, 무적 0.18초). 크로스헤어 대신 현재 콤보 범위 호가 표시된다.
// 판정은 활성 프레임 동안 자루 끝·검끝 소켓을 잇는 캡슐(반경 0.12m)이며, 같은 공격에 같은 대상 1회.
using System.Collections.Generic;
using UnityEngine;

namespace YardShooter
{
    public class SwordWeapon : WeaponBase
    {
        SwordStats S => data.sword;
        public float Stamina;
        public bool Guarding, ParryBuff;
        public int Stage = -1;
        float t, bufferTimer, guardStartTime = -9, sinceGuardRelease = 9, drawTimer;
        readonly HashSet<Enemy> hitSet = new();
        public Vector3 Hilt, Tip;
        Vector3 prevHilt, prevTip;
        public TrailRenderer trail;
        public System.Action ParrySucceeded;

        void Start() { Stamina = S.maxStamina; }
        public override void Refill() { Stamina = S.maxStamina; }
        public override string HudText => ParryBuff ? "스태미나 · 강화!" : $"스태미나 {Mathf.CeilToInt(Stamina)}";
        public override float HudFill => Stamina / S.maxStamina;

        public override void OnEquip()
        {
            model.gameObject.SetActive(true);
            model.SetParent(null);
            drawTimer = S.drawTime; motor.SpeedMultiplier = S.moveMultiplier;
            ComputeSockets(true);
        }
        public override void OnUnequip() { model.gameObject.SetActive(false); Stage = -1; Guarding = false; motor.SpeedMultiplier = 1; }
        public override void OnPerspectiveChanged(bool fp) { }

        public SwingStats CurrentSwing => S.combo[Mathf.Clamp(Stage < 0 ? 0 : Stage + (bufferTimer > 0 ? 1 : 0), 0, S.combo.Length - 1)];

        bool InActive => Stage >= 0 && t >= S.windup && t <= S.windup + S.combo[Stage].activeTime;

        // 무기와 무관한 구르기·스태미나 회복을 PlayerCombat이 매 프레임 호출
        public void CommonTick(WeaponInput input, float dt, bool equipped)
        {
            if (input.dodgePressed && motor.CanDodge && Stamina >= S.dodgeCost && (!equipped || Stage < 0))
            {
                Stamina -= S.dodgeCost; motor.StartDodge(S, input.move);
            }
            sinceGuardRelease += dt;
            if (Guarding) Stamina = Mathf.Max(0, Stamina - S.guardDrain * dt);
            else if (sinceGuardRelease >= S.regenDelay) Stamina = Mathf.Min(S.maxStamina, Stamina + S.staminaRegen * dt);
        }

        public PlayerHealth.Result ResolveIncoming(ref float amount, Vector3 from, bool melee)
        {
            var to = from - motor.transform.position; to.y = 0;
            bool front = Vector3.Angle(motor.Forward, to) <= S.guardArc / 2;
            if (!Guarding || !front) return PlayerHealth.Result.Hit;
            if (melee && Time.time - guardStartTime <= S.parryWindow)
            {
                ParryBuff = true;
                CombatFx.I.Hitstop(tuning.hitstopMelee * 2);
                CombatFx.I.Spark(motor.Chest + motor.Forward * 0.6f, Color.white, 16);
                ParrySucceeded?.Invoke();
                return PlayerHealth.Result.Parried;
            }
            amount *= 1 - S.guardReduce;
            Stamina = Mathf.Max(0, Stamina - S.guardHitCost);
            return PlayerHealth.Result.Hit;
        }

        public float ParryStagger => S.parryStagger;

        public override void Tick(WeaponInput input)
        {
            float dt = Time.deltaTime;
            if (drawTimer > 0) drawTimer -= dt;
            bool wantGuard = input.altHeld && Stage < 0 && Stamina > 0 && drawTimer <= 0;
            if (wantGuard && !Guarding) { Guarding = true; guardStartTime = Time.time; }
            if (!wantGuard && Guarding) { Guarding = false; sinceGuardRelease = 0; }

            if (input.firePressed && drawTimer <= 0) bufferTimer = S.inputBuffer; else bufferTimer -= dt;

            prevHilt = Hilt; prevTip = Tip;
            if (Stage < 0)
            {
                if (bufferTimer > 0 && !Guarding) Begin(0);
            }
            else
            {
                var sw = S.combo[Stage];
                float total = S.windup + sw.activeTime + sw.recoveryTime;
                bool wasActive = InActive;
                t += dt;
                if (bufferTimer > 0 && t >= total * (1 - S.cancelFraction) && Stage < S.combo.Length - 1) Begin(Stage + 1);
                else if (t >= total) Stage = -1;
                if (wasActive || InActive) { ComputeSockets(true); Sweep(); }
            }
            ComputeSockets(false);
            trail.emitting = InActive;
        }

        void Begin(int stage)
        {
            Stage = stage; t = 0; hitSet.Clear(); bufferTimer = -1;
            ComputeSockets(true); prevHilt = Hilt; prevTip = Tip;
        }

        // 소켓 위치를 공격 진행도에서 직접 계산해 모델에 적용 → 1·3인칭 모두 비주얼과 판정이 같은 값
        void ComputeSockets(bool snap)
        {
            float yaw = motor.FirstPerson ? motor.Yaw : motor.BodyYaw;
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right, c = motor.Chest;
            Vector3 hilt, tip;
            if (Stage >= 0)
            {
                var sw = S.combo[Stage];
                float k = Mathf.Clamp01((t - S.windup) / sw.activeTime);
                Vector3 dir;
                if (!sw.vertical) dir = Quaternion.AngleAxis(sw.direction * (-sw.arcDegrees / 2 + sw.arcDegrees * k), Vector3.up) * f;
                else { float e = sw.arcDegrees * 0.6f - sw.arcDegrees * k; dir = f * Mathf.Cos(e * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(e * Mathf.Deg2Rad); }
                hilt = c + dir * S.hiltOffset; tip = c + dir * sw.range;
            }
            else if (Guarding)
            {
                hilt = c + f * 0.5f + r * 0.4f + Vector3.up * 0.1f;
                tip = hilt - r + Vector3.up * 0.25f;
            }
            else
            {
                float drawK = Mathf.Clamp01(1 - drawTimer / S.drawTime);
                hilt = c + f * 0.35f + r * 0.35f + Vector3.up * (-0.15f - (1 - drawK) * 0.4f);
                tip = hilt + (f * 0.55f + Vector3.up * 0.8f).normalized;
            }
            float s = snap || Stage >= 0 ? 1 : Mathf.Min(1, Time.deltaTime * 18f);
            Hilt = Vector3.Lerp(Hilt, hilt, s); Tip = Vector3.Lerp(Tip, tip, s);
            model.position = Hilt;
            model.rotation = Quaternion.LookRotation(Tip - Hilt);
        }

        void Sweep()
        {
            if (!InActive) return;
            var sw = S.combo[Stage];
            const int N = 5;
            foreach (var en in Enemy.All)
            {
                if (en.Dead || hitSet.Contains(en)) continue;
                en.Segment(out var lo, out var hi);
                for (int i = 0; i <= N; i++)
                {
                    float k = i / (float)N;
                    var h = Vector3.Lerp(prevHilt, Hilt, k); var tp = Vector3.Lerp(prevTip, Tip, k);
                    if (Hits.SegmentSegmentDistance(h, tp, lo, hi) > S.bladeRadius + en.data.radius) continue;
                    hitSet.Add(en);
                    float dmg = sw.damage;
                    if (ParryBuff) { dmg *= 1 + S.parryBonus; ParryBuff = false; }
                    HitEnemy(en.BodyCollider, dmg, Vector3.Lerp(tp, h, 0.3f), true, default, sw.postureBreak);
                    break;
                }
            }
        }
    }
}
