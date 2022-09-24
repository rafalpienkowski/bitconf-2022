using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var customers = new List<Customer>();

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

ListenToCreateCustomerCommands();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/customers", () => customers).WithName("GetCustomers");
app.MapPost("/customers", SendCommand).WithName("CreateCustomer");

app.Run();

void SendCommand(CreateCustomer createCustomer)
{
    var message = JsonSerializer.Serialize(createCustomer);
    var body = Encoding.UTF8.GetBytes(message);
    
    channel.BasicPublish(exchange: "", routingKey: "clinic-customers-create-customer", basicProperties: null, body);
    Console.WriteLine(" [Customers] Command Sent: {0}", createCustomer);
}

void ListenToCreateCustomerCommands()
{
    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, eventArgs) =>
    {
        var body = eventArgs.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [Customers] {0}", message);

        var createCustomer = JsonSerializer.Deserialize<CreateCustomer>(message);
        if (createCustomer == null)
        {
            Console.WriteLine(" [Customers] empty create customer command received");
            return;
        }

        if (customers.Any(p => p.Id == createCustomer.Id))
        {
            Console.WriteLine(" [Customers] Customer with Id: '{0}' already exists", createCustomer.Id);
            return;
        }

        var customer = new Customer(createCustomer.Id, createCustomer.FirstName, createCustomer.LastName,
            createCustomer.Address, createCustomer.CreditCard);
        
        customers.Add(customer);
        Console.WriteLine(" [Customers] Customer created: {0}", customer);

        var customerCreated = new CustomerCreated(customer.Id, customer.FirstName, customer.LastName, customer.Address, customer.CreditCard);
        PublishEvent(customerCreated);
    };

    channel.BasicConsume(queue: "clinic-customers-create-customer", autoAck: true, consumer);
    Console.WriteLine(" [Customers] Waiting for commands");
}

void PublishEvent(CustomerCreated patientCreated)
{
    var message = JsonSerializer.Serialize(patientCreated);
    var body = Encoding.UTF8.GetBytes(message);
    channel.BasicPublish(exchange: "clinic-public-events",
        routingKey: "customer-created",
        basicProperties: null,
        body: body);
    Console.WriteLine(" [Customers] Sent {0}", message);
}

record Customer(Guid Id, string FirstName, string LastName, string Address, string CreditCard);

record CreateCustomer(Guid Id, string FirstName, string LastName, string Address, string CreditCard);

record CustomerCreated(Guid Id, string FirstName, string LastName, string Address, string CreditCard);
