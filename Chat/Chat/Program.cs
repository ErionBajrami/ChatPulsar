using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChatSystemNika.Model;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Newtonsoft.Json;

class Program
{
    private static IPulsarClient _pulsarClient;
    private static string name;
    private static string chatRoom;
    private static object _consumeLock = new object();
    private static bool roomchosen = false;

    static async Task Main(string[] args)
    {
        _pulsarClient = PulsarClient
            .Builder()
            .ServiceUrl(new Uri("pulsar://yourIP"))
            .Build();
        
        Console.WriteLine("Enter your name:");
        name = Console.ReadLine();

        while (true)
        {
            if (!roomchosen)
            {
                Console.WriteLine("Enter your ChatRoom (press 1 for default):");
                chatRoom = Console.ReadLine();
                if (chatRoom == "1")
                {
                    chatRoom = "default"; // Default chat room
                }
                roomchosen = true;

                _ = Task.Run(() => Consume());
            }

            await Produce();
        }
    }

    public static async Task Produce()
    {
        var pulsarProducer = _pulsarClient.NewProducer()
            .Topic($"persistent://public/default/{chatRoom}")
            .Create();

        while (true)
        {
            var msg = Console.ReadLine();
            if (msg == "changeroom")
            {
                roomchosen = false;
                break; // Exit the Produce method to change the room
            }
            
            var messageDto = new ChatMessage()
            {
                Name = name,
                Message = msg
            };
            
            var jsonData = JsonConvert.SerializeObject(messageDto);
            var data = Encoding.UTF8.GetBytes(jsonData);
            await pulsarProducer.Send(data);
        }
    }
    
    public static async Task Consume()
    {
        lock (_consumeLock)
        {
            var pulsarConsumer = _pulsarClient.NewConsumer()
                .Topic($"persistent://public/default/{chatRoom}")
                .SubscriptionName(name)
                .SubscriptionType(SubscriptionType.Shared)
                .Create();

            Thread thread = new Thread(async () =>
            {
                try
                {
                    await foreach (var message in pulsarConsumer.Messages())
                    {
                        var jsonData = Encoding.UTF8.GetString(message.Data.ToArray());
                        var msg = JsonConvert.DeserializeObject<ChatMessage>(jsonData);
                        if (msg.Name == name)
                        {
                            continue;
                        }
                        
                        Console.WriteLine($"[{msg.Name}] {msg.Message}");
                        await pulsarConsumer.Acknowledge(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while consuming messages: " + ex.Message);
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }
    }
}