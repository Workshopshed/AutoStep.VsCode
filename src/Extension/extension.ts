import * as path from 'path';
import { window, workspace, env, ExtensionContext } from 'vscode';
import './views/featureView';
import * as os from "os"
import * as child_process from "child_process"

import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
  Executable
} from 'vscode-languageclient';
import FeatureView from './views/featureView';

let client: LanguageClient;

export async function activate(context: ExtensionContext) {
  
  // The debug options for the server
  // --inspect=6009: runs the server in Node's Inspector mode so VS Code can attach to the server for debugging
  // let debugOptions = { execArgv: ['--nolazy', '--inspect=6009'] };

  // Determine platform language server platform path.
  let platform = os.platform();

  let build = "win-x64"

  if (platform != "win32")
  {
    // Something else, use the linux one?
    build = "linux";
  }

  // If the extension is launched in debug mode then the debug server options are used
  // Otherwise the run options are used
  let runCommand: Executable = {    
    command: context.asAbsolutePath(path.join('server', build, 'AutoStep.LanguageServer.exe')),
    args: []
  };

  let debugCommand: Executable = {
    command: context.asAbsolutePath(path.join('server', 'portable', 'AutoStep.LanguageServer.exe')),
    args: ["debug"]
  };

  let serverOptions: ServerOptions = { 
     run: runCommand,
     debug: debugCommand
  };

  // Options to control the language client
  let clientOptions: LanguageClientOptions = {
    // Register the server for plain text documents
    documentSelector: [{ scheme: 'file', language: 'autostep' }, {scheme: 'file', language: 'autostep-interaction' }],
    synchronize: {
      // Notify the server about file changes to '.clientrc files contained in the workspace
      configurationSection: "autostep",
      fileEvents: [ workspace.createFileSystemWatcher("**/*.as"), workspace.createFileSystemWatcher("**/*.asi") ]
    }
  };

  // Create the language client and start the client.
  client = new LanguageClient(
    'autostep',
    'AutoStep Language Server',
    serverOptions,
    clientOptions
  );

  // Start the client. This will also launch the server
  client.start();

  var featureView = new FeatureView(client);

  window.registerTreeDataProvider('autostep-features', featureView);

  await client.onReady();

  client.onNotification("autostep/build_complete", () => {
     featureView.refresh(); 
  });
}


export function deactivate(): Thenable<void> {
    return client.stop();
}