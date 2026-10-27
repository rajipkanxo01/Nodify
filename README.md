# Nodify

A visual dialogue editor for Yarn Spinner.

## Develop or preview

Open this repository in Unity, wait for compilation, then choose **Window > Nodify**.
The editor is an embedded package in `Packages/com.rajipkanxo01.nodify/`.
Edit its `Editor/` and `Runtime/` sources directly. No setup is needed for a fresh checkout.
The development project's scenes, assets and third-party tools stay outside the distributable package.

In `SampleScene`, enter Play mode and finish the `Test` conversation. With the Game
view focused, press **E** to interact again. The same dialogue runner keeps its
variables, so the next interaction plays the repeat greeting. Change the key on
the scene's **Dialogue Replay (E)** component if needed.

## Install in another project

First install Yarn Spinner v3.2.8 using **Window > Package Manager > Add package from git URL**:

```text
https://github.com/YarnSpinnerTool/YarnSpinner-Unity.git#v3.2.8
```

Then add Nodify using this package subfolder URL:

```text
https://github.com/rajipkanxo01/DialogueToolUnity.git?path=/Packages/com.rajipkanxo01.nodify
```

The subfolder URL requires this layout to be committed and pushed to the repository.
For a local install, choose **Add package from disk** and select
`Packages/com.rajipkanxo01.nodify/package.json` from this checkout.

See the [package README](Packages/com.rajipkanxo01.nodify/README.md) for usage and requirements.
