# Missing files

Some presets use separate site lists, BIN files, or LUA libraries. If the wizard cannot find a resource at the expected path, it asks you to choose a solution. Every missing file must be resolved before the preset can be saved.

### Available actions

- **Apply AutoCorrect** uses a file that CDPIUI found in another suitable folder. Check the suggested path before applying it.
- **Choose manually** lets you select an existing file. Prefer a resource from the same preset package with the expected extension.
- **Create empty** saves an empty file in place of the missing resource.

Use an empty file only when the resource is genuinely optional. For example, some user lists or log files can start empty. Do not replace a required LUA library or BIN file with an empty placeholder unless the preset author explicitly says it is optional.

For files whose names contain `user` or `debug`, the wizard may immediately suggest an empty file. This is a suggestion, not a guarantee of compatibility.

If every unresolved file has an AutoCorrect suggestion, selecting **Continue** applies those suggestions automatically. If at least one file has no suggestion, choose an action for it first.

Required resources are copied to separate CDPIUI storage when the preset is saved. The original files remain in place and are not deleted.

Next: [review, test, and save](cdpiui://Help/ConfigImport/4ReviewTestAndSave/).
