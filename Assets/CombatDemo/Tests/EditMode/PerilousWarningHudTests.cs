using NUnit.Framework;
using UnityEngine;

namespace Milkfrog.CombatDemo.Tests
{
    public class PerilousWarningHudTests
    {
        [Test]
        public void WarningRefreshesImmediatelyAndClearsForPauseDeathInterruptionAndLoad()
        {
            var owner = new GameObject("PerilousWarningHudTest");
            try
            {
                var hud = owner.AddComponent<MvpHud>();
                bool hudVisible = true;
                bool warning = false;
                hud.Read = () => new MvpHudData { visible = hudVisible, perilousWarning = warning };

                hud.Refresh();
                Assert.That(hud.PerilousWarningVisible, Is.False);

                warning = true;
                Assert.That(hud.PerilousWarningVisible, Is.False, "Cached warning state should wait for the next HUD refresh.");
                hud.Refresh();
                Assert.That(hud.PerilousWarningVisible, Is.True, "The warning should appear on the same HUD refresh as attack startup.");

                warning = false;
                hud.Refresh();
                Assert.That(hud.PerilousWarningVisible, Is.False, "Cancelling the attack should clear the warning immediately.");

                warning = true;
                hud.Refresh();
                Assert.That(hud.PerilousWarningVisible, Is.True);
                foreach (string interruption in new[] { "pause", "death", "attack interruption", "load" })
                {
                    warning = false;
                    hudVisible = interruption != "pause" && interruption != "death";
                    hud.Refresh();
                    Assert.That(hud.PerilousWarningVisible, Is.False, "The warning remained after " + interruption + ".");
                    hudVisible = true;
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BundledChineseFontContainsWarningAndActionGlyphs()
        {
            Font font = Resources.Load<Font>("NotoSansSC-VF");
            Assert.That(font, Is.Not.Null, "The Noto Sans SC font must be included in player Resources.");
            foreach (char character in "危闪避")
                Assert.That(font.HasCharacter(character), Is.True, "Missing glyph: " + character);
        }
    }
}
