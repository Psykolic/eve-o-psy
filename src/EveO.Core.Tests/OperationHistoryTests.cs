using System;
using EveOPreview.Services;
using FluentAssertions;
using Xunit;

namespace EveO.Tests.Services
{
	public class OperationHistoryTests
	{
		[Fact]
		public void Test_OperationHistory_KeepsOnlyTheMostRecentEntries()
		{
			OperationHistory history = new OperationHistory(20);
			DateTime start = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

			for (int index = 0; index < 25; index++)
			{
				history.Record(start.AddSeconds(index), "operacao " + index);
			}

			var snapshot = history.Snapshot();

			snapshot.Should().HaveCount(20);
			snapshot[0].Operation.Should().Be("operacao 5");
			snapshot[19].Operation.Should().Be("operacao 24");
		}

		[Fact]
		public void Test_OperationHistory_DefaultCapacityIsTwenty()
		{
			OperationHistory history = new OperationHistory();

			history.Capacity.Should().Be(20);
		}
	}
}
