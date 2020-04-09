# AutoStep.VsCode

This repository holds the VS Code Extension that provides language support for AutoStep. 

This includes:

- As-you-type compilation and linking of your tests
- Intellisense and Auto-Complete for steps.
- Choosing Tests to Run

> [Contribution Guide](https://github.com/autostep/.github/blob/master/CONTRIBUTING.md) and
> [Code of Conduct](https://github.com/autostep/.github/blob/master/CODE_OF_CONDUCT.md)

---

**Status**

AutoStep is currently under development (in alpha). You can grab the VSIX for this extension from the build artifacts
until we release a version to the marketplace (or run locally, see below).

---

## Building and Running

To build and run this extension locally, you will need:

 - The dotnet core 3.1 SDK installed.
 - node, version 12+
 - powershell (powershell core on linux or mac is fine)

To debug, open VS Code in the root of this repo and press F5. This will:

 - Run the ./build.ps1 script (in debug build mode)
 - Launch the VSCode debug instance with the extension loaded.
 - When the language server starts, it will automatically attempt to run a debugger (``Debugger.Launch``), to give you an option
   to attach to it.

Take a look at the VS Code docs on debugging extensions for more details.
