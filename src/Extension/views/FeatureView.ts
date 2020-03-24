import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient';

export class Feature extends vscode.TreeItem
{
    constructor(
        public readonly info: FeatureInfo
    )
    {
        super(info.name);

        this.id = `${info.sourceFile}/${info.name}`
        this.tooltip = info.description;
        this.description = info.sourceFile;
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
        this._onDidChangeTreeData.fire(null);
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
            var featureResponse = await this.languageClient.sendRequest<FeatureSetParams>("autostep/features");

            return featureResponse.features.map(info => new Feature(info));
        }
    }
}

interface FeatureInfo 
{
    name: string;
    description: string;
    sourceFile: string;
}

declare class FeatureSetParams 
{
    features: FeatureInfo[]
}