using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var patients = new List<Patient>();
var doctors = new List<Doctor>
{
    new(Guid.NewGuid(), "Dr Who"),
    new(Guid.NewGuid(), "Dr Doolittle")
};
var services = new List<Service>
{
    new(Guid.Parse("937db30a-612c-4402-b668-5981db65aef0"), "Visit"),
    new(Guid.Parse("c5bb2b51-7a84-4451-bcea-8635e3481c8d"), "Lab")
};
var appointments = new List<Appointment>();
var pendingChecks = new List<PendingPatientBalanceCheck>();

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

ListenToCustomerCreatedEvents();
ListenToPatientBalanceResponse();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/patients", () => patients).WithName("GetPatients");
app.MapGet("/doctors", () => doctors).WithName("GetDoctors");
app.MapGet("/services", () => services).WithName("GetServices");
app.MapGet("/appointments", () => appointments).WithName("GetAppointments");
app.MapPost("/appointments/", async (Appointment appointment) =>
{
    if (appointments.Any(a => a.Id == appointment.Id))
    {
        return Results.BadRequest($"Appointment: '{appointment.Id}' already exists");
    }

    var hasPatientMoney = await PatientHaveMoney(appointment.PatientId);
    if (!hasPatientMoney)
    {
        return Results.BadRequest($"Patient: '{appointment.PatientId}' does not have money!");
    }
    appointments.Add(appointment);
    return Results.Ok();
}).WithName("StartAppointment");

app.MapPut("/appointments/{appointmentId:guid}", (Guid appointmentId) =>
{
    var appointment = appointments.FirstOrDefault(a => a.Id == appointmentId);
    if (appointment == null)
    {
        return Results.BadRequest($"Appointment does not exist: '{appointmentId}'");
    }

    var appointmentFinished = new AppointmentFinished(appointment.Id, appointment.PatientId, appointment.DoctorId,
        appointment.ServiceId);
    
    var message = JsonSerializer.Serialize(appointmentFinished);
    var body = Encoding.UTF8.GetBytes(message);
    
    channel.BasicPublish(exchange: "clinic-public-events", routingKey: "appointment-finished", basicProperties: null, body: body);
    Console.WriteLine(" [Schedules] Sent {0}", appointmentFinished);
    
    return Results.Ok();
}).WithName("FinishAppointment");

app.Run();

void ListenToCustomerCreatedEvents()
{
    Console.WriteLine(" [Schedules] Waiting for customer created events");

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [Schedules] {0}", message);

        var customerCreated = JsonSerializer.Deserialize<CustomerCreated>(message);
        if(customerCreated == null)
        {
            Console.WriteLine(" [Schedules] empty customer created event received");
            return;
        }
        
        if (patients.Any(p => p.Id == customerCreated.Id))
        {
            Console.WriteLine(" [Schedules] Patient with Id: '{0}' already exists", customerCreated.Id);
            return;
        }
        

        var paymentAccount = new Patient(customerCreated.Id, $"{customerCreated.FirstName} {customerCreated.LastName}");
        patients.Add(paymentAccount);
    };
    channel.BasicConsume(queue: "clinic-schedules-customer-created", autoAck: true, consumer: consumer);
}

void ListenToPatientBalanceResponse()
{
    Console.WriteLine(" [Schedules] Waiting for patient balance responses");
    
    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        
        var patientBalanceResult = JsonSerializer.Deserialize<CheckPatientBalanceResult>(message);
        
        Console.WriteLine(" [Schedules] {0}", patientBalanceResult);
        if (patientBalanceResult == null)
        {
            Console.WriteLine(" [Schedules] Can't deserialize response {0}", message);
            return;
        }
        var pendingCheck = pendingChecks.FirstOrDefault(check => check.PatientId == patientBalanceResult.PatientId);
        if (pendingCheck != null)
        {
            pendingCheck.Balance = patientBalanceResult.Balance;
        }
    };

    channel.BasicConsume(queue: "clinic-schedules-patient-balance", autoAck: true, consumer: consumer);
}

async Task<bool> PatientHaveMoney(Guid patientId)
{
    var query = new CheckPatientBalance(patientId);
    var message = JsonSerializer.Serialize(query);
    var body = Encoding.UTF8.GetBytes(message);
    var messageProperties = channel.CreateBasicProperties();
    messageProperties.ReplyTo = "clinic-schedules-patient-balance";
    messageProperties.MessageId = query.PatientId.ToString();

    pendingChecks.Add(new PendingPatientBalanceCheck
    {
        PatientId = patientId,
        Balance = 0
    });
    channel.BasicPublish(exchange: "", routingKey: "clinic-patients-patient-balance", basicProperties: messageProperties, body);
    Console.WriteLine(" [Schedules] Query Sent: {0}", query);

    await Task.Delay(TimeSpan.FromSeconds(5));

    var balanceCheck = pendingChecks.FirstOrDefault(check => check.PatientId == patientId);
    if (balanceCheck == null)
    {
        return false;
    }

    var result = balanceCheck.Balance > 0;
    pendingChecks.Remove(balanceCheck);

    return result;
}

record CustomerCreated(Guid Id, string FirstName, string LastName, string Address, string CreditCard);

record Patient(Guid Id, string Name);

record Doctor(Guid Id, string Name);

record Service(Guid Id, string Name);

record Appointment(Guid Id, Guid DoctorId, Guid PatientId, Guid ServiceId);

record CheckPatientBalance(Guid PatientId);

record PendingPatientBalanceCheck
{
    public Guid PatientId { get; init; }
    public decimal Balance { get; set; }
}

record CheckPatientBalanceResult(Guid PatientId, decimal Balance);

record AppointmentFinished(Guid AppointmentId, Guid PatientId, Guid DoctorId, Guid ServiceId);
