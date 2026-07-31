Product Discovery:

Project: KnowledgeHub AI

KnowledgeHub AI is an enterprise platform that allows organizations to securely upload internal documents and enables employees to retrieve accurate information through AI-powered semantic search and conversational chat.

Our application will:
Store company documents.
Understand their meaning using embeddings.
Search semantically.
Use an LLM to generate grounded answers.
Cite the source documents.

Our Engineering Principles

These will guide every technical decision:
1. Security First
Company documents may be confidential.
2. Performance Matters
Users should get answers quickly.
3. Scalability
The system should be designed so it could grow from hundreds to many thousands of documents without fundamental redesign.
4. Maintainability
Clean, modular code that is easy to extend.
5. Explainability
Every AI answer should show where the information came from.

High-Level System Design (HLD)

Browser
    │
    ▼
Angular Application
    │
    ▼
ASP.NET Core Web API

Instead of putting everything into one giant service, we'll split responsibilities.

 Angular
    │
    ▼
ASP.NET Core API
    │
┌──────────┬───────────┬───────────┬─────────────┐
│          │           │           │             │
▼          ▼           ▼           ▼             ▼
Auth     Document      Chat      Search      Admin
Service   Service     Service    Service    Service

Each service has one responsibility.
This is called the Single Responsibility Principle (SRP).


Where do data go
                 Angular
                    │
                    ▼
          ASP.NET Core API
                    │
     ┌──────────────┴──────────────┐
     ▼                             ▼
 PostgreSQL                 Blob Storage

PostgreSQL stores
Users
Roles
Chats
Metadata
Audit logs
Document information

Blob Storage stores
The actual files.
HRPolicy.pdf
EmployeeHandbook.pdf
SecurityGuide.pdf

Because databases are not ideal for storing large files. We store metadata in PostgreSQL and the file itself in blob storage.

let's introduce AI.
When a PDF is uploaded:
Upload PDF
      │
      ▼
Blob Storage
      │
      ▼
Background Worker

The API does not process the PDF immediately.
If the API waits to:

Read the file
Extract text
Chunk it
Generate embeddings

the user could be waiting a long time.

Instead:
Upload
↓

Saved

↓

Response:
"Upload successful"

Meanwhile:
Background Worker

↓

Processes document

↓

Creates embeddings

↓

Stores vectors

The user gets a fast response while heavy work happens asynchronously.

Where do the vectors go?
We'll introduce another component.
Background Worker
       │
       ▼
Embedding Generator
       │
       ▼
PostgreSQL + pgvector

This means PostgreSQL stores both:
Relational data (users, documents, chats)
Vector embeddings

What happens when someone asks a question?
  User
  
  ↓
  
  Angular
  
  ↓
  
  Chat API
  
  ↓
  
  Vector Search
  
  ↓
  
  Top 5 Relevant Chunks
  
  ↓
  
  OpenAI
  
  ↓
  
  Answer
  
  ↓
  
  Angular

This is the heart of the application.

RAG stands for Retrieval-Augmented Generation.

Session 3 — Low-Level Design (LLD)
Now we stop thinking about services and start thinking about objects.
Imagine we're about to create a new company account.
What information do we need?
Immediately, we can identify our first entity.

Entity 1: User
User
-----------------------
Id
FirstName
LastName
Email
PasswordHash
RoleId
CreatedAt
UpdatedAt
LastLoginAt
IsActive

Notice something important. We do not store: Password Instead we store PasswordHash
Because if someone gains access to the database, we don't want them to see everyone's passwords. We'll discuss hashing in detail when we build authentication.

Entity 2: Role
Role
----------------------
Id
Name
Description

Instead of storing user we store and categorize to identify
Admin
Manager
Employee

This is called normalization and avoids duplicate data.

Entity 3: Document
When someone uploads a PDF.
We don't store the PDF inside PostgreSQL.
We store its information.
Document
-----------------------
Id
FileName
BlobUrl
UploadedBy
Size
ContentType
Status
UploadedAt

the actual file lives in Azure Blob Storage which can be redirected using BlobUrl.

Why Status? when someone uploads it may take time so we store in this Status = Processing or other to reflect the state of the file
The fromtend can show
✅ Ready
⏳ Processing
❌ Failed

Entity 4: DocumentChunk
This is where AI begins.
Suppose our document has 100 pages.
We split it into chunks.
Each chunk becomes one record.
DocumentChunk
--------------------------
Id
DocumentId
ChunkNumber
Content
Embedding
Example:
Chunk 1 Employees receive 24 annual leaves...
Chunk 2 Employees receive 12 sick leaves... and so on

Why store chunks separately?
Imagine a document with 800 pages. When someone asks: "How many sick leaves?" We don't want to search all 800 pages. We search the chunks. Much faster.

Entity 5: Conversation
Conversation
-----------------------
Id
UserId
Title
CreatedAt

Example HR Questions
Project Documentation
API Help

Entity 6: Message
Every question and answer
Message
------------------------
Id
ConversationId
Role
Content
CreatedAt

Role can be User or Assistant or ...

Entity 7: Audit Log
This is something many portfolio projects ignore. Every important action gets recorded.
AuditLog
-------------------------
Id
UserId
Action
Entity
CreatedAt

Example Rajesh Uploaded Document HRPolicy.pdf 10:45 AM or Admin Deleted User John 2:10 PM

Enterprise applications almost always have some form of audit trail.

Relationships:
Let's connect everything.
  User
   │
   ├───────< Document
   │
   ├───────< Conversation
   │
   └───────< AuditLog
  
  Conversation
   │
   └────────< Message
  
  Document
   │
   └────────< DocumentChunk
  
  Role
   │
   └────────< User

This is a simple Entity Relationship Diagram (ERD).

Why are we designing this first?
Because the database reflects the business. Why are we designing this first? Because the database reflects the business.

Session 4 – Authentication & Security
Why do we need Authentication? magine our application has no login. Anyone can: Upload company documents Read HR policies Chat with AI Download confidential files Delete documents
So the first question our application asks is:
"Who are you?"
That's Authentication.

Authentication vs Authorization
Authentication Who are you?
Authorization what are you allowed to do based on role?

What happens when you click Login?
Angular sends: POST /api/auth/login with login creds
ASP.NET Core receives the request. it looks up User Email PasswordHash
Why Hash? magine a hacker steals the database. Game over. instead we use Hash Rajesh AQJ89SJKD8239...
Even we, as developers, cannot read it.

Login Successful
Should we ask for the password on every request? That would be a terrible user experience.
Enter JWT
JWT stands for JSON Web Token. Think of it as a digital ID card.
After login:
The server creates a token.
Example
UserId: 123
Email: rajesh@company.com
Role: Employee
Expires: 30 min

The actual token is encoded and signed, so the client can't tamper with it.

Angular stores this token securely.
Every API request includes it.
Authorization
Bearer eyJhbGciOi...

Why is JWT useful?
Instead of asking:
"Who are you?" every time, the API checks the token. Much faster.

But why does JWT expire? Imagine you lose your laptop. Someone opens it. If the token never expires... They stay logged in forever. Not good. So we make tokens short-lived.
Example: 30 minutes.

Then users would keep logging in?
Exactly.  hat's where Refresh Tokens come in.

Refresh Token
Think of it like this.

JWT

↓

Temporary visitor pass

Refresh Token

↓

Permanent badge stored securely

When JWT expires:
Angular silently asks:
I have a valid Refresh Token.
Can I get a new JWT?
Server says:
Yes.
Here's another 30-minute token.
The user never notices.

Why not make JWT valid for 30 days? Security. If someone steals it, they have 30 days of access. Short-lived access tokens reduce that risk.

Our Authentication Flow
  User
  
  ↓
  
  Login
  
  ↓
  
  Server verifies password
  
  ↓
  
  Generate JWT
  
  ↓
  
  Generate Refresh Token
  
  ↓
  
  Angular stores tokens
  
  ↓
  
  API Requests
  
  ↓
  
  JWT Verified
  
  ↓
  
  Access Granted


Where does Authorization happen?
Suppose Rajesh tries: DELETE /api/users/10
The API reads the JWT: Role Employee
Endpoint requires: Admin

Result: 403 Forbidden
The request is authenticated but not authorized.

One More Security Layer
Remember our AI? Suppose an Employee asks:
"Show me HR salary documents."
Before searching vectors, the application checks:

Is the user authenticated? What role do they have? Which documents can they access?
Only authorized documents are searched.

This means the AI never even receives restricted information.

Why is this architecture important?
By now, you can see the pattern. Every feature in our system follows a pipeline:

  Request
  
  ↓
  
  Authentication
  
  ↓
  
  Authorization
  
  ↓
  
  Business Logic
  
  ↓
  
  Database / Blob / AI
  
  ↓
  
  Response

Nothing bypasses security.

Session 5 — Clean Architecture

First, I want to ask you a question. Suppose we build our application like this:
Controllers

↓

Everything

Example:
DocumentController
- Upload PDF
- Save Database
- Generate Embeddings
- Call OpenAI
- Send Email
- Write Logs
- Validate User

Looks easy, right? But imagine six months later.
The controller becomes: 2,000 lines.
Now someone says: "Replace OpenAI with Azure OpenAI."
You start changing code inside the controller.
Then another developer says: "We need AWS S3 instead of Azure Blob."
More changes.
Eventually: Everything depends on everything.
This is called a Big Ball of Mud.
Many real projects become like this.

So what's the solution? Instead of organizing by files, we organize by responsibilities.

Clean Architecture We'll split our project into layers.
             ┌─────────────────────────┐
             │   PRESENTATION LAYER    │
             │  (Controllers, Routes)  │
             └────────────┬────────────┘
                          │ (Calls)
                          ▼
┌───────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                   │
│         (Use Cases, DTOs, Interface Blueprints)       │
└─────────────────────────┬─────────────────────────────┘
                          │ (Orchestrates)
                          ▼
┌───────────────────────────────────────────────────────┐
│                     DOMAIN LAYER                      │
│        (Entities, Value Objects, Core Rules)          │
└───────────────────────────────────────────────────────┘
                          ▲
                          │ (Implements Interfaces)
             ┌────────────┴────────────┐
             │  INFRASTRUCTURE LAYER   │
             │  (PostgreSQL, AWS S3)   │
             └─────────────────────────┘

The Flow
Suppose Rajesh uploads a PDF.
  Angular
  
  ↓
  
  Presentation
  
  ↓
  
  Application
  
  ↓
  
  Infrastructure
  
  ↓
  
  Blob Storage
  
  ↓
  
  Database

Notice something. The request always moves inward.
Why is this important?
Imagine tomorrow.
Your company says: "We don't want Azure Blob anymore."
Instead: AWS S3
Where do we change code?
Only here: Infrastructure
Everything else stays the same.
That is one of the biggest benefits of this architecture.

Another Example
Today: OpenAI
Tomorrow: Azure OpenAI
Or
Gemini
Or
Claude
Do we rewrite the Application layer? No.
Only the Infrastructure implementation changes.

Why is this used in Enterprise?
Because enterprise systems live for years.
Technologies change.
Business rules change much more slowly.

Dependency Rule
Outer layers depend on inner layers.
Inner layers never depend on outer layers.

What does this mean for our project?
We'll likely have a solution structure similar to this:
KnowledgeHubAI.sln
├── KnowledgeHubAI.Api
│
├── KnowledgeHubAI.Application
│
├── KnowledgeHubAI.Domain
│
├── KnowledgeHubAI.Infrastructure
│
└── KnowledgeHubAI.Tests
Each project has a clear responsibility.

Why this matters for your career
Many developers can build a CRUD app.
Fewer can explain why a layered architecture improves maintainability, testability, and flexibility.
When an interviewer asks:
"Why did you choose Clean Architecture?"
You won't answer:
"Because it's popular."
You'll answer:
"Because it keeps business logic independent of frameworks and external services, making the application easier to test, maintain, and evolve. For example, if we switch from Azure Blob Storage to Amazon S3, only the Infrastructure layer changes."
"Because it keeps business logic independent of frameworks and external services, making the application easier to test, maintain, and evolve. For example, if we switch from Azure Blob Storage to Amazon S3, only the Infrastructure layer changes."

Traditional Architecture
Controller
↓
Service
↓
Repository
↓
Database

Clean Architecture
API
↓
Application
↓
Infrastructure
↓
Database

"But where is Domain?" Exactly.
The Domain sits beside the Application because it represents the business itself. Let's explain with CogniVault.
So basically entire service in traditional architecture is splitted in to Application and Domain
Here's why: Application uses the Domain to apply business rules.
Application also asks Infrastructure to perform technical operations.
Infrastructure works with Domain objects (for example, saving a Document entity), but the Domain never calls Infrastructure.
That dependency direction is one of the core ideas of Clean Architecture, and we'll see it in code very soon. I think once we create the Domain project and write our first Document entity, this model will become much more intuitive.

A more accurate picture would be
           API
            │
            ▼
      Application
       │        │
       ▼        ▼
    Domain   Infrastructure
                │
                ▼
      PostgreSQL / OpenAI / Blob Storage

Session 6 – Dependency Injection (DI)
This is one of the most asked topics in .NET interviews.
Without Dependency Injection
Suppose our Document Service needs Blob Storage.
Many beginners write:
public class DocumentService
{
    private AzureBlobStorageService _blobStorage =
        new AzureBlobStorageService();
}
Looks okay.

But now imagine:
Today: Azure Blob Storage
Tomorrow: AWS S3

What happens?
You have to modify DocumentService.

Now imagine 30 services are using Azure Blob.
You have to modify 30 places.
That's bad. This is called tight coupling.

Real Life Example: Imagine buying a phone charger. Suppose your phone is designed like this: Phone -> Only One Charger
If that charger breaks... You must buy that exact charger.
Now imagine USB-C. Much better. The phone doesn't care who provides the charger. It only cares that it receives power. That's exactly what interfaces do.

