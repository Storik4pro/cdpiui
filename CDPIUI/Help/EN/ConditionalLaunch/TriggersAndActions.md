# Triggers and actions

### Triggers

Three trigger types are available:

- [Hot key] starts a task from the selected key combination.
- [When a process starts] starts a task after a process appears.
- [When a process stops] starts a task after a process exits.

A process name can be entered with or without the `.exe` extension. A delay helps make sure the process state did not change by accident.

Multiple triggers use OR logic: any one of them can start the task.

### Actions

Select an action and fill in the fields that appear. The application hides fields that are not needed.

Actions run from top to bottom. Use the arrow buttons to change their order.

Simple example:

1. Add a [Hot key] trigger.
2. Select a key combination.
3. Add the [Start component] action.
4. Select an installed component.
5. Save the task and select [Run] to test it.

If [Stop task when an action fails] is selected, actions after the error are not run.

