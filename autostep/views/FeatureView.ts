import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient';

export class Feature extends vscode.TreeItem
{
    constructor(
        public readonly label: string
    )
    {
        super(label);
    }
}

export default class FeatureView implements vscode.TreeDataProvider<Feature>
{
    private readonly languageClient: LanguageClient;

    constructor(langClient: LanguageClient)
    {
        this.languageClient = langClient;
    }

    
	private _onDidChangeTreeData: vscode.EventEmitter<Feature | null> = new vscode.EventEmitter<Feature | null>();
	readonly onDidChangeTreeData: vscode.Event<Feature | null> = this._onDidChangeTreeData.event;

    refresh() {
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: Feature): vscode.TreeItem | Thenable<vscode.TreeItem> {
        return element;
    }

    async getChildren(element?: Feature | undefined): Promise<Feature[]> {
        if (element)
        {
            // Get the children?
            return Promise.resolve([]);
        }
        else 
        {
            // Get the list of features.
            var features = await this.languageClient.sendRequest<FeatureSetParams>("autostep/features");

            return features.featureNames.map(s => new Feature(s));
        }
    }

}

declare class FeatureSetParams 
{
    featureNames: string[]
}