# Conditional Launch

Conditional Launch runs selected actions from a hot key or when a process starts or stops. Tasks continue to work while the main CDPI UI window is closed.

### Task structure

A task contains triggers and actions.

A trigger starts the task. If a task has several triggers, any one of them can start it.

Actions run in order from top to bottom. For example, a task can apply a preset, wait a few seconds, and start a component.

### Priority

If the same event matches several tasks, the tasks with the highest priority are run. If several matching tasks have the same priority, all of them are run.

### Important details

- A disabled task does not react to triggers.
- Select [Run] to test a task manually.
- A process trigger can have a delay. If the process returns to its previous state before the delay ends, the task is not started.
- Use `.cdpitask` files to move tasks to another computer.

Next: [Creating and editing tasks](cdpiui://Help/ConditionalLaunch/CreatingTasks/).

