#!/usr/bin/env python3
"""Конвейер «локальная LLM делает мелкую графику кодом» (Krntija).

Что делает:
  1. читает задание tools/llm_art/tasks/<slug>/task.md (верхний блок + текст);
  2. собирает промпт: системный контракт + задание + референсы из репозитория;
  3. спрашивает Ollama и разбирает ответ на файлы (маркеры === FILE: ... ===);
  4. пишет файлы в текущий worktree (только game/ и tools/), ставит .uid новым .cs;
  5. собирает, гоняет Godot-проверки, делает снимок, повторяет с текстом ошибок;
  6. складывает всё в build/llm_art/<дата>-<slug>/ и дописывает строку в
     tools/llm_art/RESULTS.md.

Запуск (из корня worktree задачи):
    python tools/llm_art/run.py dust-vfx
    python tools/llm_art/run.py dust-vfx --dry-run
    python tools/llm_art/run.py dust-vfx --num-gpu 0 --attempts 8
"""

from __future__ import annotations

import argparse
import json
import os
import random
import re
import shutil
import string
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART_DIR = Path(__file__).resolve().parent
TASKS_DIR = ART_DIR / "tasks"
OLLAMA_URL = "http://localhost:11434/api/chat"
WRITE_ROOTS = ("game", "tools")
RUNS_DIR = ROOT / "build" / "llm_art"

# В консоли Windows кодировка cp1251: русский текст и «−» в промпте иначе падают.
for stream in (sys.stdout, sys.stderr):
    if hasattr(stream, "reconfigure"):
        stream.reconfigure(encoding="utf-8", errors="replace")

FILE_MARK = re.compile(r"^===\s*FILE:\s*(.+?)\s*===\s*$", re.MULTILINE)
WHY_MARK = re.compile(r"^-{3,}\s*ПОЧЕМУ.*$", re.MULTILINE)
FAIL_LINE = re.compile(r"FAIL|SCRIPT ERROR|НЕ ЗАШЁЛ|ЗАБЛОКИРОВАНО|ПРОВАЛИЛСЯ")

# ---------------------------------------------------------------- задание


def parse_front_matter(text: str) -> tuple[dict[str, str], str]:
    """Верхний блок task.md: простые пары «ключ: значение», без вложенности."""
    if not text.startswith("---"):
        raise SystemExit("в task.md нет верхнего блока --- ... ---")

    end = text.find("\n---", 3)
    if end < 0:
        raise SystemExit("в task.md не закрыт верхний блок ---")

    head, body = text[3:end], text[end + 4:]
    meta: dict[str, str] = {}
    for line in head.splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        meta[key.strip().lower()] = value.strip()

    return meta, body.lstrip("\n")


def split_list(value: str | None) -> list[str]:
    if not value:
        return []
    return [item.strip() for item in value.split(",") if item.strip()]


def read_ref(spec: str) -> str:
    """Референс: «путь» или «путь:с-по». Отдаёт кусок файла с заголовком."""
    match = re.fullmatch(r"(.+?):(\d+)-(\d+)", spec)
    if match:
        path, start, end = Path(match.group(1)), int(match.group(2)), int(match.group(3))
    else:
        path, start, end = Path(spec), 0, 0

    full = (ROOT / path).resolve()
    if not full.is_file():
        return f"### {spec}\n(файл не найден)\n"

    lines = full.read_text(encoding="utf-8", errors="replace").splitlines()
    if start > 0:
        chunk = lines[max(0, start - 1):end]
        return f"### {spec}\n```\n" + "\n".join(chunk) + "\n```\n"

    return f"### {spec}\n```\n" + "\n".join(lines) + "\n```\n"


def build_prompt(meta: dict[str, str], body: str) -> str:
    system = (ART_DIR / "prompt_system.md").read_text(encoding="utf-8")
    parts = [system, "\n\n## ЗАДАНИЕ\n", body.strip(), "\n\n## ФАЙЛЫ, КОТОРЫЕ НУЖНО ВЕРНУТЬ\n"]
    parts += [f"- {path}\n" for path in split_list(meta.get("writes"))]

    # Заготовка: API и сигнатуры уже верные, модель заполняет только тела.
    skeleton = meta.get("skeleton", "").strip()
    if skeleton:
        parts.append("\n## ЗАГОТОВКА — ЗАПОЛНИ ТЕЛА, СИГНАТУРЫ НЕ МЕНЯЙ\n")
        parts.append(read_ref(skeleton))
        parts.append("\nВерни получившийся файл целиком, с тем же путём из списка выше.\n")

    refs = split_list(meta.get("refs"))
    if refs:
        parts.append("\n## РЕФЕРЕНСЫ ИЗ РЕПОЗИТОРИЯ\n")
        for ref in refs:
            parts.append(read_ref(ref))

    return "".join(parts)


