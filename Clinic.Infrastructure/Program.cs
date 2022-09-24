// See https://aka.ms/new-console-template for more information

using RabbitMQ.Client;

Console.WriteLine("Setting up the infrastructure");

var factory = new ConnectionFactory
{
    HostName = "localhost"
};
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

SetupPublicExchange();
SetupCustomers();
SetupPatients();
SetupPayments();
SetupSchedules();
SetupServices();

Console.WriteLine("Infrastructure set up");

void SetupPublicExchange()
{
    channel.ExchangeDeclare(exchange: "clinic-public-events", type: ExchangeType.Topic, durable: true,
        autoDelete: false);
}

void SetupCustomers()
{ 
    channel.QueueDeclare("clinic-customers-create-customer", durable: true, exclusive: false, autoDelete: false);
}

void SetupPatients()
{
    var queueName = channel.QueueDeclare("clinic-patients-customer-created", durable: true, autoDelete: false,
        exclusive: false);
    
    channel.QueueBind(queue: queueName, exchange: "clinic-public-events", routingKey: "customer-created");

    channel.QueueDeclare("clinic-patients-patient-balance", durable: true, autoDelete: false, exclusive: false);
}

void SetupPayments()
{
    var queueName = channel.QueueDeclare("clinic-payments-customer-created", durable: true, autoDelete: false,
        exclusive: false);
    channel.QueueBind(queue: queueName, exchange: "clinic-public-events", routingKey: "customer-created");
    
    channel.QueueDeclare("clinic-payments-charge-patient", durable: true, autoDelete: false, exclusive: false);
}

void SetupSchedules()
{
    var queueName = channel.QueueDeclare("clinic-schedules-customer-created", durable: true, autoDelete: false,
        exclusive: false);
    channel.QueueBind(queue: queueName, exchange: "clinic-public-events", routingKey: "customer-created");
    
    channel.QueueDeclare("clinic-schedules-patient-balance", durable: true, autoDelete: false, exclusive: false);
}

void SetupServices()
{
    var queueName = channel.QueueDeclare("clinic-services-appointment-finished", durable: true, autoDelete: false,
        exclusive: false);
    channel.QueueBind(queue: queueName, exchange: "clinic-public-events", routingKey: "appointment-finished");
}