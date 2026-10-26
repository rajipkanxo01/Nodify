# Dialogue Tool

This repository is a Unity Package Manager package.

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
