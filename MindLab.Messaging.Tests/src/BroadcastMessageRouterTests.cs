using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MindLab.Threading;
using NUnit.Framework;
using Telerik.JustMock;

namespace MindLab.Messaging.Tests
{
    [TestFixture]
    public class BroadcastMessageRouterTests
    {
        [Test]
        public async Task PublishMessageAfterRegister_CallbackSucceed()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>(); 
            var cb = Mock.Create<AsyncMessageHandler<int>>();
            var key = string.Empty;
            var message = 15;

            Mock.Arrange(() => cb(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .MustBeCalled();
            await router.RegisterCallbackAsync(new Registration<int>(string.Empty, cb), CancellationToken.None);

            // Act
            await router.PublishMessageAsync(key, message);

            // Assert
            Mock.Assert(cb);
        }

        [Test]
        public async Task PublishMessageAfterRegister_OneReceiver()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var key = string.Empty;
            var message = 15;
            await router.RegisterCallbackAsync(
                new Registration<int>(string.Empty, args => Task.CompletedTask), 
                CancellationToken.None);

            // Act
            var result = await router.PublishMessageAsync(key, message);

            // Assert
            Assert.AreEqual(1, result.ReceiverCount);
            Assert.IsNull(result.Exception);
        }

        [Test]
        public async Task PublishMessageAfterRegister_NoException()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var key = string.Empty;
            var message = 15;
            await router.RegisterCallbackAsync(
                new Registration<int>(string.Empty, args => Task.CompletedTask),
                CancellationToken.None);

            // Act
            var result = await router.PublishMessageAsync(key, message);

            // Assert
            Assert.IsNull(result.Exception);
        }

        [Test]
        public async Task PublishMessageAfterRegister_HandlerThrowException_ResultContainException()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var key = string.Empty;
            var message = 15;
            await router.RegisterCallbackAsync(
                new Registration<int>(string.Empty, args => Task.FromException(new Exception())),
                CancellationToken.None);

            // Act
            var result = await router.PublishMessageAsync(key, message);

