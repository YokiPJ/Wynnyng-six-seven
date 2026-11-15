using NUnit.Framework;
using Wynnyng.Core;

namespace Wynnyng.Tests.EditMode
{
    public sealed class PlayerCommandBufferTests
    {
        [Test]
        public void MultipleRenderFramesKeepBothQuickShotEdgesUntilTheNextFixedStep()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Push(new PlayerCommand { ShootPressed = true, ShootHeld = true, PassPressed = true });
            buffer.Push(new PlayerCommand { ShootReleased = true, MoveX = 0.8f, Sprint = 1f });
            buffer.Push(new PlayerCommand { MoveX = 0.5f, Sprint = 1f });
            PlayerCommand first = buffer.Consume();
            Assert.That(first.ShootPressed && first.ShootReleased && first.PassPressed, Is.True);
            Assert.That(first.ShootHeld, Is.False);
            Assert.That(first.MoveX, Is.EqualTo(0.5f));
            Assert.That(first.Sprint, Is.EqualTo(1f));
            PlayerCommand second = buffer.Consume();
            Assert.That(second.ShootPressed || second.ShootReleased || second.PassPressed, Is.False);
            Assert.That(second.MoveX, Is.EqualTo(0.5f));
        }
        [Test]
        public void CatchUpFixedStepsKeepHeldInputWithoutRepeatingButtons()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Push(new PlayerCommand { ShootHeld = true, ShootPressed = true,
                ThroughPressed = true, LobPressed = true, SwitchPressed = true, PausePressed = true });
            PlayerCommand first = buffer.Consume();
            PlayerCommand second = buffer.Consume();
            Assert.That(first.ShootPressed && first.ThroughPressed && first.LobPressed && first.SwitchPressed && first.PausePressed, Is.True);
            Assert.That(second.ShootHeld, Is.True);
            Assert.That(second.ShootPressed || second.ThroughPressed || second.LobPressed || second.SwitchPressed || second.PausePressed, Is.False);
        }
        [Test]
        public void PauseClearDropsGameplayButtonsAndHeldState()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Push(new PlayerCommand { ShootHeld = true, ShootReleased = true, PassPressed = true, MoveY = 1f });
            buffer.Clear();
            PlayerCommand command = buffer.Consume();
            Assert.That(command.MoveY, Is.Zero);
            Assert.That(command.ShootHeld || command.ShootReleased || command.PassPressed, Is.False);
        }
    }
}