Interfaces
Instead of saying: AzureBlobStorageService
We say: IFileStorage
Notice the difference.
We're saying "I don't care how files are stored."
That's powerful.

Today IFileStorage -> Azure Blob
Tomorrow IFileStorage -> AWS S3
DocumentService doesn't change.

Dependency Injection
Now comes the important part. Instead of creating the object ourselves:
new AzureBlobStorageService()
we ask .NET: "Please give me something that implements IFileStorage."
.NET replies:
Sure. Here's Azure Blob Storage.
If we later configure AWS S3, the service receives that instead.
No code changes in DocumentService.

In Our Project
Let's look at our AI Module.
Today OpenAI
Tomorrow Azure OpenAI
Later Claude
Instead of writing: new OpenAIService()
We'll define: IChatService
Then: OpenAIChatService implements IChatService
or
AzureOpenAIChatService implements IChatService
The rest of our application doesn't care which implementation is used.

Another Example
For embeddings.
Today OpenAI Embeddings
Tomorrow Azure OpenAI Embeddings
Future Local Embedding Model
We'll define: IEmbeddingService
and plug in different implementations.

Why is this useful? Imagine OpenAI doubles its prices tomorrow. Our application doesn't need to be rewritten. We simply swap the implementation. That's one of the biggest benefits of depending on abstractions instead of concrete classes.

Service Lifetimes
.NET also manages how long objects live. You'll often see three lifetimes.
Singleton One object for the entire application.
Example: Configuration
Logger
Think of a library. One librarian serves everyone.
Scoped
One object per HTTP request.
Example: User requests:
GET /documents
Everything during that request shares the same scoped services. The next request gets a new set. This is commonly used for things like DbContext.
Transient
Create a new object every time it's requested. Good for lightweight, stateless services.

Which will we use? Most of our business services will be:
Scoped - Because they participate in handling a single web request.
Examples: DocumentService
ChatService
UserService

Putting it Together
When a user uploads a document:
Angular
↓
DocumentController
↓
IDocumentService
↓
IFileStorage
↓
Azure Blob
Notice the controller never knows about Azure Blob. It only knows it has a document service. And the document service only knows it has a file storage abstraction.

Why do companies love DI? Imagine a bug in Azure Blob. We can write a fake implementation for testing:
FakeFileStorage
No cloud account. No real uploads. Fast tests. That's another major advantage of DI.

"Why use Dependency Injection?" "Dependency Injection reduces coupling by depending on abstractions rather than concrete implementations. It improves maintainability, testability, and flexibility. For example, in our project we can switch from Azure Blob Storage to Amazon S3 or from OpenAI to Azure OpenAI by changing the registered implementation instead of modifying business logic."

The Biggest Mistake Developers Make
Most developers start like this: File → New Project
After 6 months their project looks like this:
Controllers
Models
Services
Helpers
Utils
Common
NewFolder
NewFolder2
😂 It becomes a mess. We won't do that. We're going to build this exactly like a professional team.

Session 7 – Setting up the Foundation
Today we decide how the repository itself should look. This decision will stay with us for the entire project.
Step 1 – Repository
The first question isn't: Which IDE?
It's: How many repositories?
There are two common approaches.
Option 1 – Separate Repositories
Frontend
KnowledgeHub-Angular
Backend
KnowledgeHub-API
Pros:
Independent deployment
Independent versioning
Cons:
More management
Two pipelines
Two issue trackers
Option 2 – Monorepo ⭐
KnowledgeHub-AI
├── frontend
├── backend
├── docs
├── docker
├── scripts
Everything is in one repository.
Which should we choose? I recommend Monorepo.
Why? Because: Easier to manage as a solo developer. One Git history. Easier onboarding. Easier CI/CD. Simpler portfolio presentation. Many companies also use monorepos successfully.

Step 2 – Folder Structure
I recommend something like this:
KnowledgeHub-AI
│
├── frontend/
│
├── backend/
│
├── docs/
│
├── docker/
│
├── scripts/
│
├── README.md
│
└── .gitignore

frontend Contains Angular. Nothing else.
backend Contains our .NET solution.
docs Very important.
We'll keep: Architecture diagrams, ER diagrams, API documentation, Design decisions, Roadmap
Interviewers love seeing documentation.
docker
Contains: Docker Compose Dockerfiles Infrastructure configuration scripts
Useful scripts like:
Run Project
Create DB
Backup DB
Seed Data

Step 3 – Backend Structure
Inside backend:
KnowledgeHub.sln
│
├── KnowledgeHub.Api
├── KnowledgeHub.Application
├── KnowledgeHub.Domain
├── KnowledgeHub.Infrastructure
├── KnowledgeHub.Tests

Exactly what we discussed in Clean Architecture

Step 4 – Frontend Structure
Angular already gives us a structure. We'll improve it later. Something like:
src
│
├── app
│     ├── core
│     ├── shared
│     ├── features
│     ├── layouts
│     └── routes
│
├── assets
└── environments
This scales much better than putting everything under app.

Step 5 – Branch Strategy
Even though you're working alone, let's use a professional workflow.
main
↓
develop
↓
feature/authentication
↓
feature/document-upload
↓
feature/chat
Why? Because interviewers may look at your Git history. A clean history demonstrates good engineering habits.

Step 6 – README
Most people write: KnowledgeHub Angular + .NET Done. ❌
We'll write a README that includes:
Project overview Features Architecture Screenshots (later) Technology stack Local setup Deployment Roadmap
Think of it as the landing page for your project.

Step 7 – Issues & Milestones
We'll divide the project into milestones.
Example:
Milestone 1 Authentication
Login
Register
JWT
Refresh Token
Milestone 2 Document Management
Upload
List
Delete
Milestone 3 AI Integration
Chunking
Embeddings
Search
This makes the project feel like a real product rather than one huge task.

So throughout the project we'll also practice: Writing meaningful commit messages. keeping pull-request-sized changes (even if you're the only contributor).
Documenting architectural decisions. Writing tests alongside features where practical.
Those habits are valuable regardless of the technology stack.

I have choose the application name as CogniVault, It is the stronger choice for an AI-powered enterprise knowledge platform. It directly communicates secure, intelligent storage

🎉 Welcome to CogniVault
From this point onward, we stop talking about architecture alone and start building. But before opening Visual Studio, I want to introduce something that most tutorials completely skip.

Session 8 — Engineering Standards
This may sound boring. It isn't. This is actually what separates enterprise software from hobby projects.
Question
Imagine you join Microsoft.
Day 1. Do you think someone says:
"Go create a new folder wherever you like." No.
Everything follows standards. We are going to create our own standards.

Standard 1 — Naming
We'll be consistent.
Projects
CogniVault.Api
CogniVault.Application
CogniVault.Domain
CogniVault.Infrastructure
CogniVault.Tests
Notice: Everything starts with CogniVault

Controllers
Good
DocumentsController
UsersController
ChatController
Bad
DocController
DataController
MyController
Names should tell you exactly what the class is responsible for.

Services
Good
DocumentService
ChatService
EmbeddingService
Bad
Helper
Utility
Manager
Processor
One of the biggest code smells in enterprise projects is vague names like Helper or Manager.

Standard 2 — Folder Structure
Inside the API project, I recommend:
Controllers
Middlewares
Extensions
Configurations
Filters
Common
No random folders.
If you can't explain why a folder exists, it probably shouldn't exist.

Standard 3 — API Design
A common beginner API looks like:
POST
/GetAllDocuments
/DeleteDocument
/GetUserById
Instead we'll follow REST conventions.
GET    /documents
GET    /documents/{id}
POST   /documents
PUT    /documents/{id}
DELETE /documents/{id}
Clean, predictable, and widely understood.

Standard 4 — Git Commits
Instead of: Updated code
We'll write: feat(auth): implement JWT authentication
feat(documents): add upload endpoint
fix(chat): handle empty prompt
refactor(storage): extract blob service
Even if you're the only developer, this habit pays off.

Standard 5 — Branches
main
develop
feature/auth
feature/document-upload
feature/chat
Professional teams rely on clear branch names.

Standard 6 — Configuration
Hardcoding is one of the worst habits.
Never do: string apiKey = "abc123";
Instead: 
appsettings.json
↓
Environment Variables
↓
Azure Key Vault (later)
That way secrets stay out of source control.

Standard 7 — Logging
Never write: Console.WriteLine("Error");
We'll use structured logging.
Instead of: Error
We'll record something like:
UserId: 123
Action: UploadDocument
DocumentId: 52
Status: Failed
Reason: File too large
This makes troubleshooting much easier.

Standard 8 — Error Handling
A beginner API might return: 500 Internal Server Error
Our API should return meaningful responses.
{
    "message": "Document size exceeds the maximum limit.",
    "errorCode": "DOCUMENT_TOO_LARGE"
}
This helps both frontend developers and API consumers.

Standard 9 — Documentation
Every feature should answer:
What problem does it solve?
Why was it designed this way?
How should another developer use it?
Good documentation is part of the product.

Standard 10 — The Golden Rule
Every class should answer one question:
Why does this class exist? If the answer is: "It does a little bit of everything." Then it's time to refactor.
One Principle I Want Us to Follow
This is something I've learned from working on enterprise systems.
Before writing code, always ask:
"Will this still make sense if the project becomes ten times bigger?" If the answer is "no," pause and rethink the design.
We don't need to over-engineer, but we should avoid choices that create unnecessary pain later.

Session 9 – Project Initialization
Today we are going to make our first engineering decisions.
| Layer            | Technology                                            | Why?                                         |
| ---------------- | ----------------------------------------------------- | -------------------------------------------- |
| Frontend         | Angular 22                                            | Your strongest frontend skill                |
| Backend          | ASP.NET Core 10 Web API                               | Enterprise standard, matches your experience |
| Database         | PostgreSQL                                            | Open source, pgvector support                |
| ORM              | Entity Framework Core                                 | Excellent .NET integration                   |
| AI Chat          | OpenAI (later Azure OpenAI compatible)                | RAG support                                  |
| Embeddings       | OpenAI Embedding Model                                | Semantic search                              |
| File Storage     | Azure Blob Storage (local storage during development) | Scalable file storage                        |
| Authentication   | JWT + Refresh Tokens                                  | Industry standard                            |
| Logging          | Serilog                                               | Structured logging                           |
| Testing          | xUnit                                                 | Standard .NET testing                        |
| Containerization | Docker                                                | Consistent environments                      |
| CI/CD            | GitHub Actions                                        | Automated builds and deployments             |

Decision 2 – Version 1 Scope
This is one of the most important decisions.
Many developers fail because they try to build everything at once.
❌ Version 1 will NOT include
OCR
Voice chat
Microsoft Teams integration
Slack integration
Multi-language support
AI agents
Workflow automation
Those are future versions.

✅ Version 1 WILL include
Authentication
Register
Login
JWT
Refresh Token
Documents
Upload PDF
List Documents
Delete Documents
AI
Chunking
Embeddings
Semantic Search
Chat
Citations
Admin
Users
Roles
Deployment
Docker
Azure
This is already a substantial product.

Decision 3 – Development Order
Now here's something I want to teach you. We won't build features in the order users see them. We'll build them in dependency order.
Think of building a house. You don't paint the walls before laying the foundation.

Our roadmap:
Foundation
    ↓
Authentication
    ↓
Database
    ↓
Document Upload
    ↓
Blob Storage
    ↓
Background Worker
    ↓
Embeddings
    ↓
Vector Search
    ↓
Chat
    ↓
Angular UI
Notice something? We're not starting with AI. Because AI depends on documents. Documents depend on authentication. Authentication depends on the backend foundation.

The First Sprint
If this were Jira, I'd create Sprint 1.
Sprint Goal
A user can register and log in securely.

The Backend We Will Build Eventually, our backend will look like this:
CogniVault.Api
↓
Controllers
↓
Application
↓
Domain
↓
Infrastructure
↓
PostgreSQL

Today, it will simply return:
GET /health
↓
200 OK
And that's okay. Professional software grows incrementally.

What We Will Actually Do Next This is where the architecture phase ends. Our next working session will involve:
Step 1 Create the GitHub repository.
Step 2 Create the folder structure.
Step 3 Create the .NET solution.
Step 4 Create the Angular application.
Step 5 Connect Angular and ASP.NET Core.
Step 6 Run the application.
Step 7 Commit:
chore: initialize CogniVault solution
That first commit is the beginning of our product.

One final thing I don't want CogniVault to become "another GitHub project." I want it to become something you're genuinely proud to show.
When a recruiter asks: "Tell me about a challenging project you've worked on."
I want you to spend 15–20 minutes confidently explaining:
The business problem.
The architecture.
The trade-offs.
The AI pipeline.
The security model.
The deployment strategy.
That's when this project becomes much more than code—it becomes evidence of your engineering thinking.

CogniVault Development Environment Setup

| Application / Tool     | Version                   | Installation / Verification                           |
| ---------------------- | ------------------------- | ----------------------------------------------------- |
| **Windows 11**         | Latest Updates            | Windows Update                                        |
| **Visual Studio**      | **2026**                  | Install with **ASP.NET and Web Development** workload |
| **.NET SDK**           | **10 LTS**                | `dotnet --version`                                    |
| **Visual Studio Code** | Latest                    | Install from official website                         |
| **Node.js**            | Latest **LTS**            | `node -v`                                             |
| **npm**                | Comes with Node.js        | `npm -v`                                              |
| **Angular CLI**        | **22**                    | `npm install -g @angular/cli`<br>`ng version`         |
| **GitHub Desktop**     | Latest                    | Install and sign in to GitHub                         |
| **Git**                | Latest                    | `git --version`                                       |
| **Docker Desktop**     | Latest                    | Install from Docker Desktop                           |
| **WSL**                | **WSL 2**                 | `wsl --install` *(Administrator PowerShell)*          |
| **Ubuntu (WSL)**       | Latest LTS                | Installed automatically with `wsl --install`          |
| **Docker Compose**     | Comes with Docker Desktop | `docker compose version`                              |

