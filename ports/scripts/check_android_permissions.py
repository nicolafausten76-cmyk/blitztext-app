"""Fail closed if the built APK acquires unexpected permissions."""
import re
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text(encoding="utf-8")
permissions = set(re.findall(r"uses-permission(?:-sdk-\d+)?: name='([^']+)'", text))
expected = {"android.permission.RECORD_AUDIO", "android.permission.INTERNET"}
if permissions != expected:
    raise SystemExit(f"Unexpected packaged permissions: {sorted(permissions)}; expected {sorted(expected)}")
print("Packaged permission allowlist passed.")
