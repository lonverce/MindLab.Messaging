# MindLab.Messaging
**MindLab.Messaging** 提供了一个轻量级的消息订阅/发布模式（进程内），支持消息单播、广播，所有接口均为使用**async/await**形式的**Task**异步接口

## 使用
用户可从nuget下载 [MindLab.Messaging](https://www.nuget.org/packages/MindLab.Messaging)

## 组件设计
**MindLab.Messaging** 参考了**RdbbitMQ**的组件设计，并根据C#自身的使用习惯做了调整，主要包含两个组件：**路由器**（MessageRouter）和**队列**（MessageQueue）。

**队列**可以**绑定**到指定的**路由器**上，它们之间是多对多关系，即一个队列可同时绑定多个路由器，一个路由器也可同时被多个队列绑定。

**队列**在**绑定**时需提供一个**key**作为消息路由的过滤依据，该key由**路由器**内部识别。

**发布者**通过**路由器**发布消息，发布时需提供一个路由键routingKey, **路由器**内部根据自身的策略将该消息转发到符合条件的队列中。

**消费者**通过**队列**接收并消费消息。

## 入门
### 示例1：消息广播

从一个简单的例子开始，我们将创建一个用于广播消息的路由器（BroadcastMessageRouter）,并模拟两个消费者分别从两条队列中消费消息, 消费者A将消息打印到屏幕上, 消费者B将消息输出到文件中
```csharp
private readonly var m_router = new BroadcastMessageRouter<string>(); // 广播式路由器

public async Task RunAsync()
{
    using var tokenSrc = new CancellationTokenSource();
    var t1 = Task.Run(async () => await ConsumerA(tokenSrc.Token));
    var t2 = Task.Run(async () => await ConsumerB(tokenSrc.Token));
    
    while(true)
    {
        var txt = Console.ReadLine();
        if(string.Equals("exit", txt))
        {
            break;
        }
        
        // 发布消息txt
        await m_router.PublishMessageAsync(string.Empty, txt);
    }
    tokenSrc.Cancel();
    await Task.WhenAll(t1, t2);
}

private async Task ConsumerA(CancellationToken token)
{
    var queueA = new AsyncMessageQueue<string>(); // 队列A, 供消费者A使用
    await using(await queueA.BindAsync(string.Empty, m_router, token)) // 绑定队列A到路由器
    {
        while(true)
        {
            var msg = await queueA.TakeMessageAsync(token); // 等待消息
            Console.WriteLine(msg.Payload);
        }
    }
}

private async Task ConsumerB(CancellationToken token)
{
    var queueB = new AsyncMessageQueue<string>(); // 队列B, 供消费者B使用
    await using(await queueA.BindAsync(string.Empty, m_router, token)) // 绑定队列B到路由器
    {
        while(true)
        {
            var msg = await queueA.TakeMessageAsync(token); // 等待消息
            File.AppendText("1.txt", msg.Payload, token); // 输出到文件
        }
    }
}
```

上述代码中，队列绑定时使用的key和消息发布时的key都使用了**string.Empty**，这是因为**BroadcastMessageRouter**在内部策略上无视了key的区别，仅是单纯地将消息广播至其绑定的所有队列中。

### 示例2：消息单播
在这个例子中，我们将创建一个用于单播消息的路由器（UnicastMessageRouter）并演示如何利用路由Key把消息发布给指定的一个或一组队列。

```csharp
private readonly var m_router = new UnicastMessageRouter<string>(); // 单播式路由器
private readonly string[] m_keys = new []{"ConsumerA", "ConsumerB"};

public async Task RunAsync()
{
    using var tokenSrc = new CancellationTokenSource();
    var t1 = Task.Run(async () => await ConsumerA(tokenSrc.Token));
    var t2 = Task.Run(async () => await ConsumerB(tokenSrc.Token));
    
    int i = 0;
    while(true)
    {
        var txt = Console.ReadLine();
        if(string.Equals("exit", txt))
        {
            break;
        }
        
        // 依次轮流使用两个不同的key
        var key = m_keys[i = 1-i];
        
        // 发布消息txt
        await m_router.PublishMessageAsync(key, txt);
    }
    tokenSrc.Cancel();
    await Task.WhenAll(t1, t2);
}

private async Task ConsumerA(CancellationToken token)
{
    var queueA = new AsyncMessageQueue<string>(); // 队列A, 供消费者A使用
    await using(await queueA.BindAsync(m_keys[0], m_router, token)) // 使用m_keys[0]绑定队列A到路由器
    {
        while(true)
        {
            var msg = await queueA.TakeMessageAsync(token); // 等待消息
            Console.WriteLine($"ConsumerA: {msg.Payload}");
        }
    }
}

private async Task ConsumerB(CancellationToken token)
{
    var queueB = new AsyncMessageQueue<string>(); // 队列B, 供消费者B使用
    await using(await queueA.BindAsync(m_keys[1], m_router, token)) // 使用m_keys[1]绑定队列B到路由器
    {
        while(true)
        {
            var msg = await queueA.TakeMessageAsync(token); // 等待消息
            Console.WriteLine($"ConsumerB: {msg.Payload}");
        }
    }
}
```

## 高级

### 消息分派
当使用Router发布消息时，消息被并行地插入到关联的队列内部，并立刻从异步方法中返回。此时，消息只是被缓存到队列中，并不意味着已经被消费者处理。

从发布者的立场而言，它并不知道该消息是否已被处理。与传统的基于委托的event不同，event总是同步且串行地触发回调方法，这会导致发布者被阻塞。同时，一旦某一个回调中出现异常，更会阻断整个业务流程并影响同一event的后继订阅者。

Router内部使用并行触发的方式公平地对待每个关联的消息队列，各个队列间是互不干扰的，如果发布者确实需要知道消息的转发情况，可以从**PublishMessageAsync()** 方法的返回值中获取相关信息。

```csharp
var result = await m_router.PublishMessageAsync(key, txt);
Console.WriteLine(result.ReceiverCount); // 接收到此消息的订阅者数量

if (result.Exception != null) // 转发消息时产生的异常
{
    Console.WriteLine(result.Exception);
}
```

### 队列容量与满载处理
如果消息发布者无节制地发布消息，远远超过了消费者处理消息的速度，那么，这些消息都会被缓存到队列中，这会导致内存占用过高，甚至造成奔溃。为了避免这种情况，我们可以在构造队列时显式指定队列容量与满载处理的方式。

```csharp
var mq = new AsyncMessageQueue<string>(8192, QueueFullBehaviour.BlockPublisher);
```
上面的代码指定队列最大容量为8192，当容量满载时，新插入消息会导致 **PublishMessageAsync()** 方法发生阻塞，直至消息从队列中被取走，这能反向抑制发布者的发布速度。在默认的无参数构造队列时，内部使用上述的两个值作为默认参数。

### 仅关注最新消息
设想一个常见的业务场景：位于后台的线程在下载一个大文件，并把下载进度以消息的形式发布到路由器；UI线程从队列中消费此进度消息，并更新到界面上的进度条控件。

在这样的业务场景，消费方（UI线程）其实并不需要处理队列中的每一条进度消息，而只需要关注最新一条的进度消息，为了达到这样的目的，我们可以使用如下方式构造队列

```csharp
var mq = new AsyncMessageQueue<string>(1, QueueFullBehaviour.RemoveOldest);
```

通过把满载处理方式设置为**QueueFullBehaviour.RemoveOldest**, 使得队列在满载时自动移除队列中最老的一条消息，然后再把新消息插入队列。同时该队列的容量又设置为1，这样一来，队列中永远都只有最新一条的消息。