VS Code Extensions
Install these extensions:
Extension	Required
C# Dev Kit	✅
Angular Language Service	✅
Docker	✅
ESLint	✅
Prettier	Recommended
GitLens	Recommended
Error Lens	Recommended

Folder Structure
CogniVault/
│
├── backend/
├── frontend/
├── docker/
├── docs/
├── scripts/
│
├── README.md
└── .gitignore

Verification Commands
.NET
dotnet --version
dotnet --list-sdks

Node.js
node -v

npm
npm -v

Angular
ng version

Git
git --version

Docker
docker --version
docker info
docker compose version

WSL
wsl --version
wsl -l -v
Expected:
NAME              STATE      VERSION
Ubuntu            Stopped    2
docker-desktop    Running    2

Development Workflow
Backend
cd backend
dotnet watch
Frontend
cd frontend
ng serve
Docker
docker compose up

✅ CogniVault Milestone 1 Completed
Development Environment
| Component          | Status |
| ------------------ | ------ |
| Windows 11         | ✅      |
| Visual Studio 2026 | ✅      |
| .NET 10 LTS        | ✅      |
| VS Code            | ✅      |
| Node.js LTS        | ✅      |
| npm                | ✅      |
| Angular CLI 22     | ✅      |
| GitHub Desktop     | ✅      |
| Git                | ✅      |
| Docker Desktop     | ✅      |
| WSL 2              | ✅      |
| Ubuntu             | ✅      |
| Docker Compose     | ✅      |
Status: 100% Complete

[✓] Product Idea
        ↓
[✓] Architecture
        ↓
[✓] Technology Selection
        ↓
[✓] Development Environment
        ↓
[➡] Project Creation
        ↓
[ ] Authentication
        ↓
[ ] Database
        ↓
[ ] Document Upload
        ↓
[ ] AI Features
        ↓
[ ] Deployment

Solution Structure
CogniVault/
│
├── backend/
│   ├── CogniVault.sln
│   │
│   ├── src/
│   │   ├── CogniVault.Api
│   │   ├── CogniVault.Application
│   │   ├── CogniVault.Domain
│   │   └── CogniVault.Infrastructure
│   │
│   └── tests/
│       ├── CogniVault.UnitTests
│       └── CogniVault.IntegrationTests
│
├── frontend/
│   └── cognivault-web/
│
├── docker/
│
├── docs/
│   ├── architecture/
│   ├── decisions/
│   ├── diagrams/
│   ├── api/
│   └── roadmap/
│
├── scripts/
│
├── README.md
│
└── .gitignore

Why this Structure?
backend
Everything related to .NET.
Nothing Angular-related should ever be inside this folder.
src
Contains only production code.
Api
Application
Domain
Infrastructure
No tests.
No scripts.
No experiments.
tests
All test projects live here.
UnitTests
IntegrationTests
This separation is common in enterprise applications and keeps production code isolated from test code.
frontend
We'll create:
cognivault-web
Why not just call it angular?
Because one day we might have:
cognivault-admin
cognivault-mobile
cognivault-public
Using a descriptive application name scales better.
docker
Later this will contain:
docker-compose.yml
postgres
redis
and any custom Dockerfiles.
docs
Not just random documents.
Real project documentation.
Architecture
API
Decision Records
Diagrams
Roadmap
scripts
Automation scripts.
For example:
setup.ps1
reset-db.ps1
seed-data.ps1

The Backend Projects
CogniVault.Domain
This is the heart of the application.
Contains:
Entities
Value Objects
Domain Rules
Business Logic
It knows nothing about:
SQL
ASP.NET
OpenAI
Docker

CogniVault.Application
Contains:
Features
Commands
Queries
Validators
Interfaces
This is where we'll use Vertical Slice Architecture.
Example:
Features
Authentication
Documents
Chat

CogniVault.Infrastructure
Contains implementations.
Examples:
PostgreSQL
OpenAI
Blob Storage
Logging
Email
If we replace PostgreSQL with another database, most of the application remains unchanged.

CogniVault.Api
The entry point.
Responsible for:
Controllers
Authentication
Middleware
Swagger
Dependency Injection
Hosting

Before we move to Session 11, please run these 5 commands and send me the output:
1. .NET
dotnet --version
dotnet --list-sdks
2. Node
node -v
3. npm
npm -v
4. Angular
ng version
5. Docker
docker --version
docker compose version
6. GitHub Desctop or Cmd
git --version

Creating .Net Project
1). Using GUI
File
   ↓
New
   ↓
Projet
Blank Solution
We are doing this because we need our oun clear architecture where as selecting a pre set templates would create the default structure which we dont want.

2). From Commands
open powershell or cmd
open backend folder use cd
execute dotnet new sln -n CogniVault
dotnet Invokes the .NET CLI.
new Tells .NET to create something from a template.
sln Means: Create a Solution. A solution is not a project. Think of it as a workspace that groups multiple projects together.
-n Means: Name
CogniVault Creates: CogniVault.slnx (Microsoft introduced the Solution File (.slnx) format. The difference is that .slnx is a modernized format that's easier to maintain and is designed to evolve better than the legacy .sln format.)
You can verify by executing dir - which will show the created CogniVault.slnx file name available with in backend

Okay Now we will be developing the backend but where we will start it is Domain Layer by crating a Domain Project okay but why we are starting at this
Imagine You Are Building a Car
Can you install the steering wheel first? No.
Can you install the engine first? Not really.
First, you need the chassis (foundation) the car structure and pillars. Everything else is attached to it.
Clean Architecture follows the same philosophy.

Our Projects creation order is
CogniVault.Domain
CogniVault.Application
CogniVault.Infrastructure
CogniVault.Api

Now let's decide which one can exist independently.
🟢 1. Domain (Foundation)
Question: Can the Document class exist without PostgreSQL? ✅ Yes. Can it exist without Angular? ✅ Yes. Can it exist without OpenAI? ✅ Yes. an it exist without an API? ✅ Yes.
So the Domain is completely independent.
That's why it's created first.
Think of it as: The business exists before the software.

🔵 2. Application
Now we ask: How do users interact with this business?
For CogniVault:
Upload Document
Ask Question
Delete Document
Search Knowledge
These are use cases. Can we write an Upload Document feature without knowing what a Document is? ❌ No.
We need the Domain.
That's why:
Application
        │
        ▼
Domain
Application depends on Domain.

🟠 3. Infrastructure
Now we ask another question.
The Application says: "Save this Document."
But... Where?
PostgreSQL? Blob Storage? Azure? AWS? Local disk?
Application doesn't know.
Infrastructure answers: "I'll take care of it."
Can Infrastructure exist without Domain? ❌ No.
It needs the Document class.
Can Infrastructure exist without Application? Usually no, because it implements interfaces and supports the application's needs.
So:
Infrastructure
        │
        ▼
Application
        │
        ▼
Domain

🔴 4. API
Finally... How does a user reach the application? HTTP. REST. Swagger. Controllers. Authentication.
The API simply exposes the Application to the outside world.
Can the API exist without Application? ❌ No.
Because the Controller eventually does something like:
_application.UploadDocument(command);
If Application doesn't exist, the API has nothing to call.
Building Order
Now the creation order becomes obvious.
Step 1
Domain
↓
Step 2
Application
↓
Step 3
Infrastructure
↓
Step 4
API
We're building from the center outward.

Runtime Order
When the application is running, the flow is the opposite.
Browser
↓
API
↓
Application
↓
Domain
↓
Infrastructure
↓
PostgreSQL

The Rule I Follow
Whenever I start a new Clean Architecture project, I ask:
"If I deleted PostgreSQL, Azure, Angular, Docker, and the Internet, what part of my software would still make sense?"
The answer is: Domain
That's why it's always the foundation.
🚀 Now We're Ready

Our creation order will be:

✅ Create the src folder.
✅ Create CogniVault.Domain (Class Library).
✅ Add it to the solution.
✅ Create CogniVault.Application.
✅ Add a reference from Application → Domain.
✅ Create CogniVault.Infrastructure.
✅ Add references from Infrastructure → Application and Infrastructure → Domain.
✅ Create CogniVault.Api.
✅ Add a reference from API → Application (and later wire up Infrastructure through dependency injection).
Notice that every project is created only after the projects it depends on already exist. That makes the dependency graph natural and avoids circular references.

Next we create a src and tests folder with in backend folder where we already have a slnx file
We can create using File Explorer or Also use commands in PowerShell
mkdir src
mkdir tests

Creating the
dotnet new classlib -n CogniVault.Domain -o src\CogniVault.Domain
or can use this if you are already with in src dir without using output location(-o)- dotnet new classlib -n CogniVault.Domain
Creates a Class Library. A Class Library is simply a project that contains C# classes. It cannot run by itself. That is exactly what we want because the Domain is not an application. It's a library of business concepts.

once that is done we have this folder structure inside src
backend/
│
├── src/
│   └── CogniVault.Domain/
│       ├── CogniVault.Domain.csproj
│       ├── Class1.cs
│       └── obj/
│
├── tests/
└── CogniVault.slnx

Why classlib? Another great question to think about.
Could we create a Web API? No.
Could we create a Console App? No.
Because the Domain should never start running. It is just a collection of business classes like:
Document
ChatSession
Citation
KnowledgeBase
User
Those classes will later be used by the Application, Infrastructure, and API.

Session 11.1 - What is a Class Library?
Let's start with a simple question.
What is a Project?
A lot of developers think: "A project is a folder." ❌ Not exactly.
A project is actually defined by a .csproj file.
For example:
CogniVault.Domain/
│
├── CogniVault.Domain.csproj   ← This is the project
├── Class1.cs
└── obj/
Without the .csproj file, it's just a folder with C# files.

It tells .NET:
Which SDK to use
Target Framework like .net 10
NuGet packages
Project references
Build settings

What is inside a .csproj?
For our Domain project, it will look something like:
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
Don't worry about the XML yet. Let's break it down.
<Project Sdk="Microsoft.NET.Sdk">
This tells .NET: "This project uses the standard .NET SDK."

<TargetFramework>net10.0
Means: Build this project using .NET 10.

Nullable enable - Enables nullable reference types.
Instead of accidentally writing: string name = null;
the compiler helps you catch potential null-related bugs.

ImplicitUsings Instead of writing this in every file:
using System;
using System.Collections.Generic;
using System.Linq;
.NET adds common namespaces automatically.
Cleaner code.

Why a Class Library? Now the important question.
Why don't we create: ASP.NET Core Web API
Because our Domain doesn't run.
It doesn't listen on a port. It doesn't have HTTP. It doesn't have Swagger.
It doesn't have Controllers. It doesn't even have Program.cs. It only contains business classes.
Imagine This
Our Domain might eventually contain:
Domain
│
├── Entities
│     ├── Document.cs
│     ├── User.cs
│     └── ChatSession.cs
│
├── ValueObjects
│
├── Enums
│
├── Exceptions
│
└── Common

These are just classes. Nothing runs. Then Who Runs?
Later we'll create: CogniVault.Api
That project will contain:
Program.cs which starts the web server.
Think of it this way:
Domain is like a book.
API is the person reading the book aloud.
The book exists on its own. Why Not Put Everything in API? Because then we'd end up with:
API
Controllers
Models
Repositories
Services
Database
Business Logic
Helpers
Utilities
After a year:
800 files Very difficult to maintain.
Instead:
Domain has only business concepts.
What Will the Command Generate?
When we run: dotnet new classlib -n CogniVault.Domain -o src/CogniVault.Domain
.NET creates approximately this:
src/
└── CogniVault.Domain/
    │
    ├── CogniVault.Domain.csproj
    ├── Class1.cs
    └── obj/
Let's understand each item.
Class1.cs
A placeholder. Microsoft creates it just to show the project isn't empty.
We'll delete it.
obj
Temporary build files. Generated automatically. Never edit it. Never commit it. Our .gitignore already ignores it.

.csproj
The heart of the project. Everything else depends on this file.
One More Thing
A project does not become part of the solution automatically. After creating the Domain project, we'll explicitly add it to our solution. That's an important distinction:

Solution (.slnx)
        │
        ├── Domain Project
        ├── Application Project
        ├── Infrastructure Project
        └── API Project

Creating a project and adding it to the solution are two separate steps.

Summary (One Line Per Concept)
Solution (.slnx) → Organizes multiple projects.
Project (.csproj) → Defines how one project is built.
Class Library → A project containing reusable C# classes; it doesn't run by itself.
API Project → The runnable web application that hosts the application.
obj folder → Temporary build artifacts.
Class1.cs → Placeholder file that we'll replace with our own code.
🚀 Now we're ready.

Session 11.2 - Adding the Domain Project to the Solution
Before we type the command, I want to ask you something.
Right now we have:
backend/
│
├── CogniVault.slnx
│
└── src/
    └── CogniVault.Domain/
        ├── CogniVault.Domain.csproj
        └── ...
Notice something... The solution and the project are just sitting beside each other.
The solution doesn't know the project exists yet.
Think of it like this. Imagine you bought a new TV. Does your TV remote automatically know about the TV? No. You have to pair it.
The same thing happens here.
What is a Solution? Think of the solution as a playlist.
Spotify Playlist
Song A
Song B
Song C
The songs exist independently. The playlist simply says "These are the songs I want together."

Exactly the same.
CogniVault.slnx
↓
Domain
↓
Application
↓
Infrastructure
↓
API

The solution doesn't contain code. It simply keeps a list of projects.
Without adding it... Visual Studio would open the solution and show:
Solution 'CogniVault'
(Empty)
you can test this manually opening slnx file in VS or executing below cmd
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet sln list
No projects found in the solution.

Even though the Domain project exists on disk. Because the solution hasn't been told about it.
The Command we should execute to add the project to soln is
Run: dotnet sln add src/CogniVault.Domain/CogniVault.Domain.csproj
Let's break it down.
dotnet - Run the .NET CLI.
sln - Work with a solution.
Notice this is different from:
dotnet new
Earlier we created something. Now we're managing the solution.
add Means Add a project into the solution. Not create. Not build.Just register it.
Project Path
src/CogniVault.Domain/CogniVault.Domain.csproj
We're simply telling the solution Here's a project. Please include it.
What Happens Internally?
Before:
CogniVault.slnx
↓
(no projects)

After:
CogniVault.slnx
↓
CogniVault.Domain

Nothing changes inside Domain. Nothing changes inside .csproj. Only the solution is updated.

Verify Run:
dotnet sln list
Expected:
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet sln list
Project(s)
----------
src\CogniVault.Domain\CogniVault.Domain.csproj

Another Important Concept
Many beginners think
Solution
↓
contains
↓
Project

Actually...
Solution
knows about
↓
Project

The project can exist without a solution. In fact, you can build it directly: dotnet build src/CogniVault.Domain/CogniVault.Domain.csproj
The solution is mainly a convenience for organizing and working with multiple projects together.

Next Project: Application
Before I give you the command, I want to explain why Application is a Class Library too.
Many people think: "Application sounds like the main application." It isn't.
In Clean Architecture, Application is not executable.
It also doesn't run by itself. Just like Domain, it's a library.
Think about this When someone uploads a document:
UploadDocument
Is that an HTTP concept? ❌ No. Is that a database concept? ❌ No.
It's a business use case. That's why it belongs in the Application project.
So why is it a Class Library?
Because it simply contains classes like:
UploadDocumentCommand
UploadDocumentHandler
DeleteDocumentHandler
AskQuestionHandler
SearchDocumentsHandler
These are just C# classes.
They don't listen for HTTP requests. They don't open ports. They don't start a web server. The API will use them later.
The Command
Just like before, we'll create it under src.
dotnet new classlib -n CogniVault.Application -o src/CogniVault.Application
What will happen? After running it:
src/
│
├── CogniVault.Domain/
│
└── CogniVault.Application/
    ├── CogniVault.Application.csproj
    ├── Class1.cs
    └── obj/

Exactly the same structure as Domain. Then we'll do two important things
Add it to the solution:
dotnet sln add src/CogniVault.Application/CogniVault.Application.csproj
Create our first project reference:
dotnet add src/CogniVault.Application/CogniVault.Application.csproj reference src/CogniVault.Domain/CogniVault.Domain.csproj
Notice this new command:
dotnet add ... reference ...

This is the first time one project will say: "I need classes from another project."
That single line is what establishes:
Application
      │
      ▼
Domain

Run these three commands, one at a time:
dotnet new classlib -n CogniVault.Application -o src/CogniVault.Application
dotnet sln add src/CogniVault.Application/CogniVault.Application.csproj
dotnet add src/CogniVault.Application/CogniVault.Application.csproj reference src/CogniVault.Domain/CogniVault.Domain.csproj

so once we added a project reference to Domain in Application, The csproj of Application Project will be updated with the below code and evedently we cab use the classes of Domain in Application proj.

<ItemGroup>
  <ProjectReference Include="..\CogniVault.Domain\CogniVault.Domain.csproj" />
</ItemGroup>

So basically both the class libraries dont know about each. As we want to use the Domain class lib we should be adding project reference.
Now Application can use every public class inside Domain.

For example, later we'll write:
using CogniVault.Domain.Entities;

public class UploadDocumentHandler
{
    public void Handle()
    {
        Document document = new Document();
    }
}

Why does this compile? Because of ProjectReference.
Without it... Visual Studio would say: The type or namespace 'CogniVault.Domain' could not be found.

Project Reference = Permission to use another project's public classes.

1. Namespace : A Namespace is a logical grouping of related classes to organize code and avoid naming conflicts.
Example:
namespace CogniVault.Domain.Entities;
public class Document
{
}
Purpose: Organizes code. prevents duplicate class names. Used with the using keyword.
Mental Model: Folder (Windows) = Namespace (C#)

2. Assembly : An Assembly is the compiled output of a project (.dll or .exe).
Example:
CogniVault.Domain.csproj
        │
Build
        ▼
CogniVault.Domain.dll
Purpose: Contains compiled code. Can be shared and referenced by other projects.
Mental Model:
Many .cs files
        │
Compiler
        ▼
One .dll (Assembly)

3. Project Reference : A Project Reference allows one project to use the public classes of another project.
Example:
<ProjectReference Include="..\CogniVault.Domain\CogniVault.Domain.csproj" />
Purpose: Links two projects. Makes another project's assembly available during compilation.
Mental Model:
Application
      │
ProjectReference
      ▼
Domain

Application can now use:
using CogniVault.Domain.Entities;
Document document = new();
How They Work Together
Solution (.slnx)
│
├── Domain Project (.csproj)
│      │
│      ├── Namespace
│      │      CogniVault.Domain.Entities
│      │
│      └── Build
│             ▼
│       CogniVault.Domain.dll (Assembly)
│
└── Application Project
       │
       ├── ProjectReference
       │
       └── using CogniVault.Domain.Entities;
One-Line Memory Trick
Namespace → Organizes classes.
Assembly → Compiled output of a project (.dll/.exe).
Project Reference → Allows one project to use another project's assembly.

The Golden Rule
The layer that needs something defines the interface.
The layer that provides it implements the interface.

Example
Who owns the business requirement? Ask yourself this question.
Imagine you tell a carpenter:
"Build me a table." Who decides what the table should look like? You.
Not the carpenter. The carpenter only builds it. Exactly the same thing here.

Application says: "I need someone who can save a document.
Infrastructure says: "I'll do it using PostgreSQL."
The requirement belongs to Application. The implementation belongs to Infrastructure.

⭐ The Principle I Want You to Remember
Don't think of interfaces as belonging to the implementation.
Think of them as belonging to the consumer.
For example:
Application needs to save documents → IDocumentRepository belongs to Application.
Application needs to send emails → IEmailService belongs to Application.
Application needs AI embeddings → IEmbeddingService belongs to Application.
Infrastructure's job is simply to say: "I know how to fulfill that contract."

Creating the Infrastructure
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet new classlib -n CogniVault.Infrastructure -o src/CogniVault.Infrastructure
The template "Class Library" was created successfully.
Processing post-creation actions...
Restoring C:\Users\rajes\Documents\GitHub\CogniVault\backend\src\CogniVault.Infrastructure\CogniVault.Infrastructure.csproj:
Restore succeeded.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet sln add src/CogniVault.Infrastructure/CogniVault.Infrastructure.csproj
Project `src\CogniVault.Infrastructure\CogniVault.Infrastructure.csproj` added to the solution.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet add src/CogniVault.Infrastructure reference src/CogniVault.Application
Reference `..\CogniVault.Application\CogniVault.Application.csproj` added to the project.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet add src/CogniVault.Infrastructure/CogniVault.Infrastructure.csproj reference src/CogniVault.Domain/CogniVault.Domain.csproj
Reference `..\CogniVault.Domain\CogniVault.Domain.csproj` added to the project.

Session 13 - Creating the API Project This is the last project in our backend architecture.
After this, our solution structure will be complete. Before we type any command, I want to answer one question.
Why is API different from the other three projects? Let's compare them.
Project	Type	Can it Run?	Responsibility
Domain	Class Library	❌ No	Business Objects
Application	Class Library	❌ No	Use Cases & Interfaces
Infrastructure	Class Library	❌ No	Implementations
API	ASP.NET Core Web API	✅ Yes	Entry point of the application

Notice something.
The first three are libraries.
The API is an application.
What does "Run" mean? When you type: dotnet run
Which project should start? Should Domain start? No. It doesn't know what to do.
Should Application start? No. It only contains business logic.
Should Infrastructure start? No. It only contains implementations.
The only project that knows how to start a web server is:
CogniVault.Api

When you execute: dotnet run
The following happens:
dotnet run
      │
      ▼
Program.cs
      │
      ▼
Create Web Server
      │
      ▼
Configure Services
      │
      ▼
Configure Middleware
      │
      ▼
Listen on http://localhost:xxxx

Everything starts from one file: Program.cs

What is Program.cs?
Think of it as: The Main() method of your application.
Years ago, every C# application looked like this:

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Application Started");
    }
}

