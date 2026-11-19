# ai-behavior-lab

게임 AI 의사결정 기법(FSM, BT, GOAP, HTN, 유틸리티 AI)으로 보스와 플레이어봇을 만들고, 진화와 적응으로 **고착되지 않는 보스**를 실험한다.
사람용 설명은 [README.md](README.md), 현재 상태와 다음 할 일은 `STATUS.md`(로컬 전용).

## 문서

- [docs/design.md](docs/design.md): 질문, 구조, 한 틱의 순서, 결정성
- [docs/combat-rules.md](docs/combat-rules.md): 전투 규칙 숫자. **코드 숫자를 바꾸면 같이 바꾼다**
- [docs/experiments.md](docs/experiments.md): 실험 1~3과 측정 지표
- [docs/roadmap.md](docs/roadmap.md): 단계별 작업

## 규칙

- **공개 저장소**다. 내 PC 경로, 개인 정보, 회사 이름을 커밋하지 않는다. 참고한 사람이나 다른 프로젝트 이름도 적지 않는다.
- `AiBehaviorLab.Core`는 화면을 모른다. Godot 4 C#에서 참조할 수 있게 `net8.0`을 유지한다. 러너와 테스트는 `net10.0`.
- **결정성**: 난수는 `DeterministicRandom`만 쓴다(`System.Random`, `DateTime`, `Guid`, `HashSet`/`Dictionary` 순회 순서에 기대지 않기). 같은 시드면 같은 결과가 나와야 하고 테스트로 지킨다.
- 시간은 내부에서 **틱**(정수)으로 다룬다. 초 단위 값은 `CombatRules.ToTicks`로 바꾼다.
- Brain은 `Observation`만 보고 `Intent`만 낸다. 시뮬레이션 상태를 직접 읽거나 바꾸지 않는다.
- 예고 범위 = 판정 범위. 조준은 단계의 선딜 시작 때 고정한다(공정성).
- 리플레이는 상태 기록 방식이다(`Replay/`). 형식을 바꾸면 `ReplayFile.CurrentFormatVersion`을 올린다. 새 Brain은 `ReplayCatalog`에 이름으로 등록해야 리플레이 검증이 된다.
- 관전 화면(`viewer/`)은 그리기만 한다. 규칙 계산은 Core에서 하고, 화면용 값(범위 모양 등)은 리플레이 프레임에 담는다. 관전·디버그 도형은 코드로 그려도 된다.
- 주석과 문서는 한국어.
- 단계가 끝나면 Codex 리뷰(`review` 스킬) → 동의한 지적 수정. push는 사용자 승인 후 리뷰 → QA 순서로.

## 확인 방법

```powershell
dotnet test AiBehaviorLab.slnx
dotnet run --project src/AiBehaviorLab.Runner -c Release -- --matches 2000 --seed 1 --limits casual
dotnet build viewer/AiBehaviorLabViewer.csproj
# 화면 확인(창이 잠깐 뜬다): Godot을 직접 실행해 스크린샷을 남기고 종료
<godot>_console.exe --path viewer -- --seed 7 --frame 805 --verify 1 --screenshot <png>
```

Godot는 winget으로 설치되어 PATH에 없을 수 있다. `viewer/run.ps1`의 찾는 순서를 참고한다.
