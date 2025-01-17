using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;

namespace TestApp.Test
{
    [TestClass]
    public sealed class RabbitMqContainerTest
    {
        private RabbitMqContainer _rabbitContainer = null!;
        private readonly string _username = "TestUser";
        private readonly string _password = "TestPassword1234";
        private readonly string _queueName = "my-test-queue";
        private readonly TaskCompletionSource<string> _taskCompletionSource = new TaskCompletionSource<string>();

        [TestInitialize]
        public async Task Setup()
        {
            _rabbitContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:4.0")
                .WithUsername(_username)
                .WithPassword(_password)
                .WithExposedPort(5672)
                .Build();

            await _rabbitContainer.StartAsync();
        }

        [TestMethod]
        public async Task Publish_Receive_Message_Over_RabbitMq()
        {
            var connectionFactory = new ConnectionFactory()
            {
                Endpoint = new AmqpTcpEndpoint(_rabbitContainer.Hostname, _rabbitContainer.GetMappedPublicPort(5672)),
                UserName = _username,
                Password = _password
            };

            await using var connection = await connectionFactory.CreateConnectionAsync();

            await using var publisherChannel = await connection.CreateChannelAsync();
            await using var consumerChannel = await connection.CreateChannelAsync();

            await consumerChannel.QueueDeclareAsync(_queueName, true, false, false);

            AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(consumerChannel);
            consumer.ReceivedAsync += ConsumerOnReceivedAsync;

            await consumerChannel.BasicConsumeAsync(_queueName, true, consumer);
            var unixTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            var basicProperties = new BasicProperties
            {
                ContentType = "application/json",
               
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(unixTime),
                Type = "TestMessage",
                AppId = Guid.NewGuid().ToString()
            };

            string message = $"Hello my friend {Guid.NewGuid()}";

            Console.WriteLine($"Publishing message '{message}'");
            await publisherChannel.BasicPublishAsync("", _queueName, false, basicProperties, Encoding.UTF8.GetBytes(message));

            var result = await _taskCompletionSource.Task;

            Assert.AreEqual(message, result);
        }

        private Task ConsumerOnReceivedAsync(object sender, BasicDeliverEventArgs @event)
        {
            string message = Encoding.UTF8.GetString(@event.Body.ToArray());
            Console.WriteLine($"RECEIVED '{message}'");
            _taskCompletionSource.SetResult(message);

            return Task.CompletedTask;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _rabbitContainer.StopAsync();
            await _rabbitContainer.DisposeAsync();
        }

    }
}
