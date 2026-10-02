// HUD는 uGUI를 코드로 생성한다(프리팹/폰트 에셋 없이 동작).
// 크로스헤어: 무기 데이터 색, 산포만큼 벌어짐, 3인칭 조준 시 위로 이동, 장검이면 숨기고 호 인디케이터.
// 우측 하단 무기 위젯은 현재 IWeapon.HudText/HudFill을 그대로 그려 내부 상태와 항상 일치.
// 피격: 가장자리 비네트 + 방향 마커. 우측 상단 1P/3P 아이콘.
// 데미지 넘버는 CombatFx가 월드 공간에 띄운다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YardShooter
{
    public class Hud : MonoBehaviour
    {
        public PlayerTuning t;
        public PlayerMotor motor;
        public PlayerCombat combat;
        public PlayerHealth health;
        public PlayerCameraRig rig;
        public YardBootstrap game;

        Font font;
        RectTransform cross; Image[] crossBars = new Image[4];
        Image hpFill, stFill, wFill, vignette;
        Text wText, viewText, info, arcText, toast;
        Image[] slots = new Image[4];
        RectTransform arcRoot; Image arcImg;
        readonly List<(RectTransform rt, float t)> markers = new();
        float toastTimer;

        void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var root = canvasGo.transform;

            vignette = Img(root, "Vignette", new Color(0.9f, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            vignette.sprite = VignetteSprite();

            cross = new GameObject("Cross", typeof(RectTransform)).GetComponent<RectTransform>();
            cross.SetParent(root, false); cross.anchorMin = cross.anchorMax = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < 4; i++) crossBars[i] = Img(cross, "c" + i, Color.white, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);

            arcRoot = new GameObject("Arc", typeof(RectTransform)).GetComponent<RectTransform>();
            arcRoot.SetParent(root, false); arcRoot.anchorMin = arcRoot.anchorMax = new Vector2(.5f, .5f);
            arcImg = Img(arcRoot, "fan", new Color(1, 1, 1, 0.18f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            arcImg.type = Image.Type.Filled; arcImg.fillMethod = Image.FillMethod.Radial360; arcImg.fillOrigin = (int)Image.Origin360.Top;
            arcImg.sprite = CircleSprite();
            arcText = Txt(arcRoot, "", 18, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(200, 30));

            Txt(root, "체력", 16, TextAnchor.LowerLeft, Vector2.zero, new Vector2(40, 120), new Vector2(200, 24));
            hpFill = Bar(root, new Vector2(40, 100), new Color(.9f, .25f, .25f), Vector2.zero);
            Txt(root, "스태미나", 16, TextAnchor.LowerLeft, Vector2.zero, new Vector2(40, 66), new Vector2(200, 24));
            stFill = Bar(root, new Vector2(40, 46), new Color(.95f, .8f, .25f), Vector2.zero);

            string[] names = { "총", "레이저", "광역", "장검" };
            for (int i = 0; i < 4; i++)
            {
                slots[i] = Img(root, "slot" + i, new Color(0, 0, 0, .45f), Vector2.right, Vector2.right, new Vector2(-40 - (3 - i) * 92 - 84, 150), new Vector2(84, 50));
                slots[i].rectTransform.pivot = Vector2.zero;
                var tx = Txt(slots[i].transform, $"{i + 1}\n{names[i]}", 16, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(84, 50));
                tx.rectTransform.anchorMin = tx.rectTransform.anchorMax = new Vector2(.5f, .5f);
            }
            wText = Txt(root, "", 34, TextAnchor.LowerRight, Vector2.right, new Vector2(-40, 80), new Vector2(500, 50));
            wText.rectTransform.pivot = Vector2.right;
            wFill = Bar(root, new Vector2(-340, 46), Color.white, Vector2.right);

            viewText = Txt(root, "1P", 26, TextAnchor.UpperRight, Vector2.one, new Vector2(-40, -30), new Vector2(120, 40));
            viewText.rectTransform.pivot = Vector2.one;
            info = Txt(root, "", 18, TextAnchor.UpperLeft, Vector2.up, new Vector2(40, -30), new Vector2(600, 30));
            info.rectTransform.pivot = Vector2.up;
            toast = Txt(root, "", 40, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, 200), new Vector2(800, 60));

            health.Hurt += OnHurt;
            if (combat.Sword) combat.Sword.ParrySucceeded += () => Toast("패링!");
        }

        public void Toast(string s, float time = 1.2f) { toast.text = s; toastTimer = time; }

        void OnHurt(Vector3 from)
        {
            var c = vignette.color; c.a = 0.85f; vignette.color = c;
            var m = Img(vignette.transform.parent, "hitdir", new Color(1, .2f, .2f, .9f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(40, 14));
            m.rectTransform.pivot = new Vector2(.5f, -9f);
            var to = from - motor.transform.position; to.y = 0;
            float ang = Vector3.SignedAngle(motor.Forward, to, Vector3.up);
            m.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
            markers.Add((m.rectTransform, 0.7f));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var cur = combat.Current;
            for (int i = 0; i < 4; i++) slots[i].color = i == combat.Index ? new Color(1, 1, 1, .3f) : new Color(0, 0, 0, .45f);
            wText.text = cur.HudText; wText.color = cur.Data.crosshairColor;
            Fill(wFill, cur.HudFill); wFill.color = cur.Data.crosshairColor;
            Fill(hpFill, health.Hp / t.maxHealth);
            if (combat.Sword) Fill(stFill, combat.Sword.Stamina / combat.Sword.Data.sword.maxStamina);
            viewText.text = motor.FirstPerson ? "1P" : "3P";
            info.text = game.InfoLine;

            bool sword = combat.IsSword;
            cross.gameObject.SetActive(!sword);
            arcRoot.gameObject.SetActive(sword);
            float h = ((RectTransform)cross.parent).rect.height;
            cross.anchoredPosition = new Vector2(0, rig.CrosshairRaise * 0.5f * h);
            float gap = 4 + Mathf.Tan(cur.CrosshairSpreadDeg * Mathf.Deg2Rad) / Mathf.Tan(t.fov * 0.5f * Mathf.Deg2Rad) * h * 0.5f;
            Vector2[] pos = { new(0, gap + 6), new(0, -gap - 6), new(-gap - 6, 0), new(gap + 6, 0) };
            for (int i = 0; i < 4; i++)
            {
                crossBars[i].rectTransform.anchoredPosition = pos[i];
                crossBars[i].rectTransform.sizeDelta = i < 2 ? new Vector2(3, 12) : new Vector2(12, 3);
                crossBars[i].color = cur.Data.crosshairColor;
            }
            if (sword)
            {
                var s = combat.Sword; var sw = s.CurrentSwing;
                float R = 80 + sw.range * 60;
                arcImg.rectTransform.sizeDelta = new Vector2(R * 2, R * 2);
                arcImg.fillAmount = sw.arcDegrees / 360f;
                arcImg.rectTransform.localRotation = Quaternion.Euler(0, 0, sw.arcDegrees / 2);
                arcImg.color = s.Guarding ? new Color(.4f, .8f, 1f, .25f) : s.ParryBuff ? new Color(1, .85f, .2f, .3f) : new Color(1, 1, 1, .18f);
                arcText.text = s.Guarding ? "가드" : $"{(s.Stage < 0 ? 1 : s.Stage + 1)}단 · {sw.range}m";
            }
            var vc = vignette.color; vc.a = Mathf.MoveTowards(vc.a, 0, dt * 2.5f); vignette.color = vc;
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var m = markers[i]; m.t -= dt; markers[i] = m;
                if (m.t <= 0) { Destroy(m.rt.gameObject); markers.RemoveAt(i); }
            }
            if (toastTimer > 0) { toastTimer -= dt; if (toastTimer <= 0) toast.text = ""; }
        }

        static void Fill(Image i, float v) { i.rectTransform.localScale = new Vector3(Mathf.Clamp01(v), 1, 1); }

        Image Img(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        Text Txt(Transform parent, string s, int size, TextAnchor anchor, Vector2 a, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject("txt", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = a; rt.pivot = a; rt.anchoredPosition = pos; rt.sizeDelta = sz;
            var tx = go.GetComponent<Text>(); tx.font = font; tx.fontSize = size; tx.alignment = anchor; tx.text = s; tx.color = Color.white; tx.raycastTarget = false;
            return tx;
        }

        Image Bar(Transform root, Vector2 pos, Color c, Vector2 anchor)
        {
            var bg = Img(root, "barbg", new Color(0, 0, 0, .5f), anchor, anchor, pos, new Vector2(300, 14));
            bg.rectTransform.pivot = Vector2.zero;
            var fill = Img(bg.transform, "fill", c, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(300, 14));
            fill.rectTransform.pivot = Vector2.zero;
            return fill;
        }

        static Sprite CircleSprite()
        {
            const int N = 128; var tex = new Texture2D(N, N);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, d <= 1 ? (d > 0.93f ? 1f : 0.6f) : 0));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f));
        }

        static Sprite VignetteSprite()
        {
            const int N = 64; var tex = new Texture2D(N, N);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((d - 0.6f) / 0.5f)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f));
        }
    }
}