            // Assert
            Assert.IsNotNull(result.Exception);
        }

        [Test]
        public async Task RegisterSameActionTwice_CallbackOnlyOnce()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>(); 
            var cb = Mock.Create<AsyncMessageHandler<int>>();
            var key = string.Empty;
            var message = 15;

            Mock.Arrange(() => cb(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .Occurs(1);

            // Act
            await router.RegisterCallbackAsync(new Registration<int>("1", cb), CancellationToken.None);
            await router.RegisterCallbackAsync(new Registration<int>("2", cb), CancellationToken.None); // register twice
            await router.PublishMessageAsync(key, message);

            // Assert
            Mock.Assert(cb);
        }

        [Test]
        public async Task RegisterSameActionAndKeyTwice_ExceptionThrown()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var cb = Mock.Create<AsyncMessageHandler<int>>();
            var key = string.Empty;

            // Act
            await router.RegisterCallbackAsync(new Registration<int>(key, cb), CancellationToken.None);

            // Assert
            Assert.CatchAsync<InvalidOperationException>(() =>
                router.RegisterCallbackAsync(new Registration<int>(key, cb), CancellationToken.None));
        }

        [Test]
        public async Task PublishMessage_FirstHandlerThrow_SecondHandlerInvoked()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var cb = Mock.Create<AsyncMessageHandler<int>>();
            var key = string.Empty;
            var message = 15;

            Mock.Arrange(() => cb(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .Occurs(1);
            await router.RegisterCallbackAsync(new Registration<int>("1", args => Task.FromException(new Exception())), 
                CancellationToken.None);
            await router.RegisterCallbackAsync(new Registration<int>("2", cb), CancellationToken.None); // register twice

            // Act
            await router.PublishMessageAsync(key, message);

            // Assert
            Mock.Assert(cb);
        }

        [Test]
        public async Task PublishAfterDispose_NoCallback()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var cb = Mock.Create<AsyncMessageHandler<int>>();

            Mock.Arrange(() => cb(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .Occurs(1);

            await using (await router.RegisterCallbackAsync(new Registration<int>(string.Empty, cb), CancellationToken.None))
            {
                // Act
                await router.PublishMessageAsync(string.Empty, 1);
            }

            // Act
            await router.PublishMessageAsync(string.Empty, 1);

            // Assert
            Mock.Assert(cb);
        }

        [Test]
        public async Task PublishMessage_AllSubscribersReceived()
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var cb = Mock.Create<AsyncMessageHandler<int>>();
            var cb2 = Mock.Create<AsyncMessageHandler<int>>();
            var key = string.Empty;
            var message = 15;

            Mock.Arrange(() => cb(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .Occurs(1);
            Mock.Arrange(() => cb2(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .Occurs(1);

            await router.RegisterCallbackAsync(new Registration<int>("1", cb), CancellationToken.None);
            await router.RegisterCallbackAsync(new Registration<int>("2", cb2), CancellationToken.None);

            // Act
            await router.PublishMessageAsync(key, message);

            // Assert
            Mock.Assert(cb);
            Mock.Assert(cb2);
        }

        [Test]
        [TestCase("123", "456", 789)]
        [TestCase("", "456", 789)]
        [TestCase("123", "", 789)]
        public async Task PublishMessage_MessageArgCorrect(string bindKey, string publishKey, int message)
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();

            Task Callback(MessageArgs<int> args)
            {
                Assert.IsTrue(args.BindingKey.Contains(bindKey));
                Assert.AreEqual(publishKey, args.PublishKey);
                Assert.AreSame(router, args.FromRouter);
                Assert.AreEqual(message, args.Payload);

                return Task.CompletedTask;
            }

            await router.RegisterCallbackAsync(new Registration<int>(bindKey, Callback));
            await router.PublishMessageAsync(publishKey, message);
        }

        [Test]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        public async Task PublishMessage_BindInMultipleThreads_TotalCallback(int times)
        {
            // Arrange
            var router = new BroadcastMessageRouter<int>();
            var cb = Mock.Create<AsyncMessageHandler<int>>();

            Mock.Arrange(() => cb(Arg.IsAny<MessageArgs<int>>()))
                .Returns(Task.CompletedTask)
                .Occurs(times);

            await router.RegisterCallbackAsync(new Registration<int>(string.Empty, cb));
            var cancellationTokenSrc = new CancellationTokenSource();

            var bindTask = Task.Run(async () =>
            {
                var reg = new Registration<int>("1", args => Task.CompletedTask);

                while (!cancellationTokenSrc.IsCancellationRequested)
                {
                    await using (await router.RegisterCallbackAsync(reg, CancellationToken.None))
                    {
                        await Task.Delay(1, CancellationToken.None);
                    }
                }
            }, CancellationToken.None);

            for (int i = 0; i < times; i++)
            {
                await router.PublishMessageAsync(string.Empty, 1);
            }

            cancellationTokenSrc.Cancel();
            await bindTask;

            Mock.Assert(cb);
        }

        [Test]
        [TestCase(1,1)]
        [TestCase(1, 10)]
        [TestCase(10, 1)]
        [TestCase(4, 1)]
        [TestCase(10, 10)]
        [TestCase(100, 100)]
        public async Task PublishMessage_AllQueueReceived(int queueCount, int messageCount)
        {
            var queueTask = new Task[queueCount];
            var router = new BroadcastMessageRouter<int>();

            for (int i = 0; i < queueCount; i++)
            {
                var mq = new MessageQueue<int>();
                var binding = await mq.BindAsync(string.Empty, router);

                queueTask[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < messageCount; j++)
                    {
                        var msg = await mq.TakeMessageAsync();
                        Assert.AreEqual(j, msg.Payload);
                    }

                    Assert.IsFalse(mq.TryTakeMessage(out _));
                    await binding.DisposeAsync();
                    Assert.IsFalse(mq.TryTakeMessage(out _));
                });
            }

            for (int i = 0; i < messageCount; i++)
            {
                await router.PublishMessageAsync(string.Empty, i);
            }

            await Task.WhenAll(queueTask);
        }
        [Test]
        public async Task MultiThreadTest()
        {
            List<Task> tasks = new List<Task>();
            var locker = new AsyncReaderWriterLock();

            for (int i = 0; i < 4; ++i)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (await locker.WaitForReadAsync())
                    {
                        await Task.Delay(5);
                    }
                    using (await locker.WaitForWriteAsync())
                    {
                        await Task.Delay(5);
                    }
                }));
            }

            await Task.WhenAll(tasks.ToArray());
        }
    }
}