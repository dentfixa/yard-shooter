// 플레이: 클릭으로 시작 → WASD 이동, V 시점 전환, 1~4 무기, 좌/우클릭 공격·보조, Alt/Q 구르기.
// 구조: Input → Player(이동·카메라) → PlayerCombat(IWeapon 4종) → Enemies → HUD. 수치는 data.js에만 있다.
import * as THREE from 'three';
import { PLAYER as P, CAMERA as C, SWAY, COMMON, WEAPONS as W, ENEMIES as E } from './data.js';

const DEG = Math.PI / 180;
const V3 = THREE.Vector3;
const UP = new V3(0, 1, 0);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const $ = (id) => document.getElementById(id);

// ---------------- renderer / scene ----------------
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.setSize(innerWidth, innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
document.body.prepend(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x8fa6bd);
scene.fog = new THREE.Fog(0x8fa6bd, 45, 110);
const camera = new THREE.PerspectiveCamera(C.fov, innerWidth / innerHeight, 0.03, 300);
camera.rotation.order = 'YXZ';
scene.add(camera);
addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});

scene.add(new THREE.HemisphereLight(0xdde8ff, 0x4a4030, 0.75));
const sun = new THREE.DirectionalLight(0xfff1d8, 2.2);
sun.position.set(18, 30, 12);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
Object.assign(sun.shadow.camera, { left: -28, right: 28, top: 24, bottom: -24, near: 1, far: 80 });
scene.add(sun);
const lamp = new THREE.PointLight(0xffaa66, 25, 18);
lamp.position.set(0, 4, 0);
scene.add(lamp);

// ---------------- world ----------------
const solids = [];      // 충돌용 AABB
const worldMeshes = []; // 레이 타겟(환경)
const mat = (c, o = {}) => new THREE.MeshStandardMaterial({ color: c, roughness: 0.85, ...o });

function addBox(x, z, w, h, d, color, opts = {}) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), opts.material || mat(color));
  m.position.set(x, (opts.y || 0) + h / 2, z);
  m.castShadow = !opts.thin; m.receiveShadow = true;
  m.userData = { thick: !!opts.thick, thin: !!opts.thin };
  scene.add(m); worldMeshes.push(m);
  m.updateMatrixWorld();
  solids.push({ box: new THREE.Box3().setFromObject(m), mesh: m });
  return m;
}

const YW = 40, YD = 30;
const ground = new THREE.Mesh(new THREE.PlaneGeometry(YW, YD), mat(0x6d7562));
ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; ground.userData = {};
scene.add(ground); worldMeshes.push(ground);
const grid = new THREE.GridHelper(40, 20, 0x000000, 0x000000);
grid.material.opacity = 0.08; grid.material.transparent = true; grid.scale.z = YD / YW; scene.add(grid);

addBox(0, -YD / 2 - 0.25, YW + 1, 3, 0.5, 0x8a8a8a);
addBox(0, YD / 2 + 0.25, YW + 1, 3, 0.5, 0x8a8a8a);
addBox(-YW / 2 - 0.25, 0, 0.5, 3, YD, 0x8a8a8a);
addBox(YW / 2 + 0.25, 0, 0.5, 3, YD, 0x8a8a8a);
// 엄폐 박스 8
[[-8, 4], [8, 4], [-4, -3], [4, -3], [-14, -6], [14, -6], [-2, -10], [10, -11]].forEach(([x, z], i) =>
  addBox(x, z, 1.6, i % 2 ? 1.2 : 1.6, 1.6, 0xa07a4a));
// 발판 2 (높이 다름)
addBox(-13, 8, 5, 0.8, 4, 0x5a6c80);
addBox(13, 9, 5, 1.3, 4, 0x5a6c80);
// ThickCover 벽 2
addBox(-6, -7, 6, 3, 0.6, 0x444a52, { thick: true });
addBox(7, 0, 0.6, 3, 6, 0x444a52, { thick: true });
// 유리(얇은 엄폐) 2 — 레이저 1회 관통 확인용
const glassMat = new THREE.MeshStandardMaterial({ color: 0x99ddff, transparent: true, opacity: 0.28, roughness: 0.1 });
addBox(0, 2, 3, 2.2, 0.08, 0, { thin: true, material: glassMat });
addBox(-10, -1, 0.08, 2.2, 3, 0, { thin: true, material: glassMat });

// ---------------- audio ----------------
let actx = null;
function sfx(kind) {
  if (!actx) return;
  const t = actx.currentTime, g = actx.createGain(); g.connect(actx.destination);
  const tone = (type, f0, f1, dur, vol) => {
    const o = actx.createOscillator(); o.type = type;
    o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(f1, 1), t + dur);
    g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    o.connect(g); o.start(t); o.stop(t + dur);
  };
  const noise = (dur, vol, freq = 1200) => {
    const b = actx.createBuffer(1, actx.sampleRate * dur, actx.sampleRate), d = b.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    const s = actx.createBufferSource(); s.buffer = b;
    const f = actx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = freq;
    g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    s.connect(f); f.connect(g); s.start(t);
  };
  switch (kind) {
    case 'rifle': noise(0.09, 0.25, 2500); break;
    case 'shell': tone('triangle', 3200, 2600, 0.05, 0.03); break;
    case 'click': tone('square', 900, 800, 0.03, 0.05); break;
    case 'reload1': tone('square', 300, 200, 0.06, 0.08); break;
    case 'reload2': tone('square', 500, 700, 0.08, 0.08); break;
    case 'pulse': tone('sawtooth', 1800, 400, 0.12, 0.08); break;
    case 'beam': tone('sine', 700, 690, 0.1, 0.03); break;
    case 'overheat': tone('square', 200, 80, 0.3, 0.08); break;
    case 'launch': tone('sine', 300, 120, 0.15, 0.15); break;
    case 'boom': noise(0.5, 0.45, 500); break;
    case 'swing': noise(0.12, 0.08, 3000); break;
    case 'hit': tone('square', 220, 120, 0.06, 0.08); break;
    case 'parry': tone('triangle', 1600, 2400, 0.2, 0.15); break;
    case 'enemyShot': noise(0.06, 0.08, 1500); break;
    case 'hurt': tone('sawtooth', 160, 90, 0.15, 0.12); break;
  }
}

// ---------------- input ----------------
const keys = new Set(), pressedKeys = new Set();
const mouse = { dx: 0, dy: 0, held: [false, false, false], pressed: [false, false, false], released: [false, false, false], wheel: 0 };
let locked = false, started = false;
const blockKeys = ['AltLeft', 'AltRight', 'Space', 'Tab', 'ControlLeft', 'KeyC'];
addEventListener('keydown', (e) => {
  if (blockKeys.includes(e.code)) e.preventDefault();
  if (!keys.has(e.code)) pressedKeys.add(e.code);
  keys.add(e.code);
});
addEventListener('keyup', (e) => { keys.delete(e.code); if (e.code.startsWith('Alt')) e.preventDefault(); });
addEventListener('blur', () => { keys.clear(); mouse.held = [false, false, false]; });
const canvas = renderer.domElement;
$('start').addEventListener('click', () => {
  if (!actx) actx = new (window.AudioContext || window.webkitAudioContext)();
  canvas.requestPointerLock();
});
canvas.addEventListener('mousedown', (e) => {
  if (!locked) { canvas.requestPointerLock(); return; }
  mouse.held[e.button] = true; mouse.pressed[e.button] = true;
});
addEventListener('mouseup', (e) => { if (mouse.held[e.button]) mouse.released[e.button] = true; mouse.held[e.button] = false; });
addEventListener('mousemove', (e) => { if (locked) { mouse.dx += e.movementX; mouse.dy += e.movementY; } });
addEventListener('wheel', (e) => { if (locked) mouse.wheel += Math.sign(e.deltaY); }, { passive: true });
addEventListener('contextmenu', (e) => e.preventDefault());
document.addEventListener('pointerlockchange', () => {
  locked = document.pointerLockElement === canvas;
  $('start').style.display = locked ? 'none' : 'flex';
  if (locked) started = true;
});
// 포인터 잠금이 거부되는 환경(임베디드 브라우저 등)에서도 게임은 시작되게 한다
document.addEventListener('pointerlockerror', () => { started = true; $('start').style.display = 'none'; });

