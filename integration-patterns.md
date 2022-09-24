---
marp: true
_class: lead
paginate: true
backgroundColor: #fff
---

![bg 110%](wzorce.png)

---

# Rafał Pieńkowski ![bg left:40% ](rafal_pienkowski.jpg) 

### Senio§r Backend Engineer

---

# Github project: https://github.com/rafalpienkowski/bitconf-2022

# Slack: bitconf-2022.slack.com 

---

# Integration styles

- File transfer
- Shared Database
- Remote Procedure Invocation
- Messaging

> Enterprise integration patterns G. Hohpe, B. Woolf

---

# Benefits

- Remote/Asynchronous communication
- Platform/Language integration
- Throttling
- Reliable communication
- Mediation
- Variable timing
- Loose coupling
- Scaling

---

# Costs

- **Complex programming model**
- Sequence issues
- Synchronous scenarios
- Performance
- Possible vendor lock-in

---

![bg](food-truck.jpg)

---

![bg w:500](courier.jpg)
![bg w:550](postman.jpg)

---

![bg w:500](rabbitmq.png)
![bg w:500](kafka.jpeg)

---

![bg w:500](courier.jpg)
![bg w:550](postman.jpg)

---

# Run RabbitMQ

```sh
docker run -d --hostname my-rabbit --name some-rabbit -p 8080:15672 -p 5672:5672 rabbitmq:3-management
```
---

# Craete Minimal API project

```sh
dotnet new webapi -minimal -o Clinic.Customers
```

---

# Resources

- https://www.enterpriseintegrationpatterns.com/
- https://bettersoftwaredesign.pl/episodes/34 - A autonomii zmiany w architekturze mikroserwisowej z Łukaszem Szydło
- photos from [unsplask.com](https://unsplash.com/)

---

# Feedback

[https://forms.gle/xgTjPou1sAgwaRWh6](https://forms.gle/xgTjPou1sAgwaRWh6)
![](qr.png)

![bg left:60% ](puppy.jpg) 