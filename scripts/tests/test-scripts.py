"""Exercise the per-OS scripts in scripts/{linux,macos,wsl,wsl2} with fake platform and build tools,
and check scripts/windows statically.

Run with: python3 scripts/tests/test-scripts.py

Covers the same-set rule across all five directories, self-containment (no per-OS script runs
another script), the OS guards, argument handling, the install/reinstall/remove semantics, the
files each script writes (the Linux app, launcher and icon; Termrig.app on macOS), run and the
display checks, and kill-termrig. Nothing real is built or installed: dotnet, uname, pgrep, pkill
and the macOS bundle tools are fakes that log their arguments, and the scripts run against a
temporary copy of the repository with a temporary HOME. The WSL scripts run with WSL_SKIP_CHECK=1.
The .bat files cannot run here, so they get static checks only.
"""
import json
import os
import plistlib
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
UNIX = ['linux', 'macos', 'wsl', 'wsl2']
SCRIPTS = ['install-tool', 'reinstall-tool', 'remove-tool', 'run', 'kill-termrig']
HELPERS = {'_wsl-common.sh', 'app-bundle.sh'}

FAKE = r'''#!/usr/bin/env python3
import json, os, pathlib, plistlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
env = os.environ
home = pathlib.Path(env['HOME'])
def log():
    with open(env['CALL_LOG'], 'a') as f: f.write(json.dumps([name, *args]) + '\n')
if name == 'uname':
    if args == ['-s']: print(env['MOCK_UNAME'])
    elif args == ['-m']: print(env.get('MOCK_ARCH', 'x86_64'))
    elif args == ['-r']: print('6.8.0-generic')
    sys.exit(0)
PKGS = {'libX11.so.6': ('libx11-6', 'libX11'), 'libICE.so.6': ('libice6', 'libICE'), 'libSM.so.6': ('libsm6', 'libSM'),
        'libXext.so.6': ('libxext6', 'libXext'), 'libXrandr.so.2': ('libxrandr2', 'libXrandr'),
        'libXi.so.6': ('libxi6', 'libXi'), 'libXcursor.so.1': ('libxcursor1', 'libXcursor'),
        'libXfixes.so.3': ('libxfixes3', 'libXfixes'), 'libfontconfig.so.1': ('libfontconfig1', 'fontconfig')}
installed_pkgs = pathlib.Path(env['HOME']) / '.fake-installed-packages'
if name == 'ldconfig':
    # The desktop libraries, minus MOCK_MISSING_LIBS that no fake apt-get/dnf has installed, as
    # ldconfig -p lists them.
    have = installed_pkgs.read_text().split() if installed_pkgs.exists() else []
    missing = [so for so in env.get('MOCK_MISSING_LIBS', '').split() if not set(PKGS.get(so, ())) & set(have)]
    arch = env.get('MOCK_LIB_ARCH', 'x86-64')
    libs = ['libX11.so.6', 'libICE.so.6', 'libSM.so.6', 'libXext.so.6', 'libXrandr.so.2', 'libXi.so.6',
            'libXcursor.so.1', 'libXfixes.so.3', 'libfontconfig.so.1', 'libc.so.6']
    print(f'{len(libs)} libs found in cache `/etc/ld.so.cache\'')
    for so in libs:
        if so not in missing: print(f'\t{so} (libc6,{arch}) => /usr/lib/{so}')
    sys.exit(0)
if name == 'pgrep':
    log()
    running = env.get('MOCK_RUNNING') or (env.get('MOCK_RUNNING_FILE') and os.path.exists(env['MOCK_RUNNING_FILE']))
    sys.exit(0 if running else 1)
log()
if name == 'sudo':
    os.execvp(args[0], args)
if name in ('apt-get', 'dnf'):
    if 'install' in args:
        if env.get('MOCK_PKG_FAIL'): sys.exit(100)
        with installed_pkgs.open('a') as f: f.write(' '.join(a for a in args if not a.startswith('-') and a != 'install') + '\n')
    sys.exit(0)
if name == 'pkill':
    if env.get('MOCK_RUNNING_FILE') and '-TERM' in args:
        pathlib.Path(env['MOCK_RUNNING_FILE']).unlink(missing_ok=True)
    sys.exit(0)
state = home / '.fake-termrig-tool'
if name == 'dotnet':
    if args[:3] == ['tool', 'list', '-g']:
        print('Package Id      Version      Commands')
        print('-------------------------------------')
        if state.exists(): print('termrig         0.1.1        trig')
        sys.exit(0)
    if args[:3] == ['tool', 'install', '-g']:
        if env.get('MOCK_FAIL_INSTALL'): sys.exit(1)
        if state.exists(): sys.exit(1)
        state.touch()
        trig = home / '.dotnet/tools/trig'
        trig.parent.mkdir(parents=True, exist_ok=True)
        trig.write_text('#!/bin/sh\nexit 0\n'); trig.chmod(0o755)
        sys.exit(0)
    if args[:3] == ['tool', 'uninstall', '-g']:
        if not state.exists(): sys.exit(1)
        state.unlink(); (home / '.dotnet/tools/trig').unlink(missing_ok=True)
        sys.exit(0)
    if args[:1] == ['pack']:
        out = pathlib.Path(args[args.index('--output') + 1])
        (out / 'termrig.0.1.1.nupkg').write_text('package')
        sys.exit(0)
    if args[:1] == ['publish']:
        if env.get('MOCK_FAIL_PUBLISH'): sys.exit(1)
        out = pathlib.Path(args[args.index('--output') + 1])
        out.mkdir(parents=True, exist_ok=True)
        app = out / 'Termrig'
        app.write_text('#!/usr/bin/env python3\nimport json, os\n'
                       'open(os.environ["CALL_LOG"], "a").write(json.dumps(["Termrig-app", os.environ.get("PATH"), '
                       'os.environ.get("DOTNET_ROOT")]) + "\\n")\n')
        app.chmod(0o755)
        (out / 'Termrig.dll').write_text('managed')
        sys.exit(0)
if name == 'sips': pathlib.Path(args[args.index('--out') + 1]).write_text('png')
if name == 'iconutil': pathlib.Path(args[args.index('--output') + 1]).write_text('icns')
if name == 'plutil':
    assert args[0] == '-replace' and args[2] == '-string', args
    path = pathlib.Path(args[4])
    data = plistlib.loads(path.read_bytes()); data[args[1]] = args[3]
    path.write_bytes(plistlib.dumps(data))
if name == 'codesign' and env.get('MOCK_FAIL_SIGN'): sys.exit(8)
'''

