# Dialogue Editor

A visual dialogue editor for Yarn Spinner.

This repository is a Unity Package Manager package.

Open the editor in Unity from **Window > Dialogue Editor**.

## Preview in this Unity project (Windows)

Run `./Tools/Enable-LocalPreview.ps1` in PowerShell once per checkout. This links
the root `Editor/` and `Runtime/` folders into `Assets/_Project/Scripts/`, so this
Unity project compiles the package sources directly. The links are local and
ignored by Git; edits through either path update the same files.

Open this folder in Unity, refresh Assets, and wait for compilation. Then choose
**Window > Dialogue Editor**. Yarn Spinner is already listed in this project's
`Packages/manifest.json`.

## Installation

In Unity, open **Window > Package Manager**, choose **Add package from git URL**, and use:

```text
https://github.com/rajipkanxo01/DialogueToolUnity.git
```

The package source is kept in the repository root:

```text
Runtime/
Editor/
package.json
```

Yarn Spinner v3.2.8 must be installed in the consuming Unity project's
`Packages/manifest.json`:

```json
"dev.yarnspinner.unity": "https://github.com/YarnSpinnerTool/YarnSpinner-Unity.git#v3.2.8"
```
