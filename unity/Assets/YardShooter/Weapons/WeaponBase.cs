using UnityEngine;

namespace YardShooter
{
    public abstract class WeaponBase : MonoBehaviour, IWeapon
    {
        public WeaponData data;
        public PlayerMotor motor;
        public PlayerCameraRig rig;
        public PlayerTuning tuning;
        public Transform model, muzzle;

        public WeaponData Data => data;
        public abstract string HudText { get; }
        public abstract float HudFill { get; }
        public virtual float CrosshairSpreadDeg => 0.3f;
        public virtual void Refill() { }

        public virtual void OnEquip() { model.gameObject.SetActive(true); Attach(motor.FirstPerson); }
        public virtual void OnUnequip() { model.gameObject.SetActive(false); }
        public virtual void OnPerspectiveChanged(bool fp) { Attach(fp); }
        public abstract void Tick(WeaponInput input);

        protected void Attach(bool fp)
        {
            if (!model) return;
            model.SetParent(fp ? rig.fpHand : rig.tpHand, false);
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
        }

        // 1인칭은 카메라 레이 그대로, 3인칭은 화면 중앙 레이가 맞은 점을 향해 총구에서 다시 쏜다.
        protected Ray ShotRay(float range, float spreadDeg, out float far)
        {
            var ar = rig.AimRay();
            var dir = Hits.RandomCone(ar.direction, spreadDeg);
            if (motor.FirstPerson) { far = range; return new Ray(ar.origin, dir); }
            Vector3 point = Physics.Raycast(ar.origin, dir, out var h, range + 10f, Hits.Mask, QueryTriggerInteraction.Ignore)
                ? h.point : ar.origin + dir * range;
            var m = muzzle.position;
            far = Mathf.Min(range, Vector3.Distance(m, point) + 0.5f);
            return new Ray(m, (point - m).normalized);
        }

        protected void HitEnemy(Collider c, float damage, Vector3 point, bool melee, Vector3 knock = default, float postureBreak = 0)
        {
            var hb = c.GetComponent<Hitbox>();
            if (!hb || hb.owner.Dead) return;
            bool head = data.headshotAllowed && hb.part == HitPart.Head;
            float dmg = damage * (head ? tuning.headshotMultiplier : 1f);
            hb.owner.TakeDamage(dmg, point, knock, postureBreak);
            CombatFx.I.Number(point, dmg, head ? new Color(1, 0.85f, 0.2f) : Color.white);
            CombatFx.I.Spark(point, melee ? Color.white : new Color(1, 0.8f, 0.3f));
            CombatFx.I.Hitstop(melee ? tuning.hitstopMelee : tuning.hitstopRanged);
        }
    }
}
