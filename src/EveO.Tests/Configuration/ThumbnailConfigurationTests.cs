// <auto_generate>
// Tests for ThumbnailConfiguration functionality
// </auto_generate>
using System;
using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;
using EveOPreview.Configuration.Implementation;

namespace EveO.Tests.Configuration
{
    public class ThumbnailConfigurationTests
    {
        [Fact]
        public void Test_ThumbnailConfiguration_Initialization()
        {
            // Test that ThumbnailConfiguration initializes properly with default values
            var config = new ThumbnailConfiguration();

            config.ConfigVersion.Should().Be(1);
            config.CycleGroup1ForwardHotkeys.Should().Contain("F14");
            config.CycleGroup1ClientsOrder.Should().ContainKey("EVE - Example DPS Toon 1");
            config.ThumbnailSize.Should().Be(new Size(384, 216));
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
            var key = config.StringToKey("Control+F14");
            key.Should().Be(Keys.Control | Keys.F14);
        }

        [Fact]
        public void Test_IsPriorityClient_ReturnsFalseForNonPriority()
        {
            var config = new ThumbnailConfiguration();
            config.IsPriorityClient("TestClient").Should().BeFalse();
        }
    }
}
