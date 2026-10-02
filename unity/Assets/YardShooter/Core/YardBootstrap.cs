// 플레이 방법: 아무 씬(새 URP 프로젝트의 SampleScene 포함)에서 Play만 누르면 이 스크립트가 40x30m 야드, 플레이어,
// 무기 4종, 적, HUD, NavMesh를 전부 생성한다. 시작은 1인칭 + 소총, 적 스폰 완료 상태.
// P: 전멸 시 자동 리스폰(8초) 토글. ESC: 마우스 커서 해제, 클릭: 다시 잠금.
// 모든 수치는 Resources/YardShooter/*.asset(에디터가 자동 생성)에서 읽는다.
// 이 파일의 좌표·크기는 레벨 레이아웃이며 게임플레이 튜닝 수치가 아니다.
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace YardShooter
{
    public class YardBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindAnyObjectByType<YardBootstrap>() == null) new GameObject("YardShooter").AddComponent<YardBootstrap>();
        }

        PlayerTuning tuning;
        WeaponData rifleD, laserD, blastD, swordD;
        EnemyData gruntD, bruiserD;
        PlayerMotor motor; PlayerHealth health; PlayerCombat combat;
        GameInput input; Hud hud;
        readonly List<Vector3> gruntSpawns = new(), bruiserSpawns = new();
        float wipeTimer = -1; bool autoRespawn = true;
        Shader lit;

        public string InfoLine
        {
            get
            {
                int alive = 0; foreach (var e in Enemy.All) if (!e.Dead) alive++;
                return $"적 {alive}" + (wipeTimer > 0 ? $" · 리스폰 {wipeTimer:0.0}s" : "") + (autoRespawn ? "" : " · 자동 리스폰 OFF (P)");
            }
        }

        T Load<T>(string n) where T : Object
        {
            var a = Resources.Load<T>("YardShooter/" + n);
            if (!a) Debug.LogError($"YardShooter: Resources/YardShooter/{n}.asset 이 없습니다. 메뉴 YardShooter > Create Default Data 를 실행하세요.");
            return a;
        }

        void Awake()
        {
            tuning = Load<PlayerTuning>("PlayerTuning");
            rifleD = Load<WeaponData>("Rifle"); laserD = Load<WeaponData>("Laser"); blastD = Load<WeaponData>("Blast"); swordD = Load<WeaponData>("Sword");
            gruntD = Load<EnemyData>("Grunt"); bruiserD = Load<EnemyData>("Bruiser");
            if (!tuning || !rifleD || !laserD || !blastD || !swordD || !gruntD || !bruiserD) { enabled = false; return; }
            lit = Shader.Find("Universal Render Pipeline/Lit");
            Application.targetFrameRate = 60;

            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);

            BuildWorld();
            var fx = new GameObject("CombatFx").AddComponent<CombatFx>(); fx.tuning = tuning;
            BuildPlayer();
            SpawnWave();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        Material Mat(Color c) { var m = new Material(lit); m.color = c; return m; }

        GameObject Box(string name, Vector3 center, Vector3 size, Color c, CoverType cover = CoverType.Normal, bool addCover = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.position = center; go.transform.localScale = size;
            go.GetComponent<Renderer>().material = Mat(c);
            go.isStatic = true;
            if (addCover) go.AddComponent<Cover>().type = cover;
            return go;
        }

        void BuildWorld()
        {
            var world = new GameObject("Yard");
            const float W = 40, D = 30;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(W / 10, 1, D / 10);
            ground.GetComponent<Renderer>().material = Mat(new Color(.43f, .46f, .38f));
            ground.transform.SetParent(world.transform);
            Box("WallN", new Vector3(0, 1.5f, D / 2 + .25f), new Vector3(W + 1, 3, .5f), Color.gray).transform.SetParent(world.transform);
            Box("WallS", new Vector3(0, 1.5f, -D / 2 - .25f), new Vector3(W + 1, 3, .5f), Color.gray).transform.SetParent(world.transform);
            Box("WallE", new Vector3(W / 2 + .25f, 1.5f, 0), new Vector3(.5f, 3, D), Color.gray).transform.SetParent(world.transform);
            Box("WallW", new Vector3(-W / 2 - .25f, 1.5f, 0), new Vector3(.5f, 3, D), Color.gray).transform.SetParent(world.transform);

            Vector2[] boxes = { new(-8, -4), new(8, -4), new(-4, 3), new(4, 3), new(-14, 6), new(14, 6), new(-2, 10), new(10, 11) };
            for (int i = 0; i < boxes.Length; i++)
            {
                float h = i % 2 == 0 ? 1.6f : 1.2f;
                Box("Cover" + i, new Vector3(boxes[i].x, h / 2, boxes[i].y), new Vector3(1.6f, h, 1.6f), new Color(.63f, .48f, .29f), CoverType.Normal, true).transform.SetParent(world.transform);
            }
            Box("PlatformLow", new Vector3(-13, .4f, -8), new Vector3(5, .8f, 4), new Color(.35f, .42f, .5f)).transform.SetParent(world.transform);
            Box("PlatformHigh", new Vector3(13, .65f, -9), new Vector3(5, 1.3f, 4), new Color(.35f, .42f, .5f)).transform.SetParent(world.transform);
            Box("ThickCoverA", new Vector3(-6, 1.5f, 7), new Vector3(6, 3, .6f), new Color(.27f, .29f, .32f), CoverType.Thick, true).transform.SetParent(world.transform);
            Box("ThickCoverB", new Vector3(7, 1.5f, 0), new Vector3(.6f, 3, 6), new Color(.27f, .29f, .32f), CoverType.Thick, true).transform.SetParent(world.transform);
            var glassMat = Mat(new Color(.6f, .85f, 1f, .3f));
            glassMat.SetFloat("_Surface", 1); glassMat.SetOverrideTag("RenderType", "Transparent");
            glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glassMat.SetInt("_ZWrite", 0); glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glassMat.renderQueue = 3000;
            foreach (var (p, s) in new[] { (new Vector3(0, 1.1f, -2), new Vector3(3, 2.2f, .08f)), (new Vector3(-10, 1.1f, 1), new Vector3(.08f, 2.2f, 3)) })
            {
                var g = Box("Glass", p, s, Color.white, CoverType.Thin, true);
                g.GetComponent<Renderer>().material = glassMat; g.transform.SetParent(world.transform);
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft; sun.color = new Color(1, .95f, .85f);
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 18; lamp.intensity = 8; lamp.color = new Color(1, .7f, .4f);
            lamp.transform.position = new Vector3(0, 4, 0);

            var surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            foreach (var p in new[] { new Vector2(-17, 12), new(17, 12), new(-17, 0), new(17, 2), new(0, 13), new(-9, 13) }) gruntSpawns.Add(new Vector3(p.x, 0, p.y));
            foreach (var p in new[] { new Vector2(-17, -12), new(17, -13), new(-17, 6), new(17, -4), new(12, 13), new(-3, 13) }) bruiserSpawns.Add(new Vector3(p.x, 0, p.y));
        }

        void BuildPlayer()
        {
            var p = new GameObject("Player") { layer = Hits.PlayerLayer };
            // 컴포넌트 Awake가 참조를 받기 전에 실행되지 않도록 비활성 상태에서 구성한다
            p.SetActive(false);
            p.transform.position = new Vector3(0, 0.1f, -11);
            p.AddComponent<CharacterController>();
            input = p.AddComponent<GameInput>();
            motor = p.AddComponent<PlayerMotor>(); motor.t = tuning; motor.input = input;

            var visual = new GameObject("Body").transform; visual.SetParent(p.transform, false);
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(torso.GetComponent<Collider>());
            torso.transform.SetParent(visual, false);
            torso.transform.localScale = new Vector3(tuning.radius * 2, tuning.height / 2, tuning.radius * 2);
            torso.transform.localPosition = Vector3.up * tuning.height / 2;
            torso.GetComponent<Renderer>().material = Mat(new Color(.23f, .44f, .69f));
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visor.GetComponent<Collider>());
            visor.transform.SetParent(visual, false);
            visor.transform.localScale = new Vector3(.3f, .12f, .1f); visor.transform.localPosition = new Vector3(0, tuning.eyeStand, .3f);
            visor.GetComponent<Renderer>().material = Mat(Color.black);
            motor.bodyVisual = visual;
            motor.bodyRenderers = new[] { torso.GetComponent<Renderer>(), visor.GetComponent<Renderer>() };

            var camGo = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var cam = camGo.GetComponent<Camera>(); cam.nearClipPlane = 0.03f;
            var fpHand = new GameObject("FpHand").transform; fpHand.SetParent(camGo.transform, false); fpHand.localPosition = new Vector3(.22f, -.2f, .42f);
            var tpHand = new GameObject("TpHand").transform; tpHand.SetParent(p.transform, false); tpHand.localPosition = new Vector3(.28f, 1.3f, .25f);

            health = p.AddComponent<PlayerHealth>(); health.t = tuning; health.motor = motor;
            combat = p.AddComponent<PlayerCombat>(); combat.t = tuning; combat.input = input; combat.motor = motor; combat.health = health;
            health.combat = combat;
            var rig = camGo.AddComponent<PlayerCameraRig>();
            rig.t = tuning; rig.motor = motor; rig.combat = combat; rig.cam = cam; rig.fpHand = fpHand; rig.tpHand = tpHand;
            combat.rig = rig;
            p.SetActive(true);

            var holder = new GameObject("Weapons").transform; holder.SetParent(p.transform, false);
            var rifle = MakeGun<RifleWeapon>(holder, rifleD, rig, new Color(.17f, .17f, .17f), .7f, .07f);
            var laser = MakeGun<LaserWeapon>(holder, laserD, rig, new Color(.1f, .33f, .38f), .55f, .08f);
            laser.beam = NewLine("Beam", Color.cyan, .06f); laser.beam.enabled = false;
            laser.beamEnd = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(laser.beamEnd.GetComponent<Collider>()); laser.beamEnd.localScale = Vector3.one * .25f;
            laser.beamEnd.GetComponent<Renderer>().material = CombatFx.I.Additive(Color.cyan); laser.beamEnd.gameObject.SetActive(false);
            var blast = MakeGun<BlastWeapon>(holder, blastD, rig, new Color(.42f, .29f, .13f), .5f, .12f);
            blast.health = health;
            blast.arc = NewLine("Arc", new Color(1, .7f, .4f), .04f); blast.arc.enabled = false;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = new Vector3(.9f, .01f, .9f);
            marker.GetComponent<Renderer>().material = CombatFx.I.Additive(new Color(1, .6f, .2f, .8f));
            blast.marker = marker.transform; marker.SetActive(false);
            var sword = MakeSword(holder, rig);

            combat.Init(new IWeapon[] { rifle, laser, blast, sword });

            var hudGo = new GameObject("HudController");
            hud = hudGo.AddComponent<Hud>();
            hud.t = tuning; hud.motor = motor; hud.combat = combat; hud.health = health; hud.rig = rig; hud.game = this;
        }

        LineRenderer NewLine(string name, Color c, float w)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.startWidth = lr.endWidth = w; lr.material = CombatFx.I.Additive(c); lr.positionCount = 2;
            return lr;
        }

        T MakeGun<T>(Transform holder, WeaponData d, PlayerCameraRig rig, Color c, float len, float girth) where T : WeaponBase
        {
            var w = new GameObject(d.displayName).AddComponent<T>();
            w.transform.SetParent(holder, false);
            w.data = d; w.motor = motor; w.rig = rig; w.tuning = tuning;
            var model = new GameObject(d.displayName + "Model").transform;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(model, false); body.transform.localScale = new Vector3(girth, girth * 1.4f, len); body.transform.localPosition = new Vector3(0, 0, len / 2);
            body.GetComponent<Renderer>().material = Mat(c);
            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(grip.GetComponent<Collider>());
            grip.transform.SetParent(model, false); grip.transform.localScale = new Vector3(girth * .8f, girth * 2, girth); grip.transform.localPosition = new Vector3(0, -girth * 1.3f, len * .15f);
            grip.GetComponent<Renderer>().material = Mat(c);
            var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(model, false); muzzle.localPosition = new Vector3(0, 0, len + .02f);
            w.model = model; w.muzzle = muzzle;
            model.gameObject.SetActive(false);
            return w;
        }

        SwordWeapon MakeSword(Transform holder, PlayerCameraRig rig)
        {
            var w = new GameObject(swordD.displayName).AddComponent<SwordWeapon>();
            w.transform.SetParent(holder, false);
            w.data = swordD; w.motor = motor; w.rig = rig; w.tuning = tuning;
            var model = new GameObject("SwordModel").transform;
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(model, false);
            float len = swordD.sword.combo[0].range - swordD.sword.hiltOffset;
            blade.transform.localScale = new Vector3(.05f, .012f, len); blade.transform.localPosition = new Vector3(0, 0, len / 2);
            blade.GetComponent<Renderer>().material = Mat(new Color(.85f, .87f, .9f));
            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(guard.GetComponent<Collider>());
            guard.transform.SetParent(model, false); guard.transform.localScale = new Vector3(.2f, .03f, .04f);
            guard.GetComponent<Renderer>().material = Mat(new Color(.54f, .42f, .16f));
            var tip = new GameObject("TrailTip").transform; tip.SetParent(model, false); tip.localPosition = new Vector3(0, 0, len * .7f);
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = .12f; trail.startWidth = len * .6f; trail.endWidth = 0; trail.material = CombatFx.I.Additive(new Color(1, 1, 1, .35f)); trail.emitting = false;
            w.trail = trail; w.model = model; w.muzzle = model;
            model.gameObject.SetActive(false);
            return w;
        }

        bool Visible(Vector3 p)
        {
            var cam = Camera.main; if (!cam) return false;
            var to = p + Vector3.up * 1.2f - cam.transform.position;
            if (Vector3.Angle(cam.transform.forward, to) > cam.fieldOfView * 0.75f) return false;
            return !Physics.Raycast(cam.transform.position, to.normalized, to.magnitude - .1f, Hits.Mask, QueryTriggerInteraction.Ignore);
        }

        void SpawnWave()
        {
            Spawn(gruntD, gruntSpawns);
            Spawn(bruiserD, bruiserSpawns);
        }

        void Spawn(EnemyData d, List<Vector3> points)
        {
            var hidden = points.FindAll(p => !Visible(p));
            var pool = hidden.Count > 0 ? hidden : points;
            for (int i = 0; i < d.spawnCount; i++)
            {
                var pos = pool[(i + Random.Range(0, pool.Count)) % pool.Count];
                if (NavMesh.SamplePosition(pos, out var h, 3f, NavMesh.AllAreas)) pos = h.position;
                CreateEnemy(d, pos);
            }
        }

        void CreateEnemy(EnemyData d, Vector3 pos)
        {
            var go = new GameObject(d.kind.ToString());
            go.SetActive(false);
            go.transform.position = pos;
            var agent = go.AddComponent<NavMeshAgent>(); agent.baseOffset = 0;
            var e = go.AddComponent<Enemy>();
            e.data = d; e.player = motor; e.playerHealth = health; e.playerSword = combat.Sword;
            float headR = d.kind == EnemyKind.Grunt ? .2f : .3f;
            float bodyH = d.height - headR * 2;

            var bodyM = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyM.transform.SetParent(go.transform, false);
            bodyM.transform.localScale = new Vector3(d.radius * 2, bodyH / 2, d.radius * 2);
            bodyM.transform.localPosition = Vector3.up * bodyH / 2;
            bodyM.GetComponent<Renderer>().material = Mat(d.color);
            var hb = bodyM.AddComponent<Hitbox>(); hb.owner = e; hb.part = HitPart.Body;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(go.transform, false);
            head.transform.localScale = Vector3.one * headR * 2;
            head.transform.localPosition = Vector3.up * (d.height - headR);
            head.GetComponent<Renderer>().material = Mat(Color.Lerp(d.color, Color.white, .3f));
            var hh = head.AddComponent<Hitbox>(); hh.owner = e; hh.part = HitPart.Head;

            var gunTip = new GameObject("GunTip").transform; gunTip.SetParent(go.transform, false); gunTip.localPosition = new Vector3(.3f, 1.25f, .5f);
            var gun = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(gun.GetComponent<Collider>());
            gun.transform.SetParent(go.transform, false); gun.transform.localScale = new Vector3(.08f, .1f, .4f); gun.transform.localPosition = new Vector3(.3f, 1.25f, .3f);
            gun.GetComponent<Renderer>().material = Mat(Color.black);
            if (d.kind == EnemyKind.Bruiser) gun.transform.localScale = Vector3.one * .4f;

            e.BodyCollider = bodyM.GetComponent<Collider>();
            e.renderers = new[] { bodyM.GetComponent<Renderer>(), head.GetComponent<Renderer>() };
            e.gunTip = gunTip;
            go.SetActive(true);
        }

        void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            }
            if (input && input.Debug.WasPressedThisFrame()) { autoRespawn = !autoRespawn; hud.Toast(autoRespawn ? "자동 리스폰 ON" : "자동 리스폰 OFF"); }

            bool anyAlive = false;
            foreach (var e in Enemy.All) if (!e.Dead) { anyAlive = true; break; }
            if (!anyAlive)
            {
                if (wipeTimer < 0 && autoRespawn) { wipeTimer = gruntD.respawnDelay; hud.Toast("전멸! 리스폰 대기"); }
                if (wipeTimer > 0) { wipeTimer -= Time.deltaTime; if (wipeTimer <= 0) { wipeTimer = -1; SpawnWave(); } }
            }
        }
    }
}
