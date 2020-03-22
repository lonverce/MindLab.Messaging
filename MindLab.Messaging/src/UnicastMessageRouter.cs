using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
#if DEBUG
using System.Diagnostics.Contracts; 
#endif
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MindLab.Threading;

namespace MindLab.Messaging
{
    using Internals;

    /// <summary>
    /// 单播消息路由器
    /// </summary>
    /// <typeparam name="TMessage">消息类型</typeparam>
    public sealed class UnicastMessageRouter<TMessage> : IMessageRouter<TMessage>, 
        IMessagePublisher<TMessage>, ICallbackDisposable<TMessage>
    {
        #region Fields

        private readonly IEqualityComparer<AsyncMessageHandler<TMessage>> m_handlerComparer 
            = EqualityComparer<AsyncMessageHandler<TMessage>>.Default;

        private readonly IHashCodeGenerator<AsyncMessageHandler<TMessage>> m_hashCodeGenerator
            = DelegateHashCodeGenerator<AsyncMessageHandler<TMessage>>.Default;

        private readonly AsyncReaderWriterLock m_lock = new AsyncReaderWriterLock();
        private readonly ConcurrentDictionary<string, SortedListSlim<AsyncMessageHandler<TMessage>>> m_subscribers 
            = new ConcurrentDictionary<string, SortedListSlim<AsyncMessageHandler<TMessage>>>(StringComparer.CurrentCultureIgnoreCase);

        #endregion

        #region Public Methods

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
            
            if (!m_subscribers.TryGetValue(key, out var handlers) 
                || handlers.Count == 0)
            {
                return MessagePublishResult.None;
            }

            var result = new MessagePublishResult
            {
                ReceiverCount = (uint)handlers.Count
            };
            var bindings = new[] { key };

            try
            {
                var subscribers = handlers
                    .Select(func =>
                    {
                        var msgArgs = new MessageArgs<TMessage>(
                            this, key,
                            new ReadOnlyCollection<string>(bindings),
                            message);

                        return Task.Run(() => func(msgArgs));
                    })
                    .ToArray();

                await Task.WhenAll(subscribers);
            }
            catch (Exception e)
            {
                result.Exception = e;
            }

            return result;
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
        public async Task<IAsyncDisposable> RegisterCallbackAsync(Registration<TMessage> registration,
            CancellationToken cancellation = default)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            bool ok = true;

            using (await m_lock.WaitForReadAsync(cancellation))
            {
                m_subscribers.AddOrUpdate(registration.RegisterKey, 
                    key => new SortedListSlim<AsyncMessageHandler<TMessage>>(
                        registration.Handler, 
                        m_handlerComparer,
                        m_hashCodeGenerator), 
                    (key, sortedList) =>
                    {
                        if (sortedList.TryAppend(registration.Handler, out var newList))
                        {
                            return newList;
                        }

                        ok = false;
                        return sortedList;
                    });
            }

            if (!ok)
            {
                throw new InvalidOperationException("Don't register again !");
            }

            return new CallbackDisposer<TMessage>(this, registration);
        }

        #endregion

        #region Private

        async Task ICallbackDisposable<TMessage>.DisposeCallback(Registration<TMessage> registration)
        {
            bool shouldCleanUp = true;

            using (await m_lock.WaitForReadAsync())
            {
                m_subscribers.AddOrUpdate(registration.RegisterKey,
                    key => new SortedListSlim<AsyncMessageHandler<TMessage>>(
                        m_handlerComparer, m_hashCodeGenerator),
                    (key, sortedList) =>
                    {
                        if (!sortedList.TryRemove(registration.Handler, out var newList))
                        {
                            shouldCleanUp = false;
                            return sortedList;
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
                if (!m_subscribers.TryGetValue(registration.RegisterKey, out var list))
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
                    m_subscribers.TryRemove(registration.RegisterKey, out list);
#if DEBUG
                Contract.Assert(ok);
                Contract.Assert(list.Count == 0);
#endif
            }
        }

        #endregion
    }
}