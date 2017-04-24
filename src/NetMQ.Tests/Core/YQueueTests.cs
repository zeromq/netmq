using NetMQ.Core.Utils;
using Xunit;

namespace NetMQ.Tests.Core
{
    public class YQueueTests
    {
        [Fact]
        public void PushingToQueueShouldIncreaseBackPosition()
        {
            string one = "One";
            string two = "Two";
            string three = "Three";

            var queue = new YQueue<string>(100);
            Assert.Equal(0, queue.BackPos);
            queue.Push(ref one);
            Assert.Equal(1, queue.BackPos);
            queue.Push(ref two);
            Assert.Equal(2, queue.BackPos);
            queue.Push(ref three);
            Assert.Equal(3, queue.BackPos);
        }

        [Fact]
        public void PoppingFromQueueShouldIncreaseFrontPosition()
        {
            var queue = new YQueue<string>(100);

            string one = "One";
            string two = "Two";
            string three = "Three";

            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
            Assert.Equal(0, queue.FrontPos);
            queue.Pop();
            Assert.Equal(1, queue.FrontPos);
            queue.Pop();
            Assert.Equal(2, queue.FrontPos);
            queue.Pop();
            Assert.Equal(3, queue.FrontPos);
        }

        [Fact]
        public void QueuedItemsShouldBeReturned()
        {
            string one = "One";
            string two = "Two";
            string three = "Three";

            var queue = new YQueue<string>(100);
            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
            Assert.Equal("One", queue.Pop());
            Assert.Equal("Two", queue.Pop());
            Assert.Equal("Three", queue.Pop());
        }

        [Fact]
        public void SmallChunkSizeShouldNotAffectBehavior()
        {
            string one = "One";
            string two = "Two";
            string three = "Three";
            string four = "Four";
            string five = "Five";

            var queue = new YQueue<string>(2);
            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
            queue.Push(ref four);
            queue.Push(ref five);
            Assert.Equal("One", queue.Pop());
            Assert.Equal("Two", queue.Pop());
            Assert.Equal("Three", queue.Pop());
            Assert.Equal("Four", queue.Pop());
            Assert.Equal("Five", queue.Pop());
            // On empty queue the front position should be equal to back position
            Assert.Equal(queue.FrontPos, queue.BackPos);
        }

        [Fact]
        public void UnpushShouldRemoveLastPushedItem()
        {
            string one = "One";
            string two = "Two";
            string three = "Three";

            var queue = new YQueue<string>(2);
            queue.Push(ref one);
            queue.Push(ref two);
            queue.Push(ref three);
            // Back position should be decremented after unpush
            Assert.Equal(3, queue.BackPos);
            // Unpush should return the last item in a queue
            Assert.Equal("Three", queue.Unpush());
            Assert.Equal(2, queue.BackPos);
            Assert.Equal("Two", queue.Unpush());
            Assert.Equal(1, queue.BackPos);
            Assert.Equal("One", queue.Unpush());
            Assert.Equal(0, queue.BackPos);
        }
    }
}