const padPrev = {};
function readInput() {
  const k = (c) => keys.has(c), kp = (c) => pressedKeys.has(c);
  const inp = {
    mx: (k('KeyD') ? 1 : 0) - (k('KeyA') ? 1 : 0),
    my: (k('KeyW') ? 1 : 0) - (k('KeyS') ? 1 : 0),
    lookX: mouse.dx * P.sensitivity, lookY: mouse.dy * P.sensitivity,
    sprint: k('ShiftLeft'), crouch: k('KeyC') || k('ControlLeft'),
    jumpP: kp('Space'), viewP: kp('KeyV'), reloadP: kp('KeyR'),
    dodgeP: kp('AltLeft') || kp('KeyQ'), debugP: kp('KeyP'),
    fireP: mouse.pressed[0], fireH: mouse.held[0], fireR: mouse.released[0],
    altP: mouse.pressed[2], altH: mouse.held[2], altR: mouse.released[2],
    slot: kp('Digit1') ? 0 : kp('Digit2') ? 1 : kp('Digit3') ? 2 : kp('Digit4') ? 3 : -1,
    cycle: mouse.wheel,
  };
  const pad = navigator.getGamepads ? [...navigator.getGamepads()].find(Boolean) : null;
  if (pad) {
    const dz = (v) => (Math.abs(v) < 0.15 ? 0 : v);
    const b = (i) => !!(pad.buttons[i] && pad.buttons[i].pressed);
    const bp = (i) => b(i) && !padPrev[i];
    const br = (i) => !b(i) && padPrev[i];
    if (dz(pad.axes[0]) || dz(pad.axes[1])) { inp.mx = dz(pad.axes[0]); inp.my = -dz(pad.axes[1]); }
    inp.lookX += dz(pad.axes[2] || 0) * P.padLookSpeed * dtReal;
    inp.lookY += dz(pad.axes[3] || 0) * P.padLookSpeed * dtReal;
    inp.sprint ||= b(10); inp.crouch ||= b(1); inp.jumpP ||= bp(0); inp.viewP ||= bp(3);
    inp.reloadP ||= bp(2); inp.dodgeP ||= bp(11);
    inp.fireP ||= bp(7); inp.fireH ||= b(7); inp.fireR ||= br(7);
    inp.altP ||= bp(6); inp.altH ||= b(6); inp.altR ||= br(6);
    if (bp(5)) inp.cycle += 1; if (bp(4)) inp.cycle -= 1;
    pad.buttons.forEach((_, i) => (padPrev[i] = b(i)));
  }
  pressedKeys.clear(); mouse.dx = mouse.dy = 0; mouse.wheel = 0;
  mouse.pressed = [false, false, false]; mouse.released = [false, false, false];
  return inp;
}

// ---------------- raycast helpers ----------------
const raycaster = new THREE.Raycaster();
const enemies = [];
const enemyParts = () => enemies.filter((e) => !e.dead).flatMap((e) => e.parts);
function castAll(origin, dir, far, withEnemies = true) {
  raycaster.set(origin, dir); raycaster.far = far;
  return raycaster.intersectObjects(withEnemies ? worldMeshes.concat(enemyParts()) : worldMeshes, false);
}
function castFirst(origin, dir, far, withEnemies = true) { return castAll(origin, dir, far, withEnemies)[0] || null; }
function blocked(a, b) {
  const d = b.clone().sub(a), len = d.length();
  return !!castFirst(a, d.normalize(), len - 0.05, false);
}
function randomCone(dir, deg) {
  if (deg <= 0) return dir.clone();
  const a = Math.random() * Math.PI * 2, r = Math.sqrt(Math.random()) * Math.tan(deg * DEG);
  const t = Math.abs(dir.y) < 0.99 ? new V3(0, 1, 0) : new V3(1, 0, 0);
  const u = new V3().crossVectors(dir, t).normalize(), v = new V3().crossVectors(dir, u);
  return dir.clone().addScaledVector(u, Math.cos(a) * r).addScaledVector(v, Math.sin(a) * r).normalize();
}
function segSegDist(p1, q1, p2, q2) {
  const d1 = q1.clone().sub(p1), d2 = q2.clone().sub(p2), r = p1.clone().sub(p2);
  const a = d1.dot(d1), e = d2.dot(d2), f = d2.dot(r);
  let s, t;
  const c = d1.dot(r), b = d1.dot(d2), den = a * e - b * b;
  s = den > 1e-8 ? clamp((b * f - c * e) / den, 0, 1) : 0;
  t = (b * s + f) / e;
  if (t < 0) { t = 0; s = clamp(-c / a, 0, 1); } else if (t > 1) { t = 1; s = clamp((b - c) / a, 0, 1); }
  return p1.clone().addScaledVector(d1, s).distanceTo(p2.clone().addScaledVector(d2, t));
}

// ---------------- effects ----------------
const effects = [];
function addEffect(obj, life, update) { scene.add(obj); effects.push({ obj, t: 0, life, update }); }
function tracer(a, b, color, life, width = 0.015) {
  const len = a.distanceTo(b);
  const m = new THREE.Mesh(new THREE.CylinderGeometry(width, width, len, 6, 1, true),
    new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false }));
  m.position.copy(a).lerp(b, 0.5);
  m.quaternion.setFromUnitVectors(UP, b.clone().sub(a).normalize());
  addEffect(m, life, (e, k) => (e.obj.material.opacity = 0.9 * (1 - k)));
}
const sparkGeo = new THREE.BoxGeometry(0.05, 0.05, 0.05);
function sparks(p, color = 0xffdd66, n = 8) {
  for (let i = 0; i < n; i++) {
    const m = new THREE.Mesh(sparkGeo, new THREE.MeshBasicMaterial({ color }));
    m.position.copy(p);
    const v = new V3(Math.random() - 0.5, Math.random() * 0.8, Math.random() - 0.5).normalize().multiplyScalar(3 + Math.random() * 3);
    addEffect(m, 0.2, (e, k, dt) => { e.obj.position.addScaledVector(v, dt); e.obj.scale.setScalar(1 - k); });
  }
}
let hitstop = 0, shakeT = 0;
const floaters = [];
function floatNumber(p, val, color = '#fff') {
  const el = document.createElement('div');
  el.textContent = Math.round(val); el.style.color = color;
  $('floaters').appendChild(el);
  floaters.push({ el, p: p.clone(), t: 0 });
}
function toast(text, time = 1.2) { $('toast').textContent = text; toastT = time; }
let toastT = 0;

// ---------------- player ----------------
const player = {
  pos: new V3(0, 0, 11), vel: new V3(), yaw: 0, pitch: 0, bodyYaw: 0,
  height: P.height, eye: P.eyeStand, grounded: true, hp: P.maxHealth, dead: false, deadT: 0,
  fp: true, viewBlend: 0, aiming: false, tpDist: C.tpDist, tpShoulder: C.tpShoulder,
  dodgeT: 0, dodgeDir: new V3(), dodgeCd: 0, invuln: 0, knock: new V3(),
  recoilPending: 0, recoilRate: 0, speedMult: 1,
};
const bodyMat = mat(0x3b6fb0);
const body = new THREE.Group();
const torso = new THREE.Mesh(new THREE.CapsuleGeometry(P.radius, P.height - P.radius * 2, 4, 12), bodyMat);
torso.position.y = P.height / 2; torso.castShadow = true;
const headM = new THREE.Mesh(new THREE.BoxGeometry(0.3, 0.12, 0.1), mat(0x222222));
headM.position.set(0, 1.6, -0.3); headM.castShadow = true;
body.add(torso, headM); scene.add(body);
const tpHand = new THREE.Group(); tpHand.position.set(0.28, 1.3, -0.25); body.add(tpHand);
const fpHand = new THREE.Group(); fpHand.position.set(0.22, -0.2, -0.42); camera.add(fpHand);

function forwardOf(yaw) { return new V3(-Math.sin(yaw), 0, -Math.cos(yaw)); }
function rightOf(yaw) { return new V3(Math.cos(yaw), 0, -Math.sin(yaw)); }
function lookDir() { return new V3(0, 0, -1).applyEuler(new THREE.Euler(player.pitch, player.yaw, 0, 'YXZ')); }
function chest() { return player.pos.clone().add(new V3(0, player.height * 0.72, 0)); }

function supportHeight(pos, r) {
  let top = 0;
  for (const s of solids) {
    const b = s.box;
    if (pos.x > b.min.x - r * 0.6 && pos.x < b.max.x + r * 0.6 && pos.z > b.min.z - r * 0.6 && pos.z < b.max.z + r * 0.6
      && b.max.y <= pos.y + P.step + 0.001) top = Math.max(top, b.max.y);
  }
  return top;
}
function pushOut(pos, r, h) {
  for (const s of solids) {
    const b = s.box;
    if (pos.y + h <= b.min.y || pos.y >= b.max.y - P.step) continue;
    const cx = clamp(pos.x, b.min.x, b.max.x), cz = clamp(pos.z, b.min.z, b.max.z);
    let dx = pos.x - cx, dz = pos.z - cz;
    const d = Math.hypot(dx, dz);
    if (d >= r) continue;
    if (d > 1e-5) { pos.x = cx + (dx / d) * r; pos.z = cz + (dz / d) * r; }
    else {
      const opts = [[b.min.x - r - pos.x, 0], [b.max.x + r - pos.x, 0], [0, b.min.z - r - pos.z], [0, b.max.z + r - pos.z]];
      opts.sort((a, c) => Math.abs(a[0] + a[1]) - Math.abs(c[0] + c[1]));
      pos.x += opts[0][0]; pos.z += opts[0][1];
    }
  }
}