checks = 0


def check(condition, *detail):
    global checks
    assert condition, detail
    checks += 1


# ---- Static checks on the real checkout ----

names = {}
for p in UNIX:
    names[p] = sorted(f.stem for f in (root / 'scripts' / p).glob('*.sh') if f.name not in HELPERS)
    check(names[p] == sorted(SCRIPTS), p, names[p])
windows = sorted(f.stem for f in (root / 'scripts/windows').glob('*.bat'))
check(windows == sorted(SCRIPTS), 'windows', windows)
check(sorted(f.name for f in (root / 'scripts/windows').iterdir()) == sorted(s + '.bat' for s in SCRIPTS))
template = (root / 'scripts/wsl/_wsl-common.sh').read_bytes()
check(template == (root / 'scripts/wsl2/_wsl-common.sh').read_bytes(), 'the two _wsl-common.sh copies differ')

# Self-contained: no per-OS script runs another script (sourcing the function libraries is fine).
for p in UNIX:
    for f in (root / 'scripts' / p).glob('*.sh'):
        text = f.read_text()
        check(text.startswith('#!/usr/bin/env bash\n') or f.name == '_wsl-common.sh', f)
        check('\r' not in text, f, 'CRLF')
        check(os.access(f, os.X_OK), f, 'not executable')
        if f.name in HELPERS:
            continue
        check('set -euo pipefail' in text and 'die() {' in text, f)
        code = '\n'.join(l for l in text.splitlines() if not l.lstrip().startswith('#'))
        for pattern in [r'\.\./(linux|macos|wsl|wsl2|windows)/', r'build-app', r'wsl_run\b',
                        r'\$REPO_ROOT"?/[\w-]+\.sh', r'\bexec (sh|bash) ']:
            check(not re.search(pattern, code), f, pattern)
        sources = re.findall(r'^\s*\. "([^"]+)"', text, re.M)
        check(all(s in ('$SCRIPT_DIR/_wsl-common.sh', '$SCRIPT_DIR/app-bundle.sh') for s in sources), f, sources)
        if p.startswith('wsl'):
            for line in ['WSL_FLAVOR=' + p, '. "$SCRIPT_DIR/_wsl-common.sh"', 'wsl_require\n',
                         'wsl_sanitize_path\n', 'wsl_check_repo "$REPO_ROOT"']:
                check(line in text, f, line)
            check(not re.search(r'^command -v \w+ ', text, re.M), f, 'use wsl_require_cmd on WSL')

