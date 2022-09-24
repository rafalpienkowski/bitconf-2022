
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var services = new List<Service>
{
    new(Guid.Parse("937db30a-612c-4402-b668-5981db65aef0"), "Visit", 123),
    new(Guid.Parse("c5bb2b51-7a84-4451-bcea-8635e3481c8d"), "Lab", 321)
};

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

ListenAppointmentFinished();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/services", () => services).WithName("GetServices");

app.Run();

void ListenAppointmentFinished()
{
    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, eventArgs) =>
    {
        var body = eventArgs.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        
        var appointmentFinished = JsonSerializer.Deserialize<AppointmentFinished>(message);
        Console.WriteLine(" [Services] Received {0}", appointmentFinished);
        if (appointmentFinished == null)
        {
            Console.WriteLine(" [Services] unable to deserialize message: '{0}'", message);
            return;
        }

        var service = services.FirstOrDefault(s => s.Id == appointmentFinished.ServiceId);
        if (service == null)
        {
            Console.WriteLine(" [Services] unable to find service: '{0}'", appointmentFinished.ServiceId);
            return;
        }

        var chargePatient = new ChargePatient(appointmentFinished.PatientId, service.Price);
        var responseMessage = JsonSerializer.Serialize(chargePatient);
        var bytes = Encoding.UTF8.GetBytes(responseMessage);

        channel.BasicPublish(exchange: "", routingKey: "clinic-payments-charge-customer", basicProperties: null, bytes);
        Console.WriteLine(" [Services] Command Sent: {0}", chargePatient);
    };

    channel.BasicConsume(queue: "clinic-services-appointment-finished", autoAck: true, consumer);
    Console.WriteLine(" [Services] Waiting for events");
}

record Service(Guid Id, string Name, decimal Price);

record AppointmentFinished(Guid AppointmentId, Guid PatientId, Guid DoctorId, Guid ServiceId);

record ChargePatient(Guid PatientId, decimal Amount);