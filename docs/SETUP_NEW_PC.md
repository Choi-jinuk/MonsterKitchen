# 새 PC 개발 환경 세팅 매뉴얼 (Claude Code 실행용)

> 최종 갱신: 2026-09-19
> 대상: Windows 11 x64. 기준 PC 세팅을 그대로 재현.
>
> **Claude Code 에게**: 이 문서를 위에서 아래로 실행한다. 각 단계의 `확인` 명령이 통과해야 다음 단계로 간다.
> 이미 충족된 단계는 건너뛴다. 사용자 조작이 필요한 단계(🧑 표시)는 멈추고 사용자에게 요청한다.
> 셸은 Bash(Git Bash) 기준. 프로젝트 루트 = `<PROJECT>` (clone 위치).

---

## 0. 기준 버전 (Source of Truth)

| 항목 | 버전 | 비고 |
|---|---|---|
| Unity Editor | **6000.3.24f1** (6000.3 LTS) | `ProjectSettings/ProjectVersion.txt` 가 정답. 이 문서와 다르면 파일 값을 따른다 |
| Editor 모듈 | Windows Build Support (IL2CPP), Android Build Support, Android SDK & NDK Tools, OpenJDK, iOS Build Support | |
| Unity CLI (`unity`) | 1.0.0-beta.10 이상 | winget `Unity.CLI` |
| `com.unity.pipeline` | 0.7.0-exp.1 | manifest 에 포함 → 자동 설치 |
| Coplay MCP (`com.coplaydev.unity-mcp`) | git `#main` (10.2.x) | manifest 에 포함 → 자동 설치 |
| `com.unity.ide.rider` | 3.0.40 | manifest 에 포함 |
| JetBrains Rider | 2026.1.3 이상 | |
| Claude Code | 2.1.x 이상 | |
| Git | 2.54+ | |
| uv / uvx | 0.11+ | Coplay MCP 서버 실행에 필요 |
| Python | 3.12 | 스크립트/플러그인 보조 |
| Node.js | 24.x | 플러그인 보조 |

---

## 1. 기본 도구 설치

```bash
winget install --id Git.Git -e
winget install --id astral-sh.uv -e
winget install --id Python.Python.3.12 -e
winget install --id OpenJS.NodeJS -e
winget install --id Unity.UnityHub -e
winget install --id Unity.CLI -e
winget install --id JetBrains.Rider -e
```

설치 후 **새 터미널**에서 PATH 반영 확인.

확인:
```bash
git --version && uv --version && uvx --version && python --version && node --version && unity --version
```

---

## 2. 프로젝트 Clone

```bash
git clone <REPO_URL> <PROJECT>
cd <PROJECT>
```

🧑 `<REPO_URL>` 은 사용자에게 묻는다. 이미 clone 되어 있으면 건너뜀.

---

## 3. Unity 로그인 & Editor 설치

🧑 라이선스 로그인 (브라우저 필요):
```bash
unity auth login
unity auth status          # 로그인 확인
```

Editor + 모듈 설치 (버전은 ProjectVersion.txt 에서 읽는다):
```bash
VER=$(grep '^m_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}')
unity install "$VER" -a x86_64 \
  -m android android-sdk-ndk-tools android-open-jdk-17.0.18+8 ios windows-il2cpp \
  --cm --accept-eula -y --non-interactive
```

- 모듈 ID 가 바뀌어 `Couldn't find module "X". Did you mean: Y` 가 나오면 제안된 Y 로 교체.
- UAC 프롬프트 🧑 승인 필요.

확인:
```bash
unity editors -i                 # $VER 행 + 모듈 5종 표시
unity editors verify "$VER"
unity doctor
```

---

## 4. 프로젝트 첫 오픈 (패키지 설치)

```bash
unity open .
```

- 첫 오픈은 Library 재생성으로 오래 걸림 (수 분~수십 분).
- `Packages/manifest.json` 의 `com.unity.pipeline`, `com.coplaydev.unity-mcp`, `com.unity.ide.rider` 자동 설치됨.
- Safe Mode 진입 시 → 컴파일 에러. 7단계 전에 해결.

