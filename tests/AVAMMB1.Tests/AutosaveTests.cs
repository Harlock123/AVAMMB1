using AVAMMB1.Core.Persistence;

namespace AVAMMB1.Tests;

/// <summary>Rotating autosaves, thumbnails and play time.</summary>
public class AutosaveTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "avammb1-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Autosaves_RotateThroughThreeSlots_OldestFirst()
    {
        var dir = TempDir();
        try
        {
            var s = TestContent.StartedSession();
            var saves = new SaveGameService(dir);
            var written = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                s.State.Steps = i;
                written.Add(saves.AutoSave("here", s.State));
                Thread.Sleep(20); // distinct file times
            }
            Assert.Equal([10, 11, 12, 10, 11], written);
            Assert.Equal(4, saves.Load(11).State.Steps);
            Assert.Equal(11, saves.MostRecentSlot()); // Continue picks up the newest autosave
            var autos = saves.List().Where(x => x.IsAuto).ToList();
            Assert.Equal(SaveGameService.AutoSlotCount, autos.Count);
            Assert.All(autos, a => Assert.Equal("Autosave", a.Name));
            Assert.All(Enumerable.Range(0, SaveGameService.SlotCount), slot => Assert.False(saves.Exists(slot))); // manual slots untouched
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Saves_KeepAThumbnailAndPlayTime()
    {
        var dir = TempDir();
        try
        {
            var s = TestContent.StartedSession();
            s.State.PlaySeconds = 3725;
            var saves = new SaveGameService(dir);
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
            saves.Save(2, "Pic", "here", s.State, png);
            var info = saves.List()[2];
            Assert.Equal(TimeSpan.FromSeconds(3725), info.PlayTime);
            Assert.NotNull(info.ThumbnailPath);
            Assert.Equal(png, File.ReadAllBytes(info.ThumbnailPath!));

            saves.Save(2, "No pic", "here", s.State); // overwritten without a picture: the old one goes
            Assert.Null(saves.List()[2].ThumbnailPath);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