function updatePlayer(inp, dt) {
  if (player.dead) {
    player.deadT -= dt;
    if (player.deadT <= 0) respawnPlayer();
    return;
  }
  player.yaw -= inp.lookX;
  player.pitch = clamp(player.pitch - inp.lookY, P.pitchMin * DEG, P.pitchMax * DEG);
  if (player.recoilPending > 0) {
    const r = Math.min(player.recoilPending, player.recoilRate * dt);
    player.pitch -= r; player.recoilPending -= r;
  }
  if (inp.viewP) { player.fp = !player.fp; combat.perspectiveChanged(player.fp); }

  const wantH = inp.crouch ? P.crouchHeight : P.height;
  if (wantH > player.height) {
    const headroom = !castFirst(player.pos.clone().add(new V3(0, player.height, 0)), UP, P.height - player.height + 0.05, false);
    if (headroom) player.height = Math.min(P.height, player.height + (P.height - P.crouchHeight) / P.crouchBlend * dt);
  } else player.height = Math.max(wantH, player.height - (P.height - P.crouchHeight) / P.crouchBlend * dt);
  const crouching = player.height < P.height - 0.05;
  const eyeT = crouching ? P.eyeCrouch : P.eyeStand;
  player.eye += clamp(eyeT - player.eye, -(P.eyeStand - P.eyeCrouch) / P.crouchBlend * dt, (P.eyeStand - P.eyeCrouch) / P.crouchBlend * dt);

  const f = forwardOf(player.yaw), r = rightOf(player.yaw);
  const wish = f.multiplyScalar(inp.my).add(r.multiplyScalar(inp.mx));
  if (wish.lengthSq() > 1) wish.normalize();
  let speed = crouching ? P.crouchSpeed : inp.sprint && inp.my > 0 ? P.sprint : P.walk;
  speed *= player.speedMult;
  const target = wish.multiplyScalar(speed);
  const a = Math.min(1, P.accel * dt * (player.grounded ? 1 : P.airControl));
  player.vel.x += (target.x - player.vel.x) * a;
  player.vel.z += (target.z - player.vel.z) * a;

  player.dodgeCd -= dt; player.invuln -= dt;
  if (player.dodgeT > 0) {
    player.dodgeT -= dt;
    const s = W.sword.dodgeDist / W.sword.dodgeTime;
    player.vel.x = player.dodgeDir.x * s; player.vel.z = player.dodgeDir.z * s;
  }
  if (inp.jumpP && player.grounded) { player.vel.y = P.jump; player.grounded = false; }
  player.vel.y += P.gravity * dt;
  player.vel.add(player.knock); player.knock.set(0, 0, 0);

  const wasGrounded = player.grounded;
  player.pos.addScaledVector(player.vel, dt);
  pushOut(player.pos, P.radius, player.height);
  const sup = supportHeight(player.pos, P.radius);
  if (player.pos.y <= sup + (wasGrounded && player.vel.y <= 0 ? P.step : 0) && player.vel.y <= 0) {
    player.pos.y = sup; player.vel.y = 0; player.grounded = true;
  } else player.grounded = false;
  if (player.vel.y > 0) {
    const hit = castFirst(player.pos.clone().add(new V3(0, player.height - 0.1, 0)), UP, 0.15 + player.vel.y * dt, false);
    if (hit) player.vel.y = 0;
  }
  player.pos.x = clamp(player.pos.x, -YW / 2 + P.radius, YW / 2 - P.radius);
  player.pos.z = clamp(player.pos.z, -YD / 2 + P.radius, YD / 2 - P.radius);

  // 몸통 yaw는 카메라를 따라가되 프레임당 최대 각도로 제한(3인칭 strafe 느낌)
  if (player.fp) player.bodyYaw = player.yaw;
  else {
    let d = Math.atan2(Math.sin(player.yaw - player.bodyYaw), Math.cos(player.yaw - player.bodyYaw));
    const maxStep = C.bodyTurnDegPerFrame * DEG * dt * 60;
    player.bodyYaw += clamp(d, -maxStep, maxStep);
    if (combat.current === sword) {
      d = Math.atan2(Math.sin(player.yaw - player.bodyYaw), Math.cos(player.yaw - player.bodyYaw));
      if (Math.abs(d) > C.swordAlignDeg * DEG) player.bodyYaw = player.yaw - Math.sign(d) * C.swordAlignDeg * DEG;
    }
  }
  body.position.copy(player.pos);
  body.rotation.y = player.bodyYaw;
  torso.scale.y = player.height / P.height;
  torso.position.y = player.height / 2;
  headM.position.y = player.height - 0.2;
}

function startDodge(inpMx, inpMy) {
  const d = forwardOf(player.yaw).multiplyScalar(inpMy).add(rightOf(player.yaw).multiplyScalar(inpMx));
  if (d.lengthSq() < 0.01) d.copy(forwardOf(player.yaw));
  player.dodgeDir.copy(d.normalize());
  player.dodgeT = W.sword.dodgeTime; player.invuln = W.sword.dodgeIFrames; player.dodgeCd = W.sword.dodgeCooldown;
}

// ---------------- camera ----------------
const camPos = new V3();
let swayPos = new V3(), swayVel = new V3(), swayRot = new V3(), swayRotVel = new V3();
function updateCamera(inp, dt) {
  player.viewBlend = clamp(player.viewBlend + (player.fp ? -1 : 1) * dt / C.switchTime, 0, 1);
  const b = player.viewBlend * player.viewBlend * (3 - 2 * player.viewBlend);
  const fpPos = player.pos.clone().add(new V3(0, player.eye, 0));
  const aimNow = player.aiming && combat.current !== sword;
  player.tpDist += ((aimNow ? C.aimDist : C.tpDist) - player.tpDist) * Math.min(1, dt * 12);
  player.tpShoulder += ((aimNow ? C.aimShoulder : C.tpShoulder) - player.tpShoulder) * Math.min(1, dt * 12);
  const pivot = player.pos.clone().add(new V3(0, C.tpHeight * (player.height / P.height), 0));
  const rt = rightOf(player.yaw);
  let shoulderPt = pivot.clone().addScaledVector(rt, player.tpShoulder);
  const sh = castFirst(pivot, rt, player.tpShoulder + C.probeRadius, false);
  if (sh) shoulderPt = pivot.clone().addScaledVector(rt, Math.max(0, sh.distance - C.probeRadius));
  const back = lookDir().negate();
  let dist = player.tpDist;
  // SphereCast 근사: 중심 + 4방향 오프셋 레이 중 가장 가까운 충돌로 당긴다
  const up2 = new V3().crossVectors(back, rt).normalize();
  for (const o of [new V3(), rt.clone().multiplyScalar(C.probeRadius), rt.clone().multiplyScalar(-C.probeRadius), up2.clone().multiplyScalar(C.probeRadius), up2.clone().multiplyScalar(-C.probeRadius)]) {
    const h = castFirst(shoulderPt.clone().add(o), back, dist + C.probeRadius, false);
    if (h) dist = Math.min(dist, h.distance - C.probeRadius);
  }
  dist = Math.max(C.tpMinDist, dist);
  const tpPos = shoulderPt.addScaledVector(back, dist);
  camPos.copy(fpPos).lerp(tpPos, b);
  camera.position.copy(camPos);
  camera.rotation.set(player.pitch, player.yaw, 0, 'YXZ');
  if (shakeT > 0) {
    shakeT -= dt;
    camera.position.add(new V3(Math.random() - 0.5, Math.random() - 0.5, Math.random() - 0.5).multiplyScalar(C.shakeAmp * 2 * (shakeT / C.shakeTime)));
  }
  // FP에서는 몸통을 색/깊이 쓰기만 끄고 그림자는 유지한다
  const hideBody = b < 0.35;
  for (const m of [torso, headM]) { m.material.colorWrite = !hideBody; m.material.depthWrite = !hideBody; }

  // 무기 스웨이: 마우스 델타·이동 속도에 비례, 스프링 복귀
  const hv = new V3(player.vel.x, 0, player.vel.z).applyAxisAngle(UP, -player.yaw);
  const tgt = new V3(-inp.lookX * SWAY.mouseScale / P.sensitivity - hv.x * SWAY.moveScale, inp.lookY * SWAY.mouseScale / P.sensitivity, -hv.z * SWAY.moveScale * 0.5);
  tgt.clampLength(0, SWAY.maxPos);
  swayVel.addScaledVector(tgt.sub(swayPos), SWAY.stiffness * dt).multiplyScalar(Math.max(0, 1 - SWAY.damping * dt));
  swayPos.addScaledVector(swayVel, dt).clampLength(0, SWAY.maxPos);
  fpHand.position.set(0.22 + swayPos.x, -0.2 + swayPos.y, -0.42 + swayPos.z);
  fpHand.rotation.set(swayPos.y / SWAY.maxPos * SWAY.maxRotDeg * DEG, swayPos.x / SWAY.maxPos * SWAY.maxRotDeg * DEG, 0);
  tpHand.rotation.x = player.pitch;
  tpHand.rotation.y = player.yaw - player.bodyYaw;
}

