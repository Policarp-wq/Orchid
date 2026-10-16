using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Tests;

[TestClass]
public class SetupStateTests
{
    private static readonly MidiInputInfo Piano = new("a", "Piano"), Controller = new("b", "Controller");

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    public void DefaultStrongBeats_OnlyTheFirstBeatOfTheMeasureIsStrong(int pulses)
    {
        // Arrange
        var expected = Enumerable.Range(0, pulses).Select(pulse => pulse == 0).ToArray();

        // Act
        var actual = SetupState.DefaultStrongBeats(pulses);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void NewSetup_HasTheFourPulseDefaultPattern()
    {
        // Arrange

        // Act
        var setup = new SetupState();

        // Assert
        CollectionAssert.AreEqual(new[] { true, false, false, false }, setup.StrongBeats.ToArray());
        Assert.AreEqual(setup.PulseDenominator, setup.StrongBeats.Count);
        Assert.AreEqual("4/4", setup.MeterText);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(8, 8)]
    [DataRow(16, 16)]
    public void Pulse_ChangeResetsThePatternToTheDefaultOfTheNewLength(int pulse, int beats)
    {
        // Arrange
        var setup = new SetupState { StrongBeats = [true, false, true, false] };

        // Act
        setup.PulseDenominator = pulse;

        // Assert
        CollectionAssert.AreEqual(SetupState.DefaultStrongBeats(beats), setup.StrongBeats.ToArray());
        Assert.AreEqual(beats, setup.ClicksPerMeasure);
    }

    [TestMethod]
    public void Pulse_SettingTheSameValueKeepsTheEditedPattern()
    {
        // Arrange
        var setup = new SetupState { StrongBeats = [true, false, true, false] };

        // Act
        setup.PulseDenominator = 4;

        // Assert
        CollectionAssert.AreEqual(new[] { true, false, true, false }, setup.StrongBeats.ToArray());
    }

    [TestMethod]
    public void Pulse_AnUnsupportedValueIsIgnored()
    {
        // Arrange
        var setup = new SetupState();

        // Act
        setup.PulseDenominator = 3;

        // Assert
        Assert.AreEqual(4, setup.PulseDenominator);
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(0)]
    public void StrongBeats_APatternOfTheWrongLengthIsRejectedNotTrimmedOrPadded(int length)
    {
        // Arrange
        var setup = new SetupState();

        // Act
        void Assign() => setup.StrongBeats = new bool[length];

        // Assert
        Assert.ThrowsExactly<ArgumentException>(Assign);
        CollectionAssert.AreEqual(new[] { true, false, false, false }, setup.StrongBeats.ToArray());
    }

    [TestMethod]
    public void StrongBeats_ChangeRaisesTheSummary()
    {
        // Arrange
        var setup = new SetupState();
        var raised = new List<string?>();
        setup.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        setup.StrongBeats = [true, false, true, false];

        // Assert
        CollectionAssert.Contains(raised, nameof(SetupState.StrongBeatsSummary));
        Assert.AreEqual(Display.Format(Strings.StrongMany, "1, 3", 4), setup.StrongBeatsSummary);
    }

    [TestMethod]
    [DataRow("....", "No strong beats")]
    [DataRow("S...", "Beat 1 of 4")]
    [DataRow("S.S.", "Beats 1, 3 of 4")]
    [DataRow("SSSS", "Every beat is strong (4)")]
    [DataRow("..S.....", "Beat 3 of 8")]
    [DataRow("S..S..", "Beats 1, 4 of 6")]
    [DataRow("S..", "Beat 1 of 3")]
    public void StrongBeatsSummary_NamesTheStrongBeatsOfAnyMeter(string pattern, string expected)
    {
        // Arrange
        var beats = pattern.Select(flag => flag == 'S').ToArray();

        // Act
        var actual = Display.StrongBeats(beats);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(10, 30)]
    [DataRow(500, 240)]
    [DataRow(97, 97)]
    public void Bpm_StaysInsideTheSupportedRange(int requested, int expected)
    {
        // Arrange
        var setup = new SetupState();

        // Act
        setup.Bpm = requested;

        // Assert
        Assert.AreEqual(expected, setup.Bpm);
    }

    [TestMethod]
    public void CountIn_OnlyTheOfferedChoicesAreAccepted()
    {
        // Arrange
        var setup = new SetupState();

        // Act
        setup.CountInBars = 7;
        var rejected = setup.CountInBars;
        setup.CountInBars = 0;

        // Assert
        Assert.AreEqual(1, rejected);
        Assert.AreEqual(0, setup.CountInBars);
    }

    [TestMethod]
    public async Task Refresh_SelectsTheFirstInputAndKeepsTheSelectionWhileItIsStillPresent()
    {
        // Arrange
        var catalog = new FakeMidiInputCatalog { Inputs = { Piano, Controller } };
        var setup = new SetupState();
        await setup.RefreshAsync(catalog, CancellationToken.None);
        setup.SelectedInput = setup.Inputs[1];

        // Act
        await setup.RefreshAsync(catalog, CancellationToken.None);

        // Assert
        Assert.AreEqual("b", setup.SelectedInput?.Id);
    }

    [TestMethod]
    public async Task Refresh_FallsBackToTheFirstInputWhenTheSelectedDeviceIsGone()
    {
        // Arrange
        var catalog = new FakeMidiInputCatalog { Inputs = { Piano, Controller } };
        var setup = new SetupState();
        await setup.RefreshAsync(catalog, CancellationToken.None);
        setup.SelectedInput = setup.Inputs[1];
        catalog.Inputs.Remove(Controller);

        // Act
        await setup.RefreshAsync(catalog, CancellationToken.None);

        // Assert
        Assert.AreEqual("a", setup.SelectedInput?.Id);
    }

    [TestMethod]
    public async Task Refresh_WithNoDevicesLeavesNothingSelectedAndCannotStart()
    {
        // Arrange
        var catalog = new FakeMidiInputCatalog { Inputs = { Piano } };
        var setup = new SetupState();
        await setup.RefreshAsync(catalog, CancellationToken.None);
        catalog.Inputs.Clear();

        // Act
        await setup.RefreshAsync(catalog, CancellationToken.None);

        // Assert
        Assert.IsNull(setup.SelectedInput);
        Assert.IsFalse(setup.CanStart);
        Assert.IsTrue(setup.HasNoInputs);
    }

    [TestMethod]
    public void SelectedInput_AnInputThatIsNotListedIsIgnored()
    {
        // Arrange
        var setup = new SetupState();

        // Act
        setup.SelectedInput = Piano;

        // Assert
        Assert.IsNull(setup.SelectedInput);
    }

    [TestMethod]
    public void ToSettings_CarriesEverythingTheRecorderNeeds()
    {
        // Arrange
        var setup = new SetupState { Bpm = 90, CountInBars = 2 };
        setup.PulseDenominator = 8;
        setup.StrongBeats = [true, false, false, false, true, false, false, false];

        // Act
        var settings = setup.ToSettings();

        // Assert
        Assert.AreEqual(90, settings.Bpm);
        Assert.AreEqual(8, settings.PulseDenominator);
        Assert.AreEqual((4, 4), (settings.MeterNumerator, settings.MeterDenominator));
        Assert.AreEqual(2, settings.CountInBars);
        Assert.AreEqual(8, settings.ClicksPerMeasure);
        CollectionAssert.AreEqual(setup.StrongBeats.ToArray(), settings.StrongBeats.ToArray());
    }
}
