# AGENTS.md

## Mission
Build and iterate on **Midnight Familiar** as a Unity project with small, safe, testable changes.
Prioritize clarity and maintainability over cleverness.

## Project Canon
- Core vision and design intent: `docs/GAME_VISION.md`
- Treat this as the primary source for tone, mechanics, and player fantasy.
- If implementation choices conflict with it, call that out explicitly.

## Project Context
- Engine: Unity
- Primary gameplay code location: `Assets/Scripts/`
- Current baseline script style: simple MonoBehaviour classes, clear lifecycle methods (`Start`, `Update`), and readable defaults.

## Working Rules
- Keep changes focused to the requested task.
- Do not refactor unrelated files while implementing a feature/fix.
- Do not delete or move assets unless explicitly asked.
- Prefer extending existing scripts before introducing new architecture.
- If a risky change is needed, propose it first and explain tradeoffs.

## Code Style (C# / Unity)
- Use PascalCase for classes, methods, and public members.
- Use camelCase for local variables and private fields.
- Prefer `[SerializeField] private` for inspector-exposed fields instead of public fields.
- Keep `[SerializeField] private` only for required objects and not simple variables such as numbers or text.
- Keep MonoBehaviour methods short and intention-revealing.
- Add brief comments only where behavior is non-obvious.
- Avoid magic numbers; use named constants or serialized fields.

## Unity-Specific Guidance
- Keep gameplay logic in `Assets/Scripts/`.
- Favor Unity lifecycle methods appropriately (`Awake`, `Start`, `Update`, etc.).
- Minimize per-frame work inside `Update`; use events/timers where possible.
- Null-check references that may be unassigned in scenes/prefabs.
- Preserve scene/prefab references when editing scripts.

## Validation Checklist (Before Completion)
1. Ensure scripts compile without C# errors.
2. Check Unity Console for new warnings/errors introduced by the change.
3. Verify the affected gameplay behavior in Play Mode.
4. Confirm no unintended file changes are included.

## Change Delivery Expectations
- Summarize what changed and why.
- Call out assumptions made.
- If something could not be verified locally, state that explicitly.
- Suggest clear next steps only when they add value.

## Non-Goals
- No unsolicited large-scale rewrites.
- No dependency additions unless requested.
- No style-only churn in untouched code.