// 크로스헤어가 가리키는 월드 지점. 3인칭 조준 중엔 화면 중앙보다 약간 위.
function aimNdcY() { return !player.fp && player.aiming && combat.current !== sword ? C.aimCrosshairNdcY : 0; }
function aimRay(spreadDeg = 0) {
  camera.updateMatrixWorld();
  raycaster.setFromCamera(new THREE.Vector2(0, aimNdcY()), camera);
  const dir = randomCone(raycaster.ray.direction.clone(), spreadDeg);
  return { origin: raycaster.ray.origin.clone(), dir };
}
// FP는 카메라 레이, TP는 카메라 레이가 맞은 점을 향해 총구에서 다시 쏜다
function weaponShot(muzzle, range, spreadDeg) {
  const ar = aimRay(spreadDeg);
  if (player.fp) return { origin: ar.origin, dir: ar.dir, far: range };
  const h = castFirst(ar.origin, ar.dir, range + 10);
  const point = h ? h.point : ar.origin.clone().addScaledVector(ar.dir, range);
  const dir = point.clone().sub(muzzle).normalize();
  return { origin: muzzle.clone(), dir, far: Math.min(range, muzzle.distanceTo(point) + 0.5) };
}

// ---------------- damage ----------------
function damageEnemy(en, amount, point, { melee = false, part = 'body', headable = false, knock = null, breakTime = 0 } = {}) {
  if (en.dead) return;
  const head = headable && part === 'head';
  const dmg = amount * (head ? COMMON.headMult : 1);
  en.hp -= dmg;
  floatNumber(point, dmg, head ? '#ffdd33' : '#fff');
  sparks(point, melee ? 0xffffff : 0xffcc55);
  sfx('hit');
  hitstop = Math.max(hitstop, melee ? COMMON.hitstopMelee : COMMON.hitstopRanged);
  en.flash = 0.1;
  en.flinch = Math.max(en.flinch, en.d.flinch);
  if (breakTime) en.stun = Math.max(en.stun, breakTime);
  if (knock) en.knock.add(knock.clone().divideScalar(en.d.mass / 10));
  if (en.hp <= 0) en.die();
}

function hurtPlayer(amount, fromPos, { melee = false, self = false } = {}) {
  if (player.dead || player.invuln > 0) return 'dodged';
  const toSrc = fromPos.clone().sub(player.pos).setY(0).normalize();
  const facing = forwardOf(player.yaw);
  const inFront = toSrc.dot(facing) >= Math.cos(W.sword.guardArc / 2 * DEG);
  if (!self && combat.current === sword && sword.guarding && inFront) {
    if (melee && performance.now() / 1000 - sword.guardStart <= W.sword.parryWindow) {
      sword.parrySuccess(); return 'parried';
    }
    amount *= 1 - W.sword.guardRangedReduce;
    sword.stamina = Math.max(0, sword.stamina - W.sword.guardHitCost);
  }
  player.hp -= amount;
  sfx('hurt');
  $('vig').style.opacity = 1; vigT = 0.35;
  if (!self) {
    const ang = Math.atan2(toSrc.x, toSrc.z) - Math.atan2(facing.x, facing.z);
    const el = document.createElement('div'); el.className = 'hd'; el.innerHTML = '<b></b>';
    el.style.transform = `rotate(${-ang}rad)`;
    $('hitdirs').appendChild(el); setTimeout(() => el.remove(), 700);
  }
  if (player.hp <= 0) { player.hp = 0; player.dead = true; player.deadT = P.respawnDelay; toast('사망 — 잠시 후 부활'); }
  return 'hit';
}
let vigT = 0;
function respawnPlayer() {
  player.dead = false; player.hp = P.maxHealth; player.pos.set(0, 0, 11); player.vel.set(0, 0, 0);
  rifle.mag = W.rifle.mag; rifle.reserve = W.rifle.reserve; blast.ammo = W.blast.ammo; laser.energy = W.laser.energy;
}

// ---------------- weapons (IWeapon: onEquip / onUnequip / tick(input) / onPerspectiveChanged) ----------------
function makeGunModel(color, len, girth) {
  const g = new THREE.Group();
  const m = mat(color, { metalness: 0.4 });
  const b = new THREE.Mesh(new THREE.BoxGeometry(girth, girth * 1.4, len), m); b.position.z = -len / 2;
  const grip = new THREE.Mesh(new THREE.BoxGeometry(girth * 0.8, girth * 2, girth), m); grip.position.set(0, -girth * 1.3, -len * 0.15);
  const muzzle = new THREE.Object3D(); muzzle.position.z = -len - 0.02;
  g.add(b, grip, muzzle); g.userData.muzzle = muzzle;
  g.traverse((o) => { if (o.isMesh) o.castShadow = true; });
  return g;
}
function attachModel(model, fp) { (fp ? fpHand : tpHand).add(model); }
function muzzleWorld(model) { model.updateMatrixWorld(true); return model.userData.muzzle.getWorldPosition(new V3()); }

class Rifle {
  constructor() {
    this.d = W.rifle; this.mag = this.d.mag; this.reserve = this.d.reserve; this.cd = 0;
    this.spread = this.d.spreadMin; this.sinceShot = 9; this.reloadT = 0; this.reloadStage = 0;
    this.model = makeGunModel(0x2b2b2b, 0.7, 0.07);
  }
  onEquip() { attachModel(this.model, player.fp); }
  onUnequip() { this.model.removeFromParent(); this.reloadT = 0; }
  onPerspectiveChanged(fp) { if (this.model.parent) attachModel(this.model, fp); }
  startReload() {
    if (this.reloadT > 0 || this.mag === this.d.mag || this.reserve <= 0) return;
    this.reloadT = this.d.reload; this.reloadStage = 0;
  }
  tick(inp, dt) {
    this.cd -= dt; this.sinceShot += dt;
    if (this.sinceShot > 0.02) {
      const rate = (this.d.spreadMax - this.d.spreadMin) / this.d.spreadRecover;
      this.spread = Math.max(this.d.spreadMin, this.spread - rate * dt * (this.sinceShot > this.d.spreadRecover * 0.2 ? 1 : 0));
    }
    if (this.reloadT > 0) {
      this.reloadT -= dt;
      const prog = 1 - this.reloadT / this.d.reload;
      if (this.reloadStage === 0 && prog > 0.35) { sfx('reload1'); this.reloadStage = 1; }
      if (this.reloadStage === 1 && prog > 0.85) { sfx('reload2'); this.reloadStage = 2; }
      if (this.reloadT <= 0) {
        const n = Math.min(this.d.mag - this.mag, this.reserve);
        this.mag += n; this.reserve -= n;
      }
      return;
    }
    if (inp.reloadP) { this.startReload(); return; }
    if (inp.fireH && this.cd <= 0) {
      if (this.mag > 0) this.fire();
      else { if (inp.fireP) sfx('click'); this.startReload(); }
    }
  }
  fire() {
    const d = this.d, interval = 60 / d.rpm;
    this.mag--; this.cd = interval; this.sinceShot = 0;
    const muzzle = muzzleWorld(this.model);
    const shot = weaponShot(muzzle, d.range, this.spread);
    this.spread = Math.min(d.spreadMax, this.spread + d.spreadPerSec * interval);
    const h = castFirst(shot.origin, shot.dir, shot.far);
    const end = h ? h.point : shot.origin.clone().addScaledVector(shot.dir, shot.far);
    if (h && h.object.userData.enemy) damageEnemy(h.object.userData.enemy, d.damage, h.point, { part: h.object.userData.part, headable: true });
    else if (h) sparks(h.point, 0xaaaaaa, 4);
    tracer(muzzle, end, 0xfff2a0, d.tracerTime, player.fp ? 0.006 : 0.015);
    const kick = d.kickV * DEG;
    player.pitch += kick; player.yaw += (Math.random() * 2 - 1) * d.kickH * DEG;
    player.recoilPending += kick * d.recoverFrac; player.recoilRate = (kick * d.recoverFrac) / d.recoverTime;
    sfx('rifle'); setTimeout(() => sfx('shell'), 120);
  }
  hud() {
    return { text: this.reloadT > 0 ? '재장전…' : `${this.mag} / ${this.reserve}`, bar: this.reloadT > 0 ? 1 - this.reloadT / this.d.reload : this.mag / this.d.mag, color: '#fff' };
  }
  get crossSpread() { return this.spread; }
}