확인 (Editor 열린 상태):
```bash
unity pipeline list              # Running=true, Pipeline=true, Server Reachable=true (포트 78xx)
unity command editor_status --json
unity command console_status     # 컴파일 실패 플래그 false
```

`Server Reachable=false` 이면: Editor 창 포커스 1회 → 재확인. 그래도 실패면 `unity command` 대신 Editor 메뉴에서 Pipeline 설정 확인.

---

## 5. Coplay MCP 서버 (UnityMCP) 기동

🧑 Unity Editor 에서:
1. 메뉴 `Window > MCP for Unity` 열기
2. Transport: **HTTP**, 포트 **8080** (URL `http://127.0.0.1:8080/mcp`)
3. **Start Server** 클릭 (내부적으로 `uvx` 로 Python 서버 실행 — 1단계 uv 필수)
4. 상태가 Connected 가 될 때까지 대기

확인:
```bash
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8080/mcp   # 000 이 아니면 서버 기동됨
```

---

## 6. Claude Code MCP 등록

프로젝트 루트에서 실행 (local scope — 이 PC·이 프로젝트 전용):

```bash
claude mcp add --transport http -s local UnityMCP http://127.0.0.1:8080/mcp
claude mcp add -s local unity-cli -- unity mcp --project-path "$(pwd -W 2>/dev/null || pwd)"
```

- `--project-path` 는 **이 PC 의 절대경로** (예: `C:/UnityProject/MonsterKitchen`). 슬래시 `/` 사용.
- 이미 등록돼 있으면 `claude mcp remove <name> -s local` 후 재등록.

확인:
```bash
claude mcp list        # UnityMCP ✔ Connected, unity-cli ✔ Connected
```

---

## 7. Claude Code 플러그인 & 설정

### 7-1. 사용자 설정 (`~/.claude/settings.json`)

기존 파일이 있으면 **병합** (덮어쓰기 금지). 필요한 키:

```json
{
  "model": "opus",
  "enabledPlugins": {
    "caveman@caveman": true,
    "superpowers@claude-plugins-official": true,
    "watch@claude-video": true,
    "i-have-adhd@i-have-adhd": true,
    "andrej-karpathy-skills@karpathy-skills": true,
    "understand-anything@understand-anything": true
  },
  "extraKnownMarketplaces": {
    "caveman":        { "source": { "source": "github", "repo": "JuliusBrussee/caveman" } },
    "claude-video":   { "source": { "source": "github", "repo": "bradautomates/claude-video" } },
    "i-have-adhd":    { "source": { "source": "github", "repo": "ayghri/i-have-adhd" } },
    "karpathy-skills":{ "source": { "source": "github", "repo": "forrestchang/andrej-karpathy-skills" }, "autoUpdate": false },
    "understand-anything": { "source": { "source": "github", "repo": "Egonex-AI/Understand-Anything" } }
  }
}
```

### 7-2. 플러그인 설치

Bash 에서 실행 (Claude Code 가 직접 실행 가능):
```bash
claude plugin marketplace add JuliusBrussee/caveman
claude plugin marketplace add bradautomates/claude-video
claude plugin marketplace add ayghri/i-have-adhd
claude plugin marketplace add forrestchang/andrej-karpathy-skills
claude plugin marketplace add Egonex-AI/Understand-Anything     # 구 Lum1104/Understand-Anything → 이전됨

claude plugin install superpowers@claude-plugins-official
claude plugin install caveman@caveman
claude plugin install watch@claude-video
claude plugin install i-have-adhd@i-have-adhd
claude plugin install andrej-karpathy-skills@karpathy-skills
claude plugin install understand-anything@understand-anything
```
이미 추가된 marketplace 는 에러 무시.

확인:
```bash
claude plugin list      # 전부 ✔ enabled
```
- 기준 버전: karpathy 1.0.0, understand-anything 2.9.7, superpowers 6.3.0.
- `i-have-adhd` 0.1.0 은 upstream 매니페스트 버그로 `✘ failed to load` (Duplicate hooks file) — 알려진 문제, 무시 가능.
- 플러그인 변경 후 Claude Code **재시작** 해야 스킬 반영.

