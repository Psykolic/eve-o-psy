using System;
using System.Collections.Generic;
using EveOPreview.Services;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Services
{
	public class ProcessSetDiffTests
	{
		private static ProcessSetEntry Entry(int handle, string title = "EVE")
		{
			return new ProcessSetEntry(new IntPtr(handle), title);
		}

		[Fact]
		public void Test_Update_HandleMissingForOneCycle_DoesNotRemove()
		{
			var diff = new ProcessSetDiff();

			diff.Update(new[] { Entry(1) }).Added.Should().ContainSingle();

			var missResult = diff.Update(Array.Empty<ProcessSetEntry>());
			var returnResult = diff.Update(new[] { Entry(1) });

			missResult.Removed.Should().BeEmpty();
			returnResult.Removed.Should().BeEmpty();
			returnResult.Added.Should().BeEmpty();
			diff.Known.Should().ContainSingle(x => x.Handle == new IntPtr(1));
		}

		[Fact]
		public void Test_Update_CallbackProvaProcessoMorto_RemoveNoPrimeiroCiclo()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1) });

			// ProcessMonitor devolve false quando o processo saiu (ou a janela ficou obsoleta):
			// a histerese de 3 ciclos nao deve segurar a thumbnail na tela (issue #150).
			var removal = diff.Update(Array.Empty<ProcessSetEntry>(), _ => false).Removed;

			removal.Should().ContainSingle();
			removal[0].Handle.Should().Be(new IntPtr(1));
			removal[0].Misses.Should().Be(1);
			diff.Known.Should().BeEmpty();
		}

		[Fact]
		public void Test_Update_CallbackMantemHandleAlemDoLimite()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1) });

			for (int i = 0; i < 5; i++)
			{
				// Handle sumiu, mas o processo continua vivo (travado, minimizado, login).
				diff.Update(Array.Empty<ProcessSetEntry>(), _ => true).Removed.Should().BeEmpty();
			}

			diff.Known.Should().ContainSingle(x => x.Handle == new IntPtr(1));
		}

		[Fact]
		public void Test_Update_HandleMissingThreeCycles_RemovesExactlyOnce()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1) });

			diff.Update(Array.Empty<ProcessSetEntry>()).Removed.Should().BeEmpty();
			diff.Update(Array.Empty<ProcessSetEntry>()).Removed.Should().BeEmpty();

			var removal = diff.Update(Array.Empty<ProcessSetEntry>()).Removed;

			removal.Should().ContainSingle();
			removal[0].Handle.Should().Be(new IntPtr(1));
			removal[0].Title.Should().Be("EVE");
			removal[0].Misses.Should().Be(3);

			// Uma remoção confirmada não pode ser repetida nos ciclos seguintes.
			diff.Update(Array.Empty<ProcessSetEntry>()).Removed.Should().BeEmpty();
		}

		[Fact]
		public void Test_Update_HandleReturnsAfterRemoval_IsAddedAgain()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1) });
			diff.Update(Array.Empty<ProcessSetEntry>());
			diff.Update(Array.Empty<ProcessSetEntry>());
			diff.Update(Array.Empty<ProcessSetEntry>()).Removed.Should().ContainSingle();

			var result = diff.Update(new[] { Entry(1, "EVE - Login") });

			result.Added.Should().ContainSingle();
			result.Added[0].Handle.Should().Be(new IntPtr(1));
			result.Added[0].Title.Should().Be("EVE - Login");
			result.Removed.Should().BeEmpty();
		}

		[Fact]
		public void Test_Update_EmptyCyclesWithoutKnownHandles_DoNotProduceRemovals()
		{
			var diff = new ProcessSetDiff();

			for (int cycle = 0; cycle < 5; cycle++)
			{
				var result = diff.Update(Array.Empty<ProcessSetEntry>());

				result.Added.Should().BeEmpty();
				result.Updated.Should().BeEmpty();
				result.Removed.Should().BeEmpty();
			}
		}

		[Fact]
		public void Test_Update_FirstCycleWithKnownHandle_DoesNotRemove()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1) });

			var result = diff.Update(Array.Empty<ProcessSetEntry>());

			result.Removed.Should().BeEmpty();
			diff.Known.Should().ContainSingle();
		}

		[Fact]
		public void Test_Update_LiveHandleAfterThreshold_PostponesRemoval()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1) });

			Func<IntPtr, bool> keepAlive = _ => true;

			diff.Update(Array.Empty<ProcessSetEntry>(), keepAlive).Removed.Should().BeEmpty();
			diff.Update(Array.Empty<ProcessSetEntry>(), keepAlive).Removed.Should().BeEmpty();
			diff.Update(Array.Empty<ProcessSetEntry>(), keepAlive).Removed.Should().BeEmpty();
			diff.Update(Array.Empty<ProcessSetEntry>(), keepAlive).Removed.Should().BeEmpty();

			// Quando o processo/handle realmente deixa de existir, a remoção é confirmada.
			var removal = diff.Update(Array.Empty<ProcessSetEntry>(), _ => false).Removed;

			removal.Should().ContainSingle();
			removal[0].Misses.Should().BeGreaterThanOrEqualTo(3);
		}

		[Fact]
		public void Test_Update_TitleChange_ProducesUpdateAndKeepsWindow()
		{
			var diff = new ProcessSetDiff();
			diff.Update(new[] { Entry(1, "EVE") });

			var result = diff.Update(new[] { Entry(1, "EVE - Character") });

			result.Updated.Should().ContainSingle();
			result.Updated[0].Title.Should().Be("EVE - Character");
			result.Added.Should().BeEmpty();
			result.Removed.Should().BeEmpty();
		}

		[Fact]
		public void Test_Update_ZeroHandleIsIgnored()
		{
			var diff = new ProcessSetDiff();

			var result = diff.Update(new[] { Entry(0) });

			result.Added.Should().BeEmpty();
			diff.Known.Should().BeEmpty();
		}
	}
}
