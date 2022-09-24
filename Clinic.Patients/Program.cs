using System.Text;
using RabbitMQ.Client;
using System.Text.Json;
using RabbitMQ.Client.Events;

var patients = new List<Patient>();

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

ListenToCustomerCreatedEvents();
ListenToCheckPatientBalance();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/patients", () => patients).WithName("GetPatients");
app.Run();

void ListenToCustomerCreatedEvents()
{
    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, eventArgs) =>
    {
        var body = eventArgs.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [Patients] {0}", message);

        var customerCreated = JsonSerializer.Deserialize<CustomerCreated>(message);
        if (customerCreated == null)
        {
            Console.WriteLine(" [Patients] empty customer created event received");
            return;
        }

        if (patients.Any(p => p.Id == customerCreated.Id))
        {
            Console.WriteLine(" [Patients] Patient with Id: '{0}' already exists", customerCreated.Id);
            return;
        }
        
        var patient = new Patient(customerCreated.Id, customerCreated.LastName, customerCreated.LastName,
            customerCreated.Address, new CardIndex(Guid.NewGuid(), "Card", new List<string>()));
        
        patients.Add(patient);
        Console.WriteLine(" [Patients] Patient created: {0}", patient);
    };

    channel.BasicConsume(queue: "clinic-patients-customer-created", autoAck: true, consumer);
    Console.WriteLine(" [Patients] Waiting for events");
}

void ListenToCheckPatientBalance()
{
    
    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, eventArgs) =>
    {
        var body = eventArgs.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [Patients] {0}", message);
        
        var checkPatientBalance = JsonSerializer.Deserialize<CheckPatientBalance>(message);
        if (checkPatientBalance == null)
        {
            Console.WriteLine(" [Patients] unable to deserialize message: '{0}'", message);
            return;
        }
        var balance = DateTime.UtcNow.Second;
        var result = new CheckPatientBalanceResult(checkPatientBalance.PatientId, balance);
        
        var responseMessage = JsonSerializer.Serialize(result);
        var responseBody = Encoding.UTF8.GetBytes(responseMessage);

        var properties = channel.CreateBasicProperties();
        properties.CorrelationId = eventArgs.BasicProperties.MessageId;
        channel.BasicPublish(exchange: "", routingKey: eventArgs.BasicProperties.ReplyTo, basicProperties: properties, responseBody);
        Console.WriteLine(" [Customers] Command Sent: {0}", result);
    };

    channel.BasicConsume(queue: "clinic-patients-patient-balance", autoAck: true, consumer);
}

record Patient(Guid Id, string FirstName, string LastName, string Address, CardIndex CardIndex);

record CardIndex(Guid Id, string Name, List<string> Records);

record CustomerCreated(Guid Id, string FirstName, string LastName, string Address, string CreditCard);

record CheckPatientBalance(Guid PatientId);

record CheckPatientBalanceResult(Guid PatientId, decimal Balance);


