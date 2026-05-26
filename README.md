<img width="1024" height="434" alt="image" src="https://github.com/user-attachments/assets/36b9b132-3c56-448d-830f-d063bde788db" />

# 🎓 AskEIVA: Customer Service Agent

[![Funded by: EIVA](https://img.shields.io/badge/Funded%20by-EIVA-0A2540?style=flat-square)](https://www.eiva.com)
[![Status: Public Alpha](https://img.shields.io/badge/Status-Public%20Alpha-orange?style=flat-square)](https://github.com/lauPhilip/AskEivaNet)
[![Platform: .NET 10](https://img.shields.io/badge/Platform-.NET%2010-blueviolet?style=flat-square)](https://dotnet.microsoft.com)

**AskEIVA** is a production-grade, AI-native knowledge orchestration platform built on Clean Architecture and Domain-Driven Design (DDD) principles. It is engineered to transform dense subsea survey instrumentation manuals, complex product specifications, and historical customer support records into an interconnected, highly traceable, and auditable intelligence ecosystem.

---

## 🏛️ About the Project

Developed as a reference implementation for an **Agentic Industrial Ecosystem**, AskEIVA serves as an intelligent engineering companion for subsea professionals. The platform specializes in guiding customer service staff through complex documentation and a large ticket pool.

The platform provides a centralized hub for:
* **Staff-Managed Bot Architecture:** Seamless configuration of domain-focused agents without altering core business rules.
* **Knowledge Grounding:** Ensuring LLM responses are strictly anchored inside verified corporate documentation, software manuals, and historical customer service logs to completely prevent hallucinations.
* **Topological Relationship Mapping:** Extracting implicit product, semantic hardware, and versioning associations, visually projecting them as an interactive network grid.
* **Interaction Observability:** Providing engineering and support teams with real-time audit logs, column-level search filtering, chronological trace structures, and explicit feedback metric evaluation.

---

## 🚀 Core Capabilities & Domain Context

This application demonstrates production-ready agentic orchestration mapped directly to high-end marine survey domains:

* **Entity Sequencing (Mistral AI):** Automated extraction of semantic knowledge triples (`Subject` → `Predicate` → `Object`) out of raw corporate literature, uncovering dependencies between specific software versions, hardware platforms, and operational fixes.
* **Vectorized Knowledge (Weaviate):** Concurrent hybrid search indexing (`alpha: 0.5`) across documentation libraries and historic tickets to fetch contextual text snippets with millisecond-level turnarounds.
* **Topological GraphRAG (vis.js):** An animated, client-side relational canvas mapping interconnected node clusters (e.g., binding version paths such as `4.6.7` to its parent software application `NaviPac` via explicit semantic edges).
* **Asynchronous Telemetry Logging:** Decoupled background tracing loops that automatically capture every user query, generated response context, model attribute, and success execution state without blocking the interface thread.

---

## 🏗️ Technical Architecture

Adhering strictly to **Clean Architecture** parameters, the system isolates core business rules from infrastructure volatility, splitting the codebase into four separate projects inside a unified solution layout (`AskEiva.slnx`):

```text
📁 src/
├── 📁 AskEiva.Domain/          # Core entities (KnowledgeTriple, TicketNode), Value Objects, & Repository Contracts
├── 📁 AskEiva.Application/     # CQRS Handlers (MediatR), validation pipelines, and business orchestrations
├── 📁 AskEiva.Infrastructure/  # Weaviate adapters, GraphQL query generators, REST clients, and Schema Provisioners
└── 📁 AskEiva.WebUI/           # Blazor Server UI (MudBlazor), streaming text views, and interactive canvas components
