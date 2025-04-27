using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XgpSaveTools.Extensions;

namespace XgpSaveTools.Records
{

	public record GameInfoJson(List<GameInfo> Games);
	public record GameInfo(string Name, string Package, string Handler, HandlerArgs? HandlerArgs);
	public record UnsupportedGameInfo(string Name, string Package, string Handler, HandlerArgs? HandlerArgs) : GameInfo(Name, Package, Handler, HandlerArgs);
	public record HandlerArgs(string? Suffix, string? IconFormat);
	public record UserContainerFolder(string UserTag, string Dir); //represents user container folder
	public record ContainerMetaFile(string Name, int Number, List<ContainerEntry> Files); // represents the container meta file
	public record ContainerEntry(string Name, string Path); // represents an file inside a container
	public record EntryReplacement(ContainerEntry TargetFile, FileInfo ReplacementFile); // indicates which entry to replace
	public record SaveFile(string OutputName, ContainerEntry ContainerEntry) // represents output save file when extracting
	{
		public SaveFile(string OutputName, string FilePath) : this(OutputName, new ContainerEntry(new FileInfo(FilePath).Name, new FileInfo(FilePath).FullName)) { }

		public string GetReadableFileSize()
		{
			var fileinfo = new FileInfo(ContainerEntry.Path);
			var result = IoExtensions.GetReadableFileSize(fileinfo.Length);
			return result;
		}
	}
}
