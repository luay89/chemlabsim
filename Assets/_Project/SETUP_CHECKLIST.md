# Secure Reaction Pipeline Checklist

## Unity GUI actions
1. **Encrypt reactions JSON** – Open the menu `Tools ▸ Security ▸ Encrypt Reactions JSON -> bytes` after any edit to `Assets/_Project/DataSrc/reactions.json`.
2. **Bind the encrypted asset** – In the scene, select the object with `SecureReactionLoader` and assign `Assets/_Project/DataSecure/reactions.bytes` to its **Encrypted Bytes** field.
3. **Attach the smoke test** – Add `BootSmokeTest` to your bootstrap/AppRoot GameObject and drag the same `SecureReactionLoader` component into its public field.

## Console expectations
- After running the menu command you should see: `[ReactionPacker] Encrypted reactions saved: Assets/_Project/DataSecure/reactions.bytes ...`.
- Entering Play Mode should log `[BootSmokeTest] Loaded reactions count: 1` (or the current reaction total).
- If something fails, `SecureReactionLoader` emits precise `[SecureReactionLoader] ...` error messages before throwing.
- If `reactions.json` was edited but not re-encrypted, `SecureReactionLoader` warns that `reactions.bytes` is older than source.

## Data behavior notes
- Lab output now supports multi-product reactions. If `products[]` exists, all product formulas are shown (joined with `+`).
- Legacy flat fields (`product`, `reactantA`, `reactantB`) are still supported as fallback.

## Quick fixes
- **Missing Script warning** – Ensure the script lives under `Assets/_Project/Scripts/...`, then reimport (`Right-click ▸ Reimport`) or open the C# file to trigger a recompilation.
- **Tools menu not showing** – Confirm `ReactionPacker.cs` remains under `Assets/_Project/Scripts/Security/Editor/`. Unity only adds the menu once the script compiles inside an `Editor` folder; recompile or reopen the project if needed.
- **bytes file outdated/missing** – Re-run the encryption menu and reassign the regenerated `reactions.bytes` asset to `SecureReactionLoader`.