class Laser {
  constructor() {
    this.d = W.laser; this.energy = this.d.energy; this.heat = 0; this.cd = 0; this.holdT = 0; this.holding = false;
    this.beaming = false; this.tickAcc = 0;
    this.model = makeGunModel(0x1a5560, 0.55, 0.08);
    this.beam = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 1, 8, 1, true),
      new THREE.MeshBasicMaterial({ color: 0x55ffff, transparent: true, opacity: 0.85, blending: THREE.AdditiveBlending, depthWrite: false }));
    this.beamEnd = new THREE.Mesh(new THREE.SphereGeometry(0.12, 10, 8), this.beam.material);
    this.beam.visible = this.beamEnd.visible = false; scene.add(this.beam, this.beamEnd);
  }
  onEquip() { attachModel(this.model, player.fp); }
  onUnequip() { this.model.removeFromParent(); this.stopBeam(); this.holding = false; }
  onPerspectiveChanged(fp) { if (this.model.parent) attachModel(this.model, fp); }
  stopBeam() { this.beaming = false; this.beam.visible = this.beamEnd.visible = false; this.tickAcc = 0; }
  // 얇은 엄폐(thin)는 1회 관통하며 배율 감소, ThickCover와 일반 벽은 정지
  traceLaser(origin, dir, far, damage, radius) {
    const hits = castAll(origin, dir, far);
    let mult = 1, pierced = false;
    for (const h of hits) {
      if (h.object.userData.thin && !pierced) { pierced = true; mult = this.d.pierceMult; continue; }
      if (h.object.userData.enemy) { damageEnemy(h.object.userData.enemy, damage * mult, h.point, { part: h.object.userData.part, headable: true }); return h.point; }
      return h.point;
    }
    // 레이가 얇은 빔 반경 안으로 스친 적도 판정(두께 판정)
    for (const en of enemies) {
      if (en.dead) continue;
      const c = en.center(); const t = c.clone().sub(origin).dot(dir);
      if (t < 0 || t > far) continue;
      const closest = origin.clone().addScaledVector(dir, t);
      if (closest.distanceTo(c) < en.d.radius + radius && Math.abs(closest.y - c.y) < en.d.height / 2) {
        if (!castFirst(origin, dir, t, false)) { damageEnemy(en, damage * mult, closest); return closest; }
      }
    }
    return origin.clone().addScaledVector(dir, far);
  }
  tick(inp, dt) {
    const d = this.d;
    this.cd -= dt;
    if (this.heat > 0) { this.heat -= dt; if (this.heat <= 0) this.heat = 0; }
    if (inp.fireP) { this.holding = true; this.holdT = 0; }
    if (this.holding && inp.fireH) {
      this.holdT += dt;
      if (this.holdT >= d.holdThreshold && this.heat <= 0 && this.energy > 0) this.beaming = true;
    }
    if (this.holding && !inp.fireH) {
      if (this.holdT < d.holdThreshold && this.cd <= 0 && this.heat <= 0) this.pulse();
      this.holding = false; this.stopBeam();
    }
    if (this.beaming) {
      this.energy -= d.drain * dt;
      const muzzle = muzzleWorld(this.model);
      const shot = weaponShot(muzzle, d.range, 0);
      this.tickAcc += dt;
      let end = null;
      while (this.tickAcc >= d.beamTick) { this.tickAcc -= d.beamTick; end = this.traceLaser(shot.origin, shot.dir, shot.far, d.beamDps * d.beamTick, d.beamRadius); }
      if (!end) { const h = castFirst(shot.origin, shot.dir, shot.far); end = h ? h.point : shot.origin.clone().addScaledVector(shot.dir, shot.far); }
      const len = muzzle.distanceTo(end);
      this.beam.visible = this.beamEnd.visible = true;
      this.beam.scale.set(1, len, 1); this.beam.position.copy(muzzle).lerp(end, 0.5);
      this.beam.quaternion.setFromUnitVectors(UP, end.clone().sub(muzzle).normalize());
      this.beamEnd.position.copy(end);
      if (Math.random() < dt * 10) sfx('beam');
      if (this.energy <= 0) { this.energy = 0; this.heat = d.overheat; this.stopBeam(); this.holding = false; sfx('overheat'); }
    } else if (!inp.fireH) this.energy = Math.min(d.energy, this.energy + d.regen * dt);
  }
  pulse() {
    const d = this.d; this.cd = d.pulseCooldown;
    const muzzle = muzzleWorld(this.model);
    const shot = weaponShot(muzzle, d.range, 0);
    const end = this.traceLaser(shot.origin, shot.dir, shot.far, d.pulseDamage, d.pulseRadius);
    tracer(muzzle, end, 0x66ffff, d.afterglow, 0.035);
    sfx('pulse');
  }
  hud() {
    const t = this.heat > 0 ? '과열' : `에너지 ${Math.ceil(this.energy)}`;
    return { text: t, bar: this.energy / this.d.energy, color: this.heat > 0 ? '#f44' : '#3ff' };
  }
  get crossSpread() { return 0.2; }
}

class Blast {
  constructor() {
    this.d = W.blast; this.ammo = this.d.ammo; this.cd = 0; this.projectiles = [];
    this.model = makeGunModel(0x6b4a20, 0.5, 0.12);
    this.marker = new THREE.Mesh(new THREE.RingGeometry(0.3, 0.45, 24), new THREE.MeshBasicMaterial({ color: 0xff9a2e, side: THREE.DoubleSide, transparent: true, opacity: 0.85, depthTest: false }));
    this.marker.rotation.x = -Math.PI / 2; this.marker.visible = false;
    this.arcLine = new THREE.Line(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0xffbb66, transparent: true, opacity: 0.8 }));
    this.arcLine.visible = false; scene.add(this.marker, this.arcLine);
    this.subSteps = 12;
  }
  onEquip() { attachModel(this.model, player.fp); }
  onUnequip() { this.model.removeFromParent(); this.marker.visible = this.arcLine.visible = false; }
  onPerspectiveChanged(fp) { if (this.model.parent) attachModel(this.model, fp); }
  launchParams() {
    const muzzle = muzzleWorld(this.model);
    const ar = aimRay(0);
    const h = castFirst(ar.origin, ar.dir, 200);
    const aimPt = h ? h.point : ar.origin.clone().addScaledVector(ar.dir, 200);
    // 1인칭은 카메라 방향 그대로, 3인칭은 총구에서 조준점 방향
    const dir = player.fp ? ar.dir : aimPt.sub(muzzle).normalize();
    return { pos: muzzle, vel: dir.multiplyScalar(this.d.speed) };
  }
  // 실제 투사체와 같은 적분식·같은 서브스텝을 써서 마커 오차를 없앤다
  stepSim(pos, vel, dt) {
    const prev = pos.clone();
    vel.y += P.gravity * this.d.gravityScale * dt;
    pos.addScaledVector(vel, dt);
    const seg = pos.clone().sub(prev), len = seg.length();
    if (len > 0) {
      const h = castFirst(prev, seg.normalize(), len);
      if (h) return h;
    }
    if (pos.y < 0) return { point: new V3(pos.x, 0, pos.z), object: ground };
    return null;
  }
  predict() {
    const { pos, vel } = this.launchParams();
    const pts = [pos.clone()];
    const dt = this.d.predictStepTime / this.subSteps;
    for (let s = 0; s < this.d.predictSteps; s++) {
      for (let k = 0; k < this.subSteps; k++) {
        const h = this.stepSim(pos, vel, dt);
        if (h) { pts.push(h.point.clone()); return { pts, hit: h.point }; }
      }
      pts.push(pos.clone());
    }
    return { pts, hit: null };
  }
  tick(inp, dt) {
    this.cd -= dt;
    if (inp.altH) {
      const p = this.predict();
      this.arcLine.geometry.setFromPoints(p.pts); this.arcLine.visible = true;
      this.marker.visible = !!p.hit;
      if (p.hit) this.marker.position.copy(p.hit).add(new V3(0, 0.03, 0));
    } else { this.marker.visible = this.arcLine.visible = false; }
    if (inp.altR || (inp.fireP && !inp.altH)) this.fire();
  }
  fire() {
    if (this.cd > 0 || this.ammo <= 0) { if (this.ammo <= 0) sfx('click'); return; }
    this.ammo--; this.cd = this.d.cooldown;
    const { pos, vel } = this.launchParams();
    const m = new THREE.Mesh(new THREE.SphereGeometry(0.12, 10, 8), new THREE.MeshBasicMaterial({ color: 0xffaa44 }));
    m.position.copy(pos); scene.add(m);
    this.projectiles.push({ m, pos: pos.clone(), vel: vel.clone(), life: 0, acc: 0 });
    sfx('launch');
  }
  updateProjectiles(dt) {
    const step = this.d.predictStepTime / this.subSteps;
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const pr = this.projectiles[i];
      pr.acc += dt; pr.life += dt;
      let hit = null;
      while (pr.acc >= step && !hit) { pr.acc -= step; hit = this.stepSim(pr.pos, pr.vel, step); }
      pr.m.position.copy(pr.pos);
      if (hit || pr.life > this.d.maxLife) {
        this.explode(hit ? hit.point : pr.pos);
        scene.remove(pr.m); this.projectiles.splice(i, 1);
      }
    }
  }
  explode(c) {
    const d = this.d;
    const center = c.clone().add(new V3(0, 0.1, 0));
    const falloff = (dist) => d.dmgCenter + (d.dmgEdge - d.dmgCenter) * clamp(dist / d.radius, 0, 1);
    for (const en of enemies) {
      if (en.dead) continue;
      const ch = en.center(), dist = ch.distanceTo(center);
      if (dist > d.radius + en.d.radius) continue;
      if (blocked(center, ch)) continue;
      const kdir = ch.clone().sub(center).setY(0.4).normalize();
      damageEnemy(en, falloff(Math.max(0, dist - en.d.radius)), ch, { knock: kdir.multiplyScalar(d.impulse * 10) });
    }
    const pc = chest(), pd = pc.distanceTo(center);
    if (pd <= d.radius && !blocked(center, pc)) {
      hurtPlayer(falloff(pd) * d.selfMult, center, { self: true });
      player.knock.add(pc.clone().sub(center).setY(0.3).normalize().multiplyScalar(P.selfKnockSpeed));
    }
    const s = new THREE.Mesh(new THREE.SphereGeometry(1, 24, 16), new THREE.MeshBasicMaterial({ color: 0xffa040, transparent: true, opacity: 0.6, blending: THREE.AdditiveBlending, depthWrite: false }));
    s.position.copy(center);
    addEffect(s, d.shockTime * 2, (e, k) => {
      const g = Math.min(1, k * 2); e.obj.scale.setScalar(Math.max(0.01, g * d.radius));
      e.obj.material.opacity = k < 0.5 ? 0.6 : 0.6 * (1 - (k - 0.5) * 2);
    });
    sparks(center, 0xff8822, 20);
    shakeT = C.shakeTime; sfx('boom');
  }
  hud() { return { text: `광역탄 ${this.ammo}`, bar: this.cd > 0 ? 1 - this.cd / this.d.cooldown : 1, color: '#ff9a2e' }; }
  get crossSpread() { return 0.6; }
}

