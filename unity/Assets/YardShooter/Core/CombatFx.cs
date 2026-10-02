using System.Collections.Generic;
using UnityEngine;

namespace YardShooter
{
    // 히트 피드백(스파크·데미지 넘버·히트스톱·셰이크)을 한 곳에서 관리해 무기 코드가 연출을 몰라도 되게 한다.
    public class CombatFx : MonoBehaviour
    {
        public static CombatFx I;
        public PlayerTuning tuning;
        float hitstopUntil;
        public float ShakeTimer { get; private set; }

        class Floater { public TextMesh text; public Vector3 pos; public float t; }
        readonly List<Floater> floaters = new();
        readonly List<(GameObject go, float t, float life, System.Action<GameObject, float> update)> fx = new();
        Material additive;

        void Awake()
        {
            I = this;
            additive = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        public Material Additive(Color c)
        {
            var m = new Material(additive);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.color = c;
            return m;
        }

        public void Hitstop(float duration) { hitstopUntil = Mathf.Max(hitstopUntil, Time.unscaledTime + duration); }
        public void Shake() { ShakeTimer = tuning.shakeTime; }

        public void Spark(Vector3 p, Color c, int n = 8)
        {
            for (int i = 0; i < n; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = Vector3.one * 0.05f;
                go.transform.position = p;
                go.GetComponent<Renderer>().material = Additive(c);
                var v = new Vector3(Random.value - 0.5f, Random.value * 0.8f, Random.value - 0.5f).normalized * Random.Range(3f, 6f);
                Add(go, 0.2f, (g, k) => { g.transform.position += v * Time.unscaledDeltaTime; g.transform.localScale = Vector3.one * 0.05f * (1 - k); });
            }
        }

        public void Tracer(Vector3 a, Vector3 b, Color c, float life, float width)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = lr.endWidth = width;
            lr.material = Additive(c);
            Add(go, life, (g, k) => { var col = c; col.a = 1 - k; lr.material.color = col; });
        }

        public void Add(GameObject go, float life, System.Action<GameObject, float> update) => fx.Add((go, 0, life, update));

        public void Number(Vector3 p, float value, Color c)
        {
            var go = new GameObject("Dmg");
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; go.GetComponent<MeshRenderer>().material = font.material;
            tm.text = Mathf.RoundToInt(value).ToString();
            tm.characterSize = 0.06f; tm.fontSize = 64; tm.anchor = TextAnchor.MiddleCenter; tm.color = c; tm.fontStyle = FontStyle.Bold;
            floaters.Add(new Floater { text = tm, pos = p });
        }

        void Update()
        {
            Time.timeScale = Time.unscaledTime < hitstopUntil ? 0f : 1f;
            ShakeTimer -= Time.unscaledDeltaTime;
            float dt = Time.unscaledDeltaTime;
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var e = fx[i]; e.t += dt; fx[i] = e;
                float k = e.t / e.life;
                if (k >= 1) { Destroy(e.go); fx.RemoveAt(i); continue; }
                e.update(e.go, k);
            }
            var cam = Camera.main;
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i]; f.t += dt;
                if (f.t >= tuning.damageFloatTime) { Destroy(f.text.gameObject); floaters.RemoveAt(i); continue; }
                f.text.transform.position = f.pos + Vector3.up * f.t * 1.2f;
                if (cam) f.text.transform.rotation = cam.transform.rotation;
                var col = f.text.color; col.a = 1 - f.t / tuning.damageFloatTime; f.text.color = col;
            }
        }
    }
}