Modern ASP.NET Core hides a lot of that boilerplate, but Program.cs is still the entry point.
It's the first code that executes.
Why does API reference Application?
Imagine a controller.
public class DocumentsController
{
}
Inside it we'll eventually write: UploadDocumentHandler
Where is that class? Application
So API needs Application.
Why does API reference Infrastructure? Remember this line from our earlier discussion? IDocumentRepository
Application only knows the interface.
Who provides the implementation? Infrastructure
Inside Program.cs, we'll tell ASP.NET Core: "Whenever someone asks for IDocumentRepository, give them PostgreSqlDocumentRepository."
That registration happens in the API project. Therefore the API needs Infrastructure.
Does API reference Domain? Technically, it can.
But in our design, we'll try to avoid it.
Why?
Because the API should communicate with the Application layer, not directly with business entities.
This keeps the API thin and focused on HTTP concerns.

Final Dependency Diagram
After creating the API project, our architecture will look like this:

                 API
               /     \
              ▼       ▼
      Application   Infrastructure
             │          │
             └────┬─────┘
                  ▼
               Domain

Read it as:
API knows about Application.
API knows about Infrastructure.
Application knows about Domain.
Infrastructure knows about Application and Domain.
No arrows point outward from Domain.
That's the core principle of Clean Architecture.

Commands
1. Create the project
dotnet new webapi -n CogniVault.Api -o src/CogniVault.Api
2. Add it to the solution
dotnet sln add src/CogniVault.Api/CogniVault.Api.csproj
3. Add references
Reference Application:
dotnet add src/CogniVault.Api/CogniVault.Api.csproj reference src/CogniVault.Application/CogniVault.Application.csproj
Reference Infrastructure:
dotnet add src/CogniVault.Api/CogniVault.Api.csproj reference src/CogniVault.Infrastructure/CogniVault.Infrastructure.csproj

PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet new webapi -n CogniVault.Api -o src/CogniVault.Api
The template "ASP.NET Core Web API" was created successfully.
Processing post-creation actions...
Restoring C:\Users\rajes\Documents\GitHub\CogniVault\backend\src\CogniVault.Api\CogniVault.Api.csproj:
Restore succeeded with 1 warning(s) in 4.4s
    C:\Users\rajes\Documents\GitHub\CogniVault\backend\src\CogniVault.Api\CogniVault.Api.csproj : warning NU1903: Package 'Microsoft.OpenApi' 2.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc
Restore succeeded.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet sln add src/CogniVault.Api/CogniVault.Api.csproj
Project `src\CogniVault.Api\CogniVault.Api.csproj` added to the solution.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet add src/CogniVault.Api/CogniVault.Api.csproj reference src/CogniVault.Application/CogniVault.Application.csproj
Reference `..\CogniVault.Application\CogniVault.Application.csproj` added to the project.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> dotnet add src/CogniVault.Api/CogniVault.Api.csproj reference src/CogniVault.Infrastructure/CogniVault.Infrastructure.csproj
Reference `..\CogniVault.Infrastructure\CogniVault.Infrastructure.csproj` added to the project.
PS C:\Users\rajes\Documents\GitHub\CogniVault\backend> Infrastructure

Session 14 - Understanding the ASP.NET Core Web API Project
Open your project.nYou should see something like this:
CogniVault.Api
│
├── Properties
│   └── launchSettings.json
│
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── CogniVault.Api.csproj
├── CogniVault.Api.http
└── (WeatherForecast files - depending on template)
Let's understand each one.

1. CogniVault.Api.csproj You've already seen .csproj files before. The API one is no different.
Its job is:
Target .NET version
NuGet packages
Project References
Build settings
For example:
<Project Sdk="Microsoft.NET.Sdk.Web">
Notice something different? Earlier our class libraries had: Microsoft.NET.Sdk
Now we have Microsoft.NET.Sdk.Web
Why? Because this isn't just C# code anymore.
It also includes:
Kestrel Web Server
ASP.NET Core
MVC
Dependency Injection
Configuration
Middleware
HTTP Pipeline
Everything needed to run a web server.

2. Program.cs ⭐ (Most Important File) If I had to rank the files:
Program.cs is the brain of the application. Everything starts here.
Imagine pressing:
dotnet run
What happens?
dotnet run
↓
Program.cs executes
↓
Creates Web Application
↓
Registers Services
↓
Builds App
↓
Configures Middleware
↓
Starts Web Server
↓
Listening on localhost
Everything begins here. Think of Program.cs as...
Imagine opening a restaurant. Before customers arrive you: Unlock the door Turn on lights Start POS machine Hire employees Prepare kitchen Only then Customers enter.
That's exactly Program.cs.
Look inside Program.cs
It probably looks similar to:
var builder = WebApplication.CreateBuilder(args);
Question:
What is "builder"?
Think of it as: The application is still under construction.
Nothing is running yet. You're only configuring it. Then you'll see: builder.Services.AddOpenApi();
or builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
depending on the template.
This means:
Register Swagger. Notice the word: Services We'll spend an entire session on Dependency Injection because this line is one of the most important concepts in ASP.NET Core.
For now, think of it as: "Register things the application can use."
Later you'll see
var app = builder.Build();
This means: Construction is finished.
Now create the application. Then you'll see
app.Run(); This is the line that starts the web server.
Without it... Nothing happens.

3. appsettings.json
Imagine your application has settings.
Example: Database Name Connection String API Keys Logging URLs Where should they go?
Inside code? No. They belong in configuration. That's exactly what this file is for.
Example:

{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}

Later ours will contain:
{
  "ConnectionStrings": {
    "PostgreSql": "..."
  },
  "OpenAI": {

  },
  "BlobStorage": {
  }
}

