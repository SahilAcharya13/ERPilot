<<<<<<< HEAD
# AI-Powered ERP Database Chatbot

An enterprise-ready, production-grade AI-powered ERP database chatbot system. This system allows authenticated users (with specific role permissions) to interact with an ERP database using natural language, translating user commands into safe, parameterized SQL queries while enforcing rigorous security controls.

---

## 🚀 Work Implemented to Date

### Phase 1: Database Layer & Natural Language Scaffolding
* **Domain Model & Schema**: Scaffolded database schema using EF Core Code-First.
  * Entities: `Customers`, `Products`, `Orders`, `OrderItems`, `Payments`, `Users`, and `AiActionLogs`.
  * Support for soft-deletes (`IsDeleted`, `DeletedAt`) mapped using global EF Core query filters.
* **AI NLP-to-SQL Pipeline**:
  * Implemented intent parsing, parameter extraction, and plain-English explanations.
  * Non-destructive queries (SELECTs) execute immediately.
  * Destructive DML actions (INSERT, UPDATE, DELETE) return a pending confirmation token instead of running automatically.
* **SQL Safety Guard (AST Analysis)**:
  * Restricts SQL execution to permitted tables.
  * Blocks inline raw SQL comments, SQL injections, and system table accesses (e.g., `sys.`, `sqlite_master`).

### Phase 2: Security, JWT Authentication, and RBAC
* **JWT-Based Authentication**:
  * Added `POST /api/auth/login` to authenticate users and issue HMAC-SHA256 signed access and refresh tokens.
  * Enforced password security using `BCrypt.Net-Next` with static pre-computed seed hashes.
* **Role-Based Access Control (RBAC)**:
  * Strict permission mapping applied using controller attributes `[Authorize(Roles = "...")]`:
    * **Admin**: Full CRUD access on all entities, including order deletion.
    * **Manager**: Can view reports/ledger, and override/approve any pending AI action.
    * **Sales**: Can create and read orders, and record payments.
    * **Accounts**: Can read orders, read and write payments, and view customer ledgers.
* **AI Token Owner Gates & Expiry**:
  * Cache tokens expire after **5 minutes** (returning `410 Gone`).
  * Only the creator can approve their pending AI actions, **unless** overridden by an `Admin` or `Manager`.
  * Audit logging tracks both the prompt creator (`UserID`) and the action approver (`ApprovedByUserId`).
* **Rate Limiting**:
  * IP-based rate limiting on `/auth/login` (5 requests/minute).
  * User-based rate limiting on `/chat` (10 requests/minute).

---

## 🛠️ Project Structure
```text
ERP-Solution/
│
├── src/
│   ├── ERP.Domain/             # Entities, Enums, Interfaces, Common bases
│   ├── ERP.Infrastructure/     # DbContext, Repositories, NLP Engine, Services
│   └── ERP.WebAPI/             # Controllers, Middlewares, Program Startup
│
├── tests/
│   └── ERP.WebAPI.Tests/       # Integration tests (JWT, RBAC, Rate Limiting, Approval)
│
└── ERP-Solution.sln           # Visual Studio / .NET Core Solution file
```

---

## ⚙️ Setup and Configuration

1. **Prerequisites**:
   * [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
   * A SQLite or SQL Server database.

2. **Configuration (`appsettings.json`)**:
   Add JwtSettings in `src/ERP.WebAPI/appsettings.json`:
   ```json
   "JwtSettings": {
     "SecretKey": "ThisIsAVerySecretKeyForERPDatabaseSystem2026!",
     "Issuer": "ErpSystem",
     "Audience": "ErpSystemClients"
   }
   ```

3. **Database Migration Setup**:
   Ensure EF tool is installed:
   ```bash
   dotnet tool install --global dotnet-ef
   ```
   Apply local SQLite migrations:
   ```bash
   dotnet ef database update --project src/ERP.Infrastructure --startup-project src/ERP.WebAPI
   ```

---

## 🚦 Running the Application

### Running Local Development Server
To start the Web API:
```bash
dotnet run --project src/ERP.WebAPI
```
The API is exposed at `https://localhost:7196` and `http://localhost:5246`.

### Running Integration Tests
Execute the comprehensive test suites validating the security bounds:
```bash
dotnet test
```

---

## 🔐 Auth Seeding (Default Users)
The local SQLite db seeds the following test credentials:

| Role | Email | Password |
|---|---|---|
| **Admin** | `admin@erp.com` | `Admin123!` |
| **Sales** | `sales@erp.com` | `Sales123!` |
| **Accounts** | `accounts@erp.com` | `Accounts123!` |
| **Manager** | `manager@erp.com` | `Manager123!` |
=======
# ERPilot
>>>>>>> 29518fcba49302ae2a8d97ff4092ea818bf1958f
