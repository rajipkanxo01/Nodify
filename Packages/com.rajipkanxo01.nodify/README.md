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

The editor generates conversation `.yarn` files beside their conversation assets and
one `Dialogue Variables.yarn` file beside the assigned Yarn Project. Stable asset and
variable identities are retained internally; generated Yarn uses readable variable names.
The old project-ID variables filename is migrated automatically.

Use Yarn Spinner's Dialogue Runner and presenters to play the generated dialogue in your game.
