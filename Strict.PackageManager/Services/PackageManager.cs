﻿using Grpc.Core;
using System.IO.Compression;

namespace Strict.PackageManager.Services;

public sealed class PackageManager : Strict.PackageManager.PackageManager.PackageManagerBase
{
	private readonly ILogger<PackageManager> logger;

	public PackageManager(ILogger<PackageManager> logger) => this.logger = logger;

	public override Task<EmptyReply> DownloadPackage(PackageDownloadRequest requestModel,
		ServerCallContext context) =>
		(Task<EmptyReply>)DownloadAndExtract(requestModel.PackageUrl,
			requestModel.PackageName, requestModel.TargetPath);

	private async Task DownloadAndExtract(string packageUrl, string packageName,
		string targetPath)
	{
		logger.LogTrace("Service invoked " + packageUrl + " " + packageName + " " + targetPath);
		var localZip = Path.Combine(CacheFolder, packageName + ".zip");
		using HttpClient client = new();
		await DownloadFile(client, new Uri(packageUrl + "/archive/master.zip"), localZip);
		await Task.Run(() =>
			UnzipInCacheFolderAndMoveToTargetPath(packageName, targetPath, localZip));
	}

	private static string CacheFolder =>
		Path.Combine( //ncrunch: no coverage, only downloaded and cached on non development machines
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StrictPackages);
	private const string StrictPackages = nameof(StrictPackages);

	private static async Task DownloadFile(HttpClient client, Uri uri, string fileName)
	{
		await using var stream = await client.GetStreamAsync(uri);
		await using var file = new FileStream(fileName, FileMode.CreateNew);
		await stream.CopyToAsync(file);
	}

	private static void UnzipInCacheFolderAndMoveToTargetPath(string packageName, string targetPath,
		string localZip)
	{
		ZipFile.ExtractToDirectory(localZip, CacheFolder, true);
		var masterDirectory = Path.Combine(CacheFolder, packageName + "-master");
		if (!Directory.Exists(masterDirectory))
			throw new NoMasterFolderFoundFromPackage(packageName, localZip);
		if (Directory.Exists(targetPath))
			new DirectoryInfo(targetPath).Delete(true);
		TryMoveOrCopyWhenDeletionDidNotFullyWork(targetPath, masterDirectory);
	}

	public sealed class NoMasterFolderFoundFromPackage : Exception
	{
		public NoMasterFolderFoundFromPackage(string packageName, string localZip) : base(
			packageName + ", localZip: " + localZip) { }
	}

	private static void TryMoveOrCopyWhenDeletionDidNotFullyWork(string targetPath,
		string masterDirectory)
	{
		try
		{
			Directory.Move(masterDirectory, targetPath);
		}
		catch
		{
			foreach (var file in Directory.GetFiles(masterDirectory))
				File.Copy(file, Path.Combine(targetPath, Path.GetFileName(file)), true);
		}
	}
}