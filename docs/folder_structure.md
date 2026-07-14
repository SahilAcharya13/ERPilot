# Enterprise Project Folder Structure

This document outlines the standard enterprise directory structure for the ASP.NET Core C# solution following **Clean Architecture** patterns.

```
ERP-Solution/
│
├── ERP-Solution.sln                      # Visual Studio Solution File
├── docker-compose.yml                    # Local Dev (SQL Server, Redis) setup
├── README.md                             # Setup & Run guidelines
│
├── src/
│   ├── ERP.Domain/                       # Enterprise Domain Entities (Core)
│   │   ├── Common/                       # Auditable Base entities
│   │   ├── Entities/                     # Database Models (Customer, Order, Product, etc.)
│   │   ├── Enums/                        # Status (OrderStatus, PaymentMode, Roles)
│   │   ├── Exceptions/                   # Custom domain-level exceptions
│   │   └── ValueObjects/                 # Reusable business logic values (e.g. Money, Address)
│   │
│   ├── ERP.Application/                  # Core Business Logic & Interfaces
│   │   ├── Common/
│   │   │   ├── Behaviours/               # MediatR pipeline behaviours (Logging, Validation)
│   │   │   ├── Interfaces/               # IDbContext, IAiEngine, ISpeechService, ICurrentUserService
│   │   │   └── Mappings/                 # AutoMapper profiles
│   │   ├── Customers/                    # Customers Domain CQRS
│   │   │   ├── Commands/                 # CreateCustomer, UpdateCustomer Handlers
│   │   │   └── Queries/                  # GetCustomersList, GetCustomerLedger Handlers
│   │   ├── Orders/                       # Orders Domain CQRS
│   │   │   ├── Commands/                 # CreateOrder, UpdateOrderStatus Handlers
│   │   │   └── Queries/                  # GetPendingOrders, GetOrderDetails Handlers
│   │   ├── Payments/                     # Payments Domain CQRS
│   │   │   ├── Commands/                 # RecordPayment Handler
│   │   │   └── Queries/                  # GetPaymentsList, GetLastPayment Handlers
│   │   └── NLP/                          # Conversational AI Logic Commands
│   │       ├── Commands/                 # ProcessPromptCommand, ExecuteApprovedSqlCommand
│   │       └── Models/                   # ChatPromptRequest, ExtractedEntitiesDto
│   │
│   ├── ERP.Infrastructure/               # Database, Identity, Cache, & Services
│   │   ├── Persistence/                  # EF Core Data Access
│   │   │   ├── Configurations/           # Fluent API entity configs (keys, index declarations)
│   │   │   ├── Contexts/                 # ApplicationDbContext (Migrations & Seeds)
│   │   │   └── Interceptors/             # EF Core Interceptor to auto-fill Audit fields
│   │   ├── Identity/                     # User Registration & JWT Authentication Handlers
│   │   ├── Services/
│   │   │   ├── AiEngine/                 # OpenAI API orchestrator, System Prompts
│   │   │   ├── Speech/                   # Azure Cognitive Speech / Whisper integrations
│   │   │   └── SqlSafety/                # Microsoft.SqlServer.TransactSql.ScriptDom AST guard
│   │   └── Caching/                      # Redis Distributed Cache service implementation
│   │
│   └── ERP.WebAPI/                       # HTTP API Presentation Layer
│       ├── Controllers/                  # API routing (ChatController, OrdersController, etc.)
│       ├── Middleware/                   # Custom Global Error Handler & Security headers
│       ├── Filters/                      # ApiExceptionFilterAttribute, RoleAuthorizationFilter
│       ├── Program.cs                    # Application bootstrapping & DI registrations
│       └── appsettings.json              # Config variables (databases, JWT keys, OpenAi endpoints)
│
└── tests/
    ├── ERP.Domain.UnitTests/             # Unit tests for Domain Entities
    ├── ERP.Application.UnitTests/        # Handler & Validation rules Unit tests
    ├── ERP.Infrastructure.Integration/   # DB Context & AST parser integration tests
    └── ERP.WebAPI.IntegrationTests/      # End-to-end API HTTP Client integration tests
```

## Folder Structure Rationale

1. **Decoupled Architecture**: Since `ERP.Domain` and `ERP.Application` have zero dependencies on framework-specific elements (like Entity Framework or HTTP Controllers), they can be easily moved to different platforms (such as Azure Functions or console applications) if requirements change.
2. **Feature-by-Folder (CQRS)**: Under `ERP.Application`, files are organized by feature area (e.g., `Customers/Commands`) rather than technical type. This speeds up developers' work when editing endpoints, as command handlers, validators, and DTOs are located in the same directory.
3. **Pipeline Behaviours**: Cross-cutting concerns such as logging request execution speeds, validation exceptions (`FluentValidation`), and transaction boundaries are handled transparently using **MediatR Pipeline Behaviours** without cluttering the application use cases.
