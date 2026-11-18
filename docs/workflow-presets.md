# Normal-run workflow presets

Open AutoRetainer settings Ã¢â€ â€™ Retainers Ã¢â€ â€™ Workflow presets. Select a preset, change its allowed workflows, or use **Save as new preset** to copy it under a unique name. The bell overlay also has a Workflow selector. Selection and edits apply to the next normal run, never start work, and never alter an active run's snapshot.

For AutoDuty:

1. Select **AutoDuty**, or run `/autoretainer preset AutoDuty`.
2. Edit that preset to allow only the operations wanted between duties.
3. Enable stock AutoDuty normally. Its `/autoretainer e` call uses the selected preset.

`/autoretainer preset` lists available names. `/autoretainer preset Market` selects Market without starting it. `/autoretainer e` starts the selected normal workflow; `t`/`toggle` uses the same entry point. Automatic bell activation and the normal bell Run button use the same selection. Explicit venture, market, storage and cleanup actions retain their independent scope. Multi mode and Artisan retain their existing workflows.

Bundled presets are All enabled, AutoDuty, Ventures and gil, Storage, Market, Glamour collection, and Disposal. AutoDuty initially allows ventures, configured gil handling and existing entrust plans. It excludes markets, collection, dresser cleanup, disposal and other postprocessing plugins. Existing installations default to All enabled and retain their normal venture-driven scheduling.

Custom selections make one pass over selected retainers, including non-venture visits for storage/market operations. Normal per-retainer settings, manual-items exclusions, strategy permission and item protections remain authoritative. The preset only narrows enabled features: it never enables a disabled Steward feature, changes an entrust plan, or bypasses log-only mode. Empty selections perform no work. A completed custom pass disables the scheduler; configured window-closing preferences remain in effect.

Stock AutoDuty still decides when to call AutoRetainer using venture readiness. Presets do not make it visit retainers on every duty or when no venture is due. Completed custom passes with ventures excluded suppress external readiness queries for one minute to prevent immediately repeating a pass that deliberately left ventures untouched. Explicit `/e` starts a new pass without this delay.

Collection travel remains deferred while AutoDuty is active. Dresser cleanup is allowed during collection visits; this switch alone does not request a dresser visit. Disposal permits the existing normal-run disposal stages; it does not invoke the separate full cleanup workflow or AutoDuty's separate GC stage. The Entrust selection also controls post-dresser entrust; excluded follow-up storage leaves restored items in inventory.

Update Steward together with AutoRetainer for custom presets. A restricted run will not invoke postprocessing with an older Steward that cannot apply its workflow mask. Missing Steward continues to use the existing safe standalone restrictions.

Local builds, logic tests and source wiring tests cover this integration. In-game acceptance remains required: exercise a market-only pass with a completed venture, storage-only with markets enabled globally, preset edits during a run, an empty preset, manual workflow buttons after a pass, AutoDuty completion/resumption and cancellation during a pending transaction. No saved live configuration is changed by installation.
