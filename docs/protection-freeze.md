# Protection-list freeze investigation, 2026-10-27

The affected session logged 15 Steward update stalls of 5,567–6,101 ms between 05:32:38 and 05:40:53 UTC. The crash handler recorded process termination at 05:41:37 without an actionable crash stack. These logs predate installation of the 0.1.0.39 / 4.6.3.13 releases.

The saved default protection list contains item 4868 (Gysahl Greens). The selected Steward cleanup plan inherits default protections and contains 22,766 hard-list candidate IDs. The previous managed-plan optimization returned the original plan only while default protection was empty. Adding the first default protection therefore restored `DSFClone` on every effective-settings read. Steward protection queries and disposal previews repeatedly exercise this path on the game thread. This matches the reported trigger and logged update stalls; the logs do not contain a sampled stack proving every stalled millisecond came from that method.

The fix applies only to managed plans inheriting protection alone. It combines current local/default protection without serializing candidate lists; other lists retain the same read-through references as the pre-existing empty-default fast path. Scalar settings are copied from the current plan, not cached. Neither saved input is mutated. Raw access, non-managed plans and other inheritance modes retain their existing behavior.

Tests verify all public settings fields, immediate protection additions/removals, demo mode and limits, and bounded allocations with 22,766 candidates. In the standalone local test, 1,000 effective-settings reads took 1.3 ms and allocated 856,040 bytes. This is not an in-game frame-time measurement. No game configuration or existing protections were changed during investigation.
