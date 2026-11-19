// Testes de núcleo (rodam no Linux) para o tipo neutro de hotkey e para a configuração
// movida do executável para o EveO.Core (issue #162).
using System.Drawing;
using EveOPreview.Configuration;
using EveOPreview.Configuration.Implementation;

namespace EveO.Core.Tests
{
	public class HotkeyBindingTests
	{
		[Fact]
		public void None_IsEmpty()
		{
			HotkeyBinding.None.IsEmpty.Should().BeTrue();
			HotkeyBinding.None.Value.Should().BeEmpty();
			HotkeyBinding.None.ToString().Should().BeEmpty();
		}

		[Fact]
		public void Value_RoundTripsText()
		{
			var binding = new HotkeyBinding("Control+F14");
			binding.Value.Should().Be("Control+F14");
			binding.IsEmpty.Should().BeFalse();
		}

		[Fact]
		public void ConstructedWithNull_IsNormalizedToEmpty()
		{
			var binding = new HotkeyBinding(null);
			binding.Value.Should().BeEmpty();
			binding.IsEmpty.Should().BeTrue();
		}

		[Fact]
		public void Equality_IsByValue()
		{
			var a = new HotkeyBinding("F14");
			var b = new HotkeyBinding("F14");
			var c = new HotkeyBinding("F13");

			(a == b).Should().BeTrue();
			(a != c).Should().BeTrue();
			a.Equals(b).Should().BeTrue();
			a.Equals((object)b).Should().BeTrue();
			a.Equals(c).Should().BeFalse();
		}

		[Fact]
		public void Default_StructValue_IsEmpty()
		{
			HotkeyBinding binding = default;
			binding.Value.Should().BeEmpty();
			binding.IsEmpty.Should().BeTrue();
		}
	}

	public class ThumbnailConfigurationCoreTests
	{
		[Fact]
		public void Defaults_MatchLegacyConfiguration()
		{
			var config = new ThumbnailConfiguration();

			config.ConfigVersion.Should().Be(ThumbnailConfiguration.CURRENT_CONFIG_VERSION);
			config.CycleGroup1ForwardHotkeys.Should().Contain("F14");
			config.CycleGroup1BackwardHotkeys.Should().Contain("F13");
			config.ToggleSpotlightHotkeys.Should().Contain("Control+F12");
			config.MinimizeAllClientsHotkeys.Should().Contain("Control+F22");
			config.ThumbnailSize.Should().Be(new Size(384, 216));
			config.ThumbnailMinimumSize.Should().Be(new Size(32, 18));
		}

		[Fact]
		public void OverlayLabelFont_IsNeutralText_DefaultEmpty()
		{
			var config = new ThumbnailConfiguration();

			// No Core a fonte é guardada como texto (issue #162); o executável define o padrão.
			config.OverlayLabelFont.Should().BeEmpty();

			config.OverlayLabelFont = "Liberation Sans, 10pt, style=Bold";
			config.OverlayLabelFont.Should().Be("Liberation Sans, 10pt, style=Bold");
		}

		[Fact]
		public void GetClientHotkey_UnknownClient_ReturnsNone()
		{
			var config = new ThumbnailConfiguration();

			config.GetClientHotkey("desconhecido").IsEmpty.Should().BeTrue();
		}

		[Fact]
		public void SetClientHotkey_ThenGetClientHotkey_RoundTrips()
		{
			var config = new ThumbnailConfiguration();

			config.SetClientHotkey("Pilot", new HotkeyBinding("Control+F14"));

			var hotkey = config.GetClientHotkey("Pilot");
			hotkey.Value.Should().Be("Control+F14");
			(hotkey == new HotkeyBinding("Control+F14")).Should().BeTrue();
		}

		[Fact]
		public void ApplyRestrictions_MigratesLegacyMinimumSize()
		{
			var config = new ThumbnailConfiguration
			{
				ConfigVersion = 1,
				ThumbnailMinimumSize = new Size(192, 108)
			};

			config.ApplyRestrictions();

			config.ThumbnailMinimumSize.Should().Be(new Size(32, 18));
			config.ConfigVersion.Should().Be(ThumbnailConfiguration.CURRENT_CONFIG_VERSION);
		}

		[Fact]
		public void ApplyRestrictions_KeepsCustomMinimumSize()
		{
			var config = new ThumbnailConfiguration
			{
				ConfigVersion = 1,
				ThumbnailMinimumSize = new Size(120, 68)
			};

			config.ApplyRestrictions();

			config.ThumbnailMinimumSize.Should().Be(new Size(120, 68));
		}

		[Fact]
		public void ApplyRestrictions_ClampsThumbnailRefreshPeriod()
		{
			var config = new ThumbnailConfiguration();
			config.ThumbnailRefreshPeriod = 5000;
			config.ApplyRestrictions();
			config.ThumbnailRefreshPeriod.Should().Be(1000);

			config.ThumbnailRefreshPeriod = 1;
			config.ApplyRestrictions();
			config.ThumbnailRefreshPeriod.Should().Be(300);
		}
	}
}
