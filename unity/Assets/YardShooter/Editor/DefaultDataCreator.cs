// 프로젝트를 열면 Resources/YardShooter/에 기본 데이터 에셋이 없을 때 자동 생성한다.
// 이후 튜닝은 생성된 .asset을 인스펙터에서 수정하며, 이 파일은 초기값의 유일한 출처다.
// 메뉴 YardShooter > Create Default Data 로 강제 재생성(덮어쓰기)할 수 있다.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YardShooter.EditorTools
{
    [InitializeOnLoad]
    public static class DefaultDataCreator
    {
        const string Dir = "Assets/YardShooter/Resources/YardShooter";

        static DefaultDataCreator() { EditorApplication.delayCall += () => Create(false); }

        [MenuItem("YardShooter/Create Default Data")]
        static void Force() => Create(true);

        static void Create(bool overwrite)
        {
            Directory.CreateDirectory(Dir);
            bool made = false;
            made |= Make(overwrite, "PlayerTuning", Player);
            made |= Make(overwrite, "Rifle", Rifle);
            made |= Make(overwrite, "Laser", Laser);
            made |= Make(overwrite, "Blast", Blast);
            made |= Make(overwrite, "Sword", Sword);
            made |= Make(overwrite, "Grunt", Grunt);
            made |= Make(overwrite, "Bruiser", Bruiser);
            if (made) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log("YardShooter: 기본 데이터 에셋 생성 완료"); }
        }

        static bool Make<T>(bool overwrite, string name, System.Action<T> fill) where T : ScriptableObject
        {
            string path = $"{Dir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing && !overwrite) return false;
            var a = existing ? existing : ScriptableObject.CreateInstance<T>();
            fill(a);
            if (!existing) AssetDatabase.CreateAsset(a, path); else EditorUtility.SetDirty(a);
            return true;
        }

        static void Player(PlayerTuning t)
        {
            t.height = 1.8f; t.radius = 0.35f; t.walkSpeed = 4.5f; t.sprintSpeed = 7.2f; t.acceleration = 14f; t.airControl = 0.6f;
            t.jumpSpeed = 6.5f; t.gravity = -20f; t.slopeLimit = 45f; t.stepOffset = 0.35f;
            t.crouchHeight = 1.1f; t.crouchSpeed = 2.2f; t.crouchBlendTime = 0.15f; t.eyeStand = 1.62f; t.eyeCrouch = 1.05f;
            t.maxHealth = 100f; t.respawnDelay = 2f;
            t.mouseSensitivity = 0.12f; t.padLookSpeed = 180f; t.pitchMin = -80f; t.pitchMax = 80f;
            t.switchTime = 0.25f; t.fov = 75f; t.tpDistance = 3.2f; t.tpShoulder = 0.45f; t.tpPivotHeight = 1.55f; t.tpMinDistance = 0.6f; t.probeRadius = 0.2f;
            t.aimDistance = 2.2f; t.aimShoulder = 0.65f; t.aimCrosshairRaise = 0.1f; t.bodyTurnDegPerFrame = 12f; t.swordAlignDeg = 8f;
            t.shakeAmplitude = 0.08f; t.shakeTime = 0.18f;
            t.swayMaxPos = 0.02f; t.swayMaxRotDeg = 1.5f; t.swayStiffness = 120f; t.swayDamping = 14f; t.swayMouseScale = 0.0006f; t.swayMoveScale = 0.0025f;
            t.weaponSwitchTime = 0.28f; t.fireBufferTime = 0.12f; t.hitstopRanged = 0.02f; t.hitstopMelee = 0.05f; t.headshotMultiplier = 1.75f; t.damageFloatTime = 0.6f;
        }

        static void Rifle(WeaponData w)
        {
            w.kind = WeaponKind.Rifle; w.displayName = "Rifle"; w.crosshairColor = Color.white; w.headshotAllowed = true;
            w.rifle = new RifleStats
            {
                damage = 18, rpm = 620, magSize = 30, reserve = 120, reloadTime = 1.7f, range = 80,
                spreadMin = 0.35f, spreadPerSec = 1.1f, spreadMax = 3.8f, spreadRecoverTime = 0.18f,
                kickVertical = 0.55f, kickHorizontal = 0.22f, recoverFraction = 0.6f, recoverTime = 0.12f, tracerTime = 0.05f,
            };
        }

        static void Laser(WeaponData w)
        {
            w.kind = WeaponKind.Laser; w.displayName = "Laser"; w.crosshairColor = Color.cyan; w.headshotAllowed = true;
            w.laser = new LaserStats
            {
                holdThreshold = 0.18f, pulseDamage = 34, pulseCooldown = 0.42f, pulseRadius = 0.04f, range = 60, afterglow = 0.15f,
                beamDps = 46, beamTick = 0.1f, maxEnergy = 100, drainPerSec = 28, regenPerSec = 18, overheatTime = 0.6f, beamRadius = 0.06f,
                pierceMultiplier = 0.55f,
            };
        }

        static void Blast(WeaponData w)
        {
            w.kind = WeaponKind.Blast; w.displayName = "Blast"; w.crosshairColor = new Color(1f, 0.6f, 0.18f); w.headshotAllowed = false;
            w.blast = new BlastStats
            {
                speed = 18, gravityScale = 0.65f, radius = 4.2f, damageCenter = 72, damageEdge = 26, selfDamageMultiplier = 0.3f,
                enemyImpulse = 900, playerKnockSpeed = 2.5f, cooldown = 0.85f, shockwaveTime = 0.25f, maxLifetime = 5,
                ammo = 6, predictSteps = 24, subSteps = 12, predictStepTime = 0.1f,
            };
        }

        static void Sword(WeaponData w)
        {
            w.kind = WeaponKind.Sword; w.displayName = "Sword"; w.crosshairColor = new Color(0.8f, 0.8f, 0.8f); w.headshotAllowed = false;
            w.sword = new SwordStats
            {
                drawTime = 0.3f, moveMultiplier = 0.92f, inputBuffer = 0.2f, cancelFraction = 0.35f, windup = 0.07f, bladeRadius = 0.12f, hiltOffset = 0.35f,
                combo = new[]
                {
                    new SwingStats { vertical = false, arcDegrees = 140, range = 2.1f, damage = 28, activeTime = 0.12f, recoveryTime = 0.18f, direction = 1 },
                    new SwingStats { vertical = false, arcDegrees = 150, range = 2.2f, damage = 32, activeTime = 0.12f, recoveryTime = 0.18f, direction = -1 },
                    new SwingStats { vertical = true, arcDegrees = 80, range = 2.4f, damage = 46, activeTime = 0.12f, recoveryTime = 0.24f, postureBreak = 0.4f },
                },
                guardArc = 100, guardReduce = 0.6f, parryWindow = 0.16f, parryStagger = 0.45f, parryBonus = 0.4f,
                maxStamina = 100, guardDrain = 18, guardHitCost = 12, staminaRegen = 22, regenDelay = 0.4f,
                dodgeDistance = 4.5f, dodgeTime = 0.35f, dodgeIFrames = 0.18f, dodgeCooldown = 0.7f, dodgeCost = 28,
            };
        }

        static void Grunt(EnemyData e)
        {
            e.kind = EnemyKind.Grunt; e.maxHealth = 80; e.radius = 0.4f; e.height = 1.8f; e.mass = 70; e.moveSpeed = 3f; e.flinchTime = 0.15f;
            e.spawnCount = 4; e.color = new Color(0.36f, 0.54f, 0.23f);
            e.range = 18; e.damage = 8; e.rpm = 180; e.burstCount = 3; e.burstPauseTime = 1f; e.reaimInterval = 2f; e.baseHitChance = 0.55f;
            e.ragdollFadeDelay = 2f; e.respawnDelay = 8f;
        }

        static void Bruiser(EnemyData e)
        {
            e.kind = EnemyKind.Bruiser; e.maxHealth = 180; e.radius = 0.6f; e.height = 2.3f; e.mass = 160; e.moveSpeed = 3.4f; e.flinchTime = 0.1f;
            e.spawnCount = 2; e.color = new Color(0.6f, 0.23f, 0.16f);
            e.triggerRange = 7.5f; e.windupTime = 0.4f; e.chargeDistance = 6; e.chargeTime = 0.35f; e.damage = 22; e.recoverTime = 0.7f; e.cooldown = 2.2f;
            e.ragdollFadeDelay = 2f; e.respawnDelay = 8f;
        }
    }
}
