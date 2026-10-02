// 플레이 방법: WASD 이동, Shift 달리기, Space 점프, C/LeftCtrl 웅크리기, V 시점 전환.
// 1~4 / 휠로 무기, 좌클릭 공격, 우클릭 보조(조준·예측·가드), R 재장전, LeftAlt 구르기.
// 게임패드: 좌스틱 이동, 우스틱 시점, RT 공격, LT 보조, A 점프, B 웅크리기, Y 시점, LB/RB 무기, R3 구르기.
// 입력 에셋 없이 코드로 액션을 만들어 새 프로젝트에 스크립트만 복사해도 바로 동작하게 한다.
using UnityEngine;
using UnityEngine.InputSystem;

namespace YardShooter
{
    public class GameInput : MonoBehaviour
    {
        public InputAction Move, Look, Sprint, Jump, Crouch, View, Fire, Alt, Reload, Dodge, Next, Prev, Scroll, Debug;
        public InputAction[] Slots = new InputAction[4];
        public bool LookIsMouse { get; private set; }

        void Awake()
        {
            Move = new InputAction("Move");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");
            Look = new InputAction("Look");
            Look.AddBinding("<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick");
            Sprint = Btn("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Jump = Btn("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Crouch = Btn("Crouch", "<Keyboard>/c", "<Gamepad>/buttonEast");
            Crouch.AddBinding("<Keyboard>/leftCtrl");
            View = Btn("View", "<Keyboard>/v", "<Gamepad>/buttonNorth");
            Fire = Btn("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Alt = Btn("Alt", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            Reload = Btn("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            Dodge = Btn("Dodge", "<Keyboard>/leftAlt", "<Gamepad>/rightStickPress");
            Next = Btn("Next", "<Gamepad>/rightShoulder", null);
            Prev = Btn("Prev", "<Gamepad>/leftShoulder", null);
            Scroll = new InputAction("Scroll", binding: "<Mouse>/scroll/y");
            Debug = Btn("Debug", "<Keyboard>/p", null);
            for (int i = 0; i < 4; i++) Slots[i] = Btn("Slot" + (i + 1), "<Keyboard>/" + (i + 1), null);

            foreach (var a in All()) a.Enable();
        }

        static InputAction Btn(string name, string kb, string pad)
        {
            var a = new InputAction(name, InputActionType.Button, kb);
            if (pad != null) a.AddBinding(pad);
            return a;
        }

        System.Collections.Generic.IEnumerable<InputAction> All()
        {
            yield return Move; yield return Look; yield return Sprint; yield return Jump; yield return Crouch;
            yield return View; yield return Fire; yield return Alt; yield return Reload; yield return Dodge;
            yield return Next; yield return Prev; yield return Scroll; yield return Debug;
            foreach (var s in Slots) yield return s;
        }

        void OnDestroy() { foreach (var a in All()) a.Dispose(); }

        public Vector2 LookDelta(PlayerTuning t)
        {
            var v = Look.ReadValue<Vector2>();
            LookIsMouse = Look.activeControl != null && Look.activeControl.device is Mouse;
            return LookIsMouse ? v * t.mouseSensitivity : v * t.padLookSpeed * Time.unscaledDeltaTime;
        }

        public int CycleDelta()
        {
            float s = Scroll.ReadValue<float>();
            int d = s > 0.01f ? -1 : s < -0.01f ? 1 : 0;
            if (Next.WasPressedThisFrame()) d += 1;
            if (Prev.WasPressedThisFrame()) d -= 1;
            return d;
        }
    }
}
