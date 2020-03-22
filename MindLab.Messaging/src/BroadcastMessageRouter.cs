using System;
using System.Collections.Concurrent;
#if DEBUG
using System.Diagnostics.Contracts; 
#endif
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MindLab.Messaging.Internals;
using MindLab.Threading;

namespace MindLab.Messaging
{
    /// <summary>
    /// 广播式消息路由器
    /// </summary>
    public class BroadcastMessageRouter<TMessage> : IMessageRouter<TMessage>,
        IMessagePublisher<TMessage>, ICallbackDisposable<TMessage>
    {
        private volatile ConcurrentDictionary<AsyncMessageHandler<TMessage>, SortedListSlim<string>> m_handlers = 
            new ConcurrentDictionary<AsyncMessageHandler<TMessage>, SortedListSlim<string>>();
        private readonly AsyncReaderWriterLock m_lock = new AsyncReaderWriterLock();
        private readonly StringComparer m_keyStringComparer;
        private readonly StringHashCodeGenerator m_keyCodeGenerator;

        /// <summary>
        /// 广播式消息路由器
        /// </summary>
        public BroadcastMessageRouter()
        {
            m_keyStringComparer = StringComparer.CurrentCultureIgnoreCase;
            m_keyCodeGenerator = new StringHashCodeGenerator(m_keyStringComparer);
        }

        /// <summary>
        /// 订阅注册回调
        /// </summary>
        /// <param name="registration"></param>
        /// <param name="cancellation"></param>
        /// <returns>通过此对象取消订阅</returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="registration"/>为空
        /// </exception>
        public async Task<IAsyncDisposable> RegisterCallbackAsync(
            Registration<TMessage> registration, 
            CancellationToken cancellation = default)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            using (await m_lock.WaitForReadAsync(cancellation))
            {
                var addOk = true;

                m_handlers.AddOrUpdate(registration.Handler,
                    handler => new SortedListSlim<string>(registration.RegisterKey,
                        m_keyStringComparer,
                        m_keyCodeGenerator),
                    (handler, sortedList) =>
                    {
                        if (sortedList.TryAppend(registration.RegisterKey, out var nextList))
                        {
                            return nextList;
                        }

                        addOk = false;
                        return sortedList;
                    });

                if (!addOk)
                {
                    throw new InvalidOperationException("Don't register again !");
                }

                return new CallbackDisposer<TMessage>(this, registration);
            }
        }

        /// <summary>
        /// 向使用指定路由键<paramref name="key"/>注册的订阅者发布消息
        /// </summary>
        /// <param name="key">路由key</param>
        /// <param name="message">消息对象</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/>为空</exception>
        public async Task<MessagePublishResult> PublishMessageAsync(string key, TMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var handlers = m_handlers
                .ToArray();

            if (handlers.Length == 0)
            {
                return MessagePublishResult.None;
            }

            var result = new MessagePublishResult();

            try
            {
                var subscribers = handlers
                    .Where(pair => pair.Value.Count > 0)
                    .Select(pair =>
                    {
                        var msgArgs = new MessageArgs<TMessage>(
                            this, key,
                            pair.Value,
                            message);

                        return Task.Run(() => pair.Key(msgArgs));
                    })
                    .ToArray();

                result.ReceiverCount = (uint)subscribers.Length;
                await Task.WhenAll(subscribers);
            }
            catch (Exception e)
            {
                result.Exception = e;
            }

            return result;
        }

        async Task ICallbackDisposable<TMessage>.DisposeCallback(Registration<TMessage> registration)
        {
            var shouldCleanUp = true;
            using (await m_lock.WaitForReadAsync())
            {
                m_handlers.AddOrUpdate(registration.Handler,
                    handler => new SortedListSlim<string>(m_keyStringComparer, m_keyCodeGenerator),
                    (handler, slim) =>
                    {
                        if (!slim.TryRemove(registration.RegisterKey, out var newList))
                        {
                            shouldCleanUp = false;
                            return slim;
                        }

                        shouldCleanUp = newList.Count == 0;
                        return newList;
                    });
            }

            if (!shouldCleanUp)
            {
                return;
            }

            using (await m_lock.WaitForWriteAsync())
            {
                if (!m_handlers.TryGetValue(registration.Handler, out var list))
                {
                    return;
                }

                if (list.Count > 0)
                {
                    return;
                }

#if DEBUG
                var ok =  
#endif
                m_handlers.TryRemove(registration.Handler, out list);
#if DEBUG
                Contract.Assert(ok);
                Contract.Assert(list.Count == 0);
#endif
            }
        }
    }
}
