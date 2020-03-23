import * as path from 'path';
import { window, workspace, ExtensionContext } from 'vscode';
import './views/featureView'

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

  // If the extension is launched in debug mode then the debug server options are used
  // Otherwise the run options are used
  let serverCommand: Executable = {
    command: context.asAbsolutePath(path.join('server', 'AutoStep.LanguageServer.exe')),
   // args: ["debug"]
  };

  let serverOptions: ServerOptions = serverCommand;

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

  window.registerTreeDataProvider('autostep-features', new FeatureView(client));

  await client.onReady();

  client.onNotification("autostep/build_complete", () => {
     featureView.refresh(); 
  });
}


export function deactivate(): Thenable<void> {
    return client.stop();
}