class Sword {
  constructor() {
    this.d = W.sword; this.stamina = this.d.stamina; this.stage = -1; this.t = 0; this.hitSet = new Set();
    this.bufferT = -1; this.guarding = false; this.guardStart = -9; this.guardRelease = 9; this.parryBuff = false;
    this.drawT = 0;
    const g = new THREE.Group();
    const blade = new THREE.Mesh(new THREE.BoxGeometry(0.05, 0.012, 1), mat(0xd8dde6, { metalness: 0.9, roughness: 0.25 }));
    blade.position.z = -0.5;
    const hilt = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.03, 0.04), mat(0x8a6a2a));
    const grip = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, 0.2), mat(0x332211)); grip.rotation.x = Math.PI / 2; grip.position.z = 0.1;
    g.add(blade, hilt, grip); g.traverse((o) => { if (o.isMesh) o.castShadow = true; });
    this.model = g; this.hilt = new V3(); this.tip = new V3(); this.prevHilt = new V3(); this.prevTip = new V3();
    this.trail = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.35, side: THREE.DoubleSide, depthWrite: false }));
    this.trail.frustumCulled = false; this.trailPts = [];
  }
  onEquip() { scene.add(this.model, this.trail); this.drawT = this.d.draw; player.speedMult = this.d.moveMult; this.computeSockets(0); this.prevHilt.copy(this.hilt); this.prevTip.copy(this.tip); }
  onUnequip() { scene.remove(this.model, this.trail); this.stage = -1; this.guarding = false; player.speedMult = 1; this.trailPts = []; }
  onPerspectiveChanged() { }
  // 블레이드 소켓(자루 끝·검끝)을 공격 진행도에서 직접 계산 → 비주얼과 판정이 같은 소스
  computeSockets(dt) {
    const yaw = player.fp ? player.yaw : player.bodyYaw;
    const f = forwardOf(yaw), r = rightOf(yaw), c = chest();
    let hilt, tip;
    if (this.stage >= 0) {
      const a = this.d.combo[this.stage];
      const k = clamp((this.t - this.d.windup) / a.active, 0, 1);
      if (a.type === 'h') {
        const ang = (a.dir * (-a.arc / 2 + a.arc * k)) * DEG;
        const dir = f.clone().applyAxisAngle(UP, -ang);
        hilt = c.clone().addScaledVector(dir, this.d.hiltOffset);
        tip = c.clone().addScaledVector(dir, a.range);
      } else {
        const e = (a.arc * 0.6 - a.arc * k) * DEG;
        const dir = f.clone().multiplyScalar(Math.cos(e)).addScaledVector(UP, Math.sin(e));
        hilt = c.clone().addScaledVector(dir, this.d.hiltOffset);
        tip = c.clone().addScaledVector(dir, a.range);
      }
    } else if (this.guarding) {
      hilt = c.clone().addScaledVector(f, 0.5).addScaledVector(r, 0.4).add(new V3(0, 0.1, 0));
      tip = hilt.clone().addScaledVector(r, -1).add(new V3(0, 0.25, 0));
    } else {
      const drawK = clamp(1 - this.drawT / this.d.draw, 0, 1);
      hilt = c.clone().addScaledVector(f, 0.35).addScaledVector(r, 0.35).add(new V3(0, -0.15 - (1 - drawK) * 0.4, 0));
      tip = hilt.clone().add(f.clone().multiplyScalar(0.55).add(new V3(0, 0.8, 0)).normalize().multiplyScalar(1));
    }
    const s = this.stage >= 0 ? 1 : Math.min(1, dt * 18);
    this.hilt.lerp(hilt, s); this.tip.lerp(tip, s);
    if (this.stage < 0 && dt === 0) { this.hilt.copy(hilt); this.tip.copy(tip); }
    this.model.position.copy(this.hilt);
    this.model.lookAt(this.hilt.clone().multiplyScalar(2).sub(this.tip));
  }
  inActive() {
    if (this.stage < 0) return false;
    const a = this.d.combo[this.stage];
    return this.t >= this.d.windup && this.t <= this.d.windup + a.active;
  }
  tick(inp, dt) {
    const d = this.d, now = performance.now() / 1000;
    if (this.drawT > 0) this.drawT -= dt;
    if (inp.dodgeP && player.dodgeCd <= 0 && this.stamina >= d.dodgeCost && player.dodgeT <= 0) {
      this.stamina -= d.dodgeCost; startDodge(inp.mx, inp.my);
    }
    const wantGuard = inp.altH && this.stage < 0 && this.stamina > 0 && this.drawT <= 0;
    if (wantGuard && !this.guarding) { this.guarding = true; this.guardStart = now; }
    if (!wantGuard && this.guarding) { this.guarding = false; this.guardRelease = 0; }
    this.guardRelease += dt;
    if (this.guarding) this.stamina = Math.max(0, this.stamina - d.guardDrain * dt);
    else if (this.guardRelease >= d.regenDelay) this.stamina = Math.min(d.stamina, this.stamina + d.regen * dt);

    if (inp.fireP && this.drawT <= 0) this.bufferT = d.buffer;
    else this.bufferT -= dt;

    this.prevHilt.copy(this.hilt); this.prevTip.copy(this.tip);
    if (this.stage < 0) {
      if (this.bufferT > 0 && !this.guarding) this.begin(0);
    } else {
      const a = d.combo[this.stage];
      const total = d.windup + a.active + a.recovery;
      const wasActive = this.inActive();
      this.t += dt;
      if (this.bufferT > 0 && this.t >= total * (1 - d.cancelFrac) && this.stage < d.combo.length - 1) this.begin(this.stage + 1);
      else if (this.t >= total) { this.stage = -1; }
      if (wasActive || this.inActive()) this.sweep(dt);
    }
    this.computeSockets(dt);
    this.updateTrail();
  }
  begin(stage) {
    this.stage = stage; this.t = 0; this.hitSet.clear(); this.bufferT = -1; sfx('swing');
    this.computeSockets(0); this.prevHilt.copy(this.hilt); this.prevTip.copy(this.tip);
  }
  // 활성 프레임에서만, 이전→현재 소켓 사이를 보간해 칼날 캡슐 판정(빠른 휘두름 터널링 방지)
  sweep(dt) {
    if (!this.inActive()) return;
    this.computeSockets(dt);
    const a = this.d.combo[this.stage];
    const N = 5;
    for (const en of enemies) {
      if (en.dead || this.hitSet.has(en)) continue;
      const lo = en.pos.clone().add(new V3(0, en.d.radius, 0)), hi = en.pos.clone().add(new V3(0, en.d.height - en.d.radius, 0));
      for (let i = 0; i <= N; i++) {
        const k = i / N;
        const h = this.prevHilt.clone().lerp(this.hilt, k), t = this.prevTip.clone().lerp(this.tip, k);
        if (segSegDist(h, t, lo, hi) <= this.d.bladeRadius + en.d.radius) {
          this.hitSet.add(en);
          let dmg = a.damage;
          if (this.parryBuff) { dmg *= 1 + this.d.parryBonus; this.parryBuff = false; }
          const p = t.clone().lerp(h, 0.3);
          damageEnemy(en, dmg, p, { melee: true, breakTime: a.breakTime || 0 });
          break;
        }
      }
    }
  }
  updateTrail() {
    if (this.inActive()) {
      this.trailPts.push(this.hilt.clone(), this.tip.clone());
      if (this.trailPts.length > 24) this.trailPts.splice(0, 2);
    } else if (this.trailPts.length) this.trailPts.splice(0, 2);
    const pts = this.trailPts, idx = [];
    for (let i = 0; i + 3 < pts.length; i += 2) idx.push(i, i + 1, i + 2, i + 1, i + 3, i + 2);
    const g = this.trail.geometry;
    g.setFromPoints(pts.length ? pts : [new V3()]); g.setIndex(idx);
  }
  parrySuccess() {
    this.parryBuff = true; sfx('parry'); toast('패링!', 0.6);
    hitstop = Math.max(hitstop, COMMON.hitstopMelee * 2);
  }
  hud() { return { text: this.parryBuff ? '스태미나 · 강화!' : `스태미나 ${Math.ceil(this.stamina)}`, bar: this.stamina / this.d.stamina, color: '#ec4' }; }
  arcInfo() { const i = this.stage >= 0 ? Math.min(this.stage + (this.bufferT > 0 ? 1 : 0), 2) : 0; return this.d.combo[i]; }
}

