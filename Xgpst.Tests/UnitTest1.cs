namespace Xgpst.Tests
{
	public class UnitTest1
	{

		[Fact]
		/// XGH test to compare files generated with this lib to the ones generated in original py script
		/// you have to manually put the files in the folder because I'm a lazy bastard
		public void CompareFiles()
		{
			var folder = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "FilesToCheck"));
			var targetFolder = folder.GetDirectories().FirstOrDefault(x => x.Name == "target");
			Assert.NotNull(targetFolder);
			var refFolderFiles = folder.GetDirectories().FirstOrDefault(x => x.Name == "refference")?.GetFiles();
			Assert.NotNull(refFolderFiles);

			foreach (var file in targetFolder.GetFiles())
			{
				var refFile = refFolderFiles.FirstOrDefault(x => x.Name == file.Name);
				if (refFile == null) continue;
				Assert.True(AreFilesIdentical(file.FullName, refFile.FullName));
			}
		}

		private bool AreFilesIdentical(string path1, string path2)
		{
			// 1) Quick length check
			var fi1 = new FileInfo(path1);
			var fi2 = new FileInfo(path2);
			if (fi1.Length != fi2.Length) return false;

			// 2) Byte-by-byte compare
			byte[] a = File.ReadAllBytes(path1);
			byte[] b = File.ReadAllBytes(path2);
			return a.SequenceEqual(b);
		}
	}
}