for f in (root / 'scripts/windows').glob('*.bat'):
    raw = f.read_bytes()
    check(b'\r\n' in raw and b'\n' not in raw.replace(b'\r\n', b''), f, 'not CRLF')
    lines = raw.decode().split('\r\n')
    code = [l for l in lines if not l.strip().upper().startswith('REM ')]
    check(lines[0] == '@echo off' and 'setlocal' in lines, f)
    check(any(l.startswith('endlocal & exit /b 0') for l in lines), f)
    check(':fail' in lines and 'endlocal & exit /b 1' in lines, f)
    for flag in ['-h', '--help']:
        check(f'if /i "%~1"=="{flag}" goto :usage' in lines, f, flag)
    check(any('Unexpected argument' in l for l in lines), f)
    body = '\r\n'.join(code)
    check(not re.search(r'\bcall "', body), f, 'calls another script')
    check('build-app' not in body and '-File' not in body and '..\\..\\' not in body.replace('"%~dp0..\\..\\"', '') .replace('%~dp0..\\..', ''), f)
    # Parentheses inside a block must be escaped.
    depth = 0
    for l in code:
        s = l.strip()
        if depth > 0 and s.lower().startswith('echo'):
            check(not re.search(r'(?<!\^)[()]', s), f, 'unescaped paren in block', l)
        depth += s.endswith('(') - (s == ')' or s.startswith(') else'))
    check(depth == 0, f, 'unbalanced blocks')
    if f.stem in ['install-tool', 'reinstall-tool', 'remove-tool', 'run']:
        check('kill-termrig.bat' in body and 'tasklist /FI "IMAGENAME eq trig.exe"' in body, f)
        check('tasklist /FI "IMAGENAME eq Termrig.exe"' in body, f)
    for l in code:
        if re.match(r'\s*(tasklist|taskkill)\b', l.strip()):
            check('USERNAME eq %USERDOMAIN%\\%USERNAME%' in l, f, 'process check not limited to this user', l)
    if f.stem in ['install-tool', 'reinstall-tool', 'remove-tool', 'run']:
        check('if /i "%TERM_PROGRAM%"=="Termrig" goto :running_here' in body and 'run this from another terminal' in body, f)
    if 'for /f "tokens=2 delims=' in body:
        check('delims=<>\t ' in body, f, 'tab-indented <Version> is not parsed')
    if f.stem == 'kill-termrig':
        check('taskkill.exe /T /PID' in body and 'taskkill.exe /F /T /PID' in body and 'Termrig\\.dll' in body
              and 'GetOwner' in body, f)
    if f.stem == 'install-tool':
        check('reinstall-tool.bat to replace it' in body, f)
    if f.stem in ['install-tool', 'reinstall-tool']:
        check('dotnet tool install -g Termrig --version %VERSION% --source "%PACKAGE_SOURCE%" --no-http-cache' in body, f)
    if f.stem in ['install-tool', 'reinstall-tool', 'run']:
        check('--runtime %RID% --self-contained true' in body and 'CreateShortcut' in body, f)
    if f.stem == 'remove-tool':
        check('dotnet tool uninstall -g Termrig' in body and 'DESKTOP_LINK' in body and 'START_MENU_LINK' in body, f)
        check('nothing to remove' in body, f)

for path in root.glob('scripts/**/*.sh'):
    subprocess.run(['bash', '-n', str(path)], check=True)
    checks += 1
shellcheck = os.environ.get('SHELLCHECK') or shutil.which('shellcheck')
if shellcheck:
    for p in UNIX:
        d = root / 'scripts' / p
        subprocess.run([shellcheck, '-x', '-P', 'SCRIPTDIR', '-S', 'warning', *sorted(x.name for x in d.glob('*.sh'))],
                       cwd=d, check=True)
        checks += 1

# ---- Behavior, against a temporary copy ----

