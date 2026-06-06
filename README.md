# App manifest channel

This branch serves `third-party-apps.json` to the Apps2Samsung desktop app at runtime
(`https://raw.githubusercontent.com/Apps2Samsung/Apps2Samsung/main/third-party-apps.json`).

Editing the file here updates the app catalog for **all installed versions** on their
next launch — no release needed. Keep it valid JSON (`python3 -m json.tool third-party-apps.json`).

The bundled fallback copy lives on `beta` at `Jellyfin2Samsung-CrossOS/Assets/third-party-apps.json`
— keep both in sync.
