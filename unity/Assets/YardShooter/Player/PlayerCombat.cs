// 플레이 방법: 1 소총, 2 레이저, 3 광역, 4 장검 / 휠·LB·RB 순환.
// 전환 후 0.28초는 캔슬 가능 윈도우로 다른 슬롯을 바로 눌러도 된다.
// 전환 중 누른 사격은 0.12초 버퍼로 전환 직후 발사된다.
// PlayerCombat은 현재 IWeapon 하나만 Tick한다.
// 구르기(LeftAlt)와 스태미나 회복은 무기와 무관하게 여기서 처리한다.
using UnityEngine;

namespace YardShooter
{
    public class PlayerCombat : MonoBehaviour
    {
        public PlayerTuning t;
        public GameInput input;
        public PlayerMotor motor;
        public PlayerHealth health;
        public PlayerCameraRig rig;
        public IWeapon[] Weapons;
        public int Index;
        public SwordWeapon Sword;

        float switchTimer, fireBuffer;
        public IWeapon Current => Weapons[Index];
        public bool IsSword => Current is SwordWeapon;

        public void Init(IWeapon[] weapons)
        {
            Weapons = weapons;
            foreach (var w in weapons) if (w is SwordWeapon s) Sword = s;
            motor.PerspectiveChanged += fp => { foreach (var w in Weapons) w.OnPerspectiveChanged(fp); };
            Current.OnEquip();
        }

        public void Refill() { foreach (var w in Weapons) if (w is WeaponBase b) b.Refill(); }

        void Equip(int i)
        {
            if (i == Index) return;
            Current.OnUnequip();
            Index = i;
            Current.OnEquip();
            switchTimer = t.weaponSwitchTime; fireBuffer = 0;
        }

        void Update()
        {
            if (Weapons == null || health.Dead) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < 4; i++) if (input.Slots[i].WasPressedThisFrame()) Equip(i);
            int cyc = input.CycleDelta();
            if (cyc != 0) Equip(((Index + cyc) % 4 + 4) % 4);

            var wi = new WeaponInput
            {
                firePressed = input.Fire.WasPressedThisFrame(), fireHeld = input.Fire.IsPressed(), fireReleased = input.Fire.WasReleasedThisFrame(),
                altPressed = input.Alt.WasPressedThisFrame(), altHeld = input.Alt.IsPressed(), altReleased = input.Alt.WasReleasedThisFrame(),
                reloadPressed = input.Reload.WasPressedThisFrame(), dodgePressed = input.Dodge.WasPressedThisFrame(),
                move = input.Move.ReadValue<Vector2>(),
            };
            motor.Aiming = wi.altHeld;
            motor.AlignBodyTight = IsSword;
            if (Sword != null) Sword.CommonTick(wi, dt, IsSword);

            if (switchTimer > 0)
            {
                switchTimer -= dt;
                if (wi.firePressed) fireBuffer = t.fireBufferTime;
                bool buffered = fireBuffer > 0;
                fireBuffer -= dt;
                wi.firePressed = wi.fireHeld = wi.fireReleased = false;
                if (switchTimer <= 0 && buffered) { wi.firePressed = true; wi.fireHeld = true; }
            }
            Current.Tick(wi);
        }
    }
}
