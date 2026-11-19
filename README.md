# ai-behavior-lab

> 패턴을 외우면 끝나는 보스를 넘어서, 적응하는 보스 AI 실험

보스 패턴이 파악되면 숙련자의 공략법이 고정되고 전투가 고인다. 이 프로젝트는 게임 AI 의사결정 기법(FSM, BT, GOAP, HTN, 유틸리티 AI)으로 보스와 플레이어봇을 만들고,

1. 플레이어봇을 진화시켜 **가상의 숙련자**가 보스를 공략하는 과정을 재현하고
2. **고정 보스와 적응형 보스**가 얼마나 빨리 공략되는지 비교한다.

## 구성

| 프로젝트 | 내용 |
|---|---|
| `src/AiBehaviorLab.Core` | 시뮬레이션 코어(고정 틱, 결정적), 전투 규칙, `IBrain` |
| `src/AiBehaviorLab.Runner` | 콘솔에서 여러 판을 병렬로 돌리고 통계를 낸다 |
| `tests/AiBehaviorLab.Tests` | 단위 테스트 |

관전 화면(Godot 4 C#)은 2단계에서 추가한다. 진행 계획은 [docs/roadmap.md](docs/roadmap.md).

## 실행

.NET 10 SDK가 필요하다.

```powershell
dotnet test AiBehaviorLab.slnx
dotnet run --project src/AiBehaviorLab.Runner -c Release -- --matches 2000 --seed 1 --limits casual
```

러너 옵션:

| 옵션 | 기본값 | 뜻 |
|---|---|---|
| `--matches` | 1000 | 판 수 |
| `--seed` | 1 | 첫 판 시드(판마다 1씩 증가) |
| `--limits` | casual | 플레이어봇 사람 한계: `none`, `casual`, `expert` |

## 문서

- [설계](docs/design.md)
- [전투 규칙 v0](docs/combat-rules.md)
- [실험](docs/experiments.md)
