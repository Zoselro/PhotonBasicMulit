## 폴더 규칙

| 분류 | 경로 |
|---|---|
| Scripts | `Assets/02. Scripts/{도메인}/` |
| SO | `Assets/03. SO/{도메인}/` |
| Prefabs | `Assets/04. Prefabs/` |
| UI 프리팹 | `Resources/UI/{Popup|Scene|Tab}/{클래스명}` |
| 유닛 SO | `Assets/03. SO/Unit/{등급}Star/{UnitId}.asset` |
| 전역 SO | `Assets/Resources/GameSettings` |
| 에디터 | `Assets/Editor/` |

## 반드시 지켜야할 점
- 한글 사용 금지
- OOP 기반 설계
- 계획부터 말하고 승인 받은 후에 작업 진행
- 최적화를 고려한 코드 작성

## 주요 시스템

| 시스템 | 위치 | 문서 |
|---|---|---|
| 스킬 | `Scripts/Skill/` | `Docs/Systems/Skill.md` |
| 유닛/파티 | `Scripts/Unit/` | `Docs/Systems/Unit.md` |
| UI/탭 | `Scripts/UI/` | `Docs/Systems/UI.md` |
| 유저 데이터 | `Scripts/User/` | `Docs/Systems/UserData.md` |
| 보상 | `Scripts/Reward/` | `Docs/Systems/Reward.md` |
| 데이터 임포트 | `Editor/DataImport/` | `Docs/Systems/DataImport.md` |
| 개발 도구 | `Scripts/Dev/`, `Editor/` | `Docs/DevTools.md` |