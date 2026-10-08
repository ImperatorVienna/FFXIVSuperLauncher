# Soil DCTravel protocol source

Copied from `src/XIVLauncher.DCTravel` in the supplied Soil checkout at commit
`4c3ba518ce6985ed808283d2e92bd5ed14fa3c4e` (same repository license).

Only protocol client and models are included. No Windows project, WPF UI, EmbedIO listener, or plugin RPC server is restored.

Local adaptations: injectable HTTP transport for offline tests; remove raw URL/response debug logging that could expose tickets; do not automatically retry mutation endpoints after ambiguous failures. Native Avalonia UI, account lifecycle and cancellation integration are outside this directory. The existing Soil history parser intentionally lists completed, returnable trips.

2026-10-08 cleanup: removed unused plugin RPC attributes/callbacks, queue-time endpoint and unused public maintenance/logout wrappers. The launcher-owned travel workflow and session refresh binding remain.
