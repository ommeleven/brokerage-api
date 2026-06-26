# .NET Fintech Learning Guide — From C# Basics to a Containerized Brokerage API

> **Who this is for:** You know Java and the basics of C#. You're now a .NET developer at a brokerage/investment platform. This guide teaches the concepts you listed by **building one real project** and adding complexity level by level.

> **How to use this:** Read a section, then type the code yourself (don't copy-paste — you learn by typing). Each level builds on the previous one. By the end you'll have a working brokerage API running in a container with a real database.

**The project we'll build:** `BrokerageApi` — a service that manages investment **accounts**, **holdings** (the stocks/assets a client owns), and lets clients **place trades** (buy/sell orders). This mirrors what a broker platform actually does.

**The progression you asked for:**

```
C# fundamentals  →  API  →  N-tier architecture  →  Dependency Injection
   →  Interfaces  →  Generics  →  Database  →  Docker image
   →  Container registry  →  Host in a Container App
```

---

## Prerequisites — install these first

```bash
# Check if .NET is installed (you want .NET 8 or 9)
dotnet --version

# If not installed, on macOS:
brew install --cask dotnet-sdk

# Docker Desktop (we need it for the later levels)
brew install --cask docker
```

You'll also want the **C# Dev Kit** extension in VS Code.

---

# Level 0 — C# Fundamentals (the things that differ from Java)

Before the project, let's nail the language concepts you listed. Coming from Java, **most of this will feel familiar** — I'll focus on the differences and the C# idioms.

Create a throwaway console app to experiment:

```bash
mkdir -p ~/src/c#/playground && cd ~/src/c#/playground
dotnet new console -n Fundamentals
cd Fundamentals
dotnet run   # prints "Hello, World!"
```

Open `Program.cs` and replace the contents as we go through each concept below.

## 0.1 — Class, Object

A **class** is a blueprint. An **object** is an instance of that blueprint. Identical concept to Java.

```csharp
// A class — the blueprint
public class Stock
{
    // Properties (C# idiom — NOT fields with getters/setters like Java)
    public string Symbol { get; set; }
    public decimal Price { get; set; }

    // Constructor
    public Stock(string symbol, decimal price)
    {
        Symbol = symbol;
        Price = price;
    }

    // Method
    public decimal ValueFor(int shares) => Price * shares;
}
```

```csharp
// An object — an instance
var apple = new Stock("AAPL", 192.50m);
Console.WriteLine(apple.ValueFor(10));   // 1925.0
```

**Key differences from Java:**

| Java | C# |
|------|-----|
| `private double price; public double getPrice() {...}` | `public decimal Price { get; set; }` (auto-property) |
| `getPrice()` / `setPrice()` | just `obj.Price` — properties look like fields but can have logic |
| `double` for money | **`decimal`** for money — `double` has rounding errors, never use it for currency |
| `PascalCase` methods, `camelCase` fields | **`PascalCase` for everything public** (methods, properties), `camelCase` for locals/params |
| `192.50` | `192.50m` — the `m` suffix means `decimal` literal |

> 💡 **Fintech rule #1:** Always use `decimal` for money and prices. `double`/`float` will give you `0.1 + 0.2 = 0.30000000000000004` bugs that are unacceptable in finance.

## 0.2 — Properties in depth

Properties are C#'s big win over Java's getter/setter boilerplate.

```csharp
public class Account
{
    // Auto-property — compiler generates the backing field
    public string Id { get; set; }

    // Read-only from outside, settable only in constructor
    public string OwnerName { get; private set; }

    // Computed property (no backing field, like a getter-only method)
    public decimal Balance { get; private set; }
    public bool IsOverdrawn => Balance < 0;   // expression-bodied property

    // Property with validation logic
    private decimal _creditLimit;
    public decimal CreditLimit
    {
        get => _creditLimit;
        set
        {
            if (value < 0) throw new ArgumentException("Credit limit cannot be negative");
            _creditLimit = value;
        }
    }

    public Account(string id, string ownerName)
    {
        Id = id;
        OwnerName = ownerName;
        Balance = 0m;
    }
}
```

## 0.3 — `var` (implicit typing)

`var` tells the compiler "figure out the type for me." It is **still strongly typed** — this is NOT like JavaScript's `var` or a dynamic type. The type is fixed at compile time.

```csharp
var symbol = "AAPL";          // compiler infers string
var price = 192.50m;          // compiler infers decimal
var shares = 100;             // compiler infers int
var apple = new Stock("AAPL", 192.50m);  // compiler infers Stock

// These are IDENTICAL in behavior:
var account1 = new Account("A1", "Alice");
Account account2 = new Account("A2", "Bob");
```

**When to use `var`:**
- ✅ When the type is obvious from the right side: `var account = new Account(...)`
- ✅ For long generic types: `var holdings = new Dictionary<string, List<Holding>>();`
- ❌ Avoid when the type isn't obvious: `var x = GetData();` — what's `x`? Prefer explicit.

`var` is purely about readability — it compiles to the exact same thing as the explicit type.

## 0.4 — Interfaces

An **interface** is a contract: "any class that implements me promises to provide these members." Same concept as Java, with C# syntax conventions.

```csharp
// Interface — note the "I" prefix convention (IList, IEnumerable, etc.)
public interface IPriceFeed
{
    decimal GetPrice(string symbol);
}

// A class implementing the interface
public class StubPriceFeed : IPriceFeed
{
    public decimal GetPrice(string symbol) => 100.00m;  // fake price for testing
}

// Another implementation
public class LivePriceFeed : IPriceFeed
{
    public decimal GetPrice(string symbol)
    {
        // would call a real market data API here
        return 192.50m;
    }
}
```

```csharp
// Code that depends on the INTERFACE, not the concrete class
IPriceFeed feed = new StubPriceFeed();
Console.WriteLine(feed.GetPrice("AAPL"));   // 100.00

feed = new LivePriceFeed();                 // swap implementation, same variable type
Console.WriteLine(feed.GetPrice("AAPL"));   // 192.50
```

**Why interfaces matter (this is the heart of everything later):** Your code depends on `IPriceFeed` (the contract), not on `LivePriceFeed` (the concrete thing). You can swap a fake one in for testing, or change vendors, without touching the code that uses it. This is what makes **Dependency Injection** powerful — we'll get there.

**Java → C# differences:**
- Convention: prefix interfaces with `I` (`IPriceFeed`, not `PriceFeed`).
- C# interfaces can have default implementations (like Java 8+), but you'll rarely need them.
- No `implements` keyword — C# uses `:` for both inheritance and interface implementation.

## 0.5 — Generics

Generics let you write code that works with **any type** while staying type-safe. Same idea as Java generics, cleaner syntax, and **no type erasure** (C# generics know their type at runtime — a real improvement over Java).

```csharp
// A generic class — T is a type parameter
public class Repository<T>
{
    private readonly List<T> _items = new();

    public void Add(T item) => _items.Add(item);
    public T? GetAt(int index) => index < _items.Count ? _items[index] : default;
    public IEnumerable<T> All() => _items;
}
```

```csharp
// Use it with any type — type-safe, no casting
var stockRepo = new Repository<Stock>();
stockRepo.Add(new Stock("AAPL", 192.50m));

var accountRepo = new Repository<Account>();
accountRepo.Add(new Account("A1", "Alice"));

// stockRepo.Add(new Account(...));  // ❌ compile error — type safety!
```

**Generic constraints** — restrict what `T` can be:

```csharp
// T must implement IIdentifiable (so we can use .Id)
public interface IIdentifiable { string Id { get; } }

public class Repository<T> where T : IIdentifiable
{
    private readonly Dictionary<string, T> _items = new();
    public void Add(T item) => _items[item.Id] = item;   // we can use item.Id because of the constraint
    public T? GetById(string id) => _items.GetValueOrDefault(id);
}
```

Common built-in generics you'll use constantly: `List<T>`, `Dictionary<TKey, TValue>`, `IEnumerable<T>`, `Task<T>`, `Nullable<T>` (aka `T?`).

---

**✅ Level 0 checkpoint.** Type out all the examples above in your `playground` project and run them. Make sure you can explain: the difference between a class and an object, why we use `decimal` for money, what `var` actually does, why an interface is a "contract," and how a generic gives you type safety. When that clicks, move on.

---

# Level 1 — Build the API (the starting point)

Now the real project. We start with the **simplest possible Web API** — everything in one file. We'll refactor it into proper architecture in the next levels. Starting messy on purpose lets you *feel* why the structure matters.

```bash
cd ~/src/c#
dotnet new webapi -n BrokerageApi --use-controllers
cd BrokerageApi
dotnet run
```

> The `webapi` template gives you ASP.NET Core. Think of it as Spring Boot's equivalent — built-in web server (Kestrel), routing, JSON serialization, and a DI container out of the box.

You'll see it start on a `https://localhost:xxxx` URL. Open `https://localhost:xxxx/swagger` in a browser — **Swagger UI** is auto-generated API docs you can test from.

## 1.1 — A first controller

A **controller** handles HTTP requests. Like a Spring `@RestController`. Create `Controllers/AccountsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace BrokerageApi.Controllers;

[ApiController]
[Route("api/[controller]")]   // route becomes "api/accounts"
public class AccountsController : ControllerBase
{
    // In-memory storage FOR NOW (we'll replace with a database later)
    private static readonly List<Account> _accounts = new()
    {
        new Account { Id = "A1", OwnerName = "Alice", Balance = 10000m },
        new Account { Id = "A2", OwnerName = "Bob",   Balance = 5000m },
    };

    [HttpGet]                          // GET /api/accounts
    public IEnumerable<Account> GetAll() => _accounts;

    [HttpGet("{id}")]                  // GET /api/accounts/A1
    public ActionResult<Account> GetById(string id)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == id);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost]                         // POST /api/accounts
    public ActionResult<Account> Create(Account account)
    {
        _accounts.Add(account);
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }
}

// Simple model for now — same file to keep it easy; we'll move it later
public class Account
{
    public string Id { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public decimal Balance { get; set; }
}
```

Run `dotnet run`, open Swagger, and try the three endpoints.

**What just happened (Java/Spring comparison):**

| Concept | Spring | ASP.NET Core |
|---------|--------|--------------|
| Mark a controller | `@RestController` | `[ApiController]` |
| Base route | `@RequestMapping("/api/accounts")` | `[Route("api/[controller]")]` |
| GET handler | `@GetMapping` | `[HttpGet]` |
| Path variable | `@PathVariable` | `{id}` in route + method param |
| Request body | `@RequestBody` | just a method parameter (auto-bound) |
| Return 404 | `ResponseEntity.notFound()` | `NotFound()` |

**The problem with this code:** business logic, data storage, and HTTP handling are all jammed into the controller. It works, but it's untestable and unmaintainable. That's exactly what N-tier architecture fixes.

---

**✅ Level 1 checkpoint.** You have a running API with GET/POST endpoints, tested via Swagger. You understand controllers, routes, and HTTP verb attributes.

---

# Level 2 — N-Tier Architecture

**N-tier** means splitting your app into layers, each with one responsibility, where each layer only talks to the one below it. The classic three tiers:

```
┌─────────────────────────────────────────┐
│  Presentation / API layer (Controllers)  │   ← handles HTTP, no business logic
├─────────────────────────────────────────┤
│  Business / Service layer (Services)      │   ← the rules: "can this trade execute?"
├─────────────────────────────────────────┤
│  Data Access layer (Repositories)         │   ← reads/writes storage
└─────────────────────────────────────────┘
              ↓
        Database / storage
```

**Why bother?** Each layer can be tested, changed, and understood independently. Swap the database without touching business rules. Test business rules without a web server. This is the single most important structural concept for a professional .NET codebase.

In .NET we usually split these into **separate projects** in one **solution** (`.sln`). A solution is just a container for multiple projects — like a Maven multi-module project.

## 2.1 — Restructure into projects

```bash
cd ~/src/c#
# Remove the old single project and start a clean solution
# (back up BrokerageApi first if you want to keep your experiments)

mkdir BrokerageSolution && cd BrokerageSolution
dotnet new sln -n Brokerage

# Create the layers as separate projects
dotnet new webapi  -n Brokerage.Api      --use-controllers   # presentation
dotnet new classlib -n Brokerage.Core                        # domain models + interfaces
dotnet new classlib -n Brokerage.Services                    # business logic
dotnet new classlib -n Brokerage.Data                        # data access

# Add all projects to the solution
dotnet sln add Brokerage.Api Brokerage.Core Brokerage.Services Brokerage.Data

# Wire up references (who can see whom). Dependencies point DOWNWARD:
dotnet add Brokerage.Api      reference Brokerage.Services Brokerage.Core
dotnet add Brokerage.Services reference Brokerage.Core Brokerage.Data
dotnet add Brokerage.Data     reference Brokerage.Core
```

The dependency direction matters:

```
Brokerage.Api  →  Brokerage.Services  →  Brokerage.Data
       ↘               ↓                    ↙
              Brokerage.Core (models + interfaces — everyone depends on it, it depends on nothing)
```

`Core` is the center — it holds the **domain models** and **interface contracts**. Nobody else's concrete code leaks into it. This is the foundation that makes DI and interfaces (next levels) clean.

## 2.2 — Core layer: the domain model

Create `Brokerage.Core/Models/Account.cs`:

```csharp
namespace Brokerage.Core.Models;

public class Account
{
    public string Id { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public decimal Balance { get; set; }
}
```

Create `Brokerage.Core/Models/Holding.cs` (a position the client owns):

```csharp
namespace Brokerage.Core.Models;

public class Holding
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Symbol { get; set; } = "";   // e.g. "AAPL"
    public int Shares { get; set; }
    public decimal AverageCost { get; set; }    // what they paid per share
}
```

## 2.3 — Data layer: storage

Create `Brokerage.Data/AccountStore.cs` (in-memory for now — the database comes in Level 5):

```csharp
using Brokerage.Core.Models;

namespace Brokerage.Data;

public class AccountStore
{
    private readonly List<Account> _accounts = new()
    {
        new Account { Id = "A1", OwnerName = "Alice", Balance = 10000m },
        new Account { Id = "A2", OwnerName = "Bob",   Balance = 5000m },
    };

    public IEnumerable<Account> GetAll() => _accounts;
    public Account? GetById(string id) => _accounts.FirstOrDefault(a => a.Id == id);
    public void Add(Account account) => _accounts.Add(account);
    public void Update(Account account)
    {
        var existing = GetById(account.Id);
        if (existing is not null) existing.Balance = account.Balance;
    }
}
```

## 2.4 — Services layer: business logic

Create `Brokerage.Services/AccountService.cs`. This is where the **rules** live:

```csharp
using Brokerage.Core.Models;
using Brokerage.Data;

namespace Brokerage.Services;

public class AccountService
{
    private readonly AccountStore _store;

    public AccountService(AccountStore store)   // we pass in the data layer (foreshadowing DI!)
    {
        _store = store;
    }

    public IEnumerable<Account> GetAllAccounts() => _store.GetAll();

    public Account? GetAccount(string id) => _store.GetById(id);

    // A business RULE — deposit money, with validation
    public Account Deposit(string accountId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive");

        var account = _store.GetById(accountId)
            ?? throw new InvalidOperationException($"Account {accountId} not found");

        account.Balance += amount;
        _store.Update(account);
        return account;
    }

    // Another rule — withdraw, but never below zero
    public Account Withdraw(string accountId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive");

        var account = _store.GetById(accountId)
            ?? throw new InvalidOperationException($"Account {accountId} not found");

        if (account.Balance < amount)
            throw new InvalidOperationException("Insufficient funds");

        account.Balance -= amount;
        _store.Update(account);
        return account;
    }
}
```

Notice: the **controller will have no business logic**, the **service has no HTTP knowledge**, and the **store has no rules**. Each layer does one job.

## 2.5 — API layer: a thin controller

Replace `Brokerage.Api/Controllers` with `AccountsController.cs`:

```csharp
using Brokerage.Core.Models;
using Brokerage.Services;
using Brokerage.Data;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    // For NOW we new it up manually. Level 3 (DI) fixes this ugliness.
    private readonly AccountService _service = new(new AccountStore());

    [HttpGet]
    public IEnumerable<Account> GetAll() => _service.GetAllAccounts();

    [HttpGet("{id}")]
    public ActionResult<Account> GetById(string id)
    {
        var account = _service.GetAccount(id);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost("{id}/deposit")]
    public ActionResult<Account> Deposit(string id, [FromBody] decimal amount)
    {
        try { return Ok(_service.Deposit(id, amount)); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id}/withdraw")]
    public ActionResult<Account> Withdraw(string id, [FromBody] decimal amount)
    {
        try { return Ok(_service.Withdraw(id, amount)); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
```

```bash
cd Brokerage.Api
dotnet run
```

Test the deposit/withdraw endpoints in Swagger. Try withdrawing more than the balance — you'll get a clean `400 Bad Request`.

**The remaining ugliness:** `new AccountService(new AccountStore())` inside the controller. The controller shouldn't know *how to build* its dependencies. That's the exact problem Dependency Injection solves.

---

**✅ Level 2 checkpoint.** You have 4 projects with clean layering. You can explain what each layer does and why dependencies point toward `Core`. Business rules live in the service, not the controller.

---

# Level 3 — Dependency Injection (DI)

**The problem:** Right now the controller does `new AccountService(new AccountStore())`. It's responsible for constructing its own dependencies. That means:
- You can't swap `AccountStore` for a test double.
- If `AccountService`'s constructor changes, every controller breaks.
- Every controller makes its own `AccountStore`, so they don't share state.

**Dependency Injection** flips this around: instead of a class *creating* its dependencies, the dependencies are *handed to it* ("injected"). A central **container** is responsible for building and supplying everything. This is identical in spirit to Spring's `@Autowired` / application context — ASP.NET Core has DI built in, no library needed.

**Inversion of Control (IoC):** the class no longer controls *how* its dependencies are made — it just declares *what* it needs in its constructor. Control is inverted to the container.

## 3.1 — Register services in the container

Open `Brokerage.Api/Program.cs`. You'll see something like `builder.Services.AddControllers();`. Add your registrations:

```csharp
using Brokerage.Data;
using Brokerage.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Register OUR services with the DI container ──
builder.Services.AddSingleton<AccountStore>();    // one shared instance for the app's lifetime
builder.Services.AddScoped<AccountService>();      // one instance per HTTP request

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

**Service lifetimes** (how long an instance lives — important to understand):

| Lifetime | Meaning | Use for |
|----------|---------|---------|
| `AddSingleton` | One instance for the whole application | Caches, in-memory stores, stateless config |
| `AddScoped` | One instance per HTTP request | Most services, database contexts |
| `AddTransient` | A new instance every time it's requested | Lightweight, stateless helpers |

> We use `Singleton` for `AccountStore` *only* because it's our temporary in-memory store and we want the data to persist across requests. Once we add a real database (Level 5), the data layer becomes `Scoped`.

## 3.2 — Let the container inject into the controller

Now the controller just *asks* for what it needs in its constructor — the container provides it:

```csharp
using Brokerage.Core.Models;
using Brokerage.Services;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly AccountService _service;

    // The container sees this constructor and injects an AccountService automatically.
    // No more "new" anywhere!
    public AccountsController(AccountService service)
    {
        _service = service;
    }

    [HttpGet]
    public IEnumerable<Account> GetAll() => _service.GetAllAccounts();

    [HttpGet("{id}")]
    public ActionResult<Account> GetById(string id)
    {
        var account = _service.GetAccount(id);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost("{id}/deposit")]
    public ActionResult<Account> Deposit(string id, [FromBody] decimal amount)
    {
        try { return Ok(_service.Deposit(id, amount)); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id}/withdraw")]
    public ActionResult<Account> Withdraw(string id, [FromBody] decimal amount)
    {
        try { return Ok(_service.Withdraw(id, amount)); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
```

**The chain of injection that now happens automatically:**

```
Container builds AccountsController
   → needs AccountService
       → AccountService needs AccountStore
           → Container provides the singleton AccountStore
```

You declared *what* you need; the container figured out *how* to build the whole graph. That's DI.

Run it — everything works the same, but now there's zero `new` for your dependencies, and state is shared correctly through the singleton store.

---

**✅ Level 3 checkpoint.** You can register a service with the right lifetime and explain Singleton vs Scoped vs Transient. You understand that DI = "declare dependencies in the constructor, the container supplies them." No more `new` for services.

---

# Level 4 — Interfaces + Generics (making it swappable & reusable)

DI becomes truly powerful when classes depend on **interfaces** instead of concrete types. Then you can swap implementations (real vs. test, vendor A vs. vendor B) just by changing one line of registration. This level combines two of your topics: **interfaces** and **generics**.

## 4.1 — Define interface contracts in Core

Create `Brokerage.Core/Interfaces/IAccountRepository.cs`:

```csharp
using Brokerage.Core.Models;

namespace Brokerage.Core.Interfaces;

public interface IAccountRepository
{
    IEnumerable<Account> GetAll();
    Account? GetById(string id);
    void Add(Account account);
    void Update(Account account);
}
```

Create `Brokerage.Core/Interfaces/IPriceFeed.cs` (we'll inject market prices):

```csharp
namespace Brokerage.Core.Interfaces;

public interface IPriceFeed
{
    decimal GetPrice(string symbol);
}
```

## 4.2 — Implement the interfaces in the data layer

Update `Brokerage.Data/AccountStore.cs` to implement the interface:

```csharp
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;

namespace Brokerage.Data;

public class InMemoryAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new()
    {
        new Account { Id = "A1", OwnerName = "Alice", Balance = 10000m },
        new Account { Id = "A2", OwnerName = "Bob",   Balance = 5000m },
    };

    public IEnumerable<Account> GetAll() => _accounts;
    public Account? GetById(string id) => _accounts.FirstOrDefault(a => a.Id == id);
    public void Add(Account account) => _accounts.Add(account);
    public void Update(Account account)
    {
        var existing = GetById(account.Id);
        if (existing is not null) existing.Balance = account.Balance;
    }
}
```

Create `Brokerage.Data/StubPriceFeed.cs`:

```csharp
using Brokerage.Core.Interfaces;

namespace Brokerage.Data;

public class StubPriceFeed : IPriceFeed
{
    // Hardcoded prices for now — swap for a real market data API later
    private readonly Dictionary<string, decimal> _prices = new()
    {
        ["AAPL"] = 192.50m,
        ["MSFT"] = 415.00m,
        ["TSLA"] = 245.30m,
    };

    public decimal GetPrice(string symbol) =>
        _prices.GetValueOrDefault(symbol, 0m);
}
```

## 4.3 — Service depends on the interface, not the concrete class

Update `Brokerage.Services/AccountService.cs`:

```csharp
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;

namespace Brokerage.Services;

public class AccountService
{
    private readonly IAccountRepository _repo;   // interface, not InMemoryAccountRepository

    public AccountService(IAccountRepository repo)
    {
        _repo = repo;
    }

    public IEnumerable<Account> GetAllAccounts() => _repo.GetAll();
    public Account? GetAccount(string id) => _repo.GetById(id);

    public Account Deposit(string accountId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Deposit amount must be positive");
        var account = _repo.GetById(accountId)
            ?? throw new InvalidOperationException($"Account {accountId} not found");
        account.Balance += amount;
        _repo.Update(account);
        return account;
    }

    public Account Withdraw(string accountId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Withdrawal amount must be positive");
        var account = _repo.GetById(accountId)
            ?? throw new InvalidOperationException($"Account {accountId} not found");
        if (account.Balance < amount) throw new InvalidOperationException("Insufficient funds");
        account.Balance -= amount;
        _repo.Update(account);
        return account;
    }
}
```

## 4.4 — Register interface → implementation mappings

Update `Program.cs` — now you map the **contract** to a **concrete class**:

```csharp
using Brokerage.Core.Interfaces;
using Brokerage.Data;
using Brokerage.Services;

// ...
builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
builder.Services.AddSingleton<IPriceFeed, StubPriceFeed>();
builder.Services.AddScoped<AccountService>();
```

**This is the payoff.** The line `AddSingleton<IAccountRepository, InMemoryAccountRepository>()` says: "whenever anyone asks for `IAccountRepository`, give them an `InMemoryAccountRepository`." To switch to a SQL-backed repository later, you change **only this one line** — `AccountService`, the controller, nothing else changes. That's loose coupling via interfaces + DI working together.

> **Testing benefit:** In a unit test you'd inject a `FakeAccountRepository` and test `AccountService`'s rules with zero database, zero web server. This is *why* the whole industry structures code this way.

## 4.5 — A generic repository (combining generics + interfaces)

You'll notice every entity (Account, Holding, Trade...) needs the same CRUD operations. Don't write `IAccountRepository`, `IHoldingRepository`, `ITradeRepository` by hand — make a **generic** one.

Create `Brokerage.Core/Interfaces/IRepository.cs`:

```csharp
namespace Brokerage.Core.Interfaces;

// Constraint: T must have an Id (so the repo can look things up)
public interface IEntity
{
    string Id { get; set; }
}

public interface IRepository<T> where T : IEntity
{
    IEnumerable<T> GetAll();
    T? GetById(string id);
    void Add(T item);
    void Update(T item);
    void Delete(string id);
}
```

Make your models implement `IEntity` (add to `Account` and `Holding`):

```csharp
public class Account : IEntity   // in Brokerage.Core/Models/Account.cs
{
    public string Id { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public decimal Balance { get; set; }
}
```

Create a single generic in-memory implementation `Brokerage.Data/InMemoryRepository.cs`:

```csharp
using Brokerage.Core.Interfaces;

namespace Brokerage.Data;

public class InMemoryRepository<T> : IRepository<T> where T : IEntity
{
    private readonly Dictionary<string, T> _items = new();

    public IEnumerable<T> GetAll() => _items.Values;
    public T? GetById(string id) => _items.GetValueOrDefault(id);
    public void Add(T item) => _items[item.Id] = item;
    public void Update(T item) => _items[item.Id] = item;
    public void Delete(string id) => _items.Remove(id);
}
```

Register it generically in `Program.cs` — **one registration covers every entity type**:

```csharp
// The "open generic" registration: works for IRepository<Account>, IRepository<Holding>, etc.
builder.Services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
```

Now `AccountService` can depend on `IRepository<Account>`, a future `HoldingService` on `IRepository<Holding>`, all served by the same generic class. **This is generics + interfaces + DI combined — the trifecta that makes .NET codebases scalable.**

---

**✅ Level 4 checkpoint.** Your services depend only on interfaces. You can swap an implementation by changing one registration line. You built a generic repository with a constraint and registered it as an open generic. You understand *why* this matters: testability and loose coupling.

---

# Level 5 — Integrate a Real Database (Entity Framework Core)

Time to replace in-memory storage with a real database. We'll use **Entity Framework Core (EF Core)** — Microsoft's ORM, the rough equivalent of JPA/Hibernate. We'll use **PostgreSQL** (common in fintech) but the pattern is identical for SQL Server.

## 5.1 — Add EF Core packages to the Data project

```bash
cd ~/src/c#/BrokerageSolution/Brokerage.Data
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL   # PostgreSQL provider
dotnet add package Microsoft.EntityFrameworkCore.Design     # for migrations
```

## 5.2 — Create the DbContext

The `DbContext` is your gateway to the database — like a JPA `EntityManager` / Hibernate `Session`. Create `Brokerage.Data/BrokerageDbContext.cs`:

```csharp
using Brokerage.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Data;

public class BrokerageDbContext : DbContext
{
    public BrokerageDbContext(DbContextOptions<BrokerageDbContext> options)
        : base(options) { }

    // Each DbSet maps to a database table
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Holding> Holdings => Set<Holding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tell EF how to store decimals precisely (critical for money!)
        modelBuilder.Entity<Account>()
            .Property(a => a.Balance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Holding>()
            .Property(h => h.AverageCost)
            .HasPrecision(18, 2);
    }
}
```

## 5.3 — An EF-backed repository

Create `Brokerage.Data/EfRepository.cs` — same `IRepository<T>` interface, now backed by the database:

```csharp
using Brokerage.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Data;

public class EfRepository<T> : IRepository<T> where T : class, IEntity
{
    private readonly BrokerageDbContext _db;
    private readonly DbSet<T> _set;

    public EfRepository(BrokerageDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public IEnumerable<T> GetAll() => _set.ToList();
    public T? GetById(string id) => _set.Find(id);

    public void Add(T item)    { _set.Add(item);    _db.SaveChanges(); }
    public void Update(T item) { _set.Update(item); _db.SaveChanges(); }
    public void Delete(string id)
    {
        var item = _set.Find(id);
        if (item is not null) { _set.Remove(item); _db.SaveChanges(); }
    }
}
```

> **Note:** This swap — from `InMemoryRepository<T>` to `EfRepository<T>` — is exactly the loose-coupling payoff from Level 4. Your `AccountService` doesn't change at all.

## 5.4 — Connection string and registration

Add the connection string to `Brokerage.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "BrokerageDb": "Host=localhost;Port=5432;Database=brokerage;Username=postgres;Password=postgres"
  },
  "Logging": { "LogLevel": { "Default": "Information" } }
}
```

Add the EF package to the API project too (needed for registration), then update `Program.cs`:

```bash
cd ~/src/c#/BrokerageSolution/Brokerage.Api
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

```csharp
using Brokerage.Core.Interfaces;
using Brokerage.Data;
using Brokerage.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the DbContext (Scoped by default — one per request)
builder.Services.AddDbContext<BrokerageDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BrokerageDb")));

// Swap in the EF repository — note: Scoped now, because DbContext is Scoped
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddSingleton<IPriceFeed, StubPriceFeed>();
builder.Services.AddScoped<AccountService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
```

> **Lifetime rule:** Anything that depends on a `Scoped` service must itself be `Scoped` (or shorter-lived). `EfRepository` uses the scoped `DbContext`, so it's `Scoped`, and `AccountService` becomes `Scoped` too. Don't put a `Scoped` thing inside a `Singleton` — that's a classic bug ("captive dependency").

## 5.5 — Run a database with Docker, then create the schema

You need a Postgres instance. The easiest way is Docker (and it's a nice preview of the next level):

```bash
docker run --name brokerage-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=brokerage -p 5432:5432 -d postgres:16
```

Install the EF CLI tool and create the schema via **migrations** (versioned schema changes — like Flyway/Liquibase):

```bash
dotnet tool install --global dotnet-ef

cd ~/src/c#/BrokerageSolution/Brokerage.Api
# Create a migration (EF inspects your models and generates SQL)
dotnet ef migrations add InitialCreate --project ../Brokerage.Data --startup-project .
# Apply it to the database
dotnet ef database update --project ../Brokerage.Data --startup-project .
```

Run `dotnet run`, create an account via Swagger POST, restart the app — **the data persists** because it's in Postgres now, not memory. That's the difference a real database makes.

---

**✅ Level 5 checkpoint.** You have a real Postgres database, an EF Core `DbContext`, a generic EF repository swapped in via one registration line, and migrations creating your schema. Data survives restarts.

---

# Level 6 — Build a Docker Image

A **Docker image** is a self-contained package of your app + its runtime + dependencies. A **container** is a running instance of an image. This is how modern apps ship — "it works on my machine" disappears because the machine ships *with* the app.

## 6.1 — Write a Dockerfile

Create `Brokerage.Api/Dockerfile`. We use a **multi-stage build**: a big SDK image to compile, then a small runtime image to run (keeps the final image lean).

```dockerfile
# ── Stage 1: build ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first and restore — this layer caches unless deps change
COPY Brokerage.Api/Brokerage.Api.csproj           Brokerage.Api/
COPY Brokerage.Services/Brokerage.Services.csproj Brokerage.Services/
COPY Brokerage.Core/Brokerage.Core.csproj         Brokerage.Core/
COPY Brokerage.Data/Brokerage.Data.csproj         Brokerage.Data/
RUN dotnet restore Brokerage.Api/Brokerage.Api.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish Brokerage.Api/Brokerage.Api.csproj -c Release -o /app/publish

# ── Stage 2: runtime (small image, no SDK) ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The app listens on port 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Brokerage.Api.dll"]
```

Add a `.dockerignore` next to it (in the solution root) so you don't copy junk into the build:

```
**/bin/
**/obj/
**/.vs/
**/.git/
```

## 6.2 — Build and run the image

```bash
cd ~/src/c#/BrokerageSolution   # build from the SOLUTION root (the Dockerfile paths assume this)

# Build the image
docker build -t brokerage-api:1.0 -f Brokerage.Api/Dockerfile .

# Run it — connect to the Postgres container we started earlier
docker run -p 8080:8080 \
  -e ConnectionStrings__BrokerageDb="Host=host.docker.internal;Port=5432;Database=brokerage;Username=postgres;Password=postgres" \
  brokerage-api:1.0
```

> Note `host.docker.internal` — from inside a container, that's how you reach services running on your Mac host (like the Postgres container's mapped port). Also note `ConnectionStrings__BrokerageDb` with a double underscore — that's how .NET maps environment variables to nested config keys.

Open `http://localhost:8080/swagger`. Your API is now running **inside a container**.

## 6.3 — Docker Compose (run API + DB together)

Manually starting two containers is tedious. **Docker Compose** defines a multi-container setup in one file. Create `docker-compose.yml` in the solution root:

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: brokerage
    ports:
      - "5432:5432"
    volumes:
      - dbdata:/var/lib/postgresql/data   # persist data across restarts

  api:
    build:
      context: .
      dockerfile: Brokerage.Api/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__BrokerageDb: "Host=db;Port=5432;Database=brokerage;Username=postgres;Password=postgres"
    depends_on:
      - db

volumes:
  dbdata:
```

```bash
docker compose up --build
```

Now `db` and `api` start together. Note the connection string uses `Host=db` — inside a Compose network, containers reach each other **by service name**. One command spins up your whole stack.

---

**✅ Level 6 checkpoint.** You can write a multi-stage Dockerfile, build an image, run your API in a container, and orchestrate API + database with Docker Compose. You understand image vs. container, and container-to-container networking.

---

# Level 7 — Push to a Container Registry

A **container registry** is a repository for Docker images — like NuGet/Maven for images, or GitHub for code. You push your image there so a cloud service can pull and run it. We'll use **Azure Container Registry (ACR)** since the hosting target (next level) is Azure, but the concept is identical for Docker Hub, AWS ECR, or GitHub Container Registry.

## 7.1 — Create the registry (Azure example)

```bash
# Install the Azure CLI if needed: brew install azure-cli
az login

# Create a resource group (a logical container for Azure resources)
az group create --name brokerage-rg --location eastus

# Create the container registry (name must be globally unique, lowercase)
az acr create --resource-group brokerage-rg \
  --name brokerageacr$RANDOM --sku Basic
# Note the actual name it creates — call it <youracr> below.
```

## 7.2 — Tag and push your image

An image name for a registry looks like `<registry>/<repository>:<tag>`. You **tag** your local image with the registry address, then **push**.

```bash
# Log in to your registry
az acr login --name <youracr>

# Tag the local image with the registry's full address
docker tag brokerage-api:1.0 <youracr>.azurecr.io/brokerage-api:1.0

# Push it
docker push <youracr>.azurecr.io/brokerage-api:1.0
```

> **Tagging tip:** Use meaningful tags. `1.0`, `1.1`, a git commit SHA, or `latest`. In real pipelines you tag with the build number or commit hash so every deploy is traceable to exact source. Avoid relying on `latest` for production — it's ambiguous about what's actually running.

Verify it landed:

```bash
az acr repository list --name <youracr> --output table
az acr repository show-tags --name <youracr> --repository brokerage-api --output table
```

> **The mental model:** build locally (or in CI) → push to registry (central store) → cloud pulls from registry to run. The registry is the hand-off point between "build" and "run." In a real job, a CI pipeline (GitHub Actions / Azure DevOps) does the build + push automatically on every merge.

---

**✅ Level 7 checkpoint.** You understand what a registry is and why it exists. You can tag an image with a registry address and push it. You know why immutable, meaningful tags matter.

---

# Level 8 — Host in a Container App (with the database)

Finally, run your containerized API in the cloud and connect it to a managed database. We'll use **Azure Container Apps** (a serverless container hosting service — it pulls your image from the registry and runs it, scaling automatically). We'll also use a **managed PostgreSQL** instead of a container DB, because production databases shouldn't be ephemeral containers.

## 8.1 — Create a managed PostgreSQL database

```bash
az postgres flexible-server create \
  --resource-group brokerage-rg \
  --name brokerage-pg-$RANDOM \
  --admin-user pgadmin \
  --admin-password '<a-strong-password>' \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --public-access 0.0.0.0 \
  --database-name brokerage
# Note the server name it creates — <yourpg>.postgres.database.azure.com
```

> `--public-access 0.0.0.0` is fine for learning. In production you'd use private networking (VNet) so the DB isn't exposed to the internet.

## 8.2 — Create the Container Apps environment and deploy

```bash
# Install the Container Apps extension
az extension add --name containerapp --upgrade

# Create the environment (the boundary your apps run inside)
az containerapp env create \
  --name brokerage-env \
  --resource-group brokerage-rg \
  --location eastus

# Deploy the app — it pulls your image from ACR
az containerapp create \
  --name brokerage-api \
  --resource-group brokerage-rg \
  --environment brokerage-env \
  --image <youracr>.azurecr.io/brokerage-api:1.0 \
  --registry-server <youracr>.azurecr.io \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 --max-replicas 3 \
  --env-vars ConnectionStrings__BrokerageDb="Host=<yourpg>.postgres.database.azure.com;Port=5432;Database=brokerage;Username=pgadmin;Password=<a-strong-password>;SSL Mode=Require"
```

Key flags explained:
- `--ingress external` + `--target-port 8080` → exposes your API to the internet on the port your container listens on.
- `--min-replicas 1 --max-replicas 3` → autoscaling. It runs at least 1 instance, scales up to 3 under load.
- `--env-vars ConnectionStrings__BrokerageDb=...` → injects the DB connection string as an environment variable (same double-underscore mapping as before). **Never hardcode connection strings in your image** — inject them at runtime.

The command prints a URL like `https://brokerage-api.<region>.azurecontainerapps.io`. Visit `/swagger` there — **your fintech API is live in the cloud, in a container, talking to a managed database.**

## 8.3 — Apply migrations to the cloud database

Your cloud DB is empty. Point the EF migration command at it once to create the schema:

```bash
cd ~/src/c#/BrokerageSolution/Brokerage.Api
ConnectionStrings__BrokerageDb="Host=<yourpg>.postgres.database.azure.com;Port=5432;Database=brokerage;Username=pgadmin;Password=<a-strong-password>;SSL Mode=Require" \
  dotnet ef database update --project ../Brokerage.Data --startup-project .
```

> In a mature setup, migrations run automatically on app startup or as a separate step in the deployment pipeline. Doing it manually once is fine for learning.

## 8.4 — Update and redeploy

When you change code, the full loop is:

```bash
# 1. Rebuild the image with a new tag
docker build -t <youracr>.azurecr.io/brokerage-api:1.1 -f Brokerage.Api/Dockerfile .
# 2. Push it
docker push <youracr>.azurecr.io/brokerage-api:1.1
# 3. Tell the Container App to use the new image
az containerapp update --name brokerage-api --resource-group brokerage-rg \
  --image <youracr>.azurecr.io/brokerage-api:1.1
```

That `build → push → update` cycle is the deployment loop you'll do constantly as a .NET developer. CI/CD automates all three steps on every merge to main.

---

**✅ Level 8 checkpoint — and the finish line.** You've taken a fintech API from a single file to a layered, dependency-injected, interface-driven, generic, database-backed service running in a container in the cloud. You traced the full path: **API → N-tier → DI → interfaces → generics → database → image → registry → container app**.

---

# The full picture — how every concept connects

```
                          HTTP request
                               │
                               ▼
   ┌──────────────────────────────────────────────┐
   │ AccountsController          [API LAYER]        │  ← Level 1
   │  - constructor injects AccountService          │  ← DI (Level 3)
   └──────────────────────────────────────────────┘
                               │
                               ▼
   ┌──────────────────────────────────────────────┐
   │ AccountService             [BUSINESS LAYER]    │  ← Level 2
   │  - depends on IRepository<Account>             │  ← Interface (Level 4)
   │  - business rules (no funds → reject)          │
   └──────────────────────────────────────────────┘
                               │
                               ▼
   ┌──────────────────────────────────────────────┐
   │ EfRepository<Account>      [DATA LAYER]        │  ← Generic (Level 4)
   │  - implements IRepository<T>                   │  ← Interface
   │  - uses BrokerageDbContext (EF Core)           │  ← Database (Level 5)
   └──────────────────────────────────────────────┘
                               │
                               ▼
                     PostgreSQL database
                               
   All of the above packaged as a Docker image (L6),
   stored in a registry (L7), running in a Container App (L8).
```

Every topic you listed has a home in this one project:

| Your topic | Where you learned it |
|------------|----------------------|
| Interface / Class / Object | Level 0, used everywhere |
| Class | Level 0 (models, services) |
| var | Level 0, used throughout |
| N-tier architecture | Level 2 (4 projects) |
| Generics | Level 4 (`IRepository<T>`, `EfRepository<T>`) |
| Dependency Injection | Level 3 + 4 (`Program.cs` registrations) |
| API → ... → Container App | Levels 1 through 8 in order |

---

# Suggested practice extensions (to cement it)

Once the 8 levels work, extend the project — this is where real learning sticks:

1. **Add a `Holding` feature end-to-end.** New controller, `HoldingService`, reuse `IRepository<Holding>`. Lets a client list what they own. (Reinforces N-tier + generics — notice how little new code you need.)
2. **Add a `TradeService.PlaceOrder(accountId, symbol, shares)`** that: checks the account has enough balance (using `IPriceFeed.GetPrice`), deducts the cost, and creates/updates a `Holding`. (Reinforces business logic across multiple repositories.)
3. **Write a unit test** for `AccountService.Withdraw` using a fake `IRepository<Account>`. No database, no web server. (This is the *whole point* of interfaces + DI — feel it.)
4. **Add input validation** with data annotations (`[Required]`, `[Range]`) on your request models.
5. **Replace `StubPriceFeed`** with a real one calling a free market-data API via `HttpClient` (registered with `AddHttpClient`). One registration line changes; nothing else does.
6. **Add structured logging** — inject `ILogger<AccountService>` and log every trade. (DI gives you loggers for free.)

---

# Cheat sheet — Java → C# quick reference

| Java | C# |
|------|-----|
| `String` | `string` |
| `boolean` | `bool` |
| `double` (money) | **`decimal`** (always for money) |
| `List<T>` (ArrayList) | `List<T>` |
| `Map<K,V>` (HashMap) | `Dictionary<K,V>` |
| `Optional<T>` | `T?` (nullable) |
| `stream().filter().map()` | LINQ: `.Where().Select()` |
| `@Override` | `override` keyword |
| `implements` / `extends` | `:` for both |
| `interface Foo` | `interface IFoo` (I prefix) |
| getter/setter | auto-property `{ get; set; }` |
| `final` | `readonly` (fields) / `const` |
| `package` | `namespace` |
| `import` | `using` |
| `null` checks | `?.`, `??`, `is null` |
| Maven module | `.csproj` project |
| `pom.xml` | `.csproj` + `dotnet add package` |
| Spring `@Autowired` | constructor injection (built-in DI) |
| Spring `@RestController` | `[ApiController]` |
| JPA `@Entity` / Hibernate | EF Core `DbContext` + `DbSet<T>` |
| Flyway / Liquibase | EF Core migrations |

---

**Final advice:** Don't rush the levels. Get each one *working and understood* before moving on. The single most important leap is **Level 3 → 4** (DI + interfaces) — that's the idea that separates someone who writes C# from someone who writes *professional .NET*. Spend extra time there. Type everything. Break things on purpose and fix them.

Good luck — by Level 8 you'll understand your company's codebase far better than the topic list alone would have gotten you.
