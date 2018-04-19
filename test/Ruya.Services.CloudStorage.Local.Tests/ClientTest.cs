﻿using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruya.Services.CloudStorage.Abstractions;
using Ruya.Services.CloudStorage.Local;

namespace Ruya.Services.CloudStorage.Local.Tests
{
	[TestClass]
	public class ClientTest
	{
		private static IServiceProvider _serviceProvider;
		private static TestContext _testContext;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext)
		{
			_testContext = testContext;

			IConfigurationRoot configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
																		 .AddJsonFile("appsettings.json", true, true)
																		 .Build();

			IServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(loggingBuilder =>
			{
#if DEBUG
											 loggingBuilder.AddSeq(configuration.GetSection("Seq"));
#endif
			                             });
			serviceCollection.AddOptions();
			serviceCollection.AddSingleton(configuration);
			serviceCollection.AddSingleton<IConfiguration>(configuration);

			serviceCollection.AddGoogleStorageService(configuration);

			_serviceProvider = serviceCollection.BuildServiceProvider();

		}


		[TestCategory("Writers")]
		[Priority(1)]
		[DataTestMethod]
		[DataRow("myBucket", "", "test_file.ignore.txt")]
		[DataRow("myBucket", "Test", "test_file.ignore.txt")]
		public void UploadFileTest(string bucketName, string remoteLocation, string fileName)
		{
			string localPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
			string remotePath = $"{remoteLocation}/{fileName}".TrimStart(Path.AltDirectorySeparatorChar);

			var client = _serviceProvider.GetService<ICloudFileService>();

			try
			{
				ICloudFileMetadata uploadedFile = client.UploadFile(localPath, remotePath, bucketName);
				bool uploadSuccess = uploadedFile.LastModified != null;
				Assert.IsTrue(uploadSuccess);
			}
			catch (ArgumentException ae) when (ae.Message.EndsWith("valid file extension"))
			{
				Assert.Fail(ae.Message);
			}
			catch (Exception ex)
			{
				Assert.Fail(ex.Message);
			}
		}

		[Priority(2)]
		[TestCategory("Readers")]
		[DataTestMethod]
		[DataRow("myBucket", "", "test_file.ignore.txt")]
		[DataRow("myBucket", "Test", "test_file.ignore.txt")]
		public void GetFileMetadataTest(string bucketName, string remoteLocation, string fileName)
		{
			string remotePath = $"{remoteLocation}/{fileName}".TrimStart(Path.AltDirectorySeparatorChar);

			var client = _serviceProvider.GetService<ICloudFileService>();
			ICloudFileMetadata actual = null;

			try
			{
				actual = client.GetFileMetadata(remotePath, bucketName);
				bool retrieveSuccess = actual?.LastModified != null;
				if (!retrieveSuccess)
				{
					throw new Exception("File or date is `null`, something is not correct");
				}
			}
			catch (ArgumentException ae) when (ae.Message.StartsWith("Not found"))
			{
				Assert.Fail("File not found");
			}
			catch (Exception ex)
			{
				Assert.Fail(ex.Message);
			}

			bool actualDateExist = actual.LastModified != null;
			bool actualSizeExist = actual.Size > 0;
			string expectedSignedUrl = actual.SignedUrl.StartsWith(@"C:\")
										   ? actual.SignedUrl
										   : string.Empty;

			string expectedBucket = bucketName;
			string expectedName = remotePath;

			Assert.AreEqual(expectedSignedUrl, actual.SignedUrl);
			Assert.AreEqual(expectedBucket, actual.Bucket);
			Assert.AreEqual(expectedName, actual.Name);
			Assert.IsTrue(actualDateExist);
			Assert.IsTrue(actualSizeExist);
		}

		[Priority(2)]
		[TestCategory("Readers")]
		[TestMethod]
		[DataRow("myBucket", "Test", "test_file.ignore.txt")]
		public void DownloadFileTest(string bucketName, string remoteLocation, string fileName)
		{
			string localPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
			string remotePath = $"{remoteLocation}/{fileName}".TrimStart(Path.AltDirectorySeparatorChar);

			string fileNameSuffix = $".{bucketName}";
			string targetFilePath = Path.GetDirectoryName(localPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(localPath) + fileNameSuffix + Path.GetExtension(localPath);
			if (File.Exists(targetFilePath))
			{
				File.Delete(targetFilePath);
			}


			var client = _serviceProvider.GetService<ICloudFileService>();
			using (FileStream downloadStream = File.OpenWrite(targetFilePath))
			{
				client.DownloadFile(remotePath, downloadStream, bucketName);
			}

			Assert.IsTrue(File.Exists(targetFilePath));

			bool fileHasData = new FileInfo(targetFilePath).Length > 0;
			File.Delete(targetFilePath);

			Assert.IsTrue(fileHasData);
		}

	}
}