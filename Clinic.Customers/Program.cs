using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = WebApplication.CreateBuilder(args);

var customers = new List<Customer>();

var factory = new ConnectionFactory() { HostName = "localhost" };
var connection = factory.CreateConnection();
var channel = connection.CreateModel();
ListenToCustomerCreatedCommands();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/customers", () => customers);
app.MapPost("/customers", (Customer customer) =>{

    string message = JsonSerializer.Serialize(customer);
    var body = Encoding.UTF8.GetBytes(message);

    var properties = channel.CreateBasicProperties();
    properties.Headers = new Dictionary<string, object>();
    properties.Headers.Add("app-version", "0.1-beta");
    properties.Headers.Add("full-name", typeof(Customer).AssemblyQualifiedName);

    channel.BasicPublish(exchange: "",
                            routingKey: "clinic-create-customer",
                            basicProperties: properties,
                            body: body);
    Console.WriteLine(" [x] Sent {0}", message);

    return Results.Accepted();
});

app.Run();


void ListenToCustomerCreatedCommands()
{
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var customer = JsonSerializer.Deserialize<Customer>(message);

            Console.WriteLine(" [x] Received {0}", customer);

            customers.Add(customer);
        };
            
        channel.BasicConsume(queue: "clinic-create-customer",
                                autoAck: true,
                                consumer: consumer);
}
public record Customer(Guid Id, string FistName, string LastName, string Address, string CreditCardNo);
