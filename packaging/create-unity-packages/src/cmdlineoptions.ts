import { Ignores } from "./RecursiveReaddir";
import { OptionDefinition, Section } from "command-line-usage";

export interface ParsedOptions {
	sourcePath: string;
	targetPath: string;
	packageName: string;
	version: string;
	baseInstallationPath: string | undefined;
	skipPackaging: boolean;
	ignores: Ignores;
	doUnityPackage: boolean;
	doPackmanPackage: boolean;
}

export const optionDefinitions : OptionDefinition[] = [
	{
		name: 'help',
		alias: 'h',
		description: 'Display this usage guide.',
	},
	{
		name: 'src',
		alias: 's',
		type: String,
		description: 'Path(s) to the source assets to be packaged. If you pass more than one, you must pass an equal amount to extras, ignores, installPath and project',
	},
	{
		name: 'out',
		alias: 'o',
		typeLabel: '{underline directory}',
		description: 'Where to save the zip and md5 files',
	},
	{
		name: 'name',
		alias: 'n',
		type: String,
		description: 'Name of the package',
	},
	{
		name: 'version',
		alias: 'v',
		type: String,
		description: 'Version of the package',
	},
	{
		name: 'extras',
		alias: 'e',
		type: String,
		description: 'Path to extra files to also be packaged (recursively',
	},
	{
		name: 'ignores',
		alias: 'i',
		type: String,
		description: 'Path to file with globs of things to ignore, like a .gitignore or an .npmignore',
	},
	{
		name: 'installPath',
		alias: 't',
		type: String,
		description: 'Installation path for unity packages (set in the meta files)',
	},
	{
		name: 'skip',
		alias: 'k',
		type: Boolean,
		defaultValue: false,
		description: 'Skip all packaging and just prepare the source tree for packaging'
	},
	{
		name: 'skipUnity',
		alias: 'u',
		type: Boolean,
		defaultValue: false,
		description: 'Skip packaging for the asset store (.unitypackage)'
	},
	{
		name: 'skipPackman',
		alias: 'p',
		type: Boolean,
		defaultValue: false,
		description: 'Skip packaging for packman (.tgz with the sources inside a "package" folder, and a zip with the sources inside a "package name" folder )'
	},
];

export const sections : Section[] = [
	{
		header: 'Unity packager',
		content: 'Takes a source tree, and creates packman and asset store packages',
	},
	{
		header: 'Options',
		optionList: optionDefinitions,
	},
];