Notice No business logic. Only settings. Why not hardcode?
Bad:
string connection =
"Server=localhost...";
Good:
ConnectionStrings Then read it.
Now changing environments becomes easy.

4. appsettings.Development.json
Suppose you're developing. Database:
localhost
Production?
Azure PostgreSQL
Should you edit code every time? No.
Instead. Development uses appsettings.Development.json
Production uses appsettings.json
or
Environment Variables. ASP.NET automatically loads the correct one. This is very powerful.

5. launchSettings.json Many beginners confuse this with appsettings.
They are completely different.
appsettings Configuration for your application.
launchSettings Configuration for Visual Studio / VS Code while launching.
Example:
{
    "applicationUrl":
    "https://localhost:7243"
}
This tells Visual Studio Which URL to launch.
It is NOT part of your production deployment.

6. WeatherForecast.cs Microsoft adds this only as a demo.
It teaches: Controllers
Models
JSON
GET endpoint

For CogniVault We're deleting it later. Because it's unrelated to our project.

7. CogniVault.Api.http
Many beginners ignore this file. It's actually useful. It allows sending HTTP requests directly from VS Code.
Example:
GET https://localhost:5001/weatherforecast
Click
Send Request
No Postman needed. Later we may use it for quick API testing.

8. Properties Folder Contains launchSettings.json Nothing more. Think of it as IDE launch configuration.
What Happens When You Press Run?
Let's connect everything.

dotnet run
        │
        ▼
Program.cs
        │
        ▼
Read appsettings.json
        │
        ▼
Register Services
        │
        ▼
Configure Middleware
        │
        ▼
Create HTTP Pipeline
        │
        ▼
Start Kestrel
        │
        ▼
Listening:
https://localhost:5001
Kestrel

Question. Who is actually listening for HTTP requests?
Not Program.cs. Program.cs starts Kestrel.
Kestrel is ASP.NET Core's built-in web server.
Browser
↓
Kestrel
↓
Program.cs Configuration
↓
Middleware
↓
Controller
↓
Application
↓
Infrastructure
↓
Database

Our Complete Architecture So Far
                    Browser
                        │
                        ▼
                  Kestrel Server
                        │
                        ▼
                  Program.cs
                        │
                        ▼
                  Controllers
                        │
                        ▼
                 Application
                        │
                        ▼
               Domain + Infrastructure

Now lets go though the API project what and all are generated
I can also see you're using the .NET 10 Web API template, which is much cleaner than the old .NET 6/7 templates. Microsoft has removed a lot of boilerplate, which is why you see AddOpenApi() instead of AddSwaggerGen().
Today, let's go through every single line.
Program.cs
Line 1 var builder = WebApplication.CreateBuilder(args);
This is the first line that executes when you run: dotnet run
Think of it as: "Create a new ASP.NET Core application that I'm about to configure." Nothing is running yet.
The server hasn't started. The application doesn't even exist yet. It's just preparing everything.
What does builder contain?
Imagine you're building a house.
Before construction starts you collect:
Cement
Bricks
Wood
Electric wiring
Builder is exactly that. It contains everything needed to build the application. Internally it contains things like:
Configuration
Dependency Injection Container
Logging
Environment
Hosting Settings
Web Server Configuration
Think of it as a giant toolbox.

What is args?
CreateBuilder(args)
args are command-line arguments.
Example: dotnet run --environment Production
ASP.NET can read those values. For now, you don't need to worry about them.

Next
builder.Services.AddOpenApi();
This is one of the most important lines. Let's break it apart.
What is builder.Services?
Remember when we discussed interfaces?
Application contains: IDocumentRepository
Infrastructure contains: PostgreSqlRepository
Question:
How will ASP.NET know that
IDocumentRepository
↓
PostgreSqlRepository

? It won't. Someone has to tell it.
That place is: builder.Services
This is called the Dependency Injection Container. Think of it as a phone directory.
Example:
Need:
IDocumentRepository
↓
Call
PostgreSqlRepository
Later we'll write things like:
builder.Services.AddScoped<
    IDocumentRepository,
    PostgreSqlRepository>();
Now ASP.NET knows exactly what to create.

Why AddOpenApi()?
OpenAPI is simply a specification that describes your API. Think of it as a catalog.
It says:
Available APIs
↓
GET /documents
POST /documents
DELETE /documents/{id}
Tools like Swagger use this catalog to generate documentation and interactive testing pages.
In .NET 10, Microsoft introduced a simpler built-in OpenAPI setup, which is why the template is cleaner.

Next
var app = builder.Build();
This is a huge moment.
Before this line: builder
After this line: app

Meaning: Construction is finished. The application now exists.
Think of buying a car.
Before assembly: Builder
After assembly: Car
Exactly the same idea.

Next
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

Question.
How does it know we're in Development? Remember this? "ASPNETCORE_ENVIRONMENT": "Development" from launchSettings.json.
That's where it comes from.
So during development: Enable OpenAPI
In production we usually don't expose everything publicly.

Next
app.UseHttpsRedirection();
Suppose someone types: http://localhost:5231
This middleware says: "No."
Redirect them to https://localhost:7145
HTTPS encrypts communication between the browser and your API. This is why modern APIs default to HTTPS.

Next
var summaries = new[]
This is just Microsoft generating sample data. Nothing architectural here. We'll delete it later.

Next
app.MapGet("/weatherforecast", () =>
This is your first endpoint.
Think of it as:
URL
↓
Method
↓
Code
Example:
GET/weatherforecast
↓
Run this code.
Inside it
Enumerable.Range(1, 5) Creates five fake weather records.
Again... Just demo code. We'll delete it.

Next
.WithName("GetWeatherForecast"); This gives the endpoint a name. OpenAPI uses it. Nothing special.

Finally
app.Run(); This is another extremely important line. Without it... Nothing happens.
This line starts:
Kestrel
HTTP Pipeline
Listening on ports
After this line the application keeps running.

What is a Pipeline?
Imagine a factory. A request enters.
Browser
↓
HTTPS
↓
Authentication
↓
Authorization
↓
Logging
↓
Endpoint
↓
Response

That sequence is called the HTTP pipeline.
Every app.Use...() you add becomes another stage in that pipeline.

WeatherForecast Record
record WeatherForecast(...)
Instead of creating
class WeatherForecast
{
}
Microsoft now uses a C# record.
Records are ideal for immutable data transfer objects (DTOs). For this demo, it's just a compact way to define the response model.
We'll discuss records later because CogniVault will use them in several places.

CogniVault.Api.http file:
GET {{CogniVault.Api_HostAddress}}/weatherforecast/
This file lets you test the API directly from your editor. Think of it as a lightweight Postman.
Later, you might have: 
POST {{Host}}/documents

###

GET {{Host}}/documents

###

DELETE {{Host}}/documents/1

Very handy during development.

appsettings.json :
{
  "Logging": {},
  "AllowedHosts": "*"
}
Logging Controls how much information ASP.NET writes to the console or log files.
Example:

Information
Warning
Error
Critical

AllowedHosts
"*"
Means: Accept requests from any host.
In production you often restrict this.

launchSettings.json
This file is only for local development.
For example:
"applicationUrl":
"https://localhost:7145;http://localhost:5231"
When Visual Studio or dotnet run starts the project, it uses these URLs. This file is not deployed to production.

The Complete Startup Flow
Now you can finally visualize what happens:
dotnet run
      │
      ▼
CreateBuilder()
      │
      ▼
Load Configuration
(appsettings.json)
      │
      ▼
Read Environment
(Development)
      │
      ▼
Register Services
(builder.Services)
      │
      ▼
Build Application
(builder.Build())
      │
      ▼
Configure Middleware
(app.Use...)
      │
      ▼
Map Endpoints
(app.MapGet / app.MapControllers)
      │
      ▼
Run Kestrel
(app.Run())
      │
      ▼
Listening for HTTP Requests
🏆 One important observation

Notice something about Program.cs.
There is no business logic. No SQL. No repository. No validation. No OpenAI. No PostgreSQL. Its only responsibility is to compose the application:
Register services
Configure middleware
Map endpoints
Start the server

This is a hallmark of a well-structured ASP.NET Core application. Business rules belong in the Application layer, data access belongs in Infrastructure, business entities belong in Domain, and Program.cs simply wires everything together.

So what we have built so far
Let's look at the application as a whole.

Browser(we make a request call like get or other)
    │
    ▼
Kestrel Server(first kestral will listens the request through the port)
    │
    ▼
Program.cs
    │
    ▼
(No Endpoints Yet)

Notice something? If you run the application now: dotnet run
The application starts successfully. The web server is listening.
But... There is no endpoint.
If someone calls: GET /documents
ASP.NET Core will respond: 404 Not Found
Because we haven't told it how to handle any requests yet. And that's completely expected.

The Next Big Topic This is where the real ASP.NET Core journey begins. We need to answer a fundamental question:
How does an HTTP request become a C# method call?
For example: POST /api/documents
How does ASP.NET know it should execute:
UploadDocument()
{
    ...
}
The answer is:
Controllers This is one of the most important concepts in ASP.NET Core.

The actual flow is:
Browser
↓
Kestrel (Web Server)
↓
Program.cs Configuration
(Middleware Pipeline)
↓
Routing
↓
Controller
↓
Application
↓
Infrastructure
↓
Database

So the answer is not directly the Controller.
Let's understand it with a simple analogy.
Imagine a Hospital A patient walks into the hospital.
Does he immediately reach the doctor? No.
The flow is:
Hospital Gate
↓
Reception
↓
Reception checks where to send him
↓
Doctor
↓
Lab
↓
Reports

In ASP.NET Core:
Browser
↓
Kestrel
(Hospital Gate)
↓
Middleware
(Reception)
↓
Routing
(Which doctor?)
↓
Controller
(Doctor)
↓
Application
(Treatment)
↓
Infrastructure
(Lab)
↓
Database
(Records)

So what does Program.cs do? Remember these lines?
app.UseHttpsRedirection();
Tomorrow we'll have lines like:
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
These lines build the HTTP pipeline. Think of them as the receptionist deciding what happens before the request reaches the controller.

How does ASP.NET know which Controller?
Now the browser sends: GET /api/documents
ASP.NET matches:
URL
↓
Route
↓
DocumentsController
↓
Get()

That matching process is called Routing.

The Complete Flow
When you type this URL in the browser: GET https://localhost:7145/api/documents
This is exactly what happens:
1. Browser sends request
↓
2. Kestrel receives it
↓
3. Middleware executes
   - HTTPS
   - Logging
   - Authentication
   - Authorization
   - etc.
↓
4. Routing checks the URL
↓
5. Finds DocumentsController
↓
6. Executes Get()
↓
7. Get() calls Application
↓
8. Application calls Infrastructure
↓
9. Infrastructure queries PostgreSQL
↓
10. Response travels back

11. Session 16 - Understanding Controllers
Before writing a single line of code, let's answer one question.
What is a Controller?Think of a controller as a receptionist.
Imagine someone enters a bank. Customer says: "I want to open an account."
The receptionist doesn't open the account. The receptionist simply sends the request to the correct department. A Controller does exactly that.
HTTP Request
↓
Controller
↓
Application Layer
↓
Business Logic
↓
Database
↓
Response

Notice... The controller should not contain business logic.
Its job is to:
Receive the request
Validate basic HTTP input
Call the Application layer
Return the response

Step 1 - Create a Controllers Folder
Inside: CogniVault.Api
Create a folder: Controllers
We keep all controllers here.
Step 2 - Create DocumentsController.cs
Create:
Controllers
    └── DocumentsController.cs

Don't worry about the code yet. We'll understand every line. The Class
Start with this:
using Microsoft.AspNetCore.Mvc;

namespace CogniVault.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{

}

Now let's understand every part.
1. Namespace
namespace CogniVault.Api.Controllers; Nothing new. It simply organizes the class.

2. [ApiController]
This is called an Attribute. Attributes are metadata. Think of them as sticky notes attached to a class.
Example:
📄 DocumentsController
Sticky Note:
"I'm an API Controller."

When ASP.NET starts, it scans the assembly. It sees: [ApiController] and says: "This class handles HTTP requests."
Without this attribute, ASP.NET won't treat it as a proper API controller.
What benefits does [ApiController] give us?
Several automatic features:
Automatic model validation
Better error responses
Parameter binding
API-specific behavior
We'll see these benefits later. For now remember: It tells ASP.NET: "This class is an API Controller."

3. [Route("api/[controller]")]
This tells ASP.NET: "Which URL belongs to this controller?" Let's break it apart.
api/ is literal text.
Then: [controller] is a placeholder. ASP.NET replaces it with the class name.
Our class is: DocumentsController
Remove: Controller
Remaining: Documents
So the final route becomes: api/documents
You didn't type: documents - ASP.NET figured it out automatically.
Why use [controller]? Suppose tomorrow you rename:
DocumentsController to FilesController
The route automatically becomes: api/files
You don't have to update the attribute. Very convenient.

4. ControllerBase
public class DocumentsController : ControllerBase
Notice the colon (:). Earlier we used it for inheritance too.
Example: Dog : Animal
Here:
DocumentsController
↓
inherits
↓
ControllerBase

Question: Why?
Because ControllerBase already contains many useful methods.
Example: Instead of writing:
return new OkObjectResult(user);
We simply write:
return Ok(user);
Where does Ok() come from? ControllerBase.
Similarly:
return BadRequest();
return NotFound();
return Created();
All of these are inherited from ControllerBase. So instead of reinventing the wheel, we inherit the functionality Microsoft already provides.

Why not inherit from Controller?
Good question. There are two base classes:
Controller and ControllerBase
Controller is used when you're returning Views (MVC applications).
Example: HTML Pages
CogniVault is an API.
It returns: JSON not HTML.
So we inherit from: ControllerBase
which is lighter and intended for APIs.
What does the class do right now? Nothing.
public class DocumentsController : ControllerBase
{

}
This is perfectly valid. It simply says: "I'm a controller." But there are no endpoints yet.

Our Next Step The next thing we'll add is our first endpoint:
[HttpGet]
public IActionResult Get()
{
    return Ok("CogniVault API is running!");
}
This may look tiny, but it introduces several important concepts:
What [HttpGet] means
What IActionResult is
What Ok() returns
How ASP.NET maps a request to this method

🎯 Your Task
Create:

Controllers/
    DocumentsController.cs

with exactly this code:

using Microsoft.AspNetCore.Mvc;

namespace CogniVault.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{

}

Don't add any methods yet.
We'll write the first endpoint together in the next step, because understanding [HttpGet], IActionResult, and Ok() deserves its own discussion.

builder.Services.AddControllers(); → Register the controller services.
app.MapControllers() → Tell ASP.NET to route incoming HTTP requests to controller actions.

Session 17 - Our First Endpoint
Step 1 - Add Our First Action
Inside the controller, add:
using Microsoft.AspNetCore.Mvc;

namespace CogniVault.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("CogniVault API is running!");
    }
}
Now let's understand every new line.
What is [HttpGet]? This is another attribute.
Earlier we learned: [ApiController]
Now: [HttpGet]
It tells ASP.NET: "This method should execute when someone sends an HTTP GET request."
Example:
GET /api/documents
↓
Calls Get()
What if we had: [HttpPost]
Then the request would be: POST /api/documents
Later we'll use: [HttpPost] [HttpPut] [HttpDelete] [HttpPatch]
Each maps to a different HTTP verb.
Why is the method named Get()? Actually... It doesn't have to be.
This is valid:
[HttpGet]
public IActionResult Hello()
{
    return Ok();
}
or
[HttpGet]
public IActionResult GetDocuments()
{
    return Ok();
}
ASP.NET doesn't care about the method name. It cares about the attribute. The attribute determines how the method is reached.
What is IActionResult? This is one of the most important return types in ASP.NET Core.
Think of it like this: A controller must always send an HTTP response.
That response could be: 200 OK
404 Not Found
400 Bad Request
401 Unauthorized
500 Internal Server Error
All of those are different kinds of Action Results.
So instead of returning only one type, we return:
IActionResult
which means: "This method will return some HTTP response."
What does Ok() do?
return Ok("CogniVault API is running!");
This returns:
Status Code: 200 OK
with the body: "CogniVault API is running!"
Internally, Ok() is just a helper method from ControllerBase.
Instead of writing: return new OkObjectResult("CogniVault API is running!");
we write: return Ok("CogniVault API is running!");
Much cleaner.
The Request Flow
Now the flow finally becomes:
Browser
↓
GET /api/documents
↓
Kestrel
↓
Middleware
↓
Routing
↓
DocumentsController
↓
Get()
↓
return Ok(...)
↓
200 OK
↓
Browser

