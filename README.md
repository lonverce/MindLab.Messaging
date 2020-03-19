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

## 示例1：消息广播

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

## 示例2：消息单播
