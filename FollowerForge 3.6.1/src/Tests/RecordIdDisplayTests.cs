using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class RecordIdDisplayTests
{
    [Fact]
    public void GearDetail_LeadsWithFormKeySoDuplicateNamesAreDistinct()
    {
        var a = RecordIdDisplay.GearDetail("0A12B3:Glasses.esp", "MSGlasses01", "Makeshift Eyeglasses", "Accessories • Light");
        var b = RecordIdDisplay.GearDetail("0A12B4:Glasses.esp", "MSGlasses02", "Makeshift Eyeglasses", "Accessories • Light");

        Assert.StartsWith("0A12B3:Glasses.esp", a, StringComparison.Ordinal);
        Assert.StartsWith("0A12B4:Glasses.esp", b, StringComparison.Ordinal);
        Assert.NotEqual(a, b);
        Assert.Contains("MSGlasses01", a, StringComparison.Ordinal);
        Assert.Contains("MSGlasses02", b, StringComparison.Ordinal);
    }

    [Fact]
    public void GearDetail_OmitsEditorIdWhenItRepeatsTheDisplayName()
    {
        var detail = RecordIdDisplay.GearDetail("01397F:Skyrim.esm", "SteelArrow", "SteelArrow", "Dawnguard");
        Assert.Equal("01397F:Skyrim.esm · Dawnguard", detail);
    }

    [Fact]
    public void PickerFilter_MatchesFormKeyAndPluginEvenWhenNameIsIdentical()
    {
        var item = new PickerItem(
            "Makeshift Eyeglasses",
            "0A12B3:Glasses.esp",
            RecordIdDisplay.GearDetail("0A12B3:Glasses.esp", "MSGlasses01", "Makeshift Eyeglasses", "mod"));

        Assert.True(PickerFilter.Matches(item, "0A12B3"));
        Assert.True(PickerFilter.Matches(item, "Glasses.esp"));
        Assert.True(PickerFilter.Matches(item, "eyeglasses"));
        Assert.False(PickerFilter.Matches(item, "0A12B4"));
    }
}
