// Writes the save-game fixture for tests/AVAMMB1.Tests/Fixtures/Saves with the game code of one release.
// Run through tools/make-save-fixture.sh, which builds this against that release's AVAMMB1.Core.
// Keep to APIs that exist in every release since 1.5, so older tags can still be rebuilt.
using AVAMMB1.Core.Content;
using AVAMMB1.Core.Dice;
using AVAMMB1.Core.Items;
using AVAMMB1.Core.Persistence;
using AVAMMB1.Core.Session;

var db = ContentDatabase.Load(new EmbeddedContentSource());
var s = new GameSession(db, new DefaultRandomSource(42));
s.NewGame(db.Config.Premades.Select(s.Factory.CreatePremade));
s.State.Flags.Add("pell_met");
s.State.Gold = 700;
s.State.MapId = "brindlemoor";
(s.State.X, s.State.Y) = (6, 13);
s.State.Party[0].Experience = 300;
s.State.Party[3].Hp = 3;
s.State.Party[2].Backpack.Add(new ItemInstance("potion_healing"));
Directory.CreateDirectory(args[0]);
new SaveGameService(args[0]).Save(1, "Fixture", "Brindlemoor (6,13) - Day 1", s.State);