For the first time, we have a real endpoint. How can we test it?
There are three ways.
Option 1 (Recommended)
Use the .http file.
GET {{CogniVault.Api_HostAddress}}/api/documents
Accept: application/json
Click Send Request.
Option 2
Open your browser:
https://localhost:7145/api/documents
Option 3
Use Swagger/OpenAPI (once we expose the UI in a later step).
What response should you get?
Status: 200 OK
Body:
"CogniVault API is running!"
A Small Improvement
Although "CogniVault API is running!" is fine for learning, APIs usually return JSON objects rather than plain strings.
For example:
return Ok(new
{
    Message = "CogniVault API is running!"
});
The response becomes:
{
  "message": "CogniVault API is running!"
}
This is closer to what production APIs return, and later we'll define proper response DTOs instead of anonymous objects.
Why are we doing this?
You might wonder: "Why not jump straight to Upload Document?"
Because I want you to understand the basic request lifecycle first.
Once you're comfortable with:
Controller
Route
HTTP verb
Action method
IActionResult
Ok()
then adding business logic is just the next step.

🎯 Your Task
Add the Get() method.
Run the API.
Call:
GET /api/documents
Confirm you receive:
{
  "message": "CogniVault API is running!"
}

(or the plain string if you keep the simpler version).

Register the controller services.
builder.Services.AddControllers();

Tell ASP.NET to route incoming HTTP requests to controller actions.
app.MapControllers();

For testing the Api we use .http file to make http calls from the file itself its a lightweight way to do testing.
Inorder to do that we need to add this in .http file
@CogniVault.Api_HostAddress = http://localhost:5231 #This is setting the local host

### CogniVault API Requests
GET {{CogniVault.Api_HostAddress}}/api/documents
Accept: application/json

###

Differences on these
OpenAPI → Describes your API. OpenAPI is not a testing tool. It is simply a document that describes your API.
It describes:
URLs
HTTP Methods
Parameters
Request Body
Response Body
Status Codes
Think of it as a blueprint.

.http file → Sends requests to your API. The .http file is simply a client.
It literally sends an HTTP request. VS Code (or Visual Studio) sends this request directly to your API. It doesn't need OpenAPI.
You could even test an API that has no OpenAPI at all.

Swagger UI → Uses the OpenAPI description to let you test your API.
Swagger reads the OpenAPI document and automatically creates a nice web page. When you click Execute, Swagger sends exactly the same HTTP request that your .http file sends.

OpenAPI
↓
describes the API
-------------------------
.http
↓
calls the API
-------------------------
Swagger
↓
uses OpenAPI to call the API

Middleware Pipeline:
The sequence of these methods is collectively called the middleware pipeline (or the HTTP request pipeline). The steps are that like app.UseHttpsRedirection(); app.MapControllers(); or Logging or Auth are called as Middleware.
when a request is made, requests do not execute the code inside Program.cs from top to bottom every time. Instead, Program.cs runs exactly once when your application boots up to build a "pipeline" in memory. Every subsequent HTTP request travels through that pre-built pipeline. Which is middleware pipeline. Those are the steps that are above.
What about services that register on top or program file. Services handle business logic and data (the "tools" your app needs to do its job).

Incoming Request for middleware
       ⬇️
┌───────────────────────┐
│ UseHttpsRedirection() │ ──❌ Is it HTTP? Stop here & redirect!
└───────────────────────┘
       ⬇️ (Yes, it's HTTPS)
┌───────────────────────┐
│   UseAuthorization()  │ ──❌ Not logged in? Stop here & return 401 Unauthorized!
└───────────────────────┘
       ⬇️ (Yes, authorized)
┌───────────────────────┐
│   MapControllers()    │ ──🏁 Final destination: Runs your C# Controller code
└───────────────────────┘

Perfect. This session is one of the most important in Clean Architecture.
Almost every enterprise API you'll work on will use DTOs, and interviewers often ask why they're needed. Let's understand them from first principles.

Session 19 - DTO (Data Transfer Object)
Before I explain what a DTO is, let me ask you a question.
Suppose our database has this table.
Documents
------------------------------------------------------------
Id
FileName
BlobPath
StorageProvider
EmbeddingStatus
CreatedBy
CreatedAt
UpdatedAt
IsDeleted
InternalNotes

Now Angular calls: GET /api/documents
Should we send all this information to the UI? Probably not.
The UI may only need:
File Name
Uploaded Date
Embedding Status

Why send everything?
Without DTO
Imagine our Domain Entity looks like this:
public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string BlobPath { get; set; }
    public string StorageProvider { get; set; }
    public bool IsDeleted { get; set; }
    public string InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}
If we directly return it: return Ok(document);
Angular receives:
{
  "id": "...",
  "fileName": "Policy.pdf",
  "blobPath": "...",
  "storageProvider": "Azure",
  "isDeleted": false,
  "internalNotes": "...",
  "createdAt": "..."
}
The frontend now knows about:
Internal storage
Internal flags
Internal implementation
That is not good design.
With DTO
Instead, we create another class.
public class DocumentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; }
}
Now we return:
return Ok(documentDto);
Angular receives:
{
  "id": "...",
  "fileName": "Policy.pdf",
  "uploadedAt": "...",
  "status": "Completed"
}
Much cleaner. What does DTO mean?
DTO stands for: Data Transfer Object
Notice the word: Transfer
Its only purpose is to transfer data between two systems. It contains data only. No business logic. Think of it Like Amazon
Amazon has huge internal information about a product.
Warehouse Rack
Supplier Cost
Internal SKU
Profit Margin
Tax Rules
Inventory Rules
Do you see all of that on Amazon's website? No.
You only see: Product Name
Price
Rating
Images
The page you see is like a DTO.
The internal warehouse model is like the Domain Entity.
So what's the difference?
Domain Entity Represents the business. Real business object.
Example: Document
It contains everything needed by the business.
DTO Represents what another application needs.
Usually:
Angular
Mobile App
Another API
Where will DTOs live?
Since we're following Clean Architecture:
API
↓
Application
↓
Domain
↓
Infrastructure
Where should they go? Not in API. Not in Domain. We'll keep them in:
Application
↓
DTOs
Why? Because the Application layer defines the contract between the outside world and the business logic.
The API simply receives a request and returns a response. The Application layer decides what that request and response look like.
Request DTO vs Response DTO We'll actually have two kinds.
Request DTO Data coming into our API.
Example:
{
    "fileName": "Policy.pdf"
}
Response DTO
Data going out of our API.
Example:
{
    "id": "...",
    "fileName": "Policy.pdf",
    "status": "Completed"
}
So there are always two directions.
Angular
↓
Request DTO
↓
Application
↓
Response DTO
↓
Angular
Why not use the Domain Entity everywhere? This is a very common beginner question.
Imagine next year we add a new property to the Document entity.
public string EncryptionKey { get; set; }
If you're returning the entity directly...
Suddenly every API starts exposing:
"encryptionKey": "..."
Even though the frontend should never see it. With DTOs... Nothing changes until you decide to include that field.
That's one of the biggest benefits: you control exactly what crosses the API boundary.
How will CogniVault use them? Later we'll have something like:
Application
│
├── Documents
│
│   ├── Commands
│   │      UploadDocumentCommand
│   │
│   ├── Queries
│   │      GetDocumentsQuery
│   │
│   ├── DTOs
│   │      DocumentDto
│   │      UploadDocumentRequest
│   │      UploadDocumentResponse

Notice something. The DTOs belong with the feature. That's a big part of Vertical Slice Architecture.
Everything related to "Documents" stays together. One thing I'd like to adjust
Earlier, we talked about creating layers like API, Application, Domain, and Infrastructure. Now we're starting to think by feature inside those layers.
Instead of having one giant DTOs folder for the entire application, we'll organize them by feature:
Application
└── Documents
    ├── Commands
    ├── Queries
    ├── DTOs
    └── ...
As CogniVault grows to include Chat, Authentication, Search, and Administration, this organization keeps each feature self-contained and much easier to navigate.

Session 20 - Designing the First Real Feature
Before writing code, every senior developer asks one question: "What problem are we solving?"
We don't create classes first. We design the feature first.
Our Product Let's remember what CogniVault does.
User
↓
Uploads PDF
↓
Store File
↓
Save Metadata
↓
Generate Chunks
↓
Generate Embeddings
↓
Store Vectors
↓
User asks question
↓
AI answers with citations
The first feature is obviously: Upload Document
What Happens When the User Uploads a Document? Imagine you're using the application.
You click: Upload Document
You select: Employee_Handbook.pdf
Then click Upload.
What should happen? Let's think about the backend.
Angular
↓
POST /api/documents
↓
API receives request
↓
Validate request
↓
Store PDF
↓
Save document information
↓
Return success
Notice something... At this stage we're not generating embeddings.
Why? Because uploading and AI processing are two different responsibilities.
Uploading should be fast. AI processing may take several seconds or even minutes. We'll handle AI processing later using background jobs.
Step 1 - Design the API
Instead of coding immediately, let's design it.
Endpoint
POST /api/documents
Request
Initially, the user uploads a file. In ASP.NET Core, uploaded files are represented by:
IFormFile
So conceptually the request is: File
Later we might also include:
Category
Tags
Description
But for version 1:
PDF File is enough.
Step 2 - What Should We Return?
Should we return: true
❌ Not useful.
Should we return: Uploaded Successfully
Better, but still limited.
A better response is:
{
    "id": "a1b2c3...",
    "fileName": "Employee_Handbook.pdf",
    "status": "Uploaded"
}
Why? Because Angular now knows:
Which document was created what its ID is What its current status is
That ID will be useful later for viewing details, deleting the document, or checking processing progress.
Step 3 - What Layers Are Involved? Let's map the flow.
Angular
↓
DocumentsController
↓
Application
↓
Infrastructure
↓
Database + Blob Storage
Notice something important. The controller doesn't know anything about databases or Azure Blob Storage.
It simply passes the request to the Application layer.
Step 4 - What Classes Will We Eventually Need?
Don't create these yet—just understand the roadmap.
Application
└── Documents
    ├── Commands
    │      UploadDocumentCommand.cs
    │      UploadDocumentCommandHandler.cs
    │
    ├── DTOs
    │      UploadDocumentRequest.cs
    │      UploadDocumentResponse.cs

Each class has a single responsibility:
UploadDocumentRequest → Data coming into the API.
UploadDocumentResponse → Data going back to the client.
UploadDocumentCommand → Represents the action "Upload a document."
UploadDocumentCommandHandler → Contains the business logic to perform that action.
Why Are There So Many Classes?
This is another question many developers ask. Wouldn't this work?
public IActionResult Upload(IFormFile file)
{
    // 200 lines of code...
}
Yes, it would. But imagine adding:
Validation
Virus scanning
Blob Storage upload
Database save
Duplicate file detection
Logging
AI processing
Suddenly your controller becomes 500 lines long. Instead, we separate responsibilities.
Controllers stay small. Business logic lives elsewhere. Our Development Strategy
We're not going to build everything at once.
We'll follow this sequence:
1. Design the feature ✅
2. Create Request DTO
3. Create Response DTO
4. Create Command
5. Create Command Handler
6. Controller calls Command
7. Stub implementation (no database yet)
8. Add persistence later
Every step leaves the application in a working state.
One More Important Concept Notice that we're designing the feature before writing code.
This is exactly how experienced teams work. They don't ask: "Which class should I create?"
They ask: "What should happen when the user clicks Upload?" Once the workflow is clear, the classes almost design themselves.

