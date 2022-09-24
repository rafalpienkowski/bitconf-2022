using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var paymentAccounts = new List<PaymentAccount>();

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

ListenToCustomerCreatedEvents();
ListenToChargePatientCommands();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/paymentAccounts", () => paymentAccounts).WithName("PaymentAccounts");
app.Run();

void ListenToCustomerCreatedEvents()
{
    Console.WriteLine(" [Payments] Waiting for customer created events");

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [Payments] {0}", message);

        var rawObject = JObject.Parse(message);
        
        var customerIdString = rawObject["Id"]?.Value<string>();
        if (!Guid.TryParse(customerIdString, out var customerId))
        {
            Console.WriteLine(" [Patients] Invalid id provided '{0}'", customerIdString);
            return;
        }
        if (paymentAccounts.Any(p => p.Id == customerId))
        {
            Console.WriteLine(" [Patients] Patient with Id: '{0}' already exists", customerId);
            return;
        }

        var firstName = rawObject["FirstName"]?.Value<string>() ?? "unknown";
        var lastName = rawObject["LastName"]?.Value<string>() ?? "unknown";
        var address = rawObject["Address"]?.Value<string>() ?? "unknown";
        var creditCard = rawObject["CreditCard"]?.Value<string>() ?? "unknown";
        if (creditCard == "unknown")
        {
            throw new ArgumentOutOfRangeException(nameof(rawObject), "Unknonw creadit card");
        }
        var paymentAccount = new PaymentAccount(Guid.NewGuid(), customerId, firstName, lastName, address, creditCard);
        
        paymentAccounts.Add(paymentAccount);
    };
    channel.BasicConsume(queue: "clinic-payments-customer-created", autoAck: true, consumer: consumer);
}

void ListenToChargePatientCommands()
{
    Console.WriteLine(" [Payments] Waiting for charge patient commands");

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);

        var chargePatient = JsonSerializer.Deserialize<ChargePatient>(message);
        Console.WriteLine(" [Payments] {0}", chargePatient);
    };
    
    channel.BasicConsume(queue: "clinic-payments-charge-patient", autoAck: true, consumer: consumer);
}

record PaymentAccount(Guid Id, Guid PatientId, string FirstName, string LastName, string Address, string CreditCard);
record ChargePatient(Guid PatientId, decimal Amount);
