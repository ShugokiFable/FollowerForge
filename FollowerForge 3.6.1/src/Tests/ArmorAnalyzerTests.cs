using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace FollowerForge.Tests;

public sealed class ArmorAnalyzerTests
{
    [Theory]
    [InlineData(BipedObjectFlag.Body, "Torso")]
    [InlineData(BipedObjectFlag.Head, "Head")]
    [InlineData(BipedObjectFlag.Hands, "Hands")]
    [InlineData(BipedObjectFlag.Feet, "Feet")]
    [InlineData(BipedObjectFlag.Shield, "Shield")]
    [InlineData(BipedObjectFlag.Ring, "Accessories")]
    public void RealBipedFlagsDriveTheWizardEquipmentGroup(BipedObjectFlag flag, string expected)
    {
        var armor = new Armor(FormKey.Factory("000800:FF_Test.esp"), SkyrimRelease.SkyrimSE)
        {
            BodyTemplate = new BodyTemplate
            {
                FirstPersonFlags = flag,
                ArmorType = ArmorType.Clothing,
            },
        };

        var detail = ArmorAnalyzer.Analyze(armor);

        Assert.Equal(expected, detail.SlotGroup);
        Assert.Contains(flag.ToString(), detail.BipedSlots);
    }
}
