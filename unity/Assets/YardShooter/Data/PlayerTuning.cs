using UnityEngine;

namespace YardShooter
{
    [CreateAssetMenu(menuName = "YardShooter/Player Tuning")]
    public class PlayerTuning : ScriptableObject
    {
        [Header("Body")]
        public float height, radius, walkSpeed, sprintSpeed, acceleration, airControl;
        public float jumpSpeed, gravity, slopeLimit, stepOffset;
        public float crouchHeight, crouchSpeed, crouchBlendTime, eyeStand, eyeCrouch;
        public float maxHealth, respawnDelay;
        [Header("Look")]
        public float mouseSensitivity, padLookSpeed, pitchMin, pitchMax;
        [Header("Camera")]
        public float switchTime, fov, tpDistance, tpShoulder, tpPivotHeight, tpMinDistance, probeRadius;
        public float aimDistance, aimShoulder, aimCrosshairRaise, bodyTurnDegPerFrame, swordAlignDeg;
        public float shakeAmplitude, shakeTime;
        [Header("Sway")]
        public float swayMaxPos, swayMaxRotDeg, swayStiffness, swayDamping, swayMouseScale, swayMoveScale;
        [Header("Combat common")]
        public float weaponSwitchTime, fireBufferTime, hitstopRanged, hitstopMelee, headshotMultiplier, damageFloatTime;
    }
}
