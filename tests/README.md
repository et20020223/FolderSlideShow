# Tests

Run on 64-bit Windows from the repository root.

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Prepare.ps1`: requires FFmpeg on PATH. Generates synthetic photos and video, then runs 56 scanner and playback-order checks. Junction checks are skipped explicitly if creation is unavailable.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\TestWebp.ps1`: requires Python and Pillow. Generates synthetic WebP fixtures and runs 23 decoder and animation checks.

Generated fixtures and test binaries are excluded by .gitignore.
