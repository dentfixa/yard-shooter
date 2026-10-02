// 플레이 방법: WASD 이동, Shift 달리기, Space 점프, C 웅크리기, V로 1인칭/3인칭 전환.
// 3인칭에서 우클릭 홀드 시 어깨 조준(거리 2.2m, 크로스헤어가 약간 위).
// 웅크린 상태에서 천장이 막혀 있으면 일어서지 않는다. 경사 45도, 스텝 0.35m.
// 몸통은 1인칭에서 ShadowsOnly로 바꿔 그림자만 남긴다.
// 구르기는 장검 데이터의 스태미나/쿨다운을 쓰지만 어떤 무기에서도 가능하다.
using UnityEngine;

namespace YardShooter
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public PlayerTuning t;
        public GameInput input;
        public Transform bodyVisual;
        public Renderer[] bodyRenderers;

        public float Yaw, Pitch, BodyYaw, Eye, Height;
        public Vector3 Velocity;
        public bool FirstPerson = true, Aiming, Crouching, Grounded;
        public float SpeedMultiplier = 1f;
        public bool AlignBodyTight;
        public float Invulnerable;
        public Vector3 PendingKnock;

        CharacterController cc;
        float recoilPending, recoilRate;
        float dodgeTimer, dodgeCooldown;
        Vector3 dodgeDir;
        public System.Action<bool> PerspectiveChanged;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = t.height; cc.radius = t.radius; cc.center = Vector3.up * t.height / 2;
            cc.slopeLimit = t.slopeLimit; cc.stepOffset = t.stepOffset;
            Height = t.height; Eye = t.eyeStand; Yaw = transform.eulerAngles.y; BodyYaw = Yaw;
        }

        public Vector3 Forward => Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
        public Vector3 Right => Quaternion.Euler(0, Yaw, 0) * Vector3.right;
        public Vector3 LookDir => Quaternion.Euler(Pitch, Yaw, 0) * Vector3.forward;
        public Vector3 Chest => transform.position + Vector3.up * Height * 0.72f;
        public bool CanDodge => dodgeCooldown <= 0 && dodgeTimer <= 0;

        public void AddRecoil(float kickDeg, float yawDeg, float recoverFraction, float recoverTime)
        {
            Pitch -= kickDeg; Yaw += yawDeg;
            recoilPending += kickDeg * recoverFraction;
            recoilRate = kickDeg * recoverFraction / recoverTime;
        }

        public void StartDodge(SwordStats s, Vector2 move)
        {
            var d = Forward * move.y + Right * move.x;
            if (d.sqrMagnitude < 0.01f) d = Forward;
            dodgeDir = d.normalized; dodgeTimer = s.dodgeTime; dodgeCooldown = s.dodgeCooldown; Invulnerable = s.dodgeIFrames;
            dodgeSpeed = s.dodgeDistance / s.dodgeTime;
        }
        float dodgeSpeed;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            var look = input.LookDelta(t);
            Yaw += look.x;
            Pitch = Mathf.Clamp(Pitch - look.y, t.pitchMin, t.pitchMax);
            if (recoilPending > 0)
            {
                float r = Mathf.Min(recoilPending, recoilRate * dt);
                Pitch += r; recoilPending -= r;
            }
            if (input.View.WasPressedThisFrame()) { FirstPerson = !FirstPerson; PerspectiveChanged?.Invoke(FirstPerson); }

            bool wantCrouch = input.Crouch.IsPressed();
            float targetH = wantCrouch ? t.crouchHeight : t.height;
            if (!wantCrouch && Height < t.height)
            {
                var top = transform.position + Vector3.up * (Height - t.radius);
                if (Physics.SphereCast(top, t.radius * 0.9f, Vector3.up, out _, t.height - Height + 0.05f, Hits.Mask, QueryTriggerInteraction.Ignore)) targetH = Height;
            }
            float hRate = (t.height - t.crouchHeight) / t.crouchBlendTime;
            Height = Mathf.MoveTowards(Height, targetH, hRate * dt);
            Crouching = Height < t.height - 0.05f;
            cc.height = Height; cc.center = Vector3.up * Height / 2;
            Eye = Mathf.MoveTowards(Eye, Crouching ? t.eyeCrouch : t.eyeStand, (t.eyeStand - t.eyeCrouch) / t.crouchBlendTime * dt);

            var move = input.Move.ReadValue<Vector2>();
            var wish = Vector3.ClampMagnitude(Forward * move.y + Right * move.x, 1f);
            float speed = Crouching ? t.crouchSpeed : input.Sprint.IsPressed() && move.y > 0 ? t.sprintSpeed : t.walkSpeed;
            var target = wish * speed * SpeedMultiplier;
            float a = Mathf.Min(1f, t.acceleration * dt * (Grounded ? 1f : t.airControl));
            Velocity.x += (target.x - Velocity.x) * a;
            Velocity.z += (target.z - Velocity.z) * a;

            dodgeCooldown -= dt; Invulnerable -= dt;
            if (dodgeTimer > 0) { dodgeTimer -= dt; Velocity.x = dodgeDir.x * dodgeSpeed; Velocity.z = dodgeDir.z * dodgeSpeed; }

            if (Grounded && input.Jump.WasPressedThisFrame()) Velocity.y = t.jumpSpeed;
            else if (Grounded && Velocity.y < 0) Velocity.y = -2f;
            Velocity.y += t.gravity * dt;
            Velocity += PendingKnock; PendingKnock = Vector3.zero;

            var flags = cc.Move(Velocity * dt);
            Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && Velocity.y > 0) Velocity.y = 0;

            // 3인칭: 몸통은 카메라 yaw를 프레임당 제한 각도로 따라간다(이동 방향과 분리)
            if (FirstPerson) BodyYaw = Yaw;
            else
            {
                BodyYaw = Mathf.MoveTowardsAngle(BodyYaw, Yaw, t.bodyTurnDegPerFrame * dt * 60f);
                if (AlignBodyTight)
                {
                    float diff = Mathf.DeltaAngle(BodyYaw, Yaw);
                    if (Mathf.Abs(diff) > t.swordAlignDeg) BodyYaw = Yaw - Mathf.Sign(diff) * t.swordAlignDeg;
                }
            }
            if (bodyVisual)
            {
                bodyVisual.rotation = Quaternion.Euler(0, BodyYaw, 0);
                bodyVisual.localScale = new Vector3(1, Height / t.height, 1);
            }
        }

        public void SetBodyVisible(bool visible)
        {
            foreach (var r in bodyRenderers)
                r.shadowCastingMode = visible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        public void Teleport(Vector3 p)
        {
            cc.enabled = false; transform.position = p; cc.enabled = true; Velocity = Vector3.zero;
        }
    }
}
