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

## Project Management Module

The Project Management module provides a centralized workspace for planning, organizing, and tracking business projects. It is designed to connect existing business modules, such as invoicing, inventory, accounting, and contacts, without replacing their responsibilities.

The initial implementation focuses on project information, team members, project stages, status tracking, and activity history.

### Core Features

- **Project Management** – Create, edit, view, and manage projects.
- **Project Contacts** – Associate projects with existing customers or business contacts.
- **Project Manager** – Assign a project manager from the company's registered staff.
- **Project Members** – Add company members to projects and define their roles or responsibilities.
- **Project Stages** – Define custom stages for each project, assign responsible staff, and set planned dates.
- **Stage Tracking** – Track progress through stage statuses, including waiting and skipped stages.
- **Waiting Management** – Record waiting reasons and next follow-up dates when a stage is blocked.
- **Status History** – Maintain an audit trail of project stage status changes.
- **Multi-Tenant Isolation** – Ensure that projects and their related records belong to the correct tenant.

### Project Lifecycle

Projects support the following statuses:

| Status | Description |
|---|---|
| Draft | Project has been created but not started. |
| Active | Project is currently being executed. |
| OnHold | Project execution is temporarily suspended. |
| Completed | Project has been completed. |
| Cancelled | Project has been cancelled. |

### Project Stages

Each project can have its own independently configurable stages. Stages are not tied to a predefined global workflow.

Supported stage statuses:

| Status | Description |
|---|---|
| NotStarted | Stage has not started. |
| InProgress | Stage is being executed. |
| Waiting | Stage is waiting for an action, resource, or external dependency. |
| Completed | Stage has been completed. |
| Skipped | Stage has intentionally been skipped. |
| Cancelled | Stage has been cancelled. |

Additional stage properties include:

- Title and description
- Display order
- Required or optional stage
- Assigned staff member
- Planned start and end dates
- Actual start and completion timestamps
- Waiting reason
- Next follow-up date
- Additional notes

When a stage enters the `Waiting` status, a waiting reason and next follow-up date must be provided.

Every stage status change is recorded in the project stage history, including the previous status, new status, description, user identifier, and change timestamp.

### Domain Model

The project management domain is located in:

`MyFirstCoreApp.Domain/Entities/Projects/`

| Entity | Responsibility |
|---|---|
| `Project` | Main project information, customer, manager, status, and dates. |
| `ProjectMember` | Staff members participating in a project. |
| `ProjectStage` | Configurable project stages and their execution information. |
| `ProjectStageHistory` | Historical record of stage status changes. |

Related enums:

- `ProjectStatus`
- `ProjectStageStatus`

The module reuses existing entities:

- `Contact` – For customers and business contacts.
- `UserTenant` – For company staff and project assignments.

It does not create separate customer or employee entities.

### Architecture and Application Services

The module follows the existing layered architecture.

**Domain**

Contains project entities, relationships, and status enums.

**Application**

Contains request models, DTOs, and service abstractions:

- `Projects/ProjectRequests.cs`
- `Projects/ProjectDtos.cs`
- `Interfaces/IProjectService.cs`

**Infrastructure**

Contains Entity Framework Core configurations and the project service implementation:

- `Persistence/Configurations/Projects/`
- `Services/ProjectService.Core.cs`
- `Services/ProjectService.MembersStages.cs`

The `IProjectService` interface provides operations for:

- Retrieving project lists and project details
- Creating, updating, and deleting projects
- Changing project status
- Adding and removing project members
- Adding, updating, and deleting project stages
- Changing stage status and recording history

The service is registered through dependency injection:

`services.AddScoped<IProjectService, ProjectService>();`

### Data Integrity and Business Rules

The following rules are applied by the application service:

- Every project and related operational record belongs to a tenant.
- A project contact must belong to the current tenant.
- Project managers, members, and assigned staff must be valid members of the current tenant.
- Duplicate project membership is not allowed.
- Projects cannot be edited after reaching the `Completed` or `Cancelled` status.
- Stage status changes are allowed only while the project is `Active`.
- Waiting stages require a waiting reason and a follow-up date.
- Stage status changes are recorded as historical entries rather than overwriting previous history.
- Soft deletion is used in accordance with the existing application conventions.

### Database and Entity Framework Core

The project entities are registered in `ApplicationDbContext`, and their configurations are applied through the existing assembly configuration mechanism.

The database migration is:

`AddProjectManagement`

It creates the following tables:

- `Projects`
- `ProjectMembers`
- `ProjectStages`
- `ProjectStageHistories`

The schema includes tenant-related indexes, foreign keys, unique constraints, and appropriate delete behaviors.

Important relationship rules:

- Project to Contact: Restrict
- Project to Project Manager: Restrict
- Project to Members: Cascade
- Project to Stages: Cascade
- Stage to History: Cascade
- Stage to Assigned User: Restrict
- Member to UserTenant: Restrict

Tenant validation for `UserTenant` and `Contact` references is also performed in the application layer because ordinary foreign keys alone do not guarantee tenant ownership.

### Permissions and Navigation

Project permissions use the `Projects` permission group.

| Permission | Description |
|---|---|
| `Project.View` | View projects |
| `Project.Create` | Create projects |
| `Project.Edit` | Edit projects |
| `Project.Delete` | Delete projects |
| `Project.Status.Change` | Change project status |
| `ProjectMember.Manage` | Manage project members |
| `ProjectStage.Manage` | Manage project stages |
| `ProjectStage.Status.Change` | Change project stage status |

The navigation menu is organized as follows:

- **مدیریت پروژه‌ها** – Parent menu
  - **لیست پروژه‌ها** – `/Projects/Projects`

The menu and permission records can be inserted manually into the database. The `IdentitySeeder` also contains the corresponding permission and menu definitions and checks for existing records before inserting them.

`SuperAdmin` receives all permissions, while `TenantOwner` receives tenant-level permissions through the existing role permission seeding logic. Other company roles can be configured through the role management interface.

### Current Implementation Scope

The initial phase includes:

- Project domain entities and database schema
- Application requests and DTOs
- Project service interface and implementation
- Project CRUD operations
- Project member management
- Project stage management
- Stage status transitions and history
- Tenant validation and permission definitions

The MVC Area, controllers, views, and user-facing project management screens are the next implementation step.

### Planned Future Integrations

The project module is designed to become the central reference for future business workflows.

Potential future phases include:

1. **Invoicing Integration** – Link sales invoices, purchase invoices, and pro forma invoices to projects.
2. **Project Expenses** – Track project-related expenses and financial performance.
3. **Company Assets and Tools** – Assign company equipment and tools to projects.
4. **Scheduling and Execution** – Plan visits, activities, and project execution schedules.
5. **Project Reporting** – Provide progress, cost, and performance reports.

These integrations are intentionally excluded from the initial implementation to keep the first phase focused and maintainable.