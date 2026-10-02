namespace YardShooter
{
    public struct WeaponInput
    {
        public bool firePressed, fireHeld, fireReleased;
        public bool altPressed, altHeld, altReleased;
        public bool reloadPressed, dodgePressed;
        public UnityEngine.Vector2 move;
    }

    public interface IWeapon
    {
        WeaponData Data { get; }
        void OnEquip();
        void OnUnequip();
        void Tick(WeaponInput input);
        void OnPerspectiveChanged(bool firstPerson);
        string HudText { get; }
        float HudFill { get; }
        float CrosshairSpreadDeg { get; }
    }
}