const rifle = new Rifle(), laser = new Laser(), blast = new Blast(), sword = new Sword();
const combat = {
  weapons: [rifle, laser, blast, sword], idx: 0, switchT: 0, fireBufT: 0,
  get current() { return this.weapons[this.idx]; },
  equip(i) {
    if (i === this.idx) return;
    this.current.onUnequip(); this.idx = i; this.current.onEquip();
    this.switchT = COMMON.switchTime; this.fireBufT = 0;
  },
  perspectiveChanged(fp) { this.weapons.forEach((w) => w.onPerspectiveChanged(fp)); },
  tick(inp, dt) {
    if (player.dead) return;
    if (inp.slot >= 0) this.equip(inp.slot);
    if (inp.cycle) this.equip((this.idx + (inp.cycle > 0 ? 1 : 3)) % 4);
    if (inp.dodgeP && this.current !== sword && player.dodgeCd <= 0 && player.dodgeT <= 0 && sword.stamina >= W.sword.dodgeCost) {
      sword.stamina -= W.sword.dodgeCost; startDodge(inp.mx, inp.my);
    }
    if (this.current !== sword && sword.guardRelease >= W.sword.regenDelay) sword.stamina = Math.min(W.sword.stamina, sword.stamina + W.sword.regen * dt);
    player.aiming = inp.altH;
    const wi = { ...inp };
    if (this.switchT > 0) {
      this.switchT -= dt;
      if (inp.fireP) this.fireBufT = COMMON.fireBuffer;
      wi.fireP = wi.fireH = wi.fireR = false;
      if (this.switchT > 0) { this.fireBufT -= dt; this.current.tick(wi, dt); return; }
      if (this.fireBufT > 0) { wi.fireP = true; wi.fireH = inp.fireH || true; }
    }
    this.current.tick(wi, dt);
  },
};
combat.current.onEquip();

// ---------------- enemies ----------------
const spawnPoints = {
  grunt: [[-17, -12], [17, -12], [-17, 0], [17, -2], [0, -13], [-9, -13]].map(([x, z]) => new V3(x, 0, z)),
  bruiser: [[-17, 12], [17, 13], [-17, -6], [17, 4], [12, -13], [-3, -13]].map(([x, z]) => new V3(x, 0, z)),
};
class Enemy {
  constructor(type, pos) {
    this.type = type; this.d = E[type]; this.hp = this.d.hp; this.pos = pos.clone();
    this.vel = new V3(); this.knock = new V3(); this.flinch = 0; this.stun = 0; this.flash = 0;
    this.dead = false; this.deadT = 0; this.state = 'move'; this.t = 0; this.cd = 1;
    this.aimT = Math.random() * this.d.reaim || 0; this.shotT = 0; this.burstLeft = 0; this.dest = null;
    const d = this.d, color = type === 'grunt' ? 0x5c8a3a : 0x9a3a2a;
    this.mat = mat(color); this.headMat = mat(type === 'grunt' ? 0x8bbf5a : 0xd06040);
    this.group = new THREE.Group();
    const headR = type === 'grunt' ? 0.2 : 0.3;
    const bodyH = d.height - headR * 2;
    const bodyM = new THREE.Mesh(new THREE.CapsuleGeometry(d.radius, Math.max(0.1, bodyH - d.radius * 2), 4, 12), this.mat);
    bodyM.position.y = bodyH / 2;
    const headMesh = new THREE.Mesh(new THREE.SphereGeometry(headR, 14, 10), this.headMat);
    headMesh.position.y = d.height - headR;
    bodyM.userData = { enemy: this, part: 'body' }; headMesh.userData = { enemy: this, part: 'head' };
    bodyM.castShadow = headMesh.castShadow = true;
    this.group.add(bodyM, headMesh);
    if (type === 'grunt') {
      const gun = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.1, 0.4), mat(0x222222));
      gun.position.set(0.3, 1.25, -0.3); this.group.add(gun); this.gunTip = new V3(0.3, 1.25, -0.52);
    } else {
      const fist = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.4, 0.4), mat(0x553322));
      fist.position.set(0, 1.2, -0.7); this.group.add(fist);
    }
    this.parts = [bodyM, headMesh];
    this.group.position.copy(this.pos);
    scene.add(this.group);
  }
  center() { return this.pos.clone().add(new V3(0, this.d.height * 0.55, 0)); }
  facePlayer(dt, rate = 8) {
    const to = player.pos.clone().sub(this.pos);
    const want = Math.atan2(-to.x, -to.z);
    const cur = this.group.rotation.y;
    const d = Math.atan2(Math.sin(want - cur), Math.cos(want - cur));
    this.group.rotation.y = cur + clamp(d, -rate * dt, rate * dt);
  }
  die() {
    this.dead = true; this.deadT = 0; this.hp = 0;
    this.mat.transparent = this.headMat.transparent = true;
    this.fallAxis = new V3(Math.random() - 0.5, 0, Math.random() - 0.5).normalize();
  }
  move(dir, speed, dt) {
    this.pos.addScaledVector(dir, speed * dt);
  }
  update(dt) {
    if (this.dead) {
      this.deadT += dt;
      const k = Math.min(1, this.deadT / 0.4);
      this.group.quaternion.setFromAxisAngle(this.fallAxis, k * Math.PI / 2);
      if (this.deadT > E.ragdollFade) {
        const o = Math.max(0, 1 - (this.deadT - E.ragdollFade) / 0.5);
        this.mat.opacity = this.headMat.opacity = o;
        if (o <= 0) { scene.remove(this.group); this.removed = true; }
      }
      return;
    }
    this.flash -= dt; this.flinch -= dt; this.stun -= dt;
    const em = this.flash > 0 ? 0xff3333 : this.state === 'windup' ? 0xffffff : 0x000000;
    this.mat.emissive.setHex(em); this.headMat.emissive.setHex(em);
    if (this.knock.lengthSq() > 0.001) { this.pos.addScaledVector(this.knock, dt); this.knock.multiplyScalar(Math.max(0, 1 - 6 * dt)); }
    if (this.type === 'grunt') this.updateGrunt(dt); else this.updateBruiser(dt);
    pushOut(this.pos, this.d.radius, this.d.height);
    for (const o of enemies) {
      if (o === this || o.dead) continue;
      const dd = this.pos.clone().sub(o.pos).setY(0), l = dd.length(), min = this.d.radius + o.d.radius;
      if (l < min && l > 1e-4) this.pos.addScaledVector(dd.normalize(), (min - l) * 0.5);
    }
    this.pos.x = clamp(this.pos.x, -YW / 2 + 0.6, YW / 2 - 0.6);
    this.pos.z = clamp(this.pos.z, -YD / 2 + 0.6, YD / 2 - 0.6);
    this.pos.y = 0;
    this.group.position.copy(this.pos);
  }
  pickCover() {
    const pp = player.pos;
    let best = null, bestScore = Infinity;
    for (const s of solids.slice(4)) {
      const c = s.box.getCenter(new V3()).setY(0);
      const away = c.clone().sub(pp).setY(0).normalize();
      const p = c.clone().addScaledVector(away, Math.max(s.box.max.x - s.box.min.x, s.box.max.z - s.box.min.z) / 2 + 0.9);
      const dist = p.distanceTo(pp);
      if (dist < 6 || dist > this.d.range - 2) continue;
      const score = p.distanceTo(this.pos) + Math.random() * 6;
      if (score < bestScore) { bestScore = score; best = p; }
    }
    // 엄폐 뒤에 너무 오래 숨지 않게 가끔 측면으로 노출되는 지점을 고른다
    if (best && Math.random() < 0.5) best.add(new V3(Math.random() - 0.5, 0, Math.random() - 0.5).multiplyScalar(4));
    this.dest = best;
  }
  updateGrunt(dt) {
    const d = this.d;
    this.aimT -= dt;
    if (this.aimT <= 0) { this.aimT = d.reaim; this.pickCover(); this.aimError = (Math.random() - 0.5) * 0.4; }
    if (this.flinch <= 0 && this.stun <= 0 && this.dest) {
      const to = this.dest.clone().sub(this.pos).setY(0);
      if (to.length() > 0.3) this.move(to.normalize(), d.speed, dt);
    }
    this.facePlayer(dt);
    if (player.dead) return;
    const eye = this.pos.clone().add(new V3(0, 1.4, 0));
    const pc = chest();
    const dist = eye.distanceTo(pc);
    const los = dist <= d.range && !blocked(eye, pc);
    this.shotT -= dt;
    if (los && this.flinch <= 0 && this.stun <= 0 && this.shotT <= 0) {
      if (this.burstLeft <= 0) this.burstLeft = d.burst;
      this.burstLeft--;
      this.shotT = this.burstLeft > 0 ? 60 / d.rpm : d.burstPause;
      const moving = Math.hypot(player.vel.x, player.vel.z) > 1;
      const chance = d.baseHit - dist * 0.015 - (moving ? 0.15 : 0) - (player.height < P.height - 0.1 ? 0.1 : 0);
      const tip = this.gunTip.clone().applyMatrix4(this.group.matrixWorld);
      let end = pc.clone();
      if (Math.random() < chance) hurtPlayer(d.damage, this.pos);
      else end.add(new V3(Math.random() - 0.5, Math.random() - 0.3, Math.random() - 0.5).multiplyScalar(1.5));
      tracer(tip, end, 0xff8855, 0.06, 0.012);
      sfx('enemyShot');
    }
  }
  updateBruiser(dt) {
    const d = this.d;
    this.cd -= dt;
    if (this.stun > 0) { this.state = 'stun'; return; }
    if (this.state === 'stun') this.state = 'move';
    const to = player.pos.clone().sub(this.pos).setY(0), dist = to.length();
    switch (this.state) {
      case 'move':
        this.facePlayer(dt, 5);
        if (this.flinch <= 0 && dist > 1.5) this.move(to.clone().normalize(), d.speed, dt);
        if (!player.dead && dist < d.triggerRange && this.cd <= 0 && !blocked(this.center(), chest())) {
          this.state = 'windup'; this.t = d.windup;
        }
        break;
      case 'windup':
        this.facePlayer(dt, 10); this.t -= dt;
        if (this.t <= 0) {
          this.state = 'charge'; this.t = d.chargeTime; this.chargeHit = false;
          this.chargeDir = to.clone().normalize();
        }
        break;
      case 'charge': {
        this.t -= dt;
        const before = this.pos.clone();
        this.move(this.chargeDir, d.chargeDist / d.chargeTime, dt);
        pushOut(this.pos, d.radius, d.height);
        if (this.pos.distanceTo(before) < d.chargeDist / d.chargeTime * dt * 0.3) this.t = 0;
        if (!this.chargeHit && this.pos.clone().setY(0).distanceTo(player.pos.clone().setY(0)) < d.radius + P.radius + 0.35) {
          this.chargeHit = true;
          const res = hurtPlayer(d.damage, this.pos, { melee: true });
          if (res === 'parried') { this.stun = W.sword.parryStagger; this.state = 'stun'; this.cd = d.cooldown; sparks(this.center(), 0xffffff, 16); return; }
          if (res === 'hit') player.knock.add(this.chargeDir.clone().multiplyScalar(6).setY(2));
        }
        if (this.t <= 0) { this.state = 'recover'; this.t = d.recover; this.cd = d.cooldown; }
        break;
      }
      case 'recover':
        this.t -= dt; if (this.t <= 0) this.state = 'move';
        break;
    }
  }
}