First, what is a Command?
imagine you're the manager of a company. An employee comes and says: "I want to apply for leave."
Is he asking for information? No.
He wants the system to do something. That's a command. A command means: "Please perform an action."
Now compare that with another request. Employee asks: "How many leave days do I have?" Nothing changes. He just wants information.
That's called a Query.

This idea comes from CQRS (Command Query Responsibility Segregation). 
There are only two kinds of operations:
READ
↓
Query
WRITE
↓
Command

In CogniVault Upload Document -> Command Delete Document -> Command Get Documents -> Query Search Documents -> Query
so application naturally divides in to
Application
├── Commands
│      Upload
│      Delete
│      Rename
│
└── Queries
       GetAll
       GetById
       Search
So why create two classes? Because each has one job.
Class	Responsibility
UploadDocumentCommand	        Carry the request data
UploadDocumentCommandHandler	Execute the business logic

One last interesting fact Later, when we introduce MediatR (or build something similar ourselves first), you'll see code like this: await _mediator.Send(command);
Look carefully. We're sending the command, not calling the handler directly.
The mediator receives the command and automatically finds the correct handler.
That's why the naming is so important. The framework can automatically match:
UploadDocumentCommand
UploadDocumentCommandHandler
We're not going to introduce MediatR immediately.

Session 21 – Our First DTO
Before we write a single class, I want to explain why we're creating a Request DTO first. Imagine the user uploads a document from Angular.
The browser sends something like: POST /api/documents
Along with the file.
ASP.NET Core receives the HTTP request. But our application doesn't understand HTTP.
Our Application layer understands objects.
So something has to convert:
HTTP Request into C# Object
That object is our Request DTO.
The Request Flow
Angular
↓
HTTP Request
↓
ASP.NET Core
↓
UploadDocumentRequest
↓
UploadDocumentCommand
↓
UploadDocumentCommandHandler
Notice something important.
The Request DTO belongs to the API contract. The Command belongs to the business logic.
They are similar, but they serve different purposes. "Can't we use the Command directly?"
This is another question many developers ask. For a very small application, you could.
Example:
Angular
↓
UploadDocumentCommand
↓
Handler
It would work. So why are we introducing another object?
Because APIs change. Business logic changes.
Sometimes independently.
For example: Today the API sends:
{
   "file": "...pdf..."
}
Next year the UI team wants:
{
   "file": "...pdf...",
   "category": "HR",
   "tags": ["policy","employee"]
}
The API contract changed. That doesn't necessarily mean the business command should change in the same way.
Keeping them separate gives us flexibility. Where should the DTO live?
Remember our Application structure? Eventually it will look like this:
Application
│
└── Documents
    │
    ├── Commands
    │
    ├── Queries
    │
    ├── DTOs
    │
    └── Interfaces
We'll place our DTOs inside the Documents feature because they're only used by that feature.
Our First Request DTO
Let's think about what information we actually need. Today...
Only one thing. The uploaded file.
So conceptually:
public class UploadDocumentRequest
{
    public IFormFile File { get; set; }
}
But... I'm not going to ask you to create this yet. 😊
Why? Because I want to teach you one more important concept first.
Look carefully.
public IFormFile File { get; set; }
Where does IFormFile come from? Not our code.
Not the Application layer. It comes from ASP.NET Core.
That creates an architectural discussion. Here's the challenge
Remember our Application layer? It should not know anything about ASP.NET Core.
If we write:
using Microsoft.AspNetCore.Http;
inside Application... Then Application now depends on ASP.NET.
That violates one of our Clean Architecture goals:
Application
❌ Should not depend on Web Frameworks
So what should we do?
There are two approaches.
Approach 1
Put IFormFile directly in the Request DTO.
Simple. Many projects do this.
Approach 2
Keep the Application completely independent of ASP.NET Core. The API receives IFormFile.
Then maps it into an Application model. This is a cleaner architecture and is often preferred in larger systems.
Which approach are we going to use? We're going to choose Approach 2. Not because the first one is wrong.
But because CogniVault is your flagship project, and I want it to demonstrate strong architectural boundaries.
What does that mean? It means we'll actually have:
HTTP Request
↓
API Model
↓
Application Command
↓
Handler
Instead of leaking ASP.NET types into the Application layer. Why am I slowing down here?
Because this is one of those decisions that separates:
"The code works."
from
"The architecture is clean."
Many tutorials skip this discussion entirely. I don't want you to simply memorize where classes go—I want you to understand why they belong there.
Today's Goal We won't create any new classes just yet.
Instead, in our next step we'll answer one architectural question:
How do we receive an uploaded file in the API without making the Application layer depend on ASP.NET Core?
Once you understand that boundary, creating the DTOs, Commands, and Handlers will feel very natural, and you'll see exactly why each class exists.

Perfect. This session is where you'll see why ASP.NET Core and Clean Architecture work so well together.

Session 22 - Model Binding

Today we're going to learn one of ASP.NET Core's most powerful features:

Model Binding

You use it every day in ASP.NET Core, even if you don't realize it.

Let's start with a simple example.

Suppose Angular sends:

POST /api/users
Content-Type: application/json

{
    "name": "Rajesh",
    "age": 27
}

Question:

How does this JSON become a C# object?

You never wrote code like:

var request = new CreateUserRequest();

request.Name = json["name"];
request.Age = json["age"];

Yet somehow your controller receives:

public IActionResult Create(CreateUserRequest request)
{
}

How?

ASP.NET Core did it automatically.

That automatic conversion is called Model Binding.

Think of it like a Translator

Imagine two people.

Angular speaks JSON

↓

Translator

↓

C# speaks Objects

The translator is Model Binding.

Example

Suppose we have:

public class CreateUserRequest
{
    public string Name { get; set; }

    public int Age { get; set; }
}

And the controller:

[HttpPost]
public IActionResult Create(CreateUserRequest request)
{
    return Ok();
}

Angular sends:

{
   "name":"Rajesh",
   "age":27
}

ASP.NET automatically creates:

CreateUserRequest request = new CreateUserRequest
{
    Name = "Rajesh",
    Age = 27
};

You never write this code.

ASP.NET does it.

Another Example

Suppose the URL is:

GET /api/documents/15

Controller:

[HttpGet("{id}")]
public IActionResult GetById(int id)
{
}

Again...

You never wrote:

id = 15;

ASP.NET did.

It looked at:

/api/documents/15

and automatically assigned:

id = 15

Again...

Model Binding.

Another Example

Query String.

URL:

GET /api/documents/search?keyword=policy

Controller:

public IActionResult Search(string keyword)
{
}

ASP.NET automatically does:

keyword = "policy";

Again...

Model Binding.

So what exactly does Model Binding do?

It converts HTTP data into C# objects.

HTTP

↓

Route Values

↓

Query Strings

↓

Headers

↓

Body

↓

Forms

↓

Files

↓

Model Binding

↓

C# Objects

This is one of the reasons ASP.NET Core feels so productive.

Now let's come back to CogniVault.

The browser uploads:

EmployeeHandbook.pdf

The request is:

POST /api/documents

Content Type:

multipart/form-data

because we're sending a file.

Question:

Can ASP.NET automatically convert the uploaded file?

Yes.

It converts it into:

IFormFile

Again...

That's Model Binding.

So where does IFormFile belong?

Now we're back to the architectural discussion.

Suppose we write:

public class UploadDocumentRequest
{
    public IFormFile File { get; set; }
}

This class now needs:

using Microsoft.AspNetCore.Http;

Question:

Can we put this class inside the Application project?

No.

Because now Application depends on ASP.NET Core.

That breaks one of our Clean Architecture principles.

So what's the solution?

We'll split the responsibility.

Instead of one object, we'll use two.

API Model
public class UploadDocumentRequest
{
    public IFormFile File { get; set; }
}

Lives in:

CogniVault.Api

because it depends on ASP.NET.

Application Command
public class UploadDocumentCommand
{
    public Stream FileStream { get; set; }

    public string FileName { get; set; }
}

Lives in:

CogniVault.Application

Notice something.

No ASP.NET types.

Only .NET types.

Much cleaner.

The Flow
Browser

↓

multipart/form-data

↓

ASP.NET Model Binding

↓

UploadDocumentRequest (API)

↓

Controller

↓

UploadDocumentCommand (Application)

↓

Handler

Notice...

The Controller becomes the bridge between the Web world and the Business world.

Why is this considered good architecture?

Imagine five years from now.

Today you have:

Angular

Tomorrow your company adds:

Mobile App
Desktop App
Another API
Background Service

None of those know about IFormFile.

They can all create the same UploadDocumentCommand.

Your business logic remains unchanged.

That's the power of keeping your Application layer independent.

Now let's answer a question you might have

"Doesn't this mean we have to copy data from one object to another?"

Yes.

Example:

var command = new UploadDocumentCommand
{
    FileName = request.File.FileName,
    FileStream = request.File.OpenReadStream()
};

At first it feels like extra work.

But in return you get:

A clean architecture
Better testability
Independence from ASP.NET Core
Easier future changes

Most enterprise systems consider that a worthwhile trade-off.

Our Next Coding Step

Now we're finally ready to create our first real models.

We'll create:

CogniVault.Api
└── Models
    └── UploadDocumentRequest.cs

and later:

CogniVault.Application
└── Documents
    └── Commands
        └── UploadDocumentCommand.cs

For the first time, you'll see the controller transform one model into another and pass it into the Application layer.

That will be the first complete example of Clean Architecture in action inside CogniVault.

Before we write any code...

I want to point out something you've probably noticed.

Earlier in the project, we created projects (API, Application, Domain, Infrastructure).

Now we're creating boundaries between them.

That's the real goal of Clean Architecture—not just having multiple projects, but ensuring each project has a clear responsibility and doesn't accidentally depend on things it shouldn't.

Once you see the controller map an API model into an Application command, the entire architecture will start feeling much more natural.

Excellent. You're asking the right questions, and I can see you're not just trying to finish the project—you want to understand why every decision is made. That's exactly how senior engineers learn.

We're now going to write our first feature in the enterprise way.

Session 23 - Creating the API Request Model

Today we're finally creating our first model.

But before we type a single line of code, let's answer one question.

Why are we creating it in the API project?

Remember our discussion:

Browser
    │
    ▼
HTTP Request
    │
    ▼
ASP.NET Core
    │
    ▼
UploadDocumentRequest   ← API Project
    │
    ▼
Controller
    │
    ▼
UploadDocumentCommand   ← Application Project

The browser communicates using HTTP.

The Application layer communicates using C# objects.

The API project's job is to translate between those two worlds.

That's why this model belongs in the API project.

Folder Structure

Inside CogniVault.Api, let's create a new folder.

CogniVault.Api
│
├── Controllers
├── Models
│     └── Documents
└── Program.cs
Why Models/Documents?

You might ask:

"Why not just put everything inside one Models folder?"

Because six months from now we'll have:

Models
│
├── Documents
├── Authentication
├── Chat
├── Users
├── Search
├── Admin

Feature-based organization scales much better than having dozens of unrelated model classes in one folder.

Create the Request Model

Create the file:

UploadDocumentRequest.cs

Inside:

using Microsoft.AspNetCore.Http;

namespace CogniVault.Api.Models.Documents;

public class UploadDocumentRequest
{
    public IFormFile File { get; set; } = default!;
}
Let's Understand Every Line
Line 1
using Microsoft.AspNetCore.Http;

Why?

Because IFormFile belongs to ASP.NET Core.

It is not part of standard C#.

Without this namespace:

IFormFile

won't be recognized.

Namespace
namespace CogniVault.Api.Models.Documents;

Notice how the namespace matches the folder structure.

CogniVault.Api
        ↓
Models
        ↓
Documents

This makes the project easy to navigate.

The Class
public class UploadDocumentRequest

Think of this class as a container.

Its only purpose is to hold data coming from the HTTP request.

No validation.

No business logic.

No database code.

Just data.

The Property
public IFormFile File { get; set; } = default!;

Let's break it down.

IFormFile

Represents the uploaded file.

When Angular sends:

EmployeeHandbook.pdf

ASP.NET converts it into an IFormFile object.

get; set;

Means the value can be:

assigned by ASP.NET Core (during model binding)
read later in the controller
= default!;

You may not have seen this before.

Why isn't it simply:

public IFormFile File { get; set; }

Because nullable reference types are enabled by default in modern .NET.

The compiler says:

"This property is non-nullable, but I don't see where it's initialized."

We know ASP.NET Core will populate it during model binding.

So we're telling the compiler:

"Trust me—this will be assigned before it's used."

That's what:

= default!;

means.

Later, we'll also learn other ways to satisfy the compiler, such as using the required keyword.

What Happens at Runtime?

Suppose Angular uploads:

EmployeeHandbook.pdf

ASP.NET Core performs model binding and effectively creates:

var request = new UploadDocumentRequest
{
    File = uploadedFile
};

You never write that code.

ASP.NET Core does it for you.

Why Doesn't This Go Into the Application Layer?

Because this class depends on:

IFormFile

which belongs to:

Microsoft.AspNetCore.Http

Our Application project should not know anything about ASP.NET Core.

That's why we separate it.

Today's Goal

Create:

CogniVault.Api
└── Models
    └── Documents
        └── UploadDocumentRequest.cs

with the class shown above.

Don't modify the controller yet.

After You've Created It

We'll move to the next step:

Creating our first UploadDocumentCommand in the Application project.

That's where you'll see the first real transition from the Web layer into the Business layer, and the Clean Architecture flow we've been discussing will start to come alive with actual code.

1. Why is IFormFile not showing an error even though I didn't add using Microsoft.AspNetCore.Http?

