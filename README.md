# AutoStep.VsCode

This repository holds the VS Code Extension that provides language support for AutoStep. 

This includes:

- As-you-type compilation and linking of your tests
- Intellisense and Auto-Complete for steps.

## Building and Running

To build and run this extension locally, you will need:

 - The dotnet core 3.1 SDK installed.
 - node, version 12+
 - powershell (powershell core on linux or mac is fine)

To debug, open VS Code in the root of this repo and press F5. This will:

 - Run the ./build.ps1 script (in debug build mode)
 - Launch the VSCode debug instance with the extension loaded.

Take a look at the VS Code docs on debugging extensions for more details.
