# CLDV6212 API Gateway

An ASP.NET Core Web API demonstrating a simple **API Gateway authentication and authorization layer** for the CLDV6212 Cloud Development module.

The project provides user registration and login using **Azure Table Storage**, **BCrypt password hashing**, and **JSON Web Tokens (JWT)**. JWT claims can then be used to protect API Gateway endpoints and implement role-based authorization.

## Architecture

```text
                         ┌─────────────────────┐
                         │    Client App       │
                         │ Web / Mobile / POS  │
                         └──────────┬──────────┘
                                    │
                                    │ HTTP + JWT
                                    ▼
                         ┌─────────────────────┐
                         │   ASP.NET Core      │
                         │    API Gateway      │
                         │                     │
                         │ Authentication      │
                         │ Authorization       │
                         │ API Endpoints       │
                         └──────────┬──────────┘
                                    │
                         ┌──────────┴──────────┐
                         │                     │
                         ▼                     ▼
                ┌────────────────┐    ┌────────────────┐
                │ Azure Table    │    │ Backend APIs / │
                │ Storage        │    │ Azure Services │
                │                │    │                │
                │ Users          │    │ Functions etc. │
                └────────────────┘    └────────────────┘
```

The API Gateway acts as the central entry point through which clients access the application's services. Authentication and authorization can therefore be handled centrally instead of being duplicated across every backend service.

## Features

* ASP.NET Core Web API
* .NET 10
* JWT Bearer authentication
* Role-based authorization
* Azure Table Storage user persistence
* BCrypt password hashing
* User registration
* User login
* JWT generation and validation
* OpenAPI support
* Docker/Linux deployment support

## Technologies

|Technology|Purpose|
|-|-|
|ASP.NET Core|Web API and gateway|
|.NET 10|Application runtime|
|JWT|Stateless authentication|
|Azure Table Storage|User account storage|
|BCrypt|Secure password hashing|
|OpenAPI|API documentation|
|Docker|Containerised deployment|

## Project Structure

```text
cldv6212-api-gateway/
│
├── api-gateway/
│   ├── Controllers/
│   │   └── AuthController.cs
│   │
│   ├── Models/
│   │   └── DTOs/
│   │
│   ├── Program.cs
│   ├── appsettings.json
│   └── api-gateway.csproj
│
├── .dockerignore
├── .gitignore
└── api-gateway.slnx
```

## Authentication Flow

The application uses JWT Bearer authentication.

```text
1. Register
   │
   ▼
POST /api/auth/register
   │
   ├── Validate registration data
   ├── Hash password using BCrypt
   └── Store user in Azure Table Storage


2. Login
   │
   ▼
POST /api/auth/login
   │
   ├── Find user in Azure Table Storage
   ├── Verify BCrypt password
   └── Generate signed JWT
                │
                ▼
          JWT returned
                │
                ▼
3. Client stores token
                │
                ▼
4. Client calls protected endpoint

Authorization: Bearer <token>
                │
                ▼
5. API Gateway validates JWT
                │
                ├── Signature
                ├── Issuer
                ├── Audience
                └── Expiration
                │
                ▼
6. Request is authenticated
```

## JWT Claims

When a user successfully logs in, the generated token contains claims describing the authenticated user.

The current implementation includes:

* Name
* Email address
* Role

For example:

```text
Name  = John Smith
Email = john@example.com
Role  = User
```

These claims can then be used by ASP.NET Core authorization.

For example:

```csharp
[Authorize]
[HttpGet]
public IActionResult ProtectedEndpoint()
{
    return Ok();
}
```

An endpoint can also be restricted by role:

```csharp
[Authorize(Roles = "Admin")]
[HttpPut]
public IActionResult AdminOnlyEndpoint()
{
    return Ok();
}
```

## User Roles

The example supports two roles:

|Role|Purpose|
|-|-|
|`User`|Standard authenticated user|
|`Admin`|Administrative user|

Public registration creates users with the `User` role.

The role is stored as the user's **PartitionKey** in Azure Table Storage and is included as a role claim in the JWT when the user logs in.

## Azure Table Storage

User accounts are stored in an Azure Table named:

```text
Users
```

The application automatically creates the table if it does not already exist.

A user entity conceptually contains:

```text
PartitionKey : User role
RowKey       : Email address
FullName     : User's name
PasswordHash : BCrypt password hash
CreatedDate  : Account creation date
```

Passwords are **never stored as plain text**.

## API Endpoints

### Register

```http
POST /api/auth/register
```

Creates a new user account.

Example request:

```json
{
  "email": "student@example.com",
  "password": "Password123!",
  "fullName": "Example Student"
}
```

Example response:

```json
{
  "email": "student@example.com",
  "fullName": "Example Student",
  "role": "User",
  "createdDate": "2026-10-07T08:00:00Z"
}
```

The password is hashed using BCrypt before the user is stored.

---

### Login

```http
POST /api/auth/login
```

