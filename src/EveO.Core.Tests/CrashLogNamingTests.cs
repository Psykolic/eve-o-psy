using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Services;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Services
{
	public class CrashLogNamingTests
	{
		private static readonly DateTime Timestamp = new DateTime(2026, 11, 19, 12, 34, 56, DateTimeKind.Utc);

		[Fact]
		public void Test_NextPath_TwoCrashesInSameSecond_GeneratesDistinctNames()
		{
			string directory = CreateTempDirectory();

			try
			{
				string first = CrashLogNaming.NextPath(directory, Timestamp, Array.Empty<string>());
				File.WriteAllText(first, "primeiro crash");

				string second = CrashLogNaming.NextPath(directory, Timestamp, new[] { first });

				second.Should().NotBe(first);
				Path.GetFileName(second).Should().StartWith("EVE-O-Preview-20261119-123456");
				Path.GetFileName(second).Should().EndWith(".log");
				File.Exists(first).Should().BeTrue();
			}
			finally
			{
				DeleteDirectory(directory);
			}
		}

		[Fact]
		public void Test_PruneKeepingNewest_KeepsExactlyFiveAndDropsOldest()
		{
			List<string> paths = Enumerable
				.Range(0, 7)
				.Select(index => Path.Combine(
					"logs",
					"EVE-O-Preview-"
					+ Timestamp.AddSeconds(index).ToString("yyyyMMdd-HHmmss")
					+ ".log"))
				.ToList();

			IReadOnlyList<string> kept = CrashLogNaming.PruneKeepingNewest(paths, 5);

			kept.Should().HaveCount(5);
			kept.Should().NotContain(paths[0]);
			kept.Should().NotContain(paths[1]);
			kept.Should().Contain(paths[2]);
			kept.Should().Contain(paths[6]);
			kept[0].Should().Be(paths[6], "o resultado vem do mais novo para o mais antigo");
		}

		[Fact]
		public void Test_PruneKeepingNewest_SameSecondSuffix_IsNewerThanBaseName()
		{
			string baseName = Path.Combine("logs", "EVE-O-Preview-20261119-123456.log");
			string suffixed = Path.Combine("logs", "EVE-O-Preview-20261119-123456-1.log");
			string nextSecond = Path.Combine("logs", "EVE-O-Preview-20261119-123457.log");

			IReadOnlyList<string> kept = CrashLogNaming.PruneKeepingNewest(
				new[] { baseName, suffixed, nextSecond },
				2);

			kept.Should().Equal(nextSecond, suffixed);
		}

		[Fact]
		public void Test_NextPath_MissingDirectory_DoesNotThrowAndCreatesIt()
		{
			string root = Path.Combine(
				Path.GetTempPath(),
				"eveo-crashlog-" + Guid.NewGuid().ToString("N"));
			string directory = Path.Combine(root, "nested", "logs");

			try
			{
				string path = CrashLogNaming.NextPath(directory, Timestamp, Array.Empty<string>());

				Path.GetDirectoryName(path).Should().Be(directory);
				Directory.Exists(directory).Should().BeTrue("o handler precisa conseguir gravar o dump");

				File.WriteAllText(path, "crash");
				File.Exists(path).Should().BeTrue();
			}
			finally
			{
				DeleteDirectory(root);
			}
		}

		[Fact]
		public void Test_NextPath_WindowsInvalidCharacters_AreSanitized()
		{
			string root = CreateTempDirectory();

			try
			{
				string directory = Path.Combine(root, "logs:<>|\"?*");
				string path = CrashLogNaming.NextPath(directory, Timestamp, Array.Empty<string>());
				string parent = Path.GetDirectoryName(path);

				parent.Should().NotContainAny("<", ">", ":", "\"", "|", "?", "*");
				Directory.Exists(parent).Should().BeTrue();
				File.WriteAllText(path, "crash");
				File.Exists(path).Should().BeTrue();
			}
			finally
			{
				DeleteDirectory(root);
			}
		}

		private static string CreateTempDirectory()
		{
			string directory = Path.Combine(
				Path.GetTempPath(),
				"eveo-crashlog-" + Guid.NewGuid().ToString("N"));

			Directory.CreateDirectory(directory);
			return directory;
		}

		private static void DeleteDirectory(string directory)
		{
			try
			{
				if (Directory.Exists(directory))
				{
					Directory.Delete(directory, true);
				}
			}
			catch
			{
				// Artefato de teste; a limpeza nao pode mascarar o resultado.
			}
		}
	}
}
