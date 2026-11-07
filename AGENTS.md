# Graphify

For codebase exploration, use the local knowledge graph when it helps locate related code:
`.graphify-venv/bin/graphify query "<question>" --budget 1000` (or `explain` / `path`).
Verify findings in the source; use normal search when the graph is incomplete or stale.
After code changes, refresh it with `.graphify-venv/bin/graphify update .`.
For the installed Graphify skill, prepend `.graphify-venv/bin` to `PATH`.

The environment and graph are local and ignored by Git. On macOS/Linux, recreate them with
`uv venv .graphify-venv`, `uv pip install --python .graphify-venv/bin/python graphifyy==0.9.103`,
then `.graphify-venv/bin/graphify extract . --code-only`. This indexes code locally,
without a semantic pass over documents or an API key. On Windows, use the corresponding
executables in `.graphify-venv/Scripts/`.