This is due to a feature introduced in modern C# called Implicit Usings.

Let's verify it.

Open your CogniVault.Api.csproj.

2. Is this Model different from a DTO?

This is an even better question.

The answer is:

Technically yes, but conceptually they're very similar.

Let's break it down.

What is a DTO?

DTO means:

Data Transfer Object

Its only job is to transfer data.

Example:

public class DocumentDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;
}

No logic.

Just data.

What is an API Model?

Our class is:

public class UploadDocumentRequest
{
    public IFormFile File { get; set; } = default!;
}

It also only contains data.

No logic.

No calculations.

So...

It behaves exactly like a DTO.

Perfect. Now we're going to build the bridge between the API layer and the Application layer.

This is the first time you'll actually see Clean Architecture in action.

Session 24 - Creating the First Command

Let's remember our flow.

Browser
    │
    ▼
HTTP Request
    │
    ▼
UploadDocumentRequest   (API)
    │
    ▼
DocumentsController
    │
    ▼
UploadDocumentCommand   (Application)
    │
    ▼
UploadDocumentCommandHandler

Today we're creating the highlighted class.

Step 1 - Create the Folder Structure

Inside CogniVault.Application, create:

Documents
│
└── Commands

Now inside Commands, create:

UploadDocumentCommand.cs

Your Application project will start looking like this:

CogniVault.Application
│
└── Documents
    │
    └── Commands
        │
        └── UploadDocumentCommand.cs

Notice something.

Everything is grouped by feature, not by type.

That is Vertical Slice Architecture.

Step 2 - What should this command contain?

Let's think like business developers.

The business doesn't know anything about:

IFormFile

It only needs:

The file contents
The file name

That's all.

So conceptually:

Upload Document

↓

File Name

+

File Content
Step 3 - Write the Class

Create:

namespace CogniVault.Application.Documents.Commands;

public class UploadDocumentCommand
{
    public string FileName { get; init; } = string.Empty;

    public Stream FileStream { get; init; } = Stream.Null;
}

Don't worry if Stream looks unfamiliar—we'll explain it.

Let's Understand Every Line
Namespace
namespace CogniVault.Application.Documents.Commands;

Again...

Namespace mirrors the folder structure.

Easy to navigate.

Class
public class UploadDocumentCommand

This class represents one business action.

Not a file.

Not a document.

An action.

Upload this document.

That's why it's called a Command.

Property 1
public string FileName { get; init; } = string.Empty;

Question:

Why aren't we storing:

EmployeeHandbook.pdf

inside the handler?

Because the command should carry everything the handler needs.

Why init instead of set?

You've probably seen:

public string Name { get; set; }

before.

Here we're using:

get;
init;

init means:

The value can be assigned only when the object is created.

Example:

var command = new UploadDocumentCommand
{
    FileName = "Policy.pdf"
};

This is allowed.

Later:

command.FileName = "Another.pdf";

❌ Not allowed.

The object becomes immutable after creation.

Why is this good?

Commands represent something that already happened.

The user clicked Upload.

The command should not change halfway through processing.

Immutable objects are:

Safer
Easier to debug
Easier to reason about

Modern .NET code often prefers init for request-like objects.

Property 2
public Stream FileStream { get; init; } = Stream.Null;

Now let's understand Stream.

What is a Stream?

Forget .NET for a moment.

Imagine water flowing through a pipe.

Tank

↓

Pipe

↓

Glass

Water flows continuously.

You don't grab the whole pipe.

You just read what flows through it.

A file works similarly.

PDF File

↓

Stream

↓

Your Code

A Stream is simply a way of reading data.

It doesn't matter whether the data comes from:

A PDF
A ZIP
A network
Memory
Azure Blob Storage

Everything can be represented as a stream.

Think of it like this.

Instead of saying:

"Here's a PDF."

We're saying:

"Here's a flow of bytes."

The business layer doesn't care where those bytes came from.

That makes it much more flexible.

Why not keep IFormFile?

Because IFormFile belongs to ASP.NET Core.

Stream belongs to .NET itself.

That's a huge difference.

API Layer

↓

IFormFile

↓

Controller

↓

Stream

↓

Application Layer

The Application layer is now completely independent of ASP.NET Core.

What is Stream.Null?

Just like we used:

string.Empty

instead of:

null

we use:

Stream.Null

instead of:

null

It's simply an empty stream.

It satisfies the compiler and avoids null reference issues.

Why are we not adding DocumentId?

Because this is an Upload command.

The document doesn't exist yet.

The handler will generate the ID later.

Visual Comparison
API Model
UploadDocumentRequest

↓

IFormFile

Represents HTTP.

Application Command
UploadDocumentCommand

↓

FileName

↓

Stream

Represents Business.

Notice how we removed every ASP.NET dependency.

That is exactly what Clean Architecture wants.

One More Modern C# Concept

You noticed we're using:

init

instead of set.

Throughout this project, I'd like to use modern C# features where they genuinely improve the code. Since you're using .NET 10 and Visual Studio 2026, we'll gradually learn features like:

init
required
File-scoped namespaces
Primary constructors (where appropriate)
Collection expressions
Pattern matching improvements

I'll always explain why we're using a feature rather than adopting it just because it's new.

🎯 Your Task

Create:

CogniVault.Application
└── Documents
    └── Commands
        └── UploadDocumentCommand.cs

with the class above.

Don't create the handler yet.

Session 25 - Connecting the API to the Application

Before we create the Handler, we're going to make the Controller create a Command.

This may seem like a small step, but it teaches one of the most important responsibilities of a Controller.

Question

What should a Controller do?

Many beginners think:

"A controller contains all the business logic."

❌ No.

A Controller should be very thin.

Its responsibilities are only:

Receive the HTTP request.
Validate basic input (later).
Convert the request into an Application object.
Call the Application layer.
Return the HTTP response.

That's it.

Think of the controller as a receptionist.

Customer

↓

Receptionist

↓

Department

The receptionist doesn't solve the customer's problem.

They simply send them to the correct department.

Exactly the same here.

Step 1 - Update the Controller

First, we'll add a new endpoint.

Instead of:

[HttpGet]
public IActionResult Get()

we'll keep that (it's useful as a health check for now) and add a POST endpoint.

Your controller will eventually have two endpoints:

GET  /api/documents

POST /api/documents
Step 2 - Add the Required Namespaces

At the top of DocumentsController.cs, add:

using CogniVault.Api.Models.Documents;
using CogniVault.Application.Documents.Commands;

Now the controller knows about:

API Models
Application Commands

Notice...

It still doesn't know anything about:

Database
Blob Storage
Repository

That's intentional.

Step 3 - Create the POST Action

Add this method inside your controller:

[HttpPost]
public IActionResult Upload(UploadDocumentRequest request)
{
    var command = new UploadDocumentCommand
    {
        FileName = request.File.FileName,
        FileStream = request.File.OpenReadStream()
    };

    return Ok(new
    {
        Message = "Command created successfully.",
        FileName = command.FileName
    });
}

Don't worry—we'll examine every line.

Understanding the Method
[HttpPost]

This tells ASP.NET Core:

"When a POST request comes to /api/documents, execute this method."

Remember:

GET  → Read

POST → Create

PUT  → Update

DELETE → Delete
Parameter
UploadDocumentRequest request

Question:

Who creates this object?

Not us.

ASP.NET Core Model Binding.

When the client uploads a file, ASP.NET automatically creates the UploadDocumentRequest object and fills its File property.

That's the model binding we discussed in the previous session.

Creating the Command
var command = new UploadDocumentCommand
{
    FileName = request.File.FileName,
    FileStream = request.File.OpenReadStream()
};

This is the first time we're crossing the architectural boundary.

Look carefully.

API Model

↓

Application Command

The Controller is performing a mapping.

It's taking a Web-specific object (IFormFile) and converting it into a Business-specific object (Stream + FileName).

This is exactly why we separated those two models.

OpenReadStream()

You asked about Stream earlier.

This line:

request.File.OpenReadStream()

means:

"Give me a stream that allows me to read the contents of the uploaded file."

Imagine uploading:

EmployeeHandbook.pdf

OpenReadStream() doesn't load the whole file into a string.

It gives us a pipe through which we can read the file's bytes.

That's much more efficient, especially for large files.

Temporary Response

Right now we're returning:

return Ok(new
{
    Message = "Command created successfully.",
    FileName = command.FileName
});

We're not uploading anything yet.

We're simply proving that:

The request was received.
Model Binding worked.
The command was created successfully.

One step at a time.

How Should We Test This?

This is an important point.

Our previous .http request was:

GET /api/documents

That won't test file uploads.

Uploading a file uses:

multipart/form-data

A plain .http request becomes awkward for that.

For now, I recommend using Swagger UI (once we enable it) or Postman/Bruno because they provide a file picker.

Later, I'll also show you how to write multipart requests in a .http file so you know both approaches.

What Have We Achieved?

For the first time, your application will perform this flow:

Browser

↓

HTTP POST

↓

ASP.NET Model Binding

↓

UploadDocumentRequest (API)

↓

DocumentsController

↓

UploadDocumentCommand (Application)

↓

Return Success

Even though no business logic exists yet, you've already implemented the most important architectural boundary in the application.

🎯 Your Task
Add the POST action to DocumentsController.
Keep the existing GET endpoint.
Build the project and ensure there are no errors.

Don't worry if we can't fully test the upload yet—that's expected.

Perfect! Now we're going to create what I call the heart of the business logic.

Up until now we've created:

API Model → Represents the HTTP request.
Command → Represents the business action.

Now we need something that can execute that action.

Session 26 - UploadDocumentCommandHandler

Let's remind ourselves of the flow.

Browser
    │
    ▼
UploadDocumentRequest (API)
    │
    ▼
DocumentsController
    │
    ▼
UploadDocumentCommand
    │
    ▼
❓ Who executes this?

The answer is:

UploadDocumentCommandHandler
What is a Handler?

A Command says:

"Please upload this document."

A CommandHandler says:

"I'll do it."

Think of it like a restaurant.

Customer

↓

Order Slip (Command)

↓

Chef (CommandHandler)

↓

Food

The order slip doesn't cook the food.

The chef does.

What will our Handler do eventually?

Not today, but eventually it will:

Receive Command

↓

Validate File

↓

Check File Type

↓

Upload to Blob Storage

↓

Create Document Entity

↓

Save to Database

↓

Publish Event (Later)

↓

Return Response

Notice how all the business logic lives here.

Not in the Controller.

Why don't we put this in the Controller?

Imagine this code:

[HttpPost]
public IActionResult Upload(...)
{
    // Validate PDF

    // Virus Scan

    // Upload Azure Blob

    // Save Database

    // Create Embeddings

    // Log Audit

    // Return Response
}

After a few months, this controller would be 500–1000 lines long.

Instead:

Controller

↓

Handler

↓

Business Logic

The controller stays small forever.

Step 1 - Create the Handler

Inside:

CogniVault.Application
└── Documents
    └── Commands

Create:

UploadDocumentCommandHandler.cs
Step 2 - Initial Handler

Let's keep it very simple.

namespace CogniVault.Application.Documents.Commands;

public class UploadDocumentCommandHandler
{
    public void Handle(UploadDocumentCommand command)
    {
        // Business logic will come here later.
    }
}
Let's Understand Every Line
Namespace
namespace CogniVault.Application.Documents.Commands;

Same as the Command.

Why?

Because both belong to the same feature.

Class
public class UploadDocumentCommandHandler

This class has one responsibility:

Execute an upload command.

Nothing else.

Handle()
public void Handle(...)

Question:

Why isn't the method called:

Upload()

or

Execute()

Because every handler has the same responsibility.

Handle the command.

Later you'll see:

DeleteDocumentCommandHandler

↓

Handle()

--------------------------

RenameDocumentCommandHandler

↓

Handle()

--------------------------

CreateUserCommandHandler

↓

Handle()

The method name stays consistent.

Parameter
UploadDocumentCommand command

The handler receives everything it needs inside the command.

It doesn't ask the controller for more information.

It doesn't know about HTTP.

It simply receives a business request.

Why void?

You may have noticed:

public void Handle(...)

Eventually, this won't stay void.

We'll return something like:

Task<UploadDocumentResponse>

because:

Uploading is asynchronous.
Blob Storage is asynchronous.
Database operations are asynchronous.

For now, we're keeping it simple so you understand the structure before introducing async and await.

The New Architecture

We've added another piece to our puzzle.

Browser
    │
    ▼
UploadDocumentRequest
    │
    ▼
DocumentsController
    │
    ▼
UploadDocumentCommand
    │
    ▼
UploadDocumentCommandHandler

Notice something.

The Controller still doesn't know how uploading works.

It simply creates a command.

The Handler is responsible for executing it.

This separation is one of the biggest strengths of Clean Architecture.

One Important Question

You might already be thinking:

"If the controller creates the command, who creates the handler?"

Excellent question.

Right now, we could manually write:

var handler = new UploadDocumentCommandHandler();
handler.Handle(command);

But that introduces another important concept:

Dependency Injection (DI).

Rather than creating the handler ourselves, we'll let ASP.NET Core create it and inject it into the controller.

That's exactly why we spent time earlier understanding builder.Services in Program.cs.

🎯 Your Task

Create:

CogniVault.Application
└── Documents
    └── Commands
        ├── UploadDocumentCommand.cs
        └── UploadDocumentCommandHandler.cs

with the simple Handle() method shown above.

Don't call it from the controller yet.

What's Next?

Now we're at a perfect point to introduce Dependency Injection properly.
Until now, DI has been mostly theoretical.
In the next session, you'll finally answer a question every .NET developer asks at some point:
"How does ASP.NET Core create my classes without me using new?"
We'll wire up UploadDocumentCommandHandler through Dependency Injection so that your controller receives it automatically.
Once you understand that, you'll understand one of the core mechanisms that powers ASP.NET Core applications.
