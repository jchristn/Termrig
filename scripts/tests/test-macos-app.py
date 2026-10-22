"""Check every macOS source entry point with an isolated home and fake tools."""
import json
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
with tempfile.TemporaryDirectory(prefix='termrig macos ') as temp:
    temp = Path(temp)
    repo = temp / 'repo'
    shutil.copytree(root / 'scripts', repo / 'scripts')
    shutil.copytree(root / 'packaging', repo / 'packaging')
    for name in ['go.sh', 'install-tool.sh', 'reinstall-tool.sh']:
        shutil.copy(root / name, repo / name)
    project = repo / 'src/Termrig.App/Termrig.App.csproj'
    project.parent.mkdir(parents=True)
    shutil.copy(root / project.relative_to(repo), project)
    bin_dir = temp / 'bin'
    bin_dir.mkdir()
    log = temp / 'calls'
    fake = '''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ['CALL_LOG'], 'a') as f: f.write(json.dumps([name, *args]) + '\\n')
if name == 'uname': print('Darwin' if args == ['-s'] else os.environ.get('TEST_ARCH', 'arm64'))
if name == 'dotnet' and args == ['tool', 'list', '--global']: print('termrig 0.1.1 trig')
if name == 'dotnet' and args[0] == 'publish':
    out = pathlib.Path(args[args.index('--output') + 1])
    out.mkdir(parents=True, exist_ok=True)
    (out / 'Termrig').write_text('executable')
if name == 'iconutil': pathlib.Path(args[args.index('--output') + 1]).touch()
if name == 'codesign' and os.environ.get('FAIL_SIGN'): sys.exit(8)
'''
    for name in ['uname', 'dotnet', 'sips', 'iconutil', 'codesign', 'open']:
        path = bin_dir / name
        path.write_text(fake)
        path.chmod(0o755)
    env = dict(os.environ, HOME=str(temp / 'home'), PATH=f'{bin_dir}:{os.environ["PATH"]}', CALL_LOG=str(log))
    for script in ['go.sh', 'scripts/macos/run.sh', 'scripts/macos/install-tool.sh', 'scripts/macos/reinstall-tool.sh']:
        for arch in ['arm64', 'x86_64']:
            log.unlink(missing_ok=True)
            p = subprocess.run(['sh', str(repo / script)], env=dict(env, TEST_ARCH=arch), capture_output=True, text=True)
            assert p.returncode == 0, p.stderr
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            publish, = [c for c in calls if c[:2] == ['dotnet', 'publish']]
            assert publish[publish.index('--runtime') + 1] == ('osx-arm64' if arch == 'arm64' else 'osx-x64')
            app = temp / 'home/Applications/Termrig.app'
            with (app / 'Contents/Info.plist').open('rb') as f:
                assert plistlib.load(f)['CFBundleExecutable'] == 'Termrig'
            assert (app / 'Contents/MacOS/Termrig').is_file()
            opens = [c for c in calls if c[0] == 'open']
            assert len(opens) == int(script in ['go.sh', 'scripts/macos/run.sh'])
            if opens:
                assert opens[0] == ['open', '-a', str(app)]
    marker = app / 'keep-me'
    marker.touch()
    p = subprocess.run(['sh', str(repo / 'scripts/macos/run.sh')], env=dict(env, FAIL_SIGN='1'), capture_output=True)
    assert p.returncode != 0 and marker.exists()
    print('Termrig macOS install/reinstall/run, both architectures, and signing failure preservation passed.')
