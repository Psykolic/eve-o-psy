// <auto_generate>
// Tests for ThumbnailConfiguration functionality
// </auto_generate>
using System;
using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;
using EveOPreview.Configuration.Implementation;
using EveOPreview.UI.Hotkeys;

namespace EveO.Tests.Configuration
{
    public class ThumbnailConfigurationTests
    {
        [Fact]
        public void Test_ThumbnailConfiguration_Initialization()
        {
            // Test that ThumbnailConfiguration initializes properly with default values
            var config = new ThumbnailConfiguration();

            config.ConfigVersion.Should().Be(ThumbnailConfiguration.CURRENT_CONFIG_VERSION);
            config.CycleGroup1ForwardHotkeys.Should().Contain("F14");
            config.CycleGroup1ClientsOrder.Should().ContainKey("EVE - Example DPS Toon 1");
            config.ThumbnailSize.Should().Be(new Size(384, 216));
            config.ThumbnailMinimumSize.Should().Be(new Size(32, 18));
        }

        [Fact]
        public void Test_ApplyRestrictions_AllowsThumbnailsSmallerThanLegacyMinimum()
        {
            var config = new ThumbnailConfiguration();
            config.ThumbnailSize = new Size(96, 54);
            config.ApplyRestrictions();

            config.ThumbnailSize.Should().Be(new Size(96, 54));
        }

        [Fact]
        public void Test_ApplyRestrictions_MigratesLegacyMinimumSize()
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
        public void Test_ApplyRestrictions_KeepsCustomMinimumSize()
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
        public void Test_GetThumbnailLocation_ReturnsCorrectLocation()
        {
            var config = new ThumbnailConfiguration();
            config.GetThumbnailLocation("TestClient", "ActiveClient", new Point(0, 0)).Should().Be(new Point(0, 0));
        }

        [Fact]
        public void Test_GetThumbnailSize_ReturnsCorrectSize()
        {
            var config = new ThumbnailConfiguration();
            config.GetThumbnailSize("TestClient", "ActiveClient", new Size(100, 100)).Should().Be(new Size(100, 100));
        }

        [Fact]
        public void Test_ApplyRestrictions_ClampsThumbnailRefreshPeriodToUpperBound_OnWindows()
        {
            var config = new ThumbnailConfiguration();
            config.ThumbnailRefreshPeriod = 2000; // Above Windows maximum of 1000
            config.ApplyRestrictions();

            config.ThumbnailRefreshPeriod.Should().Be(1000);
        }

        [Fact]
        public void Test_ApplyRestrictions_ClampsThumbnailRefreshPeriodToLowerBound_OnWindows()
        {
            var config = new ThumbnailConfiguration();
            config.ThumbnailRefreshPeriod = 100; // Below Windows minimum of 300
            config.ApplyRestrictions();

            config.ThumbnailRefreshPeriod.Should().Be(300);
        }

        [Fact]
        public void Test_StringToKey_ConvertsHotkeys()
        {
            var config = new ThumbnailConfiguration();
            var key = HotkeyConverter.ToKeys("Control+F14");
            key.Should().Be(Keys.Control | Keys.F14);
        }

        [Fact]
        public void Test_IsPriorityClient_ReturnsFalseForNonPriority()
        {
            var config = new ThumbnailConfiguration();
            config.IsPriorityClient("TestClient").Should().BeFalse();
        }

        [Fact]
        public void Test_SpotlightDefaults()
        {
            var config = new ThumbnailConfiguration();

            config.SpotlightClients.Should().BeEmpty();
            config.ToggleSpotlightHotkeys.Should().Contain("Control+F12");
            config.SpotlightSize.Should().Be(new Size(640, 360));
            config.SpotlightOpacity.Should().Be(1.0);
            config.SpotlightAlwaysOnTop.Should().BeTrue();
        }

        [Fact]
        public void Test_SpotlightSizeAndLocationArePerClient()
        {
            var config = new ThumbnailConfiguration();

            config.SetSpotlightSize("Client A", new Size(800, 450));
            config.SetSpotlightLocation("Client A", new Point(10, 20));

            config.GetSpotlightSize("Client A", new Size(640, 360)).Should().Be(new Size(800, 450));
            config.GetSpotlightSize("Client B", new Size(640, 360)).Should().Be(new Size(640, 360));
            config.GetSpotlightLocation("Client A", Point.Empty).Should().Be(new Point(10, 20));
            config.GetSpotlightLocation("Client B", Point.Empty).Should().Be(Point.Empty);
        }

        [Fact]
        public void Test_RenameSpotlightClientMovesSettings()
        {
            var config = new ThumbnailConfiguration();
            config.SpotlightClients.Add("EVE");
            config.SetSpotlightSize("EVE", new Size(800, 450));
            config.SetSpotlightLocation("EVE", new Point(10, 20));
            config.SetSpotlightRegion("EVE", new Rectangle(5, 6, 300, 200));

            config.RenameSpotlightClient("EVE", "EVE - Test Pilot");

            config.SpotlightClients.Should().Contain("EVE - Test Pilot");
            config.SpotlightClients.Should().NotContain("EVE");
            config.GetSpotlightSize("EVE - Test Pilot", Size.Empty).Should().Be(new Size(800, 450));
            config.GetSpotlightLocation("EVE - Test Pilot", Point.Empty).Should().Be(new Point(10, 20));
            config.GetSpotlightRegion("EVE - Test Pilot").Should().Be(new Rectangle(5, 6, 300, 200));
        }

        [Fact]
        public void Test_SpotlightRegion_IsStoredPerClient()
        {
            var config = new ThumbnailConfiguration();
            config.SetSpotlightRegion("Client A", new Rectangle(10, 20, 300, 200));

            config.GetSpotlightRegion("Client A").Should().Be(new Rectangle(10, 20, 300, 200));
            config.GetSpotlightRegion("Client B").Should().Be(Rectangle.Empty);

            config.SetSpotlightRegion("Client A", Rectangle.Empty);
            config.GetSpotlightRegion("Client A").Should().Be(Rectangle.Empty);
        }

        [Fact]
        public void Test_ApplyRestrictions_NormalizesNegativeSpotlightRegion()
        {
            var config = new ThumbnailConfiguration();
            config.SetSpotlightRegion("Client A", new Rectangle(-10, -5, 100, 50));

            config.ApplyRestrictions();

            config.GetSpotlightRegion("Client A").Should().Be(new Rectangle(0, 0, 100, 50));
        }
    }
}
