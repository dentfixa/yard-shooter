// 플레이 방법: V로 시점 전환(0.25초 보간). 3인칭은 가슴 피벗·우측 어깨, 벽에 막히면 SphereCast로 당긴다.
// 우클릭 조준 시 거리/어깨가 바뀌고 조준 레이 원점이 화면 중앙보다 약간 위로 이동한다.
// 무기는 AimRay()와 AimPoint()만 사용하므로 카메라 모드와 무기 모드가 서로 모른다.
// 1인칭 무기 스웨이는 FpHand에 스프링으로 적용된다.
// 카메라 셰이크는 CombatFx의 타이머를 읽는다.
using UnityEngine;

namespace YardShooter
{
    public class PlayerCameraRig : MonoBehaviour
    {
        public PlayerTuning t;
        public PlayerMotor motor;
        public PlayerCombat combat;
        public Camera cam;
        public Transform fpHand, tpHand;

        float blend, dist, shoulder;
        Vector3 swayPos, swayVel;
        Vector3 fpHandBase;
        int envMask;

        void Start()
        {
            dist = t.tpDistance; shoulder = t.tpShoulder;
            cam.fieldOfView = t.fov;
            fpHandBase = fpHand.localPosition;
            envMask = Hits.Mask;
        }

        public bool AimingTP => !motor.FirstPerson && motor.Aiming && !combat.IsSword;
        public float CrosshairRaise => AimingTP ? t.aimCrosshairRaise : 0f;

        public Ray AimRay()
        {
            var vp = new Vector3(0.5f, 0.5f + CrosshairRaise * 0.5f, 0f);
            return cam.ViewportPointToRay(vp);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            blend = Mathf.MoveTowards(blend, motor.FirstPerson ? 0 : 1, dt / t.switchTime);
            float b = Mathf.SmoothStep(0, 1, blend);
            motor.SetBodyVisible(b > 0.35f);

            var fpPos = motor.transform.position + Vector3.up * motor.Eye;
            float k = Mathf.Min(1, dt * 12f);
            dist = Mathf.Lerp(dist, AimingTP ? t.aimDistance : t.tpDistance, k);
            shoulder = Mathf.Lerp(shoulder, AimingTP ? t.aimShoulder : t.tpShoulder, k);

            var pivot = motor.transform.position + Vector3.up * t.tpPivotHeight * (motor.Height / t.height);
            var right = motor.Right;
            var shoulderPt = pivot + right * shoulder;
            if (Physics.SphereCast(pivot, t.probeRadius, right, out var sh, shoulder, envMask, QueryTriggerInteraction.Ignore))
                shoulderPt = pivot + right * Mathf.Max(0, sh.distance);
            var back = -motor.LookDir;
            float d = dist;
            if (Physics.SphereCast(shoulderPt, t.probeRadius, back, out var hit, dist, envMask, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(t.tpMinDistance, hit.distance);
            var tpPos = shoulderPt + back * d;

            cam.transform.position = Vector3.Lerp(fpPos, tpPos, b);
            cam.transform.rotation = Quaternion.Euler(motor.Pitch, motor.Yaw, 0);
            if (CombatFx.I && CombatFx.I.ShakeTimer > 0)
                cam.transform.position += Random.insideUnitSphere * t.shakeAmplitude * (CombatFx.I.ShakeTimer / t.shakeTime);

            var look = motor.input.Look.ReadValue<Vector2>() * (motor.input.LookIsMouse ? t.mouseSensitivity : 0);
            var local = Quaternion.Euler(0, -motor.Yaw, 0) * motor.Velocity;
            var target = new Vector3(-look.x * t.swayMouseScale / t.mouseSensitivity - local.x * t.swayMoveScale,
                                     -look.y * t.swayMouseScale / t.mouseSensitivity, -local.z * t.swayMoveScale * 0.5f);
            target = Vector3.ClampMagnitude(target, t.swayMaxPos);
            swayVel += (target - swayPos) * t.swayStiffness * dt;
            swayVel *= Mathf.Max(0, 1 - t.swayDamping * dt);
            swayPos = Vector3.ClampMagnitude(swayPos + swayVel * dt, t.swayMaxPos);
            fpHand.localPosition = fpHandBase + swayPos;
            fpHand.localRotation = Quaternion.Euler(-swayPos.y / t.swayMaxPos * t.swayMaxRotDeg, swayPos.x / t.swayMaxPos * t.swayMaxRotDeg, 0);
            tpHand.rotation = Quaternion.Euler(motor.Pitch, motor.Yaw, 0);
        }
    }
}
