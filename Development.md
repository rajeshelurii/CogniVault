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