# ---------------------------------------------------------------- ollama


def ask_model(model: str, prompt: str, num_gpu: int | None, temperature: float,
              num_ctx: int, timeout: int = 1800) -> str:
    options = {"temperature": temperature, "num_ctx": num_ctx, "num_predict": 4096}
    if num_gpu is not None:
        options["num_gpu"] = num_gpu

    payload = {
        "model": model,
        "messages": [{"role": "user", "content": prompt}],
        "stream": False,
        "keep_alive": "5m",
        "options": options,
    }
    data = json.dumps(payload).encode("utf-8")

    # 500 бывает сразу после загрузки модели: ждём и пробуем ещё раз.
    for attempt in range(3):
        request = urllib.request.Request(OLLAMA_URL, data=data,
                                         headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                result = json.loads(response.read().decode("utf-8"))
            return result.get("message", {}).get("content", "") or ""
        except urllib.error.HTTPError as error:
            if error.code < 500 or attempt == 2:
                raise
            print(f"  Ollama ответила {error.code}, повтор через 8 с…")
            time.sleep(8)
        except (TimeoutError, urllib.error.URLError):
            if attempt == 2:
                raise
            print("  Ollama не отвечает, повтор через 8 с…")
            time.sleep(8)

    return ""


# ---------------------------------------------------------------- файлы


def parse_answer(text: str, writes: list[str]) -> dict[str, str]:
    """Ответ модели → {путь: содержимое}. Берём только пути из задания."""
    allowed = {path.replace("\\", "/") for path in writes}
    files: dict[str, str] = {}

    marks = list(FILE_MARK.finditer(text))
    if marks:
        for index, mark in enumerate(marks):
            path = mark.group(1).strip().replace("\\", "/")
            start = mark.end()
            end = marks[index + 1].start() if index + 1 < len(marks) else len(text)
            content = text[start:end]
            content = WHY_MARK.split(content)[0]
            content = strip_fences(content)
            if path in allowed:
                files[path] = content
    else:
        # Модель забыла маркеры — берём самый большой блок кода и первый путь.
        blocks = re.findall(r"```[a-zA-Z]*\n(.*?)```", text, re.DOTALL)
        if blocks and writes:
            files[writes[0].replace("\\", "/")] = blocks[0].strip() + "\n"

    return files


def strip_fences(text: str) -> str:
    lines = text.strip().splitlines()
    while lines and lines[0].strip().startswith("```"):
        lines.pop(0)
    while lines and lines[-1].strip().startswith("```"):
        lines.pop()
    return "\n".join(lines).strip() + "\n"


def safe_target(relative: str) -> Path | None:
    target = (ROOT / relative).resolve()
    if ROOT not in target.parents:
        return None
    if target.relative_to(ROOT).parts[0] not in WRITE_ROOTS:
        return None
    return target


def write_files(files: dict[str, str], run_dir: Path) -> list[str]:
    written = []
    backup = run_dir / "backup"
    for relative, content in files.items():
        target = safe_target(relative)
        if target is None:
            print(f"  ! путь вне game/ и tools/, пропускаю: {relative}")
            continue

        if target.exists():
            backup.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, backup / target.name)

        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8", newline="\n")
        written.append(relative)
        print(f"  + {relative} ({len(content.splitlines())} строк)")

    return written


def ensure_uid(written: list[str]):
    """Правило проекта: game/scripts/*.cs коммитится вместе с .cs.uid."""
    alphabet = string.ascii_lowercase + string.digits
    for relative in written:
        if not relative.startswith("game/scripts/") or not relative.endswith(".cs"):
            continue

        uid_file = ROOT / (relative + ".uid")
        if uid_file.exists():
            continue

        token = "".join(random.choice(alphabet) for _ in range(13))
        uid_file.write_text(f"uid://{token}\n", encoding="utf-8", newline="\n")
        print(f"  + {relative}.uid")


# ---------------------------------------------------------------- сборка и проверки


