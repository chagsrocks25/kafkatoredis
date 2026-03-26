using System;
using Microsoft.Extensions.Configuration;

class Program {
    static void Main() {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        var opts = config.GetSection("Kafka").Get<KafkaToRedis.Configuration.KafkaOptions>();
        Console.WriteLine(opts?.Topic ?? "null");
    }
}
