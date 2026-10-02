// 모든 튜닝 수치의 단일 원천. 코드에는 복제하지 않는다.
export const PLAYER = {
  height: 1.8, radius: 0.35, walk: 4.5, sprint: 7.2, accel: 14, airControl: 0.6,
  jump: 6.5, gravity: -20, step: 0.35,
  crouchHeight: 1.1, crouchSpeed: 2.2, crouchBlend: 0.15,
  eyeStand: 1.62, eyeCrouch: 1.05,
  maxHealth: 100, pitchMin: -80, pitchMax: 80, sensitivity: 0.0022,
  padLookSpeed: 3.2, selfKnockSpeed: 2.5, respawnDelay: 2,
};

export const CAMERA = {
  switchTime: 0.25, fov: 75,
  tpDist: 3.2, tpShoulder: 0.45, tpHeight: 1.55, tpMinDist: 0.6, probeRadius: 0.2,
  aimDist: 2.2, aimShoulder: 0.65, aimCrosshairNdcY: 0.1,
  bodyTurnDegPerFrame: 12, swordAlignDeg: 8,
  shakeAmp: 0.08, shakeTime: 0.18,
};

export const SWAY = { maxPos: 0.02, maxRotDeg: 1.5, stiffness: 120, damping: 14, mouseScale: 0.0006, moveScale: 0.0025 };

export const COMMON = {
  switchTime: 0.28, fireBuffer: 0.12,
  hitstopRanged: 0.02, hitstopMelee: 0.05,
  headMult: 1.75, floatTime: 0.6,
};

export const WEAPONS = {
  rifle: {
    name: '소총', color: '#ffffff', damage: 18, rpm: 620, mag: 30, reserve: 120, reload: 1.7, range: 80,
    spreadMin: 0.35, spreadPerSec: 1.1, spreadMax: 3.8, spreadRecover: 0.18,
    kickV: 0.55, kickH: 0.22, recoverFrac: 0.6, recoverTime: 0.12, tracerTime: 0.05,
  },
  laser: {
    name: '레이저', color: '#3ff', holdThreshold: 0.18,
    pulseDamage: 34, pulseCooldown: 0.42, pulseRadius: 0.04, range: 60, afterglow: 0.15,
    beamDps: 46, beamTick: 0.1, energy: 100, drain: 28, regen: 18, overheat: 0.6, beamRadius: 0.06,
    pierceMult: 0.55,
  },
  blast: {
    name: '광역', color: '#ff9a2e', speed: 18, gravityScale: 0.65, radius: 4.2,
    dmgCenter: 72, dmgEdge: 26, selfMult: 0.3, impulse: 9, ammo: 6, cooldown: 0.85,
    predictSteps: 24, predictStepTime: 0.1, shockTime: 0.25, maxLife: 5,
  },
  sword: {
    name: '장검', color: '#ccc', draw: 0.3, moveMult: 0.92, buffer: 0.2, cancelFrac: 0.35,
    windup: 0.07, bladeRadius: 0.12, hiltOffset: 0.35,
    combo: [
      { type: 'h', arc: 140, range: 2.1, damage: 28, active: 0.12, recovery: 0.18, dir: 1 },
      { type: 'h', arc: 150, range: 2.2, damage: 32, active: 0.12, recovery: 0.18, dir: -1 },
      { type: 'v', arc: 80, range: 2.4, damage: 46, active: 0.12, recovery: 0.24, breakTime: 0.4 },
    ],
    guardArc: 100, guardRangedReduce: 0.6, parryWindow: 0.16, parryStagger: 0.45, parryBonus: 0.4,
    stamina: 100, guardDrain: 18, guardHitCost: 12, regen: 22, regenDelay: 0.4,
    dodgeDist: 4.5, dodgeTime: 0.35, dodgeIFrames: 0.18, dodgeCooldown: 0.7, dodgeCost: 28,
  },
};

export const ENEMIES = {
  respawnDelay: 8, ragdollFade: 2,
  grunt: {
    hp: 80, radius: 0.4, height: 1.8, mass: 70, speed: 3.0, range: 18, damage: 8, rpm: 180,
    burst: 3, burstPause: 1.0, reaim: 2, baseHit: 0.55, count: 4, flinch: 0.15,
  },
  bruiser: {
    hp: 180, radius: 0.6, height: 2.3, mass: 160, speed: 3.4, triggerRange: 7.5,
    windup: 0.4, chargeDist: 6, chargeTime: 0.35, damage: 22, recover: 0.7, cooldown: 2.2,
    count: 2, flinch: 0.1,
  },
};
