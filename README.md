# Yard Shooter

1인칭/3인칭을 런타임에 전환하며 소총 · 레이저 · 광역탄 · 장검을 한 캐릭터로 쓰는 싱글플레이 슈터 프로토타입입니다.

## 바로 플레이 (웹)

GitHub Pages 링크를 열고 화면을 클릭하면 시작합니다. 설치 필요 없음, PC 브라우저(Chrome/Edge 권장).

| 키 | 동작 |
|---|---|
| WASD / Shift | 이동 / 달리기 |
| Space / C(또는 Ctrl) | 점프 / 웅크리기 |
| V | 1인칭 ↔ 3인칭 |
| 1 2 3 4 / 휠 | 소총 · 레이저 · 광역 · 장검 |
| 좌클릭 | 공격 (레이저: 탭=펄스, 홀드=지속 빔 / 장검: 3단 콤보) |
| 우클릭 | 3인칭 조준 / 광역 낙하 예측 / 장검 가드·패링 |
| R / Alt(또는 Q) | 재장전 / 구르기 |
| P | 전멸 시 자동 리스폰 토글 |

팁: 빨간 Bruiser가 하얗게 번쩍이면 0.4초 뒤 돌진합니다. 장검(4)을 들고 돌진 직전에 우클릭하면 패링됩니다.

## 폴더

- `docs/` — 웹 버전(Three.js). GitHub Pages가 이 폴더를 그대로 호스팅합니다. 튜닝 수치는 `docs/js/data.js` 한 곳에만 있습니다.
- `unity/Assets/YardShooter/` — Unity 6 URP 버전 C# 소스.

## Unity 버전 실행

1. Unity Hub에서 **Unity 6 → Universal 3D** 템플릿으로 새 프로젝트를 만듭니다.
2. 이 저장소의 `unity/Assets/YardShooter` 폴더를 새 프로젝트의 `Assets/`에 복사합니다.
3. Package Manager에서 **Input System**, **AI Navigation**이 설치돼 있는지 확인합니다(Universal 3D 템플릿은 기본 포함). Input System 활성화 팝업이 뜨면 Yes.
4. 프로젝트가 열리면 `Assets/YardShooter/Resources/YardShooter/`에 데이터 에셋 7개가 자동 생성됩니다(안 보이면 메뉴 **YardShooter → Create Default Data**).
5. 아무 씬에서 **Play**. 야드·플레이어·무기·적·HUD·NavMesh가 런타임에 생성됩니다.

수치 조정은 생성된 `.asset`(WeaponData, PlayerTuning, EnemyData)을 인스펙터에서 바꾸면 됩니다.

### Unity 버전을 웹에 올리고 싶다면

File → Build Profiles → Web 으로 빌드한 뒤 결과물을 `docs/`에 넣고 푸시하면 같은 링크로 바뀝니다.

## 사양 대비 차이

- 태그/레이어 대신 `Cover` 컴포넌트(Thin/Thick)와 Ignore Raycast 레이어를 사용합니다. 새 프로젝트의 TagManager를 수정하지 않고 동작시키기 위함입니다.
- 1인칭 몸통 컬링은 FPArms 레이어 대신 ShadowsOnly 렌더 모드로 처리합니다(그림자는 유지).
- 웹 버전의 웅크리기는 C 키를 기본으로 합니다. 브라우저에서 Ctrl+W가 탭을 닫기 때문입니다.
- 래그돌은 프리미티브 캡슐이라 물리 래그돌 대신 쓰러짐 애니메이션 후 2초 페이드입니다.