def read_env() -> tuple[Path, Path]:
    """Пути dotnet и Godot — из tools/_env.ps1, как у check.ps1."""
    env_ps1 = (ROOT / "tools" / "_env.ps1").read_text(encoding="utf-8")
    dotnet = re.search(r"\$DotnetRoot\s*=\s*'([^']+)'", env_ps1)
    godot = re.search(r"\$GodotDir\s*=\s*'([^']+)'", env_ps1)
    exe = re.search(r"\$GodotConsoleExe\s*=\s*Join-Path\s+\$GodotDir\s+'([^']+)'", env_ps1)
    if not (dotnet and godot and exe):
        raise SystemExit("не разобрал пути в tools/_env.ps1")

    return Path(dotnet.group(1)), Path(godot.group(1)) / exe.group(1)


def child_env(dotnet_root: Path, godot_dir: Path) -> dict[str, str]:
    env = os.environ.copy()
    env["DOTNET_ROOT"] = str(dotnet_root)
    env["PATH"] = f"{dotnet_root};{godot_dir};" + env.get("PATH", "")
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env["DOTNET_NOLOGO"] = "1"
    return env


def ensure_import(godot: Path, env: dict[str, str]):
    """В свежем worktree ассетов нет в .godot: без импорта Godot-проверки красные."""
    imported = ROOT / "game" / ".godot" / "imported"
    if imported.is_dir():
        return

    print("  импорт ассетов (первый прогон в этой папке)…")
    subprocess.run([str(godot), "--headless", "--path", "game", "--import"], cwd=ROOT,
                   capture_output=True, text=True, encoding="utf-8", errors="replace",
                   env=env, timeout=1800)


def run_command(command: str, env: dict[str, str], timeout: int = 900) -> tuple[int, str]:
    print(f"  $ {command}")
    started = time.time()
    result = subprocess.run(command, cwd=ROOT, shell=True, capture_output=True,
                            text=True, encoding="utf-8", errors="replace", env=env,
                            timeout=timeout)
    output = (result.stdout or "") + (result.stderr or "")
    print(f"    код {result.returncode}, {time.time() - started:.1f} с")
    return result.returncode, output


def error_tail(output: str, limit: int = 40) -> str:
    lines = [line for line in output.splitlines() if ": error " in line or "error CS" in line]
    if not lines:
        lines = output.splitlines()[-limit:]
    return "\n".join(lines[-limit:])


def godot_check(flags: str, godot: Path, env: dict[str, str]) -> tuple[bool, str]:
    args = [str(godot), "--headless", "--path", "game", "--"] + flags.split()
    for attempt in (1, 2):
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True,
                                encoding="utf-8", errors="replace", env=env, timeout=600)
        output = (result.stdout or "") + (result.stderr or "")
        failures = [line for line in output.splitlines() if FAIL_LINE.search(line)]
        if result.returncode in (0, 1) or attempt == 2:
            ok = result.returncode == 0 and not failures
            return ok, "\n".join(failures) if failures else output[-3000:]
        print(f"    Godot упал при выходе (код {result.returncode}), повтор…")

    return False, ""


def take_shot(flags: str, godot: Path, env: dict[str, str], run_dir: Path) -> str | None:
    args = [str(godot), "--path", "game", "--"] + flags.split()
    subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8",
                   errors="replace", env=env, timeout=300)

    match = re.search(r"--shot-name=(\S+)", flags)
    name = match.group(1) if match else "shot"
    source = Path(os.environ.get("APPDATA", "")) / "Godot" / "app_userdata" / "Krntija" / f"{name}.png"
    if not source.is_file():
        print(f"  ! снимок не нашёлся: {source}")
        return None

    shots = run_dir / "shot"
    shots.mkdir(parents=True, exist_ok=True)
    target = shots / f"{name}.png"
    shutil.copy2(source, target)
    print(f"  снимок: {target.relative_to(ROOT)}")
    return str(target.relative_to(ROOT))


# ---------------------------------------------------------------- итоги


def append_result(row: str):
    results = ART_DIR / "RESULTS.md"
    if not results.exists():
        results.write_text(
            "# Прогоны конвейера\n\n"
            "| Когда | Задание | Модель | Попыток | Время | Итог | Снимок |\n"
            "|---|---|---|---:|---:|---|---|\n",
            encoding="utf-8")

    with results.open("a", encoding="utf-8") as file:
        file.write(row + "\n")


