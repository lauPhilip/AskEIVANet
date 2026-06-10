# 🎓 AskEIVA: Intelligent Knowledge Orchestration Platform

[![Funded by: EIVA](https://img.shields.io/badge/Funded%20by-EIVA-0A2540?style=flat-square)](https://www.eiva.com)
[![Status: Production Release](https://img.shields.io/badge/Status-Production%20Release-brightgreen?style=flat-square)](https://github.com/lauPhilip/AskEivaNet)
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

### Application

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