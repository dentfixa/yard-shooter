// 플레이 방법: Grunt(초록)는 엄폐 근처로 2초마다 재배치하며 18m 안에서 권총 3점사.
// Bruiser(빨강)는 접근 후 0.4초 흰색 발광 예고 → 6m 돌진. 장검 가드를 돌진 직전에 올리면 패링.
// 패링 시 돌진이 즉시 취소되고 0.45초 경직된다.
// 사망 시 쓰러진 뒤 2초 후 페이드, 전멸하면 8초 후 리스폰(P로 토글).
// 이동은 NavMeshAgent(런타임에 YardBootstrap이 NavMeshSurface를 굽는다).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace YardShooter
{
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new();
        public EnemyData data;
        public float Hp;
        public bool Dead;
        public Collider BodyCollider;
        public Renderer[] renderers;
        public Transform gunTip;
        public PlayerMotor player;
        public PlayerHealth playerHealth;
        public SwordWeapon playerSword;

        NavMeshAgent agent;
        float flinch, stun, flash, deadTimer, aimTimer, shotTimer, stateTimer, cooldown;
        int burstLeft;
        Vector3 knock, chargeDir, fallAxis;
        bool chargeHit;
        enum State { Move, Windup, Charge, Recover, Stun }
        State state;
        Material[] mats;

        public Vector3 Chest => transform.position + Vector3.up * data.height * 0.55f;
        public void Segment(out Vector3 lo, out Vector3 hi)
        {
            lo = transform.position + Vector3.up * data.radius;
            hi = transform.position + Vector3.up * (data.height - data.radius);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            Hp = data.maxHealth;
            agent = GetComponent<NavMeshAgent>();
            agent.speed = data.moveSpeed; agent.radius = data.radius; agent.height = data.height;
            agent.angularSpeed = 540; agent.acceleration = 20;
            mats = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) mats[i] = renderers[i].material;
            aimTimer = Random.value * data.reaimInterval;
            cooldown = 1f;
        }

        public void TakeDamage(float dmg, Vector3 point, Vector3 knockImpulse, float postureBreak)
        {
            if (Dead) return;
            Hp -= dmg; flash = 0.1f;
            flinch = Mathf.Max(flinch, data.flinchTime);
            if (postureBreak > 0) stun = Mathf.Max(stun, postureBreak);
            knock += knockImpulse;
            if (Hp <= 0) Die();
        }

        void Die()
        {
            Dead = true; Hp = 0; deadTimer = 0;
            if (agent) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            fallAxis = Vector3.Cross(Vector3.up, Random.onUnitSphere).normalized;
            foreach (var m in mats)
            {
                m.SetFloat("_Surface", 1);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Dead)
            {
                deadTimer += dt;
                transform.rotation = Quaternion.AngleAxis(Mathf.Min(1, deadTimer / 0.4f) * 90f, fallAxis);
                if (deadTimer > data.ragdollFadeDelay)
                {
                    float a = 1 - (deadTimer - data.ragdollFadeDelay) / 0.5f;
                    foreach (var m in mats) { var c = m.color; c.a = Mathf.Max(0, a); m.color = c; }
                    if (a <= 0) Destroy(gameObject);
                }
                return;
            }
            flash -= dt; flinch -= dt; stun -= dt;
            var em = flash > 0 ? Color.red : state == State.Windup ? Color.white : Color.black;
            foreach (var m in mats) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", em); }
            if (!agent.isOnNavMesh) return;
            if (knock.sqrMagnitude > 0.001f)
            {
                agent.Move(knock * dt);
                knock *= Mathf.Max(0, 1 - 6 * dt);
            }
            if (data.kind == EnemyKind.Grunt) Grunt(dt); else Bruiser(dt);
        }

        void Face(Vector3 target, float degPerSec)
        {
            var to = target - transform.position; to.y = 0;
            if (to.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), degPerSec * Time.deltaTime);
        }

        static bool Blocked(Vector3 a, Vector3 b)
        {
            var d = b - a;
            return Physics.Raycast(a, d.normalized, out var h, d.magnitude - 0.1f, Hits.Mask, QueryTriggerInteraction.Ignore)
                   && !h.collider.GetComponent<Hitbox>();
        }

        void PickCover()
        {
            Vector3 best = transform.position; float bestScore = float.MaxValue;
            foreach (var cv in FindObjectsByType<Cover>(FindObjectsSortMode.None))
            {
                var c = cv.transform.position; c.y = 0;
                var away = c - player.transform.position; away.y = 0; away.Normalize();
                var p = c + away * (cv.transform.localScale.magnitude * 0.4f + 0.9f);
                float d = Vector3.Distance(p, player.transform.position);
                if (d < 6 || d > data.range - 2) continue;
                float score = Vector3.Distance(p, transform.position) + Random.value * 6;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            // 엄폐 뒤에만 박혀 있지 않게 절반은 측면 노출 지점을 고른다
            if (Random.value < 0.5f) best += new Vector3(Random.value - 0.5f, 0, Random.value - 0.5f) * 4;
            if (NavMesh.SamplePosition(best, out var hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        void Grunt(float dt)
        {
            aimTimer -= dt;
            if (aimTimer <= 0) { aimTimer = data.reaimInterval; PickCover(); }
            agent.isStopped = flinch > 0 || stun > 0;
            agent.updateRotation = false;
            Face(player.transform.position, 480);
            if (playerHealth.Dead) return;
            var eye = transform.position + Vector3.up * 1.4f;
            var pc = player.Chest;
            float dist = Vector3.Distance(eye, pc);
            shotTimer -= dt;
            if (dist > data.range || flinch > 0 || stun > 0 || shotTimer > 0 || Blocked(eye, pc)) return;
            if (burstLeft <= 0) burstLeft = data.burstCount;
            burstLeft--;
            shotTimer = burstLeft > 0 ? 60f / data.rpm : data.burstPauseTime;
            bool moving = new Vector2(player.Velocity.x, player.Velocity.z).magnitude > 1;
            float chance = data.baseHitChance - dist * 0.015f - (moving ? 0.15f : 0) - (player.Crouching ? 0.1f : 0);
            var end = pc;
            if (Random.value < chance) playerHealth.TakeDamage(data.damage, transform.position, false);
            else end += Random.insideUnitSphere * 1.5f;
            CombatFx.I.Tracer(gunTip.position, end, new Color(1, 0.5f, 0.3f), 0.06f, 0.02f);
        }

        void Bruiser(float dt)
        {
            cooldown -= dt;
            if (stun > 0) { state = State.Stun; agent.isStopped = true; return; }
            if (state == State.Stun) state = State.Move;
            var to = player.transform.position - transform.position; to.y = 0;
            float dist = to.magnitude;
            switch (state)
            {
                case State.Move:
                    agent.isStopped = flinch > 0;
                    agent.updateRotation = true;
                    if (agent.isOnNavMesh) agent.SetDestination(player.transform.position);
                    if (!playerHealth.Dead && dist < data.triggerRange && cooldown <= 0 && !Blocked(Chest, player.Chest))
                    { state = State.Windup; stateTimer = data.windupTime; agent.isStopped = true; agent.updateRotation = false; }
                    break;
                case State.Windup:
                    Face(player.transform.position, 720);
                    stateTimer -= dt;
                    if (stateTimer <= 0) { state = State.Charge; stateTimer = data.chargeTime; chargeHit = false; chargeDir = to.normalized; }
                    break;
                case State.Charge:
                {
                    stateTimer -= dt;
                    var before = transform.position;
                    agent.Move(chargeDir * data.chargeDistance / data.chargeTime * dt);
                    if ((transform.position - before).magnitude < data.chargeDistance / data.chargeTime * dt * 0.3f) stateTimer = 0;
                    var flat = transform.position - player.transform.position; flat.y = 0;
                    if (!chargeHit && flat.magnitude < data.radius + 0.35f + 0.35f)
                    {
                        chargeHit = true;
                        var r = playerHealth.TakeDamage(data.damage, transform.position, true);
                        if (r == PlayerHealth.Result.Parried)
                        {
                            stun = playerSword.ParryStagger; state = State.Stun; cooldown = data.cooldown;
                            return;
                        }
                        if (r == PlayerHealth.Result.Hit) player.PendingKnock += chargeDir * 6 + Vector3.up * 2;
                    }
                    if (stateTimer <= 0) { state = State.Recover; stateTimer = data.recoverTime; cooldown = data.cooldown; }
                    break;
                }
                case State.Recover:
                    stateTimer -= dt;
                    if (stateTimer <= 0) state = State.Move;
                    break;
            }
        }
    }
}
