# Scopes, order, and warnings

Under [Preset builder], each row shows its scope, connection type, and selected strategy. The preview below is rebuilt after every change.

### Manage the list

- Use the arrow buttons to move the selected row up or down.
- [Remove] deletes the selected strategy.
- [Remove all] clears the manual config.

If one strategy is added to a scope, it becomes the primary option. If several compatible strategies are added to the same scope, the first is primary and the following strategies are fallbacks. The order of rows within one scope is therefore important.

Do not add fallbacks simply to increase their number. Each one should be verified for its entire scope.

### Site and list scopes

A strategy for one site is more specific than a strategy for an entire list. If scopes overlap, the builder places the more specific profile before the general one and displays a warning so you can review the result.

[Accept order] only confirms that you reviewed the generated order. It does not move rows, change a strategy, or confirm that the strategy works. The warning may appear again after the builder changes.

### Understand the warnings

- **No test data** — retest the strategy for the selected scope.
- **Threshold not passed** — the strategy did not produce a sufficiently reliable result.
- **Partial scope coverage** — some sites or connection types failed. Choose another strategy or narrow the scope to one site.
- **Overlapping scopes** — check which profile will be applied first.
- **Strategies cannot be combined** — keep one strategy in this scope or choose a compatible alternative.

An error may prevent the builder from creating usable config text. A warning does not block you, but it still requires a manual check.

### For advanced users

The window may show these technical codes:

- `MANUAL_STRATEGY_UNTESTED` — the strategy has no results for the selected scope;
- `MANUAL_STRATEGY_UNPROVEN` — the strategy did not reach the required threshold;
- `MANUAL_SCOPE_PARTIAL` — only part of the scope passed;
- `MANUAL_SCOPE_OVERLAP` — two scopes contain the same connections;
- `MANUAL_CIRCULAR_UNSUPPORTED` — the selected options cannot be combined as primary and fallback strategies;
- `MANUAL_STRATEGY_INAPPLICABLE` — the strategy does not support every connection in the selected scope.

Next: [test and finish the manual config](cdpiui://Help/Zapret2ManualPresetBuilding/4FinishManualPreset/).