def main() -> int:
    parser = argparse.ArgumentParser(description="Локальная LLM делает мелкую графику кодом")
    parser.add_argument("task", help="slug задания из tools/llm_art/tasks/")
    parser.add_argument("--model", default="qwen2.5-coder:7b")
    parser.add_argument("--attempts", type=int, default=5)
    parser.add_argument("--num-gpu", type=int, default=None,
                        help="слоёв модели на GPU; 0 — только процессор")
    parser.add_argument("--temperature", type=float, default=0.2)
    parser.add_argument("--num-ctx", type=int, default=8192)
    parser.add_argument("--dry-run", action="store_true", help="только собрать промпт")
    args = parser.parse_args()

    task_dir = TASKS_DIR / args.task
    task_file = task_dir / "task.md"
    if not task_file.is_file():
        print(f"нет задания: {task_file}", file=sys.stderr)
        return 2

    meta, body = parse_front_matter(task_file.read_text(encoding="utf-8"))
    writes = split_list(meta.get("writes"))
    prompt = build_prompt(meta, body)

    if meta.get("skeleton", "").strip() and len(writes) != 1:
        print("skeleton: в writes должен быть ровно один файл", file=sys.stderr)
        return 2

    if args.dry_run:
        print(prompt)
        print(f"\n--- промпт: {len(prompt)} символов, файлы: {writes}")
        return 0

    dotnet_root, godot = read_env()
    env = child_env(dotnet_root, godot.parent)
    ensure_import(godot, env)
    stamp = datetime.now().strftime("%Y%m%d-%H%M")
    run_dir = RUNS_DIR / f"{stamp}_{args.task}"
    (run_dir / "prompts").mkdir(parents=True, exist_ok=True)
    (run_dir / "answers").mkdir(parents=True, exist_ok=True)

    print(f"задание {args.task}: {len(prompt)} символов, модель {args.model}")
    started = time.time()
    feedback = ""
    written: list[str] = []
    verdict = "не собралось"
    shot = None

    for attempt in range(1, args.attempts + 1):
        print(f"\n=== попытка {attempt}/{args.attempts}")
        attempt_prompt = prompt if not feedback else (
            prompt + "\n\n## ПРОШЛАЯ ПОПЫТКА НЕ ПРОШЛА ПРОВЕРКУ\n"
            "Исправь только это и верни файлы целиком:\n```\n" + feedback + "\n```\n")

        (run_dir / "prompts" / f"{attempt}.md").write_text(attempt_prompt, encoding="utf-8")
        try:
            answer = ask_model(args.model, attempt_prompt, args.num_gpu, args.temperature,
                               args.num_ctx)
        except (urllib.error.URLError, TimeoutError) as error:
            print(f"  Ollama не ответила: {error}", file=sys.stderr)
            return 2

        (run_dir / "answers" / f"{attempt}.md").write_text(answer, encoding="utf-8")
        files = parse_answer(answer, writes)
        if not files:
            print("  модель не вернула ожидаемых файлов")
            feedback = "Ответ без файлов. Верни ровно файлы из списка, с маркерами === FILE: путь ===."
            continue

        written = write_files(files, run_dir)
        ensure_uid(written)

        build = meta.get("build", "").strip()
        if build:
            code, output = run_command(build, env)
            (run_dir / f"build_{attempt}.log").write_text(output, encoding="utf-8")
            if code != 0:
                verdict = "не собралось"
                feedback = "Сборка не прошла:\n" + error_tail(output)
                print("  сборка красная")
                continue

        check_flags = meta.get("check", "").strip()
        if check_flags:
            ok, detail = godot_check(check_flags, godot, env)
            (run_dir / f"check_{attempt}.log").write_text(detail, encoding="utf-8")
            if not ok:
                verdict = "проверка красная"
                feedback = f"Проверка {check_flags} не прошла:\n" + detail[-3000:]
                print("  проверка красная")
                continue

        verdict = "зелёное"
        shot_flags = meta.get("shot", "").strip()
        if shot_flags:
            shot = take_shot(shot_flags, godot, env, run_dir)

        break

    total = int(time.time() - started)
    summary = run_dir / "summary.md"
    summary.write_text(
        f"# {args.task}, {datetime.now():%Y-%m-%d %H:%M}\n\n"
        f"- модель: `{args.model}`, попыток: {attempt}/{args.attempts}\n"
        f"- итог: **{verdict}**, всего {total // 60} мин {total % 60} с\n"
        f"- файлы: {', '.join(f'`{path}`' for path in written) or '—'}\n"
        f"- снимок: {shot or '—'}\n", encoding="utf-8")

    append_result(f"| {datetime.now():%Y-%m-%d %H:%M} | {args.task} | {args.model} | "
                  f"{attempt} | {total // 60} мин {total % 60} с | {verdict} | {shot or '—'} |")

    print(f"\nитог: {verdict}, попыток {attempt}, {total // 60} мин {total % 60} с")
    print(f"прогон: {run_dir.relative_to(ROOT)}")
    return 0 if verdict == "зелёное" else 1


if __name__ == "__main__":
    sys.exit(main())
