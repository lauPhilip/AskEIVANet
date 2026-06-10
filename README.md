# 🎓 AskEIVA: Intelligent Knowledge Orchestration Platform

[![Funded by: EIVA](https://img.shields.io/badge/Funded%20by-EIVA-0A2540?style=flat-square)](https://www.eiva.com)
[![Status: Production Release](https://img.shields.io/badge/Status-Pre-%20Release-brightgreen?style=flat-square)](https://github.com/lauPhilip/AskEivaNet)
[![Platform: .NET 10](https://img.shields.io/badge/Platform-.NET%2010-blueviolet?style=flat-square)](https://dotnet.microsoft.com)
[![Database: Weaviate Cloud](https://img.shields.io/badge/Database-Weaviate%20Cloud-blue?style=flat-square)](https://weaviate.io)

**AskEIVA** is an enterprise-grade, AI-native knowledge orchestration platform built using **C# / .NET 10 Clean Architecture** and **Domain-Driven Design (DDD)** principles. It is engineered to ingest, chunk, vectorize, and map dense subsea survey instrumentation documentation, software manuals (NaviPac, NaviScan, NaviEdit, NaviModel), and historical helpdesk records into a traceable, highly contextual, and cross-referenced GraphRAG intelligence ecosystem.

---

## 🏛️ Project Vision & Evolution

AskEIVA addresses the critical challenge of technical discovery within complex marine engineering domains. Moving beyond traditional isolated vector search tools, the platform focuses on building an interconnected **Topological Context Mesh** that mirrors real-world relations between specific software releases, hardware components, and operational support tickets.

### 🔄 The Leap: Python Prototype to Enterprise .NET 10
* **Prototype Stage:** Built as a Python/Streamlit script executing standalone text processing runs.
* **Production Architecture:** Completely re-engineered into a strictly decoupled C# solution. By separating core domain behavior from infrastructure boundaries, the platform provides the EIVA engineering team with an easily maintainable codebase designed for horizontal scaling, strict type safety, and real-time background execution loops.

---

## 🏗️ Clean Architecture Breakdown

The solution structural design isolates domain laws from external storage frameworks, infrastructure dependencies, and UI rendering elements, splitting the project into four distinct layers:

```text
📁 src/
├── 📁 AskEiva.Domain/         # Pure domain models (TicketNode, TextChunk), Value Objects, & Repository Contracts. Zero external dependencies.
├── 📁 AskEiva.Application/    # Use cases, CQRS Command/Query Handlers (MediatR), and orchestrations (e.g., IngestDocumentationCommand).
├── 📁 AskEiva.Infrastructure/ # Concrete adapters: Weaviate vector operations, HtmlAgilityPack public scrapers, and GraphQL engines.
└── 📁 AskEiva.WebUI/          # Blazor Interactive Server UI (MudBlazor), real-time log monitors, and network graph visualizations.
```
---

### AskEIVA.Application

```text
📁AskEiva.Application/
├── 📄 AskEiva.Application.csproj
├── 📁 Documentation/
│   └── 📁 Commands/ 📄 IngestDocumentationCommand.cs
├── 📁 Graphs/
│   ├── 📁 Commands/ 📄 BuildGlobalContextGraphCommand.cs
│   └── 📁 Queries/  📄 GetEntityGraphQuery.cs
├── 📁 Jira/
│   ├── 📁 Commands/ 📄 IngestJiraIssuesCommand.cs
│   └── 📁 Utils/    📄 AtlassianDocumentParser.cs
├── 📁 Knowledge/
│   └── 📁 Queries/  📄 SearchKnowledgeQuery.cs
├── 📁 QualityAssurance/
│   └── 📁 Commands/ 📄 ExecutePipelineEvaluationCommand.cs, 📄 SubmitSwipeFeedbackCommand.cs
├── 📁 ReleaseNotes/
│   └── 📁 Commands/ 📄 IngestReleaseNotesCommand.cs
├── 📁 Telemetry/
│   ├── 📁 Queries/  📄 GetDashboardTelemetryQuery.cs
│   └── 📄 SyncTelemetryBroker.cs
└── 📁 Tickets/
    └── 📁 Commands/ 📄 IngestTicketsCommand.cs
```
* ```IngestDocumentationCommand.cs```: Coordinates the reactive public HTML parsing pipeline, running the custom sliding-window text chunker and streaming nodes directly into Weaviate.
* ```BuildGlobalContextGraphCommand.cs```: Triggers background batching routines that extract semantic knowledge triples to construct cross-referenced edges across collections.
* ```GetEntityGraphQuery.cs```: Retrieves compiled graph layouts from the vector instance to render interactive customer service data maps.
* ```IngestJiraIssuesCommand.cs```: Manages ingestion routines for internal engineering tasks and development tracking history.
* ```AtlassianDocumentParser.cs```: Normalizes complex Atlassian Document Format (ADF) payloads into clean text strings for the chunking engine.
* ```SearchKnowledgeQuery.cs```: Executes optimized hybrid semantic-keyword searches to anchor RAG prompts and prevent LLM hallucinations.
* ```ExecutePipelineEvaluationCommand.cs```: Runs automated evaluation loops against prompt outputs to verify grounding accuracy and system quality.
* ```SubmitSwipeFeedbackCommand.cs```: Saves human-in-the-loop interaction scores (upvotes/downvotes) to optimize future search rankings.
* ```IngestReleaseNotesCommand.cs```: Slices and segments EIVA software product release notes and deployment manifests into traceable reference points.
* ```GetDashboardTelemetryQuery.cs```: Aggregates live schema counts (documents, tickets, release notes) to drive the state-aware administrative UX safeguards.
* ```SyncTelemetryBroker.cs```: Relays background processing diagnostics and scraper milestones to the Blazor console interface in real-time.
* ```IngestTicketsCommand.cs```: Processes historical, multi-turn support records into embedded data objects for helpdesk triage tracking.

---

### AskEIVA.Domain

```text
📁 AskEiva.Domain/
├── 📄 AskEiva.Domain.csproj
├── 📁 Entities/
│   ├── 📄 ApplicationUser.cs
│   ├── 📄 DocumentationNode.cs
│   ├── 📄 EvaluationFeedbackLog.cs
│   ├── 📄 GraphContextChain.cs.cs
│   ├── 📄 SoftwareReleaseNode.cs
│   └── 📄 TicketNode.cs
├── 📁 Repositories/
│   ├── 📄 IDocumentationRepository.cs
│   ├── 📄 IGraphRepository.cs
│   ├── 📄 IKnowledgeRetrievalRepository.cs
│   └── 📄 ITicketRepository.cs
├── 📁 Services/
│   ├── 📄 GraphMetricsDto.cs
│   ├── 📄 IDocumentationCrawler.cs
│   ├── 📄 IFreshdeskService.cs
│   ├── 📄 IJiraService.cs
│   ├── 📄 ILlmOrchestrationService.cs
│   ├── 📄 IMistralChatService.cs
│   ├── 📄 IMistralDistillationService.cs
│   ├── 📄 IReleaseNotesScraper.cs
│   ├── 📄 JiraConfiguration.cs
│   └── 📄 JiraModels.cs
├── 📁 Utilities/
│   └── 📄 TextSplitter.cs
└── 📁 ValueObjects/
    ├── 📄 ChatTurn.cs
    ├── 📄 RetrievalMatch.cs
    └── 📄 TextChunk.cs
```

* ```ApplicationUser.cs```: identity logic for users interacting with the system for login and sign up.
* ```EvaluationFeedbackLog.cs```: Tracks evaluation benchmarks, prompt metrics, and trace flags to detect and grade generation performance.
* ```GraphContextChain.cs.cs```: Represents compiled, highly relational connection sequences linking separate vector fields across the knowledge database.
* ```SoftwareReleaseNode.cs```: Domain representation of an individual product version artifact, patch record, or deployment manifest data asset.
* ```TicketNode.cs```: Core domain entity modeling historical customer support conversations, metadata contexts, and original issue logs.
* ```IDocumentationRepository.cs```: Data access abstraction interface managing batch storage operations for documentation chunks.
* ```IGraphRepository.cs```: Defines repository mutations required to link cross-collection indices into structured graph relationship nodes.
* ```IKnowledgeRetrievalRepository.cs```: Abstraction layer managing vector lookups, global database count aggregations, and raw logging retrieval functions.
* ```ITicketRepository.cs```: Storage boundary interface defining contract requirements for persistence of parsed helpdesk interactions.
* ```GraphMetricsDto.cs```: Data transfer object standardizing structural topology statistics across the extraction system.
* ```IDocumentationCrawler.cs```: Interface defining the unbuffered asynchronous streaming parameters used to extract data assets from public websites.
* ```IFreshdeskService.cs```: Service boundary blueprint abstracting low-level operations and configurations with the support host system.
* ```IJiraService.cs```: Standardizes access vectors for fetching external issue tickets and development backlogs from Atlassian platforms.
* ```ILlmOrchestrationService.cs```: Boundary defining how contextual data blocks are combined with core system instructions to guide model interactions.
* ```IMistralChatService.cs```: Contract for generating completions and model interaction calls against the downstream Mistral API endpoint layer.
* ```IMistralDistillationService.cs```: Core abstraction engine interface defining workflows for pulling named entity relations and knowledge triples out of text logs.
* ```IReleaseNotesScraper.cs```: Abstraction specification outlining tasks for downloading and indexing binary package release summaries.
* ```JiraConfiguration.cs```: Holds environment keys, host bindings, and connectivity properties required to communicate with external project boards.
* ```JiraModels.cs```: Maps incoming Atlassian payload profiles to strongly-typed models before passing them to the application layer.
* ```TextSplitter.cs```: Core utility provider implementing sliding-window mathematics to slice dense technical logs safely at natural word bounds.
* ```ChatTurn.cs```: Immutable value object capturing a single query-response transaction block within a user session thread.
* ```RetrievalMatch.cs```: Models a prioritized fragment returned by a vector query pass, containing relevance scores and distance indicators.
* ```TextChunk.cs```: Value object modeling an individual segmented passage of plain text along with its identifying metadata tags.

---

### AskEIVA.Infrastructure

```text
📁 AskEiva.Infrastructure/
├── 📄 AskEiva.Infrastructure.csproj
├── 📁 Repositories/
│   ├── 📄 DocumentationRepository.cs
│   ├── 📄 GraphRepository.cs
│   ├── 📄 KnowledgeRetrievalRepository.cs
│   ├── 📄 TicketRepository.cs
│   ├── 📄 UserRepository.cs
│   ├── 📄 WeaviateSchemaProvisioner.cs
│   └── 📄 WeaviateUserStore.cs
└── 📁 Services/
    ├── 📄 DocumentationRepository.cs
    ├── 📄 DocumentationCrawler.cs
    ├── 📄 FreshdeskService.cs
    ├── 📄 JiraService.cs
    ├── 📄 MistralChatService.cs
    ├── 📄 MistralDistillationService.cs
    └── 📄 ReleaseNotesScraper.cs
```

* ```DocumentationRepository.cs```: Implements data access mappings for sending split documentation vectors to Weaviate cloud collections.
* ```KnowledgeRetrievalRepository.cs```: Executes complex GraphQL aggregation queries and hybrid vector-keyword searches against Weaviate cluster indices.
* ```TicketRepository.cs```: Implements support ticket batch mutations, handling raw string sanitization and vector persistence operations.
* ```UserRepository.cs```: Connects custom administration security context records with low-level storage tables.
* ```WeaviateSchemaProvisioner.cs```: Runs automated structural health steps on startup to declare classes, configurations, and vector spacing choices if they don't exist.
* ```WeaviateUserStore.cs```: Implements ASP.NET Core Identity store abstractions, routing user authentication lookups directly into the vector database.
* ```DocumentationCrawler.cs```: Integrates the direct public-facing HTML web crawler engine, using `HtmlAgilityPack` to build an unbuffered streaming pipeline.
* ```FreshdeskService.cs```: Manages explicit platform communication wrappers and configuration schemas for helpdesk platform environments.
* ```JiraService.cs```: Implements the live back-end connection logic to poll, extract, and structure incoming ticket indices from external development projects.
* ```MistralChatService.cs```: Handles outgoing HTTP payloads and response parsing with the cloud-hosted Mistral AI completion APIs.
* ```MistralDistillationService.cs```: Coordinates the entity-extraction workflows, letting the model analyze blocks of raw text to output clean data triples.
* ```ReleaseNotesScraper.cs```: Implements custom binary asset extraction logic to pull text segments and build updates directly from software patch manifests.

---

### AskEIVA.WebUI

```text
📁 AskEiva.WebUI/
├── 📄 AskEiva.WebUI.csproj
├── 📄 Program.cs
├── 📄 appsettings.json
├── 📄 appsettings.Development.json
├── 📄 release_notes_manifest.json
├── 📁 Components/
│   ├── 📄 App.razor
│   ├── 📄 Routes.razor
│   ├── 📄 _Imports.razor
│   ├── 📁 Account/
│   │   ├── 📁 Pages/
│   │   │   ├── 📁 Manage/
│   │   │   ├── 📄 _Imports.razor
│   │   │   ├── 📄 Login.razor
│   │   │   ├── 📄 Register.razor
│   │   └── 📁 Shared/
│   ├── 📁 Layout/
│   │   ├── 📄 MainLayout.razor
│   │   ├── 📄 MainLayout.razor.css
│   │   ├── 📄 ReconnectModal.razor
│   │   ├── 📄 ReconnectModal.razor.css
│   │   └── 📄 ReconnectModal.razor.js
│   └── 📁 Pages/
│       ├── 📄 Chat.razor
│       ├── 📄 ConfigurationPortal.razor
│       ├── 📄 EvaluationDashboard.razor
│       ├── 📄 GraphRAG.razor
│       ├── 📄 Home.razor
├── 📁 Models/
│   └── 📄 MeshBuildStateStore.cs
├── 📁 Properties/
│   └── 📄 launchSettings.json
└── 📁 wwwroot/
    ├── 📁 lib/
    ├── 📄 app.css
    └── 📄 favicon.png
```

* TODO: Explain WebUI files