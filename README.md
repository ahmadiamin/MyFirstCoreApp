# MyFirstCoreApp

**A Multi-Tenant Business Management System Built with ASP.NET Core**

MyFirstCoreApp is a business management web application built with ASP.NET Core and modern .NET technologies. The project follows a layered architecture and is designed to support business operations such as invoicing, inventory management, customer management, financial operations, and reporting.

## Key Features

- **Multi-Tenant Architecture** – Supports isolated business data across tenants.
- **Authentication & Authorization** – User management and role-based access control using ASP.NET Core Identity.
- **Invoice Management** – Supports sales invoices, purchase invoices, and pro forma invoices.
- **Inventory Management** – Tracks stock movements, including receipts, issues, transfers, and opening balances.
- **Customer Portal** – Provides controlled access to customer invoices and account information through secure sharing links.
- **Financial Operations** – Supports customer and supplier account tracking.
- **Reporting** – Generates business reports using Stimulsoft Reports.
- **Persian and RTL Support** – Includes a right-to-left user interface for Persian-speaking users.

## Technology Stack

- C#
- .NET 9
- ASP.NET Core MVC
- Entity Framework Core
- Microsoft SQL Server
- ASP.NET Core Identity
- Stimulsoft Reports
- HTML, CSS, JavaScript
- Bootstrap

## Architecture

The solution is organized into separate layers to improve maintainability, testability, and separation of concerns.

- **Domain** – Core business entities and domain logic.
- **Application** – Application services, business workflows, and abstractions.
- **Infrastructure** – Database access, Entity Framework Core implementations, and external integrations.
- **Web** – ASP.NET Core MVC application and user interface.
- **Tests** – Automated tests for application services and business logic.

## Getting Started

### Prerequisites

- .NET 9 SDK
- Microsoft SQL Server
- Visual Studio 2022 or a compatible IDE

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/ahmadiamin/MyFirstCoreApp.git
   ```

2. Open the solution in Visual Studio.

3. Configure the SQL
