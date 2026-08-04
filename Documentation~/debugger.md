[← Back to the README](../README.md)

# Debugger

Open **Tools > Fynite > Debugger** to watch the machines the PlayerLoop is driving. The window lists
every running machine, and shows the owner, the context type and the full active path of the one you
pick, from the top level state down to the current one.

It works during Play Mode and needs no setup: no component to add, no flag to turn on, nothing to
register. The window is read only — it watches machines, it never starts, stops or steers them — and
it asks the loop for what it needs a few times a second rather than being told, so a machine costs
exactly the same whether the window is open or has never been opened.

It is built with the Editor's own UI toolkit, so it follows the theme and docks like any other
window. The debugger lives in an Editor-only assembly and does not go into a player build.
