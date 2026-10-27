# Nodify

A visual dialogue editor for Yarn Spinner. Open **Window > Nodify** after installation.

## Requirements and installation

Validated by compilation against Unity 6000.6.0f1 and Yarn Spinner 3.2.8.
Earlier Unity versions have not been verified.

Install Yarn Spinner first through **Window > Package Manager > Add package from git URL**:

```text
https://github.com/YarnSpinnerTool/YarnSpinner-Unity.git#v3.2.8
```

Then install this package:

```text
https://github.com/rajipkanxo01/DialogueToolUnity.git?path=/Packages/com.rajipkanxo01.nodify
```

For a local installation, use **Add package from disk** and select this folder's `package.json`.
Yarn Spinner is installed separately because Unity package manifests cannot declare Git dependencies.

## Usage

Create or select a Dialogue Project in the editor and assign a Yarn Project.
Add characters and conversations, then write dialogue and connect passages with choices.
The Variables tab supports Boolean and Integer variables. Use a line's variable button
to assign values, add or subtract integers, and use variables in choice conditions.

### Automatic conditional branching

Use **+ Branch** on a passage (or **More > Automatic Branch (If / Else)** in
Passage Details) to route dialogue automatically. Select a Boolean or Integer
variable, choose its expected value or comparison, and connect the **If** and
**Else** outputs to passages. **+ Else If** adds another condition; the first
matching branch runs. These routes do not display a player choice.

For different first-time and repeat greetings:

1. Create a Boolean variable named `hasTalked` in **Variables**, defaulting to false.
2. Add an automatic branch to the start passage: **hasTalked is True** leads to
   a "Welcome back" passage; **Else** leads to a "Nice to meet you" passage.
3. On a line in the first greeting, use **+ Variable** to set `hasTalked` to true.
4. Start the conversation again using the same Yarn variable storage to get the repeat greeting.

Yarn generation supports nested conditional content, including lines, assignments,
choices, jumps, and ends. A missing condition or destination is reported by validation.
Variable values live in Yarn Spinner's variable storage; keeping them across game
restarts requires saving and restoring that storage in your game.

The editor generates conversation `.yarn` files beside their conversation assets and
one `Dialogue Variables.yarn` file beside the assigned Yarn Project. Stable asset and
variable identities are retained internally; generated Yarn uses readable variable names.
The old project-ID variables filename is migrated automatically.

Use Yarn Spinner's Dialogue Runner and presenters to play the generated dialogue in your game.