let autoRespawn = true, wipeT = -1;
function visibleFromPlayer(p) {
  const c = camera.position, to = p.clone().add(new V3(0, 1.2, 0)).sub(c);
  const ang = to.clone().normalize().dot(lookDir());
  if (ang < Math.cos(C.fov * 0.75 * DEG)) return false;
  return !blocked(c, p.clone().add(new V3(0, 1.2, 0)));
}
function spawnWave() {
  for (const type of ['grunt', 'bruiser']) {
    const pts = spawnPoints[type].slice().sort(() => Math.random() - 0.5);
    const hidden = pts.filter((p) => !visibleFromPlayer(p));
    const use = hidden.concat(pts.filter((p) => !hidden.includes(p)));
    for (let i = 0; i < E[type].count; i++) enemies.push(new Enemy(type, use[i % use.length]));
  }
}

// ---------------- HUD ----------------
const slotNames = ['총', '레이저', '광역', '장검'];
$('slots').innerHTML = slotNames.map((n, i) => `<div class="slot" id="slot${i}"><b>${i + 1}</b>${n}</div>`).join('');
const crossEls = [...document.querySelectorAll('#cross i')];
function updateHud() {
  for (let i = 0; i < 4; i++) $('slot' + i).classList.toggle('on', i === combat.idx);
  const cur = combat.current, h = cur.hud();
  $('widget').textContent = h.text; $('widget').style.color = h.color;
  const wb = $('wbar').firstElementChild; wb.style.width = clamp(h.bar, 0, 1) * 100 + '%'; wb.style.background = h.color;
  $('hp').style.width = (player.hp / P.maxHealth) * 100 + '%';
  $('st').style.width = (sword.stamina / W.sword.stamina) * 100 + '%';
  $('view').textContent = player.fp ? '1P' : '3P';
  const alive = enemies.filter((e) => !e.dead).length;
  $('info').textContent = `적 ${alive}` + (wipeT > 0 ? ` · 리스폰 ${wipeT.toFixed(1)}s` : '') + (autoRespawn ? '' : ' · 자동 리스폰 OFF (P)');

  const isSword = cur === sword;
  $('cross').style.display = isSword ? 'none' : 'block';
  $('cross').style.top = (50 - aimNdcY() * 50) + '%';
  const color = W[['rifle', 'laser', 'blast', 'sword'][combat.idx]].color;
  const gap = 4 + Math.tan(cur.crossSpread * DEG) / Math.tan(C.fov / 2 * DEG) * innerHeight / 2;
  const L = 9, Tk = 2;
  const sets = [[-Tk / 2, -gap - L, Tk, L], [-Tk / 2, gap, Tk, L], [-gap - L, -Tk / 2, L, Tk], [gap, -Tk / 2, L, Tk]];
  crossEls.forEach((el, i) => { const [x, y, w, hh] = sets[i]; Object.assign(el.style, { left: x + 'px', top: y + 'px', width: w + 'px', height: hh + 'px', background: color }); });

  const arc = $('arc');
  arc.style.display = isSword ? 'block' : 'none';
  if (isSword) {
    const a = sword.arcInfo(), R = 40 + a.range * 30, half = a.arc / 2 * DEG;
    const x1 = Math.sin(-half) * R, y1 = -Math.cos(half) * R, x2 = Math.sin(half) * R;
    arc.querySelector('path').setAttribute('d', `M0 0 L${x1} ${y1} A${R} ${R} 0 0 1 ${x2} ${y1} Z`);
    arc.querySelector('path').setAttribute('stroke', sword.guarding ? '#6cf' : sword.parryBuff ? '#fd3' : '#fff');
    arc.querySelector('text').textContent = sword.guarding ? '가드' : `${sword.stage >= 0 ? sword.stage + 1 : 1}단 · ${a.range}m`;
  }
}

// ---------------- loop ----------------
let last = performance.now(), dtReal = 0;
spawnWave();

function frame(now) {
  requestAnimationFrame(frame);
  dtReal = Math.min(0.05, (now - last) / 1000); last = now;
  const inp = readInput();
  if (!started) { updateCamera(inp, dtReal); renderer.render(scene, camera); return; }
  let dt = dtReal;
  if (hitstop > 0) { hitstop -= dtReal; dt = 0; }

  if (inp.debugP) { autoRespawn = !autoRespawn; toast(autoRespawn ? '자동 리스폰 ON' : '자동 리스폰 OFF'); }
  updatePlayer(inp, dt);
  updateCamera(inp, dtReal);
  combat.tick(inp, dt);
  blast.updateProjectiles(dt);
  enemies.forEach((e) => e.update(dt));
  for (let i = enemies.length - 1; i >= 0; i--) if (enemies[i].removed) enemies.splice(i, 1);
  if (enemies.every((e) => e.dead)) {
    if (wipeT < 0 && autoRespawn) { wipeT = E.respawnDelay; toast('전멸! 8초 후 리스폰'); }
    if (wipeT > 0) { wipeT -= dtReal; if (wipeT <= 0) { wipeT = -1; spawnWave(); } }
  }

  for (let i = effects.length - 1; i >= 0; i--) {
    const e = effects[i]; e.t += dtReal; const k = e.t / e.life;
    if (k >= 1) { scene.remove(e.obj); e.obj.geometry !== sparkGeo && e.obj.geometry.dispose(); effects.splice(i, 1); continue; }
    e.update(e, k, dtReal);
  }
  camera.updateMatrixWorld();
  for (let i = floaters.length - 1; i >= 0; i--) {
    const f = floaters[i]; f.t += dtReal;
    if (f.t >= COMMON.floatTime) { f.el.remove(); floaters.splice(i, 1); continue; }
    const p = f.p.clone().add(new V3(0, f.t * 1.2, 0)).project(camera);
    f.el.style.display = p.z > 1 ? 'none' : 'block';
    f.el.style.left = (p.x * 0.5 + 0.5) * innerWidth + 'px';
    f.el.style.top = (-p.y * 0.5 + 0.5) * innerHeight + 'px';
    f.el.style.opacity = 1 - f.t / COMMON.floatTime;
  }
  if (vigT > 0) { vigT -= dtReal; if (vigT <= 0) $('vig').style.opacity = 0; }
  if (toastT > 0) { toastT -= dtReal; if (toastT <= 0) $('toast').textContent = ''; }
  updateHud();
  renderer.render(scene, camera);
}
requestAnimationFrame(frame);
if (new URLSearchParams(location.search).has('debug')) window.__game = { player, enemies, combat, rifle, laser, blast, sword, keys, mouse, start: () => { started = true; $('start').style.display = 'none'; } };