with tempfile.TemporaryDirectory(prefix='termrig scripts ') as temp:
    temp = Path(temp)
    repo = temp / 'repo'
    shutil.copytree(root / 'scripts', repo / 'scripts', ignore=shutil.ignore_patterns('__pycache__'))
    shutil.copytree(root / 'packaging', repo / 'packaging')
    shutil.copy(root / 'go.sh', repo / 'go.sh')
    project = repo / 'src/Termrig.App/Termrig.App.csproj'
    project.parent.mkdir(parents=True)
    shutil.copy(root / 'src/Termrig.App/Termrig.App.csproj', project)
    bin_dir = temp / 'bin'
    bin_dir.mkdir()
    for n in ['dotnet', 'uname', 'pgrep', 'pkill', 'ldconfig', 'sips', 'iconutil', 'plutil', 'codesign', 'open', 'lsregister']:
        (bin_dir / n).write_text(FAKE)
        (bin_dir / n).chmod(0o755)
    no_dotnet = temp / 'bin-no-dotnet'
    shutil.copytree(bin_dir, no_dotnet)
    (no_dotnet / 'dotnet').unlink()
    # Package managers: bin has apt-get; bin-dnf has dnf instead; bin-no-pkg has neither.
    bin_dnf = temp / 'bin-dnf'
    shutil.copytree(bin_dir, bin_dnf)
    (bin_dnf / 'dnf').write_text(FAKE)
    (bin_dnf / 'dnf').chmod(0o755)
    bin_no_pkg = temp / 'bin-no-pkg'
    shutil.copytree(bin_dir, bin_no_pkg)
    for n in ['apt-get', 'sudo']:
        (bin_dir / n).write_text(FAKE)
        (bin_dir / n).chmod(0o755)
    shutil.copy(bin_dir / 'sudo', bin_dnf / 'sudo')
    log = temp / 'calls.jsonl'
    home = temp / 'home'
    system_apps = temp / 'Applications'
    base_env = {k: v for k, v in os.environ.items()
                if k not in ('DISPLAY', 'WAYLAND_DISPLAY', 'DOTNET_ROOT', 'XDG_DATA_HOME', 'NUGET_PACKAGES', 'TERM_PROGRAM')}
    base_env.update(PATH=f'{bin_dir}:/usr/bin:/bin', CALL_LOG=str(log), HOME=str(home), WSL_SKIP_CHECK='1',
                    DISPLAY=':0', WSL_DISTRO_NAME='Test', TERMRIG_SYSTEM_APPLICATIONS=str(system_apps),
                    TERMRIG_LSREGISTER=str(bin_dir / 'lsregister'))

    def reset():
        shutil.rmtree(home, ignore_errors=True)
        home.mkdir()
        shutil.rmtree(system_apps, ignore_errors=True)
        system_apps.mkdir()
        shutil.rmtree(repo / 'artifacts', ignore_errors=True)

    pgreps = []

    def run(platform, script, *args, unset=(), **env):
        log.unlink(missing_ok=True)
        e = dict(base_env, MOCK_UNAME='Darwin' if platform == 'macos' else 'Linux')
        e.update(env)
        for k in unset:
            e.pop(k, None)
        target = repo / script if script.endswith('.sh') else repo / f'scripts/{platform}/{script}.sh'
        p = subprocess.run(['bash', str(target), *args], env=e, capture_output=True, text=True)
        calls = [json.loads(x) for x in log.read_text().splitlines()] if log.exists() else []
        pgreps[:] = [c for c in calls if c[0] == 'pgrep']
        # Every process check is limited to this user's processes.
        for c in calls:
            if c[0] in ('pgrep', 'pkill'):
                check(c[c.index('-u') + 1] == str(os.getuid()), 'process check without -u', c)
        return p, [c for c in calls if c[0] != 'pgrep']

    def dotnet(calls, *prefix):
        return [c for c in calls if c[0] == 'dotnet' and c[1:1 + len(prefix)] == list(prefix)]

    def linux_paths():
        data = home / '.local/share'
        return (data / 'termrig', data / 'applications/com.jchristn.Termrig.desktop',
                data / 'icons/hicolor/scalable/apps/com.jchristn.Termrig.svg')

    # OS guards.
    for platform, wrong in [('linux', 'Darwin'), ('macos', 'Linux')]:
        for script in SCRIPTS:
            p, calls = run(platform, script, MOCK_UNAME=wrong)
            check(p.returncode != 0 and 'This script is for' in p.stderr and not dotnet(calls), platform, script, p.stderr)
    for platform in ['wsl', 'wsl2']:
        for script in SCRIPTS:
            p, calls = run(platform, script, unset=['WSL_SKIP_CHECK'])
            check(p.returncode != 0 and 'This script is for WSL' in p.stderr and not calls, platform, script, p.stderr)

    # Argument handling.
    reset()
    for platform in UNIX:
        for script in SCRIPTS:
            for flag in ['-h', '--help']:
                p, calls = run(platform, script, flag)
                check(p.returncode == 0 and p.stdout.startswith(f'Usage: ./scripts/{platform}/{script}.sh') and not calls,
                      platform, script, flag, p.stdout, p.stderr)
            p, calls = run(platform, script, 'extra')
            check(p.returncode != 0 and 'Unexpected argument: extra' in p.stderr and not calls, platform, script, p.stderr)
            # An exported CDPATH containing '.' must not break (or print from) the cd that finds the repo.
            p, calls = run(platform, script, '--help', CDPATH='.:' + str(temp))
            check(p.returncode == 0 and p.stdout.startswith(f'Usage: ./scripts/{platform}/{script}.sh'), platform, script, p.stdout, p.stderr)
            # --help and argument errors touch nothing (on WSL, not even ~/.dotnet/tools).
            check(not (home / '.dotnet').exists(), platform, script, 'help created ~/.dotnet')

    # install / reinstall / remove.
    for platform in UNIX:
        arch_env = {'MOCK_ARCH': 'arm64'} if platform == 'macos' else {}
        rid = 'osx-arm64' if platform == 'macos' else 'linux-x64'
        reset()
        cache = home / '.nuget/packages/termrig/0.1.1'
        cache.mkdir(parents=True)
        p, calls = run(platform, 'install-tool', **arch_env)
        check(p.returncode == 0, platform, p.stdout, p.stderr)
        check(not cache.exists(), platform, 'stale NuGet cache was kept')
        steps = [c[1] if c[1] != 'tool' else c[1] + ' ' + c[2] for c in dotnet(calls)]
        check([s for s in steps if s != 'tool list'] == ['pack', 'publish', 'tool install'], platform, steps)
        pack, = dotnet(calls, 'pack')
        check(pack[2] == str(project) and pack[pack.index('--output') + 1] == str(repo / 'artifacts/tools'), pack)
        install, = dotnet(calls, 'tool', 'install')
        check(install == ['dotnet', 'tool', 'install', '-g', 'Termrig', '--version', '0.1.1', '--source',
                          str(repo / 'artifacts/tools'), '--no-http-cache'], install)
        publish, = dotnet(calls, 'publish')
        check(publish[publish.index('--runtime') + 1] == rid and '--self-contained' in publish
              and '-p:PackAsTool=false' in publish, publish)
        check((home / '.dotnet/tools/trig').exists())
        check('Note: ' + str(home) + '/.dotnet/tools is not on your PATH' in p.stderr, p.stderr)
        if platform == 'macos':
            app = system_apps / 'Termrig.app'
            with (app / 'Contents/Info.plist').open('rb') as fh:
                plist = plistlib.load(fh)
            check(plist['CFBundleExecutable'] == 'Termrig' and plist['CFBundleVersion'] == '0.1.1', plist)
            check((app / 'Contents/MacOS/Termrig').is_file() and (app / 'Contents/Resources/Termrig.icns').is_file())
            check(['lsregister', '-f', str(app)] in calls, calls)
            check(any(c[0] == 'codesign' for c in calls))
        else:
            app_dir, desktop, icon = linux_paths()
            check((app_dir / 'Termrig').is_file() and os.access(app_dir / 'Termrig', os.X_OK) and icon.is_file())
            entry = desktop.read_text()
            check(f'Exec=env TERMRIG_FOREGROUND=1 "{app_dir}/Termrig"\n' in entry and '\r' not in entry, entry)
            check('Name=Termrig' in entry and 'Icon=com.jchristn.Termrig' in entry, entry)
        # Already installed: fails before building and points at reinstall.
        p, calls = run(platform, 'install-tool', **arch_env)
        check(p.returncode != 0 and f'scripts/{platform}/reinstall-tool.sh' in p.stderr and not dotnet(calls, 'pack'),
              platform, p.stderr)
        # Running: install, reinstall and remove all refuse and point at kill-termrig.
        for script in ['install-tool', 'reinstall-tool', 'remove-tool', 'run']:
            p, calls = run(platform, script, MOCK_RUNNING='1', **arch_env)
            check(p.returncode != 0 and f'scripts/{platform}/kill-termrig.sh' in p.stderr
                  and not [c for c in dotnet(calls) if c[1:3] != ['tool', 'list']], platform, script, p.stderr)
        # From a Termrig tab: still refuses, but points at another terminal instead of kill-termrig.
        for script in ['install-tool', 'reinstall-tool', 'remove-tool', 'run']:
            p, calls = run(platform, script, MOCK_RUNNING='1', TERM_PROGRAM='Termrig', **arch_env)
            check(p.returncode != 0 and 'run this from another terminal' in p.stderr and 'kill-termrig' not in p.stderr,
                  platform, script, p.stderr)
        # Not running: the checks ran (and were limited to this user, asserted in run()).
        p, calls = run(platform, 'remove-tool', TERM_PROGRAM='Termrig')
        check(p.returncode == 0 and len(pgreps) == 3, platform, pgreps)
        p, calls = run(platform, 'install-tool', **arch_env)
        check(p.returncode == 0, platform, p.stderr)
        # Reinstall over an install: builds first, then uninstalls, then installs.
        p, calls = run(platform, 'reinstall-tool', **arch_env)
        check(p.returncode == 0, platform, p.stderr)
        steps = [c[1] if c[1] != 'tool' else c[1] + ' ' + c[2] for c in dotnet(calls) if c[1:3] != ['tool', 'list']]
        check(steps == ['pack', 'publish', 'tool uninstall', 'tool install'], platform, steps)
        # A failed build leaves the install alone.
        p, calls = run(platform, 'reinstall-tool', MOCK_FAIL_PUBLISH='1', **arch_env)
        check(p.returncode != 0 and not dotnet(calls, 'tool', 'uninstall') and (home / '.fake-termrig-tool').exists(),
              platform, p.stderr)
        # Remove deletes everything install created.
        p, calls = run(platform, 'remove-tool')
        check(p.returncode == 0 and dotnet(calls, 'tool', 'uninstall'), platform, p.stderr)
        if platform == 'macos':
            check(not (system_apps / 'Termrig.app').exists() and ['lsregister', '-u', str(system_apps / 'Termrig.app')] in calls)
        else:
            check(not any(x.exists() for x in linux_paths()), platform)
        check(not (home / '.fake-termrig-tool').exists() and 'nothing to remove' not in p.stdout)
        p, calls = run(platform, 'remove-tool')
        check(p.returncode == 0 and 'nothing to remove' in p.stdout and not dotnet(calls, 'tool', 'uninstall'), platform, p.stdout)
        # Reinstall when nothing is installed.
        p, calls = run(platform, 'reinstall-tool', **arch_env)
        check(p.returncode == 0 and 'not installed; continuing' in p.stdout and not dotnet(calls, 'tool', 'uninstall'), platform)
        # An app left without the tool still counts as installed for install, and remove cleans it up.
        run(platform, 'remove-tool')
        run(platform, 'run', **arch_env)
        p, calls = run(platform, 'install-tool', **arch_env)
        check(p.returncode != 0 and 'The Termrig app is already installed' in p.stderr, platform, p.stderr)
        p, calls = run(platform, 'remove-tool')
        check(p.returncode == 0 and 'Removed' in p.stdout and not dotnet(calls, 'tool', 'uninstall'), platform, p.stdout)
        # Missing dotnet.
        if not shutil.which('dotnet', path='/usr/bin:/bin'):
            p, calls = run(platform, 'install-tool', PATH=f'{no_dotnet}:/usr/bin:/bin')
            check(p.returncode != 0 and re.search(r"'?dotnet'? was not found", p.stderr) and not calls, platform, p.stderr)

    # run: refreshes only the app, then opens the installed copy.
    for platform in ['linux', 'wsl', 'wsl2']:
        reset()
        p, calls = run(platform, 'run')
        check(p.returncode == 0, platform, p.stderr)
        check(dotnet(calls, 'publish') and not dotnet(calls, 'tool', 'install') and not dotnet(calls, 'pack'), calls)
        check(calls[-1][0] == 'Termrig-app', platform, calls)
        if platform.startswith('wsl'):
            # Termrig gets the PATH from before wsl_sanitize_path (with the Windows entries) and no
            # DOTNET_ROOT added by the helper.
            win_path = f'{bin_dir}:/usr/bin:/bin:/mnt/c/Windows/System32'
            reset()
            p, calls = run(platform, 'run', PATH=win_path)
            check(p.returncode == 0 and calls[-1] == ['Termrig-app', win_path, None], platform, calls[-1:], p.stderr)
        check(linux_paths()[1].exists())
        # No display: fails before building.
        p, calls = run(platform, 'run', unset=['DISPLAY'])
        check(p.returncode != 0 and not calls, platform, p.stderr)
        # Install still works without a display, with a note.
        reset()
        p, calls = run(platform, 'install-tool', unset=['DISPLAY'])
        check(p.returncode == 0 and 'Note:' in p.stderr and ('WSLg' in p.stderr if platform == 'wsl2' else True), platform, p.stderr)
    # Missing desktop libraries (libICE.so.6 and friends): install, reinstall and run install them with
    # sudo apt-get (or dnf) before building, after the running check, then carry on.
    def pkg_calls(calls):
        return [c for c in calls if c[0] in ('sudo', 'apt-get', 'dnf')]
    apt_env = ['sudo', 'env', 'DEBIAN_FRONTEND=noninteractive', 'NEEDRESTART_MODE=a', 'NEEDRESTART_SUSPEND=1', 'apt-get',
               '-o', 'DPkg::Lock::Timeout=300']
    apt = apt_env + ['-o', 'Dpkg::Options::=--force-confdef', '-o', 'Dpkg::Options::=--force-confold']
    system_pm = shutil.which('apt-get', path='/usr/bin:/bin') or shutil.which('dnf', path='/usr/bin:/bin')
    for platform in ['linux', 'wsl', 'wsl2']:
        reset()
        p, calls = run(platform, 'run', MOCK_MISSING_LIBS='libICE.so.6 libSM.so.6')
        check(p.returncode == 0 and 'Installed the desktop libraries.' in p.stdout and calls[-1][0] == 'Termrig-app',
              platform, p.stdout, p.stderr)
        check([c for c in calls if c[0] == 'sudo'] == [['sudo', '-v'], apt_env + ['update'], apt + ['install', '-y', 'libice6', 'libsm6']],
              platform, calls)
        check(calls.index(apt + ['install', '-y', 'libice6', 'libsm6']) < calls.index(dotnet(calls, 'publish')[0]), calls)
        for script, pkg, so in [('install-tool', 'libfontconfig1', 'libfontconfig.so.1'),
                                ('reinstall-tool', 'libxi6', 'libXi.so.6')]:
            reset()
            p, calls = run(platform, script, MOCK_MISSING_LIBS=so)
            check(p.returncode == 0 and apt + ['install', '-y', pkg] in calls and 'Warning:' not in p.stderr
                  and calls.index(apt + ['install', '-y', pkg]) < calls.index(dotnet(calls, 'pack')[0]),
                  platform, script, p.stderr)
        # Nothing missing: no package manager, no sudo.
        p, calls = run(platform, 'reinstall-tool')
        check(p.returncode == 0 and not pkg_calls(calls) and 'shared libraries' not in p.stderr, platform, p.stderr)
        # Running: refuses before installing anything.
        p, calls = run(platform, 'install-tool', MOCK_RUNNING='1', MOCK_MISSING_LIBS='libICE.so.6')
        check(p.returncode != 0 and not pkg_calls(calls), platform, calls)
        # The install fails: run stops before building, install warns and carries on, both with the command.
        reset()
        p, calls = run(platform, 'run', MOCK_MISSING_LIBS='libICE.so.6 libSM.so.6', MOCK_PKG_FAIL='1')
        check(p.returncode != 0 and 'libICE.so.6 libSM.so.6' in p.stderr and 'sudo apt install libice6 libsm6' in p.stderr
              and 'INSTALLING_ON_LINUX.md' in p.stderr and not dotnet(calls), platform, p.stderr)
        p, calls = run(platform, 'install-tool', MOCK_MISSING_LIBS='libICE.so.6', MOCK_PKG_FAIL='1')
        check(p.returncode == 0 and 'Warning: Termrig will be installed' in p.stderr and linux_paths()[0].exists(),
              platform, p.stderr)
        # TERMRIG_NO_DEPS=1: report only.
        reset()
        p, calls = run(platform, 'run', MOCK_MISSING_LIBS='libICE.so.6', TERMRIG_NO_DEPS='1')
        check(p.returncode != 0 and not pkg_calls(calls) and 'sudo apt install libice6' in p.stderr, platform, p.stderr)
        if not system_pm:
            # dnf when there is no apt-get.
            p, calls = run(platform, 'run', MOCK_MISSING_LIBS='libICE.so.6', PATH=f'{bin_dnf}:/usr/bin:/bin')
            check(p.returncode == 0 and ['sudo', 'dnf', 'install', '-y', 'libICE'] in calls, platform, calls, p.stderr)
            # No package manager (and no sudo): both commands, for the user to run.
            reset()
            p, calls = run(platform, 'run', MOCK_MISSING_LIBS='libICE.so.6', PATH=f'{bin_no_pkg}:/usr/bin:/bin')
            check(p.returncode != 0 and 'sudo apt install libice6' in p.stderr and 'sudo dnf install libICE' in p.stderr
                  and not dotnet(calls), platform, p.stderr)
        # Only libraries for this architecture count, so x86-64 copies don't satisfy an ARM64 machine.
        reset()
        p, calls = run(platform, 'run', MOCK_ARCH='arm64')
        check(p.returncode != 0 and 'libX11.so.6' in p.stderr, platform, p.stderr)
        p, calls = run(platform, 'run', MOCK_ARCH='arm64', MOCK_LIB_ARCH='AArch64')
        check(p.returncode == 0 and not pkg_calls(calls), platform, p.stderr)
        # No ldconfig at all: the check is skipped rather than guessed.
        p, calls = run(platform, 'run', TERMRIG_LDCONFIG=str(temp / 'no-such-ldconfig'), MOCK_MISSING_LIBS='libICE.so.6')
        check(p.returncode == 0 and not pkg_calls(calls), platform, p.stderr)
    # The launcher's Exec line follows the Desktop Entry quoting rules for unusual paths.
    for platform in ['linux', 'wsl', 'wsl2']:
        reset()
        data = temp / 'data $HOME "q" `b` 100%'
        p, calls = run(platform, 'install-tool', XDG_DATA_HOME=str(data))
        check(p.returncode == 0, platform, p.stderr)
        exe = str(data / 'termrig/Termrig')
        quoted = ''.join('\\' + ch if ch in '"`$\\' else ch for ch in exe).replace('\\', '\\\\').replace('%', '%%')
        entry = (data / 'applications/com.jchristn.Termrig.desktop').read_text()
        check(f'Exec=env TERMRIG_FOREGROUND=1 "{quoted}"\n' in entry, platform, entry, quoted)
        run(platform, 'remove-tool', XDG_DATA_HOME=str(data))
        check(not (data / 'termrig').exists() and not (data / 'applications/com.jchristn.Termrig.desktop').exists())
        shutil.rmtree(data, ignore_errors=True)

    # WSL 2 and Linux accept Wayland; WSL 1 needs DISPLAY (an X server).
    for platform, ok in [('linux', True), ('wsl2', True), ('wsl', False)]:
        reset()
        p, calls = run(platform, 'run', unset=['DISPLAY'], WAYLAND_DISPLAY='wayland-0')
        check((p.returncode == 0) == ok, platform, p.stderr)
        if not ok:
            check('X server' in p.stderr and 'scripts\\windows\\run.bat' in p.stderr and not calls, p.stderr)

    # macOS run, go.sh, both architectures, stale-copy cleanup, ~/Applications fallback, signing failure.
    for script in ['scripts/macos/run.sh', 'go.sh']:
        for arch, rid in [('arm64', 'osx-arm64'), ('x86_64', 'osx-x64')]:
            reset()
            stale = home / 'Applications/Termrig.app'
            stale.mkdir(parents=True)
            p, calls = run('macos', script, MOCK_ARCH=arch)
            check(p.returncode == 0, script, p.stderr)
            publish, = dotnet(calls, 'publish')
            check(publish[publish.index('--runtime') + 1] == rid, publish)
            check(not stale.exists() and 'Removed stale copy' in p.stdout, p.stdout)
            check([c for c in calls if c[0] == 'open'] == [['open', '-a', str(system_apps / 'Termrig.app')]], calls)
            check(not dotnet(calls, 'tool', 'install'))
    marker = system_apps / 'Termrig.app/keep-me'
    marker.touch()
    p, calls = run('macos', 'run', MOCK_FAIL_SIGN='1')
    check(p.returncode != 0 and marker.exists() and 'left unchanged' in p.stderr, p.stderr)
    reset()
    system_apps.chmod(0o555)
    try:
        p, calls = run('macos', 'install-tool')
        check(p.returncode == 0 and (home / 'Applications/Termrig.app/Contents/MacOS/Termrig').is_file(), p.stderr)
        p, calls = run('macos', 'remove-tool')
        check(p.returncode == 0 and not (home / 'Applications/Termrig.app').exists(), p.stderr)
    finally:
        system_apps.chmod(0o755)

    # kill-termrig.
    for platform in UNIX:
        p, calls = run(platform, 'kill-termrig')
        check(p.returncode == 0 and 'No Termrig processes are running' in p.stdout and not calls, platform, p.stdout)
        flag = temp / 'running'
        flag.touch()
        p, calls = run(platform, 'kill-termrig', MOCK_RUNNING_FILE=str(flag))
        check(p.returncode == 0 and p.stdout.endswith('Done.\n') and not flag.exists(), platform, p.stderr)
        uid = str(os.getuid())
        check(['pkill', '-TERM', '-u', uid, '-x', 'trig'] in calls and ['pkill', '-TERM', '-u', uid, '-x', 'Termrig'] in calls, calls)
        p, calls = run(platform, 'kill-termrig', MOCK_RUNNING='1')
        check(p.returncode != 0 and ['pkill', '-KILL', '-u', uid, '-x', 'Termrig'] in calls, platform, p.stderr)

print(f'{checks} checks passed: same script set on all five platforms, self-containment, OS guards, '
      'arguments, install/reinstall/remove, app files, run, display checks, desktop-library install, kill-termrig, '
      'Windows batch structure and shell syntax.')