Authenticates a user and returns a JWT.

Example request:

```json
{
  "email": "student@example.com",
  "password": "Password123!"
}
```

Example response:

```json
{
  "token": "<JWT>",
  "expires": "2026-10-07T09:00:00Z",
  "user": {
    "email": "student@example.com",
    "fullName": "Example Student",
    "role": "User"
  }
}
```

The returned token should be included when calling protected endpoints:

```http
Authorization: Bearer <JWT>
```

## Configuration

The application requires configuration for Azure Storage and JWT authentication.

Example `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },

  "AllowedHosts": "*",

  "ConnectionStrings": {
    "AzureStorage": "UseDevelopmentStorage=true"
  },

  "Jwt": {
    "Key": "DEVELOPMENT-ONLY-CHANGE-THIS-TO-A-LONG-RANDOM-SECRET-KEY",
    "Issuer": "ApiGateway",
    "Audience": "ApiClient",
    "ExpiryMinutes": 60
  }
}
```

> **Important:** Do not store production JWT secrets or Azure Storage connection strings in source control. Use environment variables, .NET User Secrets, Azure Key Vault, or another appropriate secret-management system.

## Running Locally

### Prerequisites

Install:

* .NET 10 SDK
* Git
* Azure Storage Emulator/Azurite, or access to an Azure Storage Account

### 1. Clone the repository

```bash
git clone https://github.com/Oladipo-IIE/cldv6212-api-gateway.git
cd cldv6212-api-gateway
```

### 2. Restore NuGet packages

```bash
dotnet restore
```

### 3. Start Azurite

The default development configuration uses:

```text
UseDevelopmentStorage=true
```

Therefore, a local Azure Storage emulator such as **Azurite** must be running.

Alternatively, replace the `AzureStorage` connection string with the connection string for an Azure Storage Account.

### 4. Run the application

```bash
dotnet run --project api-gateway
```

ASP.NET Core will display the application's HTTP/HTTPS URLs in the terminal.

## Testing Authentication

Using Postman, perform the steps below, or use the include postman collection and environment files.

### Step 1 — Register

Send:

```http
POST /api/auth/register
Content-Type: application/json
```

with:

```json
{
  "email": "student@example.com",
  "password": "Password123!",
  "fullName": "Example Student"
}
```

### Step 2 — Login

Send:

```http
POST /api/auth/login
Content-Type: application/json
```

with:

```json
{
  "email": "student@example.com",
  "password": "Password123!"
}
```

Copy the JWT returned by the API.

### Step 3 — Call a protected endpoint

Include the JWT in the `Authorization` header:

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

ASP.NET Core's JWT authentication middleware validates the token before allowing access to endpoints marked with `[Authorize]`.

## Authorization

Authentication answers:

> **Who is making this request?**

Authorization answers:

> **Is this user allowed to perform this operation?**

For example, a normal authenticated endpoint can use:

```csharp
[Authorize]
```

while an administrative endpoint can use:

```csharp
[Authorize(Roles = "Admin")]
```

When a request arrives, ASP.NET Core:

1. Extracts the JWT from the `Authorization` header.
2. Verifies the token's signature.
3. Checks the issuer.
4. Checks the audience.
5. Checks that the token has not expired.
6. Extracts the claims.
7. Creates the authenticated `User`.
8. Evaluates the endpoint's authorization requirements.

This allows authorization to be enforced centrally at the API Gateway.

## Security Notes

This repository is intended as an educational example.

For a production system, consider additional measures including:

* Store secrets outside `appsettings.json`.
* Use HTTPS in production.
* Use sufficiently long, randomly generated JWT signing keys.
* Implement appropriate password requirements.
* Add login rate limiting.
* Consider refresh tokens for longer-lived sessions.
* Add account lockout/brute-force protection.
* Validate all incoming request data.
* Restrict CORS to trusted origins.
* Rotate signing keys where appropriate.
* Use Azure Key Vault or an equivalent secret-management service.

## NuGet Packages

The project uses packages including:

```text
Azure.Data.Tables - for interacting with Azure table storage
BCrypt.Net-Next - for hashing passwords
Microsoft.AspNetCore.Authentication.JwtBearer - implements the authentication middleware
Azure.Storage.Blobs
Azure.Storage.Files.Shares
Azure.Storage.Queues
Microsoft.AspNetCore.OpenApi
Microsoft.Extensions.Azure
Swashbuckle.AspNetCore
```

## Purpose

This repository accompanies material for **CLDV6212 – Cloud Development**.

It demonstrates how an ASP.NET Core application can provide a central security layer between client applications and cloud-based backend services.

The example is intended to illustrate concepts including:

* API Gateways
* HTTP APIs
* Authentication vs authorization
* Stateless authentication
* JSON Web Tokens
* Claims
* Role-based authorization
* Password hashing
* Azure Storage
* ASP.NET Core middleware
* Cloud deployment