`watch` 플러그인 Whisper fallback 쓰려면 `~/.config/watch/.env` 에 `GROQ_API_KEY` 또는 `OPENAI_API_KEY` 🧑 (선택).

### 7-3. 프로젝트 스킬

`.claude/skills/unity-cli`, `.claude/skills/unity-pipeline` 은 git 에 포함 → clone 으로 이미 존재. 추가 조치 불필요.
CLI 업데이트 후 스킬 최신화가 필요하면 `unity skill show --list` 로 내용 확인 후 사용자와 상의 (`unity skill refresh` 는 이 PC 에서 `unity skill install` 한 대상만 갱신).

### 7-4. 권한 (`.claude/settings.local.json`)

최소 권한 원칙. 임의 코드 실행(`bash`/`powershell`/`node`/`npx`/`python3 *`), 파괴적 명령(`del`), 자격증명 노출(`env`/`git credential`/`cmdkey`/`reg query`/`gh auth token`), 패키지 설치(`npm install`)는 **허용 목록에 넣지 않는다** — 필요 시 매번 승인.
`unity` CLI 는 조회·테스트 명령만 허용 (`unity command get_*/find_*/list_*`, `console`, `run_tests`, `unity test`, `unity vcs diff`).


git 추적 파일. PC 고유 절대경로 없음 (2026-09-19 정리 완료) → clone 그대로 사용, 수정 불필요.
작업 중 새 권한 추가 시 **절대경로(`C:/Users/<이름>/...`) 대신** 상대경로 또는 `~/` 사용 — 다른 PC 이식성 유지.

---

## 8. Rider 연동

🧑 Rider 에서:
1. `<PROJECT>/MonsterKitchen.sln` 열기 (또는 Unity `Edit > Preferences > External Tools > External Script Editor = Rider`)
2. Unity Editor 와 Rider 하단 Unity 아이콘 연결 확인 (Play/Refresh 동기화)
3. `Settings > Plugins` 에서 **Claude Code** 플러그인 설치 → Claude Code 를 Rider 터미널에서 실행하거나 `/ide` 로 연결

확인: Claude Code 에서 `/ide` → Rider 표시. `mcp__ide__getDiagnostics` 호출 가능.

`.idea/` 는 gitignore — Rider 설정은 PC 별로 새로 생성됨.

---

## 9. 최종 검증

```bash
# Editor 열린 상태
unity pipeline list
unity command console_status
unity command run_tests --mode EditMode
claude mcp list
```

Claude Code 에서:
- UnityMCP `read_console` → 컴파일 에러 0
- UnityMCP `run_tests` (EditMode) → 전부 통과
- `unity command eval 'return UnityEngine.Application.unityVersion;'` → 기준 버전과 일치

Editor 닫힌 상태 대체:
```bash
unity test --mode EditMode --output Logs/test-editmode.xml
```

---

## 10. 트러블슈팅

| 증상 | 조치 |
|---|---|
| `unity pipeline list` Server Reachable=false | Editor 포커스 → 재확인. Safe Mode(컴파일 에러) 여부 확인 |
| UnityMCP 연결 실패 | `Window > MCP for Unity` 에서 Start Server. `uvx --version` 확인. 포트 8080 점유 여부 `netstat -ano \| grep 8080` |
| `unity test` exit 8 | 테스트 실패. `Logs/test-editmode.xml` 확인 |
| `unity test` 프로젝트 락 | Editor 열려 있음 → `unity command run_tests` 사용 |
| 도메인 리로드 중 도구 불가 | `unity command editor_status` 가 ready 될 때까지 대기 |
| 모듈 ID 불일치 | `unity install <VER> --dry-run -m ...` 로 제안 ID 확인 |
| Coplay 업데이트로 동작 변경 | manifest 가 `#main` 추적 중. `Packages/packages-lock.json` 의 hash 가 실제 고정값 — lock 파일을 커밋 상태로 유지